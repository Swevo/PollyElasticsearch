public class ElasticTransientExceptionTests
{
    [Fact]
    public void Constructor_SetsStatusCode()
    {
        var ex = new ElasticTransientException(429);
        Assert.Equal(429, ex.StatusCode);
    }

    [Fact]
    public void Constructor_MessageContainsStatusCode()
    {
        var ex = new ElasticTransientException(503);
        Assert.Contains("503", ex.Message);
    }

    [Fact]
    public void Constructor_WithInner_SetsInnerException()
    {
        var inner = new Exception("original");
        var ex = new ElasticTransientException(429, inner);
        Assert.Same(inner, ex.InnerException);
        Assert.Equal(429, ex.StatusCode);
    }

    [Fact]
    public void IsException()
    {
        Assert.IsAssignableFrom<Exception>(new ElasticTransientException(429));
    }
}
