using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using SMS.Migration;
using Testcontainers.PostgreSql;

namespace SMS.Messaging.Test.Context;

public class PostgresFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgres:17-alpine")
        .Build();

    public NpgsqlDataSource NpgsqlSource { get; private set; } = null!;

    public async ValueTask InitializeAsync()
    {
        await _container.StartAsync();

        var connectionString = _container.GetConnectionString();
        
        await using (var provider = new ServiceCollection()
                         .AddSmsMigration(connectionString)
                         .BuildServiceProvider())
        {
            provider.RunSmsMigration();
        }

        NpgsqlSource = NpgsqlDataSource.Create(connectionString);
    }

    public async ValueTask DisposeAsync()
    {
        try
        {
            await NpgsqlSource.DisposeAsync();
        }
        catch (Exception)
        {
            // ignored
        }
        
        await _container.DisposeAsync();
    }
}