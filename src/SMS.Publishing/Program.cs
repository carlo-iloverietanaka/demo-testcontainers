using RabbitMQ.Client;
using SMS.Application;
using SMS.Infrastructure;
using SMS.Migration;
using SMS.Publishing.ExternalServices.MessageBroker;
using SMS.Publishing.ExternalServices.MessageBroker.RabbitMQ;
using SMS.Publishing.Worker.Published;
using SMS.Publishing.Worker.Publishing;

var builder = Host.CreateApplicationBuilder(args);

// postgres
builder.AddNpgsqlDataSource("postgres-db");

// rabbit mq
builder.AddRabbitMQClient("messaging",
    static x => x.DisableAutoActivation = false,
    static x => x.ClientProvidedName = "SMS.Publishing");

// migration
builder.Services.AddSmsMigration(builder.Configuration.GetConnectionString("postgres-db")
                                 ?? throw new InvalidOperationException("Connection string 'postgres-db' is missing"));

// application
builder.Services.AddSmsApplication();

// application.infrastructure
builder.Services.AddSmsInfrastructure();

// workers
builder.Services.AddHostedService<SmsPublishingWorker>();
builder.Services.AddHostedService<SmsPublishedWorker>();

builder.Services.AddSingleton<IMessagingBrokerConnection<IConnection>, RabbitMQConnectionWithAspire>();

var host = builder.Build();

host.Services.RunSmsMigration();

host.Run();