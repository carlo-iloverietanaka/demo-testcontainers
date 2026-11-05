using System.Text;
using System.Text.Json;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using SMS.Broker.Contracts.Message;
using SMS.Sender.ExternalServices.MessageBroker;

namespace SMS.Sender.Worker.Sending;

public class SmsSendingWorker : BackgroundService
{
    private readonly ILogger<SmsSendingWorker> _logger;
    private readonly IMessagingBrokerConnection<IConnection> _brokerConnection;

    public SmsSendingWorker(ILogger<SmsSendingWorker> logger, IMessagingBrokerConnection<IConnection> brokerConnection)
    {
        _logger = logger;
        _brokerConnection = brokerConnection;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await using var connection = await _brokerConnection.OpenConnectionAsync("SMS.Sender", stoppingToken);
        await using var publishingChannel = await connection.CreateChannelAsync(cancellationToken: stoppingToken);

        await publishingChannel.QueueDeclareAsync(queue: "sms.publishing",
            durable: true,
            exclusive: false,
            autoDelete: false,
            cancellationToken: stoppingToken);

        await publishingChannel.BasicQosAsync(prefetchSize: 0,
            prefetchCount: 2,
            global: false,
            cancellationToken: stoppingToken);

        var publishChannelOptions = new CreateChannelOptions(
            publisherConfirmationsEnabled: true,
            publisherConfirmationTrackingEnabled: true);

        await using var publishedChannel = await connection.CreateChannelAsync(publishChannelOptions, stoppingToken);

        await publishedChannel.QueueDeclareAsync(queue: "sms.published",
            durable: true,
            exclusive: false,
            autoDelete: false,
            cancellationToken: stoppingToken);

        var consumer = new AsyncEventingBasicConsumer(publishingChannel);

        consumer.ReceivedAsync += async (_, eventArgs) =>
        {
            // ReSharper disable AccessToDisposedClosure
            await HandleReceivedAsync(eventArgs, publishingChannel, publishedChannel);
        };

        await publishingChannel.BasicConsumeAsync(queue: "sms.publishing",
            autoAck: false,
            consumer: consumer,
            cancellationToken: stoppingToken);

        await Task.Delay(Timeout.Infinite, stoppingToken);
    }

    private async Task HandleReceivedAsync(BasicDeliverEventArgs eventArgs, IChannel publishingChannel, IChannel publishedChannel)
    {
        var content = BodyToObject(eventArgs.Body.Span);

        if (content is null)
        {
            await publishingChannel.BasicNackAsync(eventArgs.DeliveryTag,
                multiple: false,
                requeue: false);

            return;
        }

        // TODO: send the sms through the provider instead of logging it
        _logger.LogInformation(
            "{Name} = sending sms {ExternalId} from {Sender} to {Recipient} via {Medium}, {ContentType}: {Content}",
            nameof(SmsSendingWorker),
            content.ExternalId,
            content.Sender,
            content.Recipient,
            content.Medium,
            content.ContentType,
            content.Content);

        var sent = await HandleDeliveryResult(publishedChannel, new SmsPublished(content.ExternalId, "Delivered"));

        if (sent is false)
        {
            // the sms is sent again on redelivery, so sending has to be idempotent on ExternalId
            await publishingChannel.BasicNackAsync(eventArgs.DeliveryTag,
                multiple: false,
                requeue: true);

            return;
        }
        
        await publishingChannel.BasicAckAsync(eventArgs.DeliveryTag, multiple: false);
    }

    private async Task<bool> HandleDeliveryResult(IChannel publishedChannel, SmsPublished result)
    {
        try
        {
            var properties = new BasicProperties
            {
                Persistent = true
            };

            var body = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(result));

            await publishedChannel.BasicPublishAsync(exchange: "",
                routingKey: "sms.published",
                mandatory: true,
                basicProperties: properties,
                body: body);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "{Name} = failed to publish the delivery result of sms {ExternalId}, requeuing the message",
                nameof(SmsSendingWorker), result.ExternalId);
            
            return false;
        }
    }

    private SmsPublishing? BodyToObject(ReadOnlySpan<byte> body)
    {
        var raw = Encoding.UTF8.GetString(body);

        try
        {
            return JsonSerializer.Deserialize<SmsPublishing>(raw);
        }
        catch (JsonException e)
        {
            _logger.LogWarning(e, "{Name} = failed to deserialize the message from broker {Raw}",
                nameof(SmsSendingWorker), raw);

            return null;
        }
    }
}