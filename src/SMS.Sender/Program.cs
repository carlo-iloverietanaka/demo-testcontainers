using RabbitMQ.Client;
using SMS.Sender.ExternalServices.MessageBroker;
using SMS.Sender.ExternalServices.MessageBroker.RabbitMQ;
using SMS.Sender.Worker.Sending;

var builder = Host.CreateApplicationBuilder(args);

// messaging broker
builder.AddRabbitMQClient("messaging",
    static x => x.DisableAutoActivation = false,
    static x => x.ClientProvidedName = "SMS.Published");

// messaging broker connection
builder.Services.AddSingleton<IMessagingBrokerConnection<IConnection>, RabbitMQConnectionWithAspire>();

// worker
builder.Services.AddHostedService<SmsSendingWorker>();

var host = builder.Build();

host.Run();
