using System.Text;
using System.Text.Json;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using SMS.Application.Output;
using SMS.Application.Services.Sms;
using SMS.Broker.Contracts.Message;
using SMS.Publishing.ExternalServices.MessageBroker;

namespace SMS.Publishing.Worker.Published;

public class SmsPublishedWorker : BackgroundService
{
    private readonly ILogger<SmsPublishedWorker> _logger;
    private readonly IMessagingBrokerConnection<IConnection> _brokerConnection;
    private readonly IServiceScopeFactory _scopeFactory;

    public SmsPublishedWorker(ILogger<SmsPublishedWorker> logger,
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
        await using var channel = await connect.CreateChannelAsync(cancellationToken: stoppingToken);

        await channel.QueueDeclareAsync(queue: "sms.published",
            durable: true,
            exclusive: false,
            autoDelete: false,
            cancellationToken: stoppingToken);

        var consumer = new AsyncEventingBasicConsumer(channel);

        consumer.ReceivedAsync += async (_, eventArgs) =>
        {
            // ReSharper disable once AccessToDisposedClosure
            await HandleReceivedAsync(eventArgs, channel);
        };

        await channel.BasicConsumeAsync(queue: "sms.published",
            autoAck: false,
            consumer: consumer,
            cancellationToken: stoppingToken);
        
        await Task.Delay(Timeout.Infinite, stoppingToken);
    }

    private async Task HandleReceivedAsync(BasicDeliverEventArgs eventArgs, IChannel channel)
    {
        var content = BodyToObject(eventArgs.Body.Span);

        if (content is null)
        {
            await channel.BasicNackAsync(eventArgs.DeliveryTag,
                multiple: false,
                requeue: false);

            return;
        }

        await using var scope = _scopeFactory.CreateAsyncScope();

        var service = scope.ServiceProvider.GetRequiredService<ISmsService>();

        // only an exact status name is accepted, Enum.TryParse would also take any number such as "99",
        // anything else is treated as failed
        var status = Enum.GetValues<SmsStatus>()
            .FirstOrDefault(x => string.Equals(x.ToString(), content.Status, StringComparison.OrdinalIgnoreCase),
                SmsStatus.Failed);

        var item = await service.UpdateSmsStatus(new SmsInputStatus(content.ExternalId, status));

        if (item is YieldError error)
        {
            _logger.LogWarning(
                "{Name} = failed tp update sms {ExternalId} to {Status}, error message is {Error} and {ErrorDescription}",
                nameof(SmsPublishedWorker),
                content.ExternalId,
                content.Status,
                error.Error,
                error.ErrorDescription);
            
            await channel.BasicNackAsync(eventArgs.DeliveryTag,
                multiple: false,
                requeue: false);
            
            return;
        }

        if (item is not YieldOk<SmsOutput>)
        {
            _logger.LogWarning("{Name} = an unexpected type {Type} from updating sms status",
                nameof(SmsPublishedWorker), item);
            
            await channel.BasicNackAsync(eventArgs.DeliveryTag,
                multiple: false,
                requeue: false);
            
            return;
        }

        await channel.BasicAckAsync(eventArgs.DeliveryTag, multiple: false);
    }

    private SmsPublished? BodyToObject(ReadOnlySpan<byte> body)
    {
        var raw = Encoding.UTF8.GetString(body);

        try
        {
            return JsonSerializer.Deserialize<SmsPublished>(raw);
        }
        catch (JsonException e)
        {
            _logger.LogWarning(e, "{Name} = failed to deserialize the message from broker {Raw}",
                nameof(SmsPublishedWorker), raw);

            return null;
        }
    }
}