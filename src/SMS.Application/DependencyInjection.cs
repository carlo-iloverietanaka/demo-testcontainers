using Microsoft.Extensions.DependencyInjection;
using SMS.Application.Services.Sms;

namespace SMS.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddSmsApplication(this IServiceCollection services)
    {
        services.AddScoped<ISmsService, SmsService>();

        return services;
    }
}
