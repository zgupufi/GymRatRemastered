namespace GymRatService.IntegrationTests;

public sealed class AdvanceableTimeProvider : TimeProvider
{
    private TimeSpan _offset = TimeSpan.Zero;

    public void Advance(TimeSpan amount) => _offset += amount;

    public override DateTimeOffset GetUtcNow() => base.GetUtcNow() + _offset;
}
