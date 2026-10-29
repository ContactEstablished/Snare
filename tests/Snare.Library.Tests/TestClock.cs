namespace Snare.Library.Tests;

/// <summary>A clock tests can set, in a fixed UTC-4 zone (Matt's EDT).</summary>
internal sealed class TestClock(DateTimeOffset now) : TimeProvider
{
    private static readonly TimeZoneInfo Edt =
        TimeZoneInfo.CreateCustomTimeZone("Test EDT", TimeSpan.FromHours(-4), "Test EDT", "Test EDT");

    public DateTimeOffset Now { get; set; } = now;

    public override DateTimeOffset GetUtcNow() => Now.ToUniversalTime();

    public override TimeZoneInfo LocalTimeZone => Edt;
}
