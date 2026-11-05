using RabbitMQ.Client;
using RabbitMQ.Client.Exceptions;

namespace SMS.Sender.ExternalServices.MessageBroker.RabbitMQ;

public class RabbitMQConnection : IMessagingBrokerConnection<IConnection>, IDisposable, IAsyncDisposable
{
    private readonly ILogger<RabbitMQConnection> _logger;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private IConnection? _connection;

    public RabbitMQConnection(ILogger<RabbitMQConnection> logger)
    {
        _logger = logger;
    }

    public IConnection OpenConnection(string name, CancellationToken cancellationToken)
    {
        return OpenConnectionAsync(name, cancellationToken)
            .GetAwaiter()
            .GetResult();
    }

    public async ValueTask<IConnection> OpenConnectionAsync(string name, CancellationToken cancellationToken)
    {
        if (_connection is not null)
        {
            return _connection;
        }

        // prevents two callers from each creating a connection and leaking one
        await _lock.WaitAsync(cancellationToken);

        try
        {
            if (_connection is not null)
            {
                return _connection;
            }

            var connectionFactory = new ConnectionFactory
            {
                AutomaticRecoveryEnabled = true,
                TopologyRecoveryEnabled = true,
                ClientProvidedName = name
            };

            _connection = await connectionFactory.CreateConnectionAsync(cancellationToken);

            return _connection;
        }
        catch (BrokerUnreachableException ex)
        {
            _logger.LogCritical(ex, "Failed to connect to RabbitMQ broker");
            throw;
        }
        finally
        {
            _lock.Release();
        }
    }

    public void Dispose()
    {
        _connection?.CloseAsync()
            .GetAwaiter()
            .GetResult();
        _connection?.Dispose();
        _lock.Dispose();
    }

    public async ValueTask DisposeAsync()
    {
        _lock.Dispose();

        if (_connection is null)
        {
            return;
        }

        await _connection.CloseAsync();
        await _connection.DisposeAsync();
    }
}
