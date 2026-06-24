/// <summary>Dependency-injection extensions for <c>PollyElasticsearch</c>.</summary>
public static class PollyElasticsearchServiceCollectionExtensions
{
    /// <summary>
    /// Registers a singleton <see cref="ResiliencePipeline"/> built by <paramref name="configure"/>
    /// and a transient <see cref="ResilientElasticsearchClient"/> that wraps the
    /// <see cref="ElasticsearchClient"/> registered in the DI container.
    /// </summary>
    public static IServiceCollection AddPollyElasticsearch(
        this IServiceCollection services,
        Action<ResiliencePipelineBuilder> configure)
    {
        var builder = new ResiliencePipelineBuilder();
        configure(builder);
        var pipeline = builder.Build();

        services.AddSingleton(pipeline);
        services.AddTransient<ResilientElasticsearchClient>(sp =>
            sp.GetRequiredService<ElasticsearchClient>().WithPolly(pipeline));

        return services;
    }

    /// <summary>
    /// Registers a singleton <see cref="ElasticsearchClient"/> for <paramref name="nodeUri"/>,
    /// then registers the resilience pipeline and <see cref="ResilientElasticsearchClient"/>.
    /// </summary>
    public static IServiceCollection AddPollyElasticsearch(
        this IServiceCollection services,
        Uri nodeUri,
        Action<ResiliencePipelineBuilder> configure)
    {
        services.AddSingleton(new ElasticsearchClient(new ElasticsearchClientSettings(nodeUri)));
        return services.AddPollyElasticsearch(configure);
    }
}
