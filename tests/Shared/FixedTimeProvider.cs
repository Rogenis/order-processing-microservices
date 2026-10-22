namespace Orders.Tests.Support;

public sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
{
    public static readonly DateTimeOffset Default = new(2026, 10, 21, 12, 0, 0, TimeSpan.Zero);

    public DateTimeOffset Now { get; set; } = now;

    public FixedTimeProvider()
        : this(Default)
    {
    }

    public override DateTimeOffset GetUtcNow() => Now;
}
