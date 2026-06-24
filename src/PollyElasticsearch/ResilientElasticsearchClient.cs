/// <summary>
/// Wraps an <see cref="ElasticsearchClient"/> with a Polly v8 <see cref="ResiliencePipeline"/>,
/// applying retry, timeout, and circuit-breaker to every operation.
/// Automatically throws <see cref="ElasticTransientException"/> for 429, 503, and 504
/// responses so Polly can retry them.
/// </summary>
public sealed class ResilientElasticsearchClient(ElasticsearchClient client, ResiliencePipeline pipeline)
{
    /// <summary>The underlying <see cref="ElasticsearchClient"/>.</summary>
    public ElasticsearchClient Inner => client;

    /// <summary>
    /// Executes any Elasticsearch operation, protected by the resilience pipeline.
    /// Throws <see cref="ElasticTransientException"/> when the response HTTP status code
    /// is in <see cref="ElasticTransientErrors.StatusCodes"/> so Polly can retry it.
    /// </summary>
    public Task<TResponse> ExecuteAsync<TResponse>(
        Func<ElasticsearchClient, CancellationToken, Task<TResponse>> operation,
        CancellationToken cancellationToken = default)
        where TResponse : ElasticsearchResponse
        => pipeline.ExecuteAsync(async ct =>
        {
            var response = await operation(client, ct);

            if (!response.IsValidResponse)
            {
                var status = response.ApiCallDetails?.HttpStatusCode;
                if (status.HasValue && ElasticTransientErrors.StatusCodes.Contains(status.Value))
                    throw new ElasticTransientException(status.Value);
            }

            return response;
        }, cancellationToken).AsTask();
}
