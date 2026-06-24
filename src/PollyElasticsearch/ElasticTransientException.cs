/// <summary>
/// Exception thrown by <see cref="ResilientElasticsearchClient"/> when Elasticsearch
/// returns a transient HTTP status code (429, 503, 504) so that Polly can handle it.
/// </summary>
public sealed class ElasticTransientException : Exception
{
    /// <summary>The HTTP status code returned by Elasticsearch.</summary>
    public int StatusCode { get; }

    /// <inheritdoc cref="ElasticTransientException"/>
    public ElasticTransientException(int statusCode)
        : base($"Elasticsearch transient error: HTTP {statusCode}. Retry is safe.")
    {
        StatusCode = statusCode;
    }

    /// <inheritdoc cref="ElasticTransientException"/>
    public ElasticTransientException(int statusCode, Exception inner)
        : base($"Elasticsearch transient error: HTTP {statusCode}. Retry is safe.", inner)
    {
        StatusCode = statusCode;
    }
}
