# SMS Pipeline: Integration Testing with Testcontainers

A demo project for the course **Integration Test and Testcontainers for .NET**.
It shows how to test a system that process message against real PostgreSQL and
RabbitMQ containers instead of mocks.

## What the project does

The SMS pipeline moves each sms through these states:

1. A client sends an SMS through the **SMS API**, and it is stored as `Pending`.
2. The **SMS Processor** picks up pending messages and pushes them to the message broker (RabbitMQ). The message becomes `Published`.
3. The **SMS Sender** delivers the message (email or webhook channel) and pushes the delivery result back to the broker.
4. The processor consumes the result and marks the message `Delivered` or `Failed`.

## What the tests show

| Test project 					| What it covers |
|-------------------------------|----------------|
| `SMS.Infrastructure.Test` 	| Database queries against a real PostgreSQL container (migrations run before the tests). |
| `SMS.Messaging.Test` 			| The sms pipeline against real PostgreSQL and RabbitMQ containers. |

The containers are started by Testcontainers when the tests begin and cleaned
when the tests are finish. There is nothing to install or configure manually.

## Prerequisites

- **Docker**, installed and running
- **.NET 10 SDK**
- Internet access on the first run, so Docker can pull the PostgreSQL and RabbitMQ images

NuGet packages are restored automatically. The tests use:

- xUnit v3
- Testcontainers.PostgreSql
- Testcontainers.RabbitMq

## Run the tests

1. Check that Docker is running:

   ```bash
   docker info
   ```

2. From the repository root, run all tests:

   ```bash
   dotnet test
   ```

   Or run one test project:

   ```bash
   dotnet test SMS.Infrastructure.Test
   dotnet test SMS.Messaging.Test
   ```

3. The first run is slower because Docker pulls the images. Later runs reuse them.

You should see a test summary with successful test.

## Try it yourself: make a test fail

You can make the test fail by changing a small thing in the class that communicates with the external service. 

Open the `QuerySms` class, change the
table name in the query to one that does not exist (for example `smss`), and run
`dotnet test` again. The tests fail because there is no table in the real database.

## Where to look in the source

- `QuerySms`: the SQL queries for reading messages
- `SMS.Messaging.Test`: how the PostgreSQL and RabbitMQ containers work together in the pipeline test
- The class fixture in the test projects: how the service is built, started, and disposed