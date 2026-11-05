using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Versioning;
using SMS.Application;
using SMS.Infrastructure;
using SMS.Migration;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddControllers();
builder.Services.AddOpenApi();

builder.Services.AddProblemDetails(static x =>
{
    x.CustomizeProblemDetails = static context =>
        context.ProblemDetails.Extensions["traceId"] = Activity.Current?.Id ?? context.HttpContext.TraceIdentifier;
});

// SMS.Web runs in the browser on its own origin
builder.Services.AddCors(static options =>
    options.AddDefaultPolicy(static x => x.AllowAnyOrigin()
        .AllowAnyHeader()
        .AllowAnyMethod()));

builder.Services.AddApiVersioning(static x =>
{
    x.DefaultApiVersion = new ApiVersion(1, 0);
    x.AssumeDefaultVersionWhenUnspecified = true;
    x.ApiVersionReader = new UrlSegmentApiVersionReader();
    x.ReportApiVersions = true;
});

// postgres
builder.AddNpgsqlDataSource("postgres-db");

// migration
builder.Services.AddSmsMigration(builder.Configuration.GetConnectionString("postgres-db")
                                 ?? throw new InvalidOperationException("Connection string 'postgres-db' is missing"));

// application
builder.Services.AddSmsApplication();

// application.infrastructure
builder.Services.AddSmsInfrastructure();

var app = builder.Build();

app.Services.RunSmsMigration();

app.UseExceptionHandler();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseCors();
app.UseHttpsRedirection();

app.MapControllers();

app.Run();
