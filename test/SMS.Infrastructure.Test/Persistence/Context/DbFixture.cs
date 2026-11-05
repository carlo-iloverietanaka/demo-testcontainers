using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using SMS.Migration;
using Testcontainers.PostgreSql;

namespace SMS.Infrastructure.Test.Persistence.Context;

public class DbFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgres:17-alpine")
        .Build();

    public NpgsqlDataSource NpgsqlSource
    {
        get => field ?? throw new InvalidOperationException("The fixture is not initialized");
        private set;
    }

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

        Dapper.DefaultTypeMap.MatchNamesWithUnderscores = true;
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