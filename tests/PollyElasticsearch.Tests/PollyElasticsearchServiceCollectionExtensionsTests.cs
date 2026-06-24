public class PollyElasticsearchServiceCollectionExtensionsTests
{
    private static readonly ElasticsearchClient _client =
        new(new ElasticsearchClientSettings(new Uri("http://localhost:9200")));

    [Fact]
    public void AddPollyElasticsearch_RegistersResiliencePipeline()
    {
        var services = new ServiceCollection();
        services.AddSingleton(_client);
        services.AddPollyElasticsearch(p => p.AddRetry(new RetryStrategyOptions
        {
            MaxRetryAttempts = 3,
            ShouldHandle = ElasticTransientErrors.IsTransient,
        }));

        var provider = services.BuildServiceProvider();
        Assert.NotNull(provider.GetRequiredService<ResiliencePipeline>());
    }

    [Fact]
    public void AddPollyElasticsearch_RegistersResilientClient()
    {
        var services = new ServiceCollection();
        services.AddSingleton(_client);
        services.AddPollyElasticsearch(p => { });

        var provider = services.BuildServiceProvider();
        var resilient = provider.GetRequiredService<ResilientElasticsearchClient>();

        Assert.NotNull(resilient);
        Assert.Same(_client, resilient.Inner);
    }

    [Fact]
    public void AddPollyElasticsearch_WithUri_RegistersClient()
    {
        var services = new ServiceCollection();
        services.AddPollyElasticsearch(new Uri("http://localhost:9200"), p => { });

        var provider = services.BuildServiceProvider();
        Assert.NotNull(provider.GetRequiredService<ResilientElasticsearchClient>());
    }

    [Fact]
    public void AddPollyElasticsearch_ReturnsServiceCollection()
    {
        var services = new ServiceCollection();
        services.AddSingleton(_client);

        var result = services.AddPollyElasticsearch(p => { });

        Assert.Same(services, result);
    }
}
