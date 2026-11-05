namespace SMS.Publishing.ExternalServices.MessageBroker;

public interface IMessagingBrokerConnection<T>
{
    T OpenConnection(string name, CancellationToken cancellationToken = default);
    ValueTask<T> OpenConnectionAsync(string name, CancellationToken cancellationToken = default);
}