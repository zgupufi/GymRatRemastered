namespace GymRatService.IntegrationTests;

public sealed class OffsetTimeProvider : TimeProvider
{
    private long _offsetTicks;

    public override DateTimeOffset GetUtcNow() => base.GetUtcNow() + TimeSpan.FromTicks(Interlocked.Read(ref _offsetTicks));

    public void Advance(TimeSpan duration) => Interlocked.Add(ref _offsetTicks, duration.Ticks);
}
