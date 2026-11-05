using Microsoft.Extensions.DependencyInjection;
using SMS.Application.ExternalServices.Persistence;
using SMS.Infrastructure.Persistence.PostgreSQL;

namespace SMS.Infrastructure;

public static class DependencyInjection
{
    extension(IServiceCollection serviceCollection)
    {
        public IServiceCollection AddSmsInfrastructure()
        {
            // persistence
            serviceCollection.AddScoped<IQuerySms, QuerySms>();

            Dapper.DefaultTypeMap.MatchNamesWithUnderscores = true;

            return serviceCollection;
        }
    }
}