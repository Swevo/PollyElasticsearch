/// <summary>
/// Pre-built Polly <see cref="PredicateBuilder"/> for common Elasticsearch transient errors.
/// Covers rate limiting (429), service unavailability (503), gateway timeouts (504),
/// and connection-level <see cref="TransportException"/> failures.
/// </summary>
public static class ElasticTransientErrors
{
    /// <summary>
    /// HTTP status codes returned by Elasticsearch that indicate a transient failure safe to retry.
    /// </summary>
    public static readonly IReadOnlySet<int> StatusCodes = new HashSet<int>
    {
        429, // TooManyRequests — rate limited by Elasticsearch or a proxy
        503, // ServiceUnavailable — cluster unavailable or performing maintenance
        504, // GatewayTimeout — proxy or load balancer timed out
    };

    /// <summary>
    /// A <see cref="PredicateBuilder"/> that handles <see cref="ElasticTransientException"/>
    /// (thrown by <see cref="ResilientElasticsearchClient"/> for 429/503/504 responses)
    /// and <see cref="TransportException"/> (thrown for connection-level failures).
    /// Assign to <c>ShouldHandle</c> on any Polly strategy.
    /// </summary>
    public static readonly PredicateBuilder IsTransient =
        (PredicateBuilder)new PredicateBuilder()
            .Handle<ElasticTransientException>()
            .Handle<TransportException>();
}
