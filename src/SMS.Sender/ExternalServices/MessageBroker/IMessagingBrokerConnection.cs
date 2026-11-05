namespace SMS.Sender.ExternalServices.MessageBroker;

public interface IMessagingBrokerConnection<T>
{
    T OpenConnection(string name, CancellationToken cancellationToken = default);
    ValueTask<T> OpenConnectionAsync(string name, CancellationToken cancellationToken = default);
}
