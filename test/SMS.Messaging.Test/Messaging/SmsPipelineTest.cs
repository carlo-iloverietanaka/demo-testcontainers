using Microsoft.Extensions.DependencyInjection;
using SMS.Application;
using SMS.Application.ExternalServices.Persistence;
using SMS.Application.Output;
using SMS.Application.Services.Sms;
using SMS.Domain.Entities;
using SMS.Infrastructure;
using SMS.Messaging.Test.Context;

namespace SMS.Messaging.Test.Messaging;

public class SmsPipelineTest : IClassFixture<PostgresFixture>, IClassFixture<RabbitmqFixture>, IAsyncLifetime
{
    private readonly PostgresFixture _postgres;
    private readonly RabbitmqFixture _rabbitmq;

    private ServiceProvider _serviceProvider = null!;
    private SmsWorkerHost _smsWorker = null!;

    public SmsPipelineTest(PostgresFixture postgres, RabbitmqFixture rabbitmq)
    {
        _postgres = postgres;
        _rabbitmq = rabbitmq;
    }

    public ValueTask InitializeAsync()
    {
        var serviceProvider = new ServiceCollection()
            .AddSingleton(_postgres.NpgsqlSource)
            .AddSmsApplication()
            .AddSmsInfrastructure()
            .BuildServiceProvider();

        _serviceProvider = serviceProvider;
        _smsWorker = new SmsWorkerHost(_rabbitmq, _serviceProvider.GetRequiredService<IServiceScopeFactory>());
        
        return ValueTask.CompletedTask;
    }

    public async ValueTask DisposeAsync()
    {
        await _smsWorker.DisposeAsync();
        await _serviceProvider.DisposeAsync();
    }

    [Fact]
    public async Task PipelineTest()
    {
        // arrange
        var externalId = default(string);

        {
            await using var scope = _serviceProvider
                .GetRequiredService<IServiceScopeFactory>()
                .CreateAsyncScope();

            var created = await scope.ServiceProvider
                .GetRequiredService<ISmsService>()
                .CreateSms(new SmsInput("Sample Content", "text/plain", "Sample Sender", "+639170000000", "sms"));

            externalId = Assert.IsType<YieldOk<SmsOutput>>(created).Value.ExternalId;
        }

        // act
        await _smsWorker.StartSmsPublishingWorker();
        await _smsWorker.StartSmsPublishedWorker();
        await _smsWorker.StartSmsSendingWorker();

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);

        timeout.CancelAfter(TimeSpan.FromSeconds(15));

        var input = default(Sms);

        {
            await using var scope = _serviceProvider
                .GetRequiredService<IServiceScopeFactory>()
                .CreateAsyncScope();
            
            var querySms = scope.ServiceProvider
                .GetRequiredService<IQuerySms>();

            async Task<Sms> QueryCreatedSms()
            {
                var item = await querySms.FindByExternalIdAsync(externalId, TestContext.Current.CancellationToken);

                if (item is null)
                {
                    Assert.Fail("The created sms was not found");
                }

                return item;
            }

            input = await QueryCreatedSms();

            while (input.Status is not nameof(SmsStatus.Delivered))
            {
                await Task.Delay(TimeSpan.FromSeconds(1), timeout.Token);

                input = await QueryCreatedSms();
            }
        }

        // assert
        Assert.NotNull(input);
        Assert.NotNull(input.CompletedAt);
        Assert.Equal(nameof(SmsStatus.Delivered), input.Status);
    }
}