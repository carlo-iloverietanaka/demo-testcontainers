using Aspire.Hosting;
using Projects;

var builder = DistributedApplication.CreateBuilder(args);

var postgres = builder.AddPostgres("postgres")
    .WithPgAdmin(x => x.WithHostPort(10111))
    .WithPgWeb(x => x.WithHostPort(10112))
    .WithDataVolume(isReadOnly: false);
var postgresDb = postgres.AddDatabase("postgres-db");

var rabbitmq = builder.AddRabbitMQ("messaging")
    .WithDataVolume(isReadOnly: false)
    .WithManagementPlugin();

// sms-api and sms-publishing run the migration on startup, so they wait for the database to be ready
var smsPublishing = builder.AddProject<SMS_Publishing>("sms-publishing")
    .WithReference(postgresDb)
    .WaitFor(postgresDb)
    .WithReference(rabbitmq)
    .WaitFor(rabbitmq);
var smsSender = builder.AddProject<SMS_Sender>("sms-sender")
    .WithReference(rabbitmq)
    .WaitFor(rabbitmq);

// the web app calls the api from the browser on the address in SMS.Web/wwwroot/appsettings.json
var smsApi = builder.AddProject<SMS_API>("sms-api", "http")
    .WithReference(postgresDb)
    .WaitFor(postgresDb);
var smsWeb = builder.AddProject<SMS_Web>("sms-web", "http")
    .WithReference(smsApi);

builder.Build().Run();