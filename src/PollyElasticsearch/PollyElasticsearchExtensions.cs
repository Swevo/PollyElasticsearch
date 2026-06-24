/// <summary>Extension methods for adding Polly resilience to Elasticsearch clients.</summary>
public static class PollyElasticsearchExtensions
{
    /// <summary>Wraps an <see cref="ElasticsearchClient"/> with the given <see cref="ResiliencePipeline"/>.</summary>
    public static ResilientElasticsearchClient WithPolly(
        this ElasticsearchClient client,
        ResiliencePipeline pipeline)
        => new(client, pipeline);

    /// <summary>Wraps an <see cref="ElasticsearchClient"/> with a pipeline built by <paramref name="configure"/>.</summary>
    public static ResilientElasticsearchClient WithPolly(
        this ElasticsearchClient client,
        Action<ResiliencePipelineBuilder> configure)
    {
        var builder = new ResiliencePipelineBuilder();
        configure(builder);
        return new(client, builder.Build());
    }
}
