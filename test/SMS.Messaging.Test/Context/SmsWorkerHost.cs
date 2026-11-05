using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using SMS.Publishing.Worker.Published;
using SMS.Publishing.Worker.Publishing;
using SMS.Sender.Worker.Sending;
using PublishBrokerConnection = SMS.Publishing.ExternalServices.MessageBroker.RabbitMQ.RabbitMQConnectionWithAspire;
using SenderBrokerConnection = SMS.Sender.ExternalServices.MessageBroker.RabbitMQ.RabbitMQConnectionWithAspire;

namespace SMS.Messaging.Test.Context;

public sealed class SmsWorkerHost : IAsyncDisposable
{
    private readonly RabbitmqFixture _fixture;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly List<BackgroundService> _workers = [];

    public SmsWorkerHost(RabbitmqFixture fixture, IServiceScopeFactory scopeFactory)
    {
        _fixture = fixture;
        _scopeFactory = scopeFactory;
    }

    public async Task StartSmsPublishingWorker()
    {
        var logger = NullLogger<SmsPublishingWorker>.Instance;
        var connect = new PublishBrokerConnection(_fixture.Connection);
        var worker = new SmsPublishingWorker(logger, connect, _scopeFactory);
        
        _workers.Add(worker);

        await worker.StartAsync(TestContext.Current.CancellationToken);
    }

    public async Task StartSmsPublishedWorker()
    {
        var logger = NullLogger<SmsPublishedWorker>.Instance;
        var connect = new PublishBrokerConnection(_fixture.Connection);
        var worker = new SmsPublishedWorker(logger, connect, _scopeFactory);
        
        _workers.Add(worker);

        await worker.StartAsync(TestContext.Current.CancellationToken);
    }

    public async Task StartSmsSendingWorker()
    {
        var logger = NullLogger<SmsSendingWorker>.Instance;
        var connect = new SenderBrokerConnection(_fixture.Connection);
        var worker = new SmsSendingWorker(logger, connect);
        
        _workers.Add(worker);

        await worker.StartAsync(TestContext.Current.CancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        foreach (var worker in _workers)
        {
            await worker.StopAsync(TestContext.Current.CancellationToken);
            
            worker.Dispose();
        }
    }
}