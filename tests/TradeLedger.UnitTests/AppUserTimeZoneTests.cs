using TradeLedger.Core.Domain;

namespace TradeLedger.UnitTests;

public class AppUserTimeZoneTests
{
    [Fact]
    public void DefaultsToUtc()
    {
        Assert.Equal("UTC", new AppUser().TimeZoneId);
    }

    [Theory]
    [InlineData("Asia/Tehran")]
    [InlineData("Europe/London")]
    [InlineData("America/Argentina/Buenos_Aires")]
    [InlineData("America/Port-au-Prince")]
    [InlineData("Etc/GMT+5")]
    public void AcceptsIanaIds(string id)
    {
        var user = new AppUser();

        user.SetTimeZone(id);

        Assert.Equal(id, user.TimeZoneId);
    }

    [Theory]
    [InlineData("utc", "UTC")]
    [InlineData("UTC", "UTC")]
    [InlineData("  Asia/Tehran  ", "Asia/Tehran")]
    public void CanonicalisesCaseAndSurroundingSpace(string input, string expected)
    {
        var user = new AppUser();

        user.SetTimeZone(input);

        Assert.Equal(expected, user.TimeZoneId);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("Tehran")]
    [InlineData("Iran Standard Time")]
    [InlineData("Asia/")]
    [InlineData("/Tehran")]
    [InlineData("Asia//Tehran")]
    [InlineData("1sia/Tehran")]
    [InlineData("Asia/Teh<ran")]
    public void RejectsAnythingThatIsNotAnIanaId(string id)
    {
        var user = new AppUser();

        var error = Assert.Throws<DomainValidationException>(() => user.SetTimeZone(id));

        Assert.Equal(400, error.StatusCode);
        Assert.True(error.Errors.ContainsKey("timeZoneId"));
        Assert.Equal("UTC", user.TimeZoneId);
    }

    [Fact]
    public void RejectsAnIdLongerThanTheColumn()
    {
        var user = new AppUser();
        var id = "Area/" + new string('a', AppUser.MaxTimeZoneIdLength);

        Assert.Throws<DomainValidationException>(() => user.SetTimeZone(id));
        Assert.Equal("UTC", user.TimeZoneId);
    }

    /// <summary>
    /// The whole point of the field. Changing the zone must not move an instant —
    /// a backtest opened at 05:00 UTC is still 05:00 UTC, it just reads 08:30 locally.
    /// </summary>
    [Fact]
    public void IsDisplayOnlyAndMovesNoInstant()
    {
        var openedAt = new DateTimeOffset(2026, 9, 3, 5, 0, 0, TimeSpan.Zero);

        var user = new AppUser();
        user.SetTimeZone("Asia/Tehran");

        Assert.Equal(
            new DateTimeOffset(2026, 9, 3, 5, 0, 0, TimeSpan.Zero),
            openedAt);
        Assert.Equal(1788411600000L, openedAt.ToUnixTimeMilliseconds());
    }
}
