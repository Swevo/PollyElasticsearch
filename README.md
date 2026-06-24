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

## Related packages

| Package | Description |
|---------|-------------|
| [PollyRedis](https://github.com/Swevo/PollyRedis) | Polly v8 for StackExchange.Redis |
| [PollyEFCore](https://github.com/Swevo/PollyEFCore) | Polly v8 for Entity Framework Core |
| [PollyDapper](https://github.com/Swevo/PollyDapper) | Polly v8 for Dapper |
| [PollyMongo](https://github.com/Swevo/PollyMongo) | Polly v8 for MongoDB |
| [PollyNpgsql](https://github.com/Swevo/PollyNpgsql) | Polly v8 for Npgsql (PostgreSQL) |
| [PollySqlClient](https://github.com/Swevo/PollySqlClient) | Polly v8 for Microsoft.Data.SqlClient |
| [PollyCosmosDb](https://github.com/Swevo/PollyCosmosDb) | Polly v8 for Azure Cosmos DB |
| [PollyAzureBlob](https://github.com/Swevo/PollyAzureBlob) | Polly v8 for Azure Blob Storage |
| [PollyAzureServiceBus](https://github.com/Swevo/PollyAzureServiceBus) | Polly v8 for Azure Service Bus |
| [PollyGrpc](https://github.com/Swevo/PollyGrpc) | Polly v8 for gRPC |
| [PollyRabbitMQ](https://github.com/Swevo/PollyRabbitMQ) | Polly v8 for RabbitMQ |
| [PollyKafka](https://github.com/Swevo/PollyKafka) | Polly v8 for Confluent.Kafka |
| [PollySignalR](https://github.com/Swevo/PollySignalR) | Polly v8 for SignalR |
| [PollyOpenAI](https://github.com/Swevo/PollyOpenAI) | Polly v8 for OpenAI .NET SDK |
| [PollyMediatR](https://github.com/Swevo/PollyMediatR) | Polly v8 for MediatR |
| [PollyHealthChecks](https://github.com/Swevo/PollyHealthChecks) | Polly v8 for ASP.NET Core Health Checks |
| [PollyAzureKeyVault](https://github.com/Swevo/PollyAzureKeyVault) | Polly v8 for Azure Key Vault |
| [PollyBackoff](https://github.com/Swevo/PollyBackoff) | Polly v8 backoff helpers |

## License

MIT © [Justin Bannister](https://github.com/Swevo)