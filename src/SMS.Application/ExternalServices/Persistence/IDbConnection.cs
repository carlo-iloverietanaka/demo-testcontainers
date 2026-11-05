namespace SMS.Application.ExternalServices.Persistence;

public interface IDbConnection<T>
{
    T OpenConnection();
    ValueTask<T> OpenConnectionAsync();
}