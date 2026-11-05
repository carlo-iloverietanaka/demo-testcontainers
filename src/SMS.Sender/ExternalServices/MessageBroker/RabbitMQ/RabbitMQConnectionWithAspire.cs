using RabbitMQ.Client;

namespace SMS.Sender.ExternalServices.MessageBroker.RabbitMQ;

public class RabbitMQConnectionWithAspire : IMessagingBrokerConnection<IConnection>
{
    private readonly IConnection _connection;

    public RabbitMQConnectionWithAspire(IConnection connection)
    {
        _connection = connection;
    }
    
    public IConnection OpenConnection(string name, CancellationToken cancellationToken = default)
    {
        return _connection;
    }

    public ValueTask<IConnection> OpenConnectionAsync(string name, CancellationToken cancellationToken = default)
    {
        return new ValueTask<IConnection>(_connection);
    }
}