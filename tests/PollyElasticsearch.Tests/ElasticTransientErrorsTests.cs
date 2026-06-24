public class ElasticTransientErrorsTests
{
    [Theory]
    [InlineData(429)]
    [InlineData(503)]
    [InlineData(504)]
    public void StatusCodes_ContainsTransientStatusCode(int statusCode)
    {
        Assert.Contains(statusCode, ElasticTransientErrors.StatusCodes);
    }

    [Theory]
    [InlineData(200)]
    [InlineData(400)]
    [InlineData(401)]
    [InlineData(403)]
    [InlineData(404)]
    [InlineData(500)]
    public void StatusCodes_DoesNotContainNonTransientStatusCode(int statusCode)
    {
        Assert.DoesNotContain(statusCode, ElasticTransientErrors.StatusCodes);
    }

    [Fact]
    public void StatusCodes_HasThreeEntries()
    {
        Assert.Equal(3, ElasticTransientErrors.StatusCodes.Count);
    }

    [Fact]
    public void IsTransient_IsNotNull()
    {
        Assert.NotNull(ElasticTransientErrors.IsTransient);
    }
}
