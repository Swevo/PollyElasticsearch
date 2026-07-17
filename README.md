# PollyElasticsearch

[![NuGet](https://img.shields.io/nuget/v/PollyElasticsearch.svg)](https://www.nuget.org/packages/PollyElasticsearch/)
[![NuGet Downloads](https://img.shields.io/nuget/dt/PollyElasticsearch.svg)](https://www.nuget.org/packages/PollyElasticsearch/)
[![CI](https://github.com/Swevo/PollyElasticsearch/actions/workflows/build.yml/badge.svg)](https://github.com/Swevo/PollyElasticsearch/actions/workflows/build.yml)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)

**Polly v8 resilience for `Elastic.Clients.Elasticsearch 8+`** — add retry, timeout, and circuit-breaker to any Elasticsearch operation in two lines.

```csharp
var client = new ElasticsearchClient(settings);

var resilient = client.WithPolly(pipeline => pipeline
    .AddRetry(new RetryStrategyOptions
    {
        MaxRetryAttempts = 3,
        Delay = TimeSpan.FromSeconds(1),
        BackoffType = DelayBackoffType.Exponential,
        ShouldHandle = ElasticTransientErrors.IsTransient,
    })
    .AddTimeout(TimeSpan.FromSeconds(10)));

var result = await resilient.ExecuteAsync((c, ct) => c.SearchAsync<Product>(s => s
    .Index("products")
    .Query(q => q.Match(m => m.Field(f => f.Name).Query("laptop"))), ct));
```

## Why PollyElasticsearch?

`Elastic.Clients.Elasticsearch` does not natively integrate with Polly. This library bridges the gap:

| Problem | Solution |
|---------|----------|
| `TransportException` on connection failure | Caught by `ElasticTransientErrors.IsTransient` |
| HTTP 429 rate-limit from Elasticsearch / proxy | Auto-thrown as `ElasticTransientException` and retried |
| HTTP 503 cluster maintenance / rolling restart | Auto-thrown as `ElasticTransientException` and retried |
| HTTP 504 gateway timeout behind a load balancer | Auto-thrown as `ElasticTransientException` and retried |
| Slow queries blocking thread pool | Wrap with `AddTimeout` |
| Cascading failures during outage | Wrap with `AddCircuitBreaker` |

## Installation

```
dotnet add package PollyElasticsearch
dotnet add package Polly.Core
```

## Quick-start

### 1. Manual wiring

```csharp
using Polly;
using Polly.Retry;

var client = new ElasticsearchClient(
    new ElasticsearchClientSettings(new Uri("https://my-cluster:9200")));

var resilient = client.WithPolly(p => p
    .AddRetry(new RetryStrategyOptions
    {
        MaxRetryAttempts = 3,
        Delay = TimeSpan.FromSeconds(2),
        BackoffType = DelayBackoffType.Exponential,
        ShouldHandle = ElasticTransientErrors.IsTransient,
    }));

// Every call is now wrapped in the Polly pipeline.
var response = await resilient.ExecuteAsync(
    (c, ct) => c.GetAsync<Product>("products", "doc-id", ct));
```

### 2. Dependency injection

```csharp
// Program.cs / Startup.cs
builder.Services.AddSingleton(new ElasticsearchClient(
    new ElasticsearchClientSettings(new Uri("https://my-cluster:9200"))));

builder.Services.AddPollyElasticsearch(pipeline => pipeline
    .AddRetry(new RetryStrategyOptions
    {
        MaxRetryAttempts = 3,
        Delay = TimeSpan.FromSeconds(1),
        BackoffType = DelayBackoffType.Exponential,
        ShouldHandle = ElasticTransientErrors.IsTransient,
    })
    .AddTimeout(TimeSpan.FromSeconds(10)));

// Inject ResilientElasticsearchClient into your services
public class ProductService(ResilientElasticsearchClient client)
{
    public Task<SearchResponse<Product>> SearchAsync(string q, CancellationToken ct) =>
        client.ExecuteAsync((c, ct2) => c.SearchAsync<Product>(s => s
            .Index("products")
            .Query(q2 => q2.Match(m => m.Field(f => f.Name).Query(q))), ct2), ct);
}
```

### 3. With a URI shortcut

```csharp
builder.Services.AddPollyElasticsearch(
    new Uri("https://my-cluster:9200"),
    pipeline => pipeline
        .AddRetry(new RetryStrategyOptions
        {
            MaxRetryAttempts = 5,
            ShouldHandle = ElasticTransientErrors.IsTransient,
        }));
```

## Transient error reference

```csharp
// Use in any Polly strategy:
ShouldHandle = ElasticTransientErrors.IsTransient
```

| Condition | Why it's transient |
|-----------|-------------------|
| `ElasticTransientException` (HTTP 429) | Rate limited — back off and retry |
| `ElasticTransientException` (HTTP 503) | Cluster down / maintenance — retry later |
| `ElasticTransientException` (HTTP 504) | Proxy/load-balancer timeout — retry |
| `TransportException` | Network failure or connection refused |

> **Note:** `ElasticTransientException` is thrown automatically by `ResilientElasticsearchClient` when the response HTTP status code is in `ElasticTransientErrors.StatusCodes` (429, 503, 504). You do not need to throw it yourself.

### Checking the status code

```csharp
.AddRetry(new RetryStrategyOptions
{
    ShouldHandle = new PredicateBuilder()
        .Handle<ElasticTransientException>(ex => ex.StatusCode == 429),
    MaxRetryAttempts = 5,
    Delay = TimeSpan.FromSeconds(10),  // respect 429 back-off window
})
```

## Advanced pipelines

### Full production pipeline

```csharp
client.WithPolly(p => p
    .AddTimeout(TimeSpan.FromSeconds(30))          // total call timeout
    .AddRetry(new RetryStrategyOptions
    {
        MaxRetryAttempts = 4,
        Delay = TimeSpan.FromSeconds(1),
        BackoffType = DelayBackoffType.Exponential,
        UseJitter = true,
        ShouldHandle = ElasticTransientErrors.IsTransient,
    })
    .AddCircuitBreaker(new CircuitBreakerStrategyOptions
    {
        FailureRatio = 0.5,
        SamplingDuration = TimeSpan.FromSeconds(30),
        MinimumThroughput = 10,
        BreakDuration = TimeSpan.FromSeconds(15),
        ShouldHandle = ElasticTransientErrors.IsTransient,
    }));
```

### Observability via Polly events

```csharp
.AddRetry(new RetryStrategyOptions
{
    ShouldHandle = ElasticTransientErrors.IsTransient,
    OnRetry = args =>
    {
        logger.LogWarning("Elasticsearch retry {Attempt} after {Delay}ms — {Exception}",
            args.AttemptNumber, args.RetryDelay.TotalMilliseconds, args.Outcome.Exception?.Message);
        return ValueTask.CompletedTask;
    },
})
```

## API reference

### `ResilientElasticsearchClient`

| Member | Description |
|--------|-------------|
| `Inner` | The underlying `ElasticsearchClient` |
| `ExecuteAsync<TResponse>(operation, ct)` | Executes `operation` through the pipeline; throws `ElasticTransientException` for 429/503/504 |

### `ElasticTransientErrors`

| Member | Description |
|--------|-------------|
| `IsTransient` | `PredicateBuilder` for `ElasticTransientException` + `TransportException` |
| `StatusCodes` | `IReadOnlySet<int>` — `{429, 503, 504}` |

### `ElasticTransientException`

| Member | Description |
|--------|-------------|
| `StatusCode` | The HTTP status code that triggered the exception |
| `Message` | Human-readable description including the status code |

### Extension methods

| Method | Description |
|--------|-------------|
| `client.WithPolly(pipeline)` | Wraps an `ElasticsearchClient` with a pre-built `ResiliencePipeline` |
| `client.WithPolly(configure)` | Builds a pipeline inline and wraps the client |

### DI extensions

| Method | Description |
|--------|-------------|
| `services.AddPollyElasticsearch(configure)` | Registers `ResiliencePipeline` + `ResilientElasticsearchClient` (requires `ElasticsearchClient` already in DI) |
| `services.AddPollyElasticsearch(uri, configure)` | Registers `ElasticsearchClient` for `uri`, then pipeline + resilient client |

## Target frameworks

| Framework | Supported |
|-----------|-----------|
| .NET 6 | ✅ |
| .NET 8 | ✅ |
| .NET 9 | ✅ |

## Related Packages

| Package | Downloads | Description |
|---|---|---|
| [PollyHealthChecks](https://www.nuget.org/packages/PollyHealthChecks) | [![Downloads](https://img.shields.io/nuget/dt/PollyHealthChecks.svg)](https://www.nuget.org/packages/PollyHealthChecks) | ASP.NET Core health checks for Polly v8 circuit breakers — expose circuit-breaker state (Closed, HalfOpen, Open, Isolated) as /health endpoint responses |
| [PollyBackoff](https://www.nuget.org/packages/PollyBackoff) | [![Downloads](https://img.shields.io/nuget/dt/PollyBackoff.svg)](https://www.nuget.org/packages/PollyBackoff) | Backoff delay strategies for Polly v8 resilience pipelines |
| [PollyGrpc](https://www.nuget.org/packages/PollyGrpc) | [![Downloads](https://img.shields.io/nuget/dt/PollyGrpc.svg)](https://www.nuget.org/packages/PollyGrpc) | Polly v8 resilience interceptor for gRPC |
| [PollyEFCore](https://www.nuget.org/packages/PollyEFCore) | [![Downloads](https://img.shields.io/nuget/dt/PollyEFCore.svg)](https://www.nuget.org/packages/PollyEFCore) | Polly v8 resilience pipelines for Entity Framework Core — wrap every EF Core query and SaveChanges with retry, timeout and circuit-breaker via a single AddPollyResilience() call |
| [PollyRabbitMQ](https://www.nuget.org/packages/PollyRabbitMQ) | [![Downloads](https://img.shields.io/nuget/dt/PollyRabbitMQ.svg)](https://www.nuget.org/packages/PollyRabbitMQ) | Polly v8 resilience for RabbitMQ.Client v7+ — retry, circuit-breaker, and timeout for IChannel operations, with built-in RabbitMqTransientErrors predicate covering AlreadyClosedException, BrokerUnreachableException, OperationInterruptedException, and ConnectFailureException |
| [PollyMailKit](https://www.nuget.org/packages/PollyMailKit) | [![Downloads](https://img.shields.io/nuget/dt/PollyMailKit.svg)](https://www.nuget.org/packages/PollyMailKit) | Polly v8 resilience pipelines for MailKit — retry, timeout, and circuit-breaker for SmtpClient.SendAsync and any MailKit SMTP operation |
| [PollyMassTransit](https://www.nuget.org/packages/PollyMassTransit) | [![Downloads](https://img.shields.io/nuget/dt/PollyMassTransit.svg)](https://www.nuget.org/packages/PollyMassTransit) | Polly v8 resilience pipelines for MassTransit — retry, timeout, and circuit-breaker for IBus.Publish and ISendEndpointProvider.Send |
| [PollyNpgsql](https://www.nuget.org/packages/PollyNpgsql) | [![Downloads](https://img.shields.io/nuget/dt/PollyNpgsql.svg)](https://www.nuget.org/packages/PollyNpgsql) | Polly v8 resilience pipelines for Npgsql (PostgreSQL) — retry, timeout, and circuit-breaker for NpgsqlConnection queries and commands, plus a built-in PostgresTransientErrors predicate covering all common PostgreSQL transient SQLSTATE codes |
| [PollyOpenAI](https://www.nuget.org/packages/PollyOpenAI) | [![Downloads](https://img.shields.io/nuget/dt/PollyOpenAI.svg)](https://www.nuget.org/packages/PollyOpenAI) | Polly v8 resilience for OpenAI and Azure OpenAI API calls |
| [PollyAzureEventHub](https://www.nuget.org/packages/PollyAzureEventHub) | [![Downloads](https://img.shields.io/nuget/dt/PollyAzureEventHub.svg)](https://www.nuget.org/packages/PollyAzureEventHub) | Polly v8 resilience pipelines for Azure Event Hubs — retry, timeout, and circuit-breaker for EventHubProducerClient and EventHubConsumerClient |
| [PollySignalR](https://www.nuget.org/packages/PollySignalR) | [![Downloads](https://img.shields.io/nuget/dt/PollySignalR.svg)](https://www.nuget.org/packages/PollySignalR) | Polly v8 reconnect policy for SignalR |
| [PollyHangfire](https://www.nuget.org/packages/PollyHangfire) | [![Downloads](https://img.shields.io/nuget/dt/PollyHangfire.svg)](https://www.nuget.org/packages/PollyHangfire) | Polly v8 resilience pipelines for Hangfire — retry, timeout, and circuit-breaker for IBackgroundJobClient.Enqueue and Schedule |
| [PollyCosmosDb](https://www.nuget.org/packages/PollyCosmosDb) | [![Downloads](https://img.shields.io/nuget/dt/PollyCosmosDb.svg)](https://www.nuget.org/packages/PollyCosmosDb) | Polly v8 resilience pipelines for Azure Cosmos DB — retry, timeout, and circuit-breaker for Container operations, plus a built-in CosmosTransientErrors predicate covering rate limiting (429), timeouts (408), partition failovers (410), and service unavailability (503) |
| [PollySendGrid](https://www.nuget.org/packages/PollySendGrid) | [![Downloads](https://img.shields.io/nuget/dt/PollySendGrid.svg)](https://www.nuget.org/packages/PollySendGrid) | Polly v8 resilience pipelines for SendGrid — retry, timeout, and circuit-breaker for ISendGridClient.SendEmailAsync |
| [PollyMongo](https://www.nuget.org/packages/PollyMongo) | [![Downloads](https://img.shields.io/nuget/dt/PollyMongo.svg)](https://www.nuget.org/packages/PollyMongo) | Polly v8 resilience pipelines for MongoDB.Driver — wrap Find, InsertOne, UpdateOne, DeleteOne and other IMongoCollection calls with retry, timeout, circuit-breaker, and more using a single ResilientMongoCollection decorator |
| [PollyDapper](https://www.nuget.org/packages/PollyDapper) | [![Downloads](https://img.shields.io/nuget/dt/PollyDapper.svg)](https://www.nuget.org/packages/PollyDapper) | Polly v8 resilience pipelines for Dapper — wrap QueryAsync, ExecuteAsync, and other Dapper calls with retry, timeout, circuit-breaker, and more using a single ResilientDbConnection decorator |
| [PollyMediatR](https://www.nuget.org/packages/PollyMediatR) | [![Downloads](https://img.shields.io/nuget/dt/PollyMediatR.svg)](https://www.nuget.org/packages/PollyMediatR) | Polly v8 resilience pipelines for MediatR — add retry, timeout, circuit-breaker, rate-limiting, hedging, and chaos engineering to any MediatR request handler with a single line of DI registration |
| [PollySqlClient](https://www.nuget.org/packages/PollySqlClient) | [![Downloads](https://img.shields.io/nuget/dt/PollySqlClient.svg)](https://www.nuget.org/packages/PollySqlClient) | Polly v8 resilience pipelines for Microsoft.Data.SqlClient (SQL Server and Azure SQL) — retry, timeout, and circuit-breaker for SqlConnection queries and commands, plus a built-in SqlServerTransientErrors predicate covering all common SQL Server and Azure SQL transient error numbers |
| [PollyAzureKeyVault](https://www.nuget.org/packages/PollyAzureKeyVault) | [![Downloads](https://img.shields.io/nuget/dt/PollyAzureKeyVault.svg)](https://www.nuget.org/packages/PollyAzureKeyVault) | Polly v8 resilience pipelines for Azure Key Vault — retry, timeout, and circuit-breaker for SecretClient, KeyClient, and CertificateClient |
| [PollyAzureQueueStorage](https://www.nuget.org/packages/PollyAzureQueueStorage) | [![Downloads](https://img.shields.io/nuget/dt/PollyAzureQueueStorage.svg)](https://www.nuget.org/packages/PollyAzureQueueStorage) | Polly v8 resilience pipelines for Azure Queue Storage — retry, timeout, and circuit-breaker for Azure.Storage.Queues QueueClient |
| [PollyRedis](https://www.nuget.org/packages/PollyRedis) | [![Downloads](https://img.shields.io/nuget/dt/PollyRedis.svg)](https://www.nuget.org/packages/PollyRedis) | Polly v8 resilience for StackExchange.Redis |
| [PollyAzureServiceBus](https://www.nuget.org/packages/PollyAzureServiceBus) | [![Downloads](https://img.shields.io/nuget/dt/PollyAzureServiceBus.svg)](https://www.nuget.org/packages/PollyAzureServiceBus) | Polly v8 resilience for Azure Service Bus — retry, circuit breaker, and timeout for sending and receiving messages |
| [PollyAzureBlob](https://www.nuget.org/packages/PollyAzureBlob) | [![Downloads](https://img.shields.io/nuget/dt/PollyAzureBlob.svg)](https://www.nuget.org/packages/PollyAzureBlob) | Polly v8 resilience pipelines for Azure Blob Storage — wrap BlobClient and BlobContainerClient operations with retry, timeout, circuit-breaker, and more using ResilientBlobClient and ResilientBlobContainerClient decorators |
| [PollyKafka](https://www.nuget.org/packages/PollyKafka) | [![Downloads](https://img.shields.io/nuget/dt/PollyKafka.svg)](https://www.nuget.org/packages/PollyKafka) | Polly v8 resilience for Confluent.Kafka — retry, circuit breaker, and timeout for producers and consumers |
| [PollyAzureTableStorage](https://www.nuget.org/packages/PollyAzureTableStorage) | [![Downloads](https://img.shields.io/nuget/dt/PollyAzureTableStorage.svg)](https://www.nuget.org/packages/PollyAzureTableStorage) | Polly v8 resilience pipelines for Azure Table Storage — retry, timeout, and circuit-breaker for Azure.Data.Tables TableClient |

## 💼 Need .NET consulting?

The author of this package is available for consulting on **Polly v8 resilience**, **Azure cloud architecture**, and **clean .NET design**.

**[→ solidqualitysolutions.com](https://www.solidqualitysolutions.com/)** · **[LinkedIn](https://www.linkedin.com/in/justbannister/)**
## License

MIT © [Justin Bannister](https://github.com/Swevo)