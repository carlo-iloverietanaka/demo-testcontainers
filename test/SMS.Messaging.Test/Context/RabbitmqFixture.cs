using RabbitMQ.Client;
using Testcontainers.RabbitMq;

namespace SMS.Messaging.Test.Context;

public class RabbitmqFixture : IAsyncLifetime
{
    private readonly RabbitMqContainer _container = new RabbitMqBuilder("rabbitmq:4-alpine")
        .Build();

    public IConnection Connection
    {
        get => field ?? throw new InvalidOperationException("The fixture is not initialized");
        private set;
    }

    public async ValueTask InitializeAsync()
    {
        await _container.StartAsync();

        var connectionFactory = new ConnectionFactory
        {
            Uri = new Uri(_container.GetConnectionString()),
            ClientProvidedName = "SMS.Messaging.Test"
        };

        Connection = await connectionFactory.CreateConnectionAsync();
    }

    public async ValueTask DisposeAsync()
    {
        try
        {
            await Connection.CloseAsync();
            await Connection.DisposeAsync();
        }
        catch (Exception)
        {
            // ignored
        }
        
        await _container.DisposeAsync();
    }
}

[CollectionDefinition(nameof(BrokerCollection))]
public class BrokerCollection : ICollectionFixture<RabbitmqFixture>;