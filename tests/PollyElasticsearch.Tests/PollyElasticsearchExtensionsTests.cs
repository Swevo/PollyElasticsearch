public class PollyElasticsearchExtensionsTests
{
    private static readonly ElasticsearchClient _client =
        new(new ElasticsearchClientSettings(new Uri("http://localhost:9200")));
    private static readonly ResiliencePipeline _pipeline = new ResiliencePipelineBuilder().Build();

    [Fact]
    public void WithPolly_Pipeline_ReturnsResilientClient()
    {
        var resilient = _client.WithPolly(_pipeline);

        Assert.NotNull(resilient);
        Assert.Same(_client, resilient.Inner);
    }

    [Fact]
    public void WithPolly_Configure_ReturnsResilientClient()
    {
        var resilient = _client.WithPolly(p => p.AddRetry(new RetryStrategyOptions
        {
            MaxRetryAttempts = 3,
            ShouldHandle = ElasticTransientErrors.IsTransient,
        }));

        Assert.NotNull(resilient);
        Assert.Same(_client, resilient.Inner);
    }

    [Fact]
    public void WithPolly_InnerIsOriginalClient()
    {
        var resilient = _client.WithPolly(_pipeline);

        Assert.Same(_client, resilient.Inner);
    }
}
