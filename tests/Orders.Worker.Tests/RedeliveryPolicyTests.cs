using Orders.Worker.Processing;

namespace Orders.Worker.Tests;

public class RedeliveryPolicyTests
{
    [Theory]
    [InlineData(1, 2)]
    [InlineData(2, 4)]
    [InlineData(5, 32)]
    public void Delay_grows_exponentially(int attempt, int expectedSeconds)
    {
        Assert.Equal(TimeSpan.FromSeconds(expectedSeconds), RedeliveryPolicy.DelayFor(attempt));
    }

    [Fact]
    public void Delay_is_capped_at_five_minutes()
    {
        Assert.All(Enumerable.Range(1, RedeliveryPolicy.MaxAttempts - 1),
            attempt => Assert.True(RedeliveryPolicy.DelayFor(attempt) <= TimeSpan.FromMinutes(5)));
    }

    [Fact]
    public void Gives_up_after_max_attempts()
    {
        Assert.Null(RedeliveryPolicy.DelayFor(RedeliveryPolicy.MaxAttempts));
    }
}
