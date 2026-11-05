using FluentMigrator.Runner;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace SMS.Migration;

public static class DependencyInjection
{
    extension(IServiceCollection serviceCollection)
    {
        public IServiceCollection AddSmsMigration(string connectionString)
        {
            serviceCollection.AddSingleton(new MigrationConnection(connectionString));

            serviceCollection.AddFluentMigratorCore()
                .ConfigureRunner(x => x
                    .AddPostgres()
                    .WithGlobalConnectionString(connectionString)
                    .ScanIn(typeof(DependencyInjection).Assembly).For.Migrations())
                .AddLogging(static x => x.AddFluentMigratorConsole());

            return serviceCollection;
        }
    }

    extension(IServiceProvider serviceProvider)
    {
        public void RunSmsMigration()
        {
            using var scope = serviceProvider.CreateScope();

            var migrationConnection = scope.ServiceProvider
                .GetRequiredService<MigrationConnection>();

            using var connection = new NpgsqlConnection(migrationConnection.ConnectionString);
            
            connection.Open();

            using (var command = new NpgsqlCommand("select pg_advisory_lock(@Key);", connection))
            {
                command.Parameters.AddWithValue("Key", 202610300000);
                command.ExecuteNonQuery();
            }

            try
            {
                var runner = scope.ServiceProvider
                    .GetRequiredService<IMigrationRunner>();

                runner.MigrateUp();
            }
            finally
            {
                using var command = new NpgsqlCommand("select pg_advisory_unlock(@Key);", connection);
                command.Parameters.AddWithValue("Key", 202610300000);
                command.ExecuteNonQuery();
            }
        }
    }

    private sealed record MigrationConnection(string ConnectionString);
}
