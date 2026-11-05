using System.Text;
using System.Text.Json;
using RabbitMQ.Client;
using SMS.Application.Output;
using SMS.Application.Services.Sms;
using SMS.Broker.Contracts.Message;
using SMS.Publishing.ExternalServices.MessageBroker;

namespace SMS.Publishing.Worker.Publishing;

public class SmsPublishingWorker : BackgroundService
{
    private readonly ILogger<SmsPublishingWorker> _logger;
    private readonly IMessagingBrokerConnection<IConnection> _brokerConnection;
    private readonly IServiceScopeFactory _scopeFactory;

    public SmsPublishingWorker(ILogger<SmsPublishingWorker> logger,
        IMessagingBrokerConnection<IConnection> brokerConnection,
        IServiceScopeFactory scopeFactory)
    {
        _logger = logger;
        _brokerConnection = brokerConnection;
        _scopeFactory = scopeFactory;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await using var connect = await _brokerConnection.OpenConnectionAsync("SMS.Publishing", stoppingToken);

        var channelOptions = new CreateChannelOptions(
            publisherConfirmationsEnabled: true,
            publisherConfirmationTrackingEnabled: true);

        await using var channel = await connect.CreateChannelAsync(channelOptions, stoppingToken);

        await channel.QueueDeclareAsync(queue: "sms.publishing",
            durable: true,
            exclusive: false,
            autoDelete: false,
            cancellationToken: stoppingToken);

        await ExecuteInterval(channel, stoppingToken);
    }

    private async Task ExecuteInterval(IChannel channel, CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(10));

        do
        {
            try
            {
                await PublishPending(channel);
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Failed to process pending sms");
            }
        } while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    private async Task PublishPending(IChannel channel)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();

        var service = scope.ServiceProvider.GetRequiredService<ISmsService>();

        // claims the pending sms and marks them as Publishing
        var item = await service.FindPendingThenLease();

        if (item is not YieldOk<List<SmsOutput>> fine)
        {
            _logger.LogWarning("Error: unexpected type {Type} from retrieving pending sms", item);
            return;
        }

        if (fine.Value.Count is 0)
        {
            _logger.LogInformation("There is no pending sms for publishing");
            return;
        }

        _logger.LogInformation("Claim: a total of {Count} pending sms for publishing", fine.Value.Count);

        var properties = new BasicProperties
        {
            Persistent = true
        };

        var toPublish = new List<string>();

        try
        {
            foreach (var sms in fine.Value)
            {
                var message = new SmsPublishing(sms.ExternalId,
                    sms.Content,
                    sms.ContentType,
                    sms.Sender,
                    sms.Recipient,
                    sms.Medium);

                var body = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(message));

                await channel.BasicPublishAsync(exchange: "",
                    routingKey: "sms.publishing",
                    mandatory: true,
                    basicProperties: properties,
                    body: body);

                toPublish.Add(sms.ExternalId);
            }
        }
        catch (Exception e)
        {
            _logger.LogError(e,
                "{Nam}: an error occured while publishing sms, a {Count} out of {Total} sms were confirmed by the broker",
                nameof(SmsPublishingWorker),
                toPublish.Count,
                fine.Value.Count);
        }

        if (toPublish.Count > 0)
        {
            await service.LeaseToPublish(toPublish);
        }

        var toPending = fine.Value
            .Select(x => x.ExternalId)
            .Except(toPublish)
            .ToList();

        if (toPending.Count > 0)
        {
            await service.LeaseToPending(toPending);
        }
    }
}