using System.Globalization;
using Rok.ViewModels.Player.Services;

namespace Rok.PresentationTests.ViewModels.Player.Services;

public class SleepTimerBadgeFormatterTests
{
    private const string Minutes = "{0} min";
    private const string Seconds = "{0} s";

    [Fact(DisplayName = "when_timer_inactive_badge_is_empty")]
    public void Format_Inactive_ReturnsEmpty()
    {
        var result = SleepTimerBadgeFormatter.Format(false, 720, Minutes, Seconds);

        Assert.Equal(string.Empty, result);
    }

    [Theory(DisplayName = "when_timer_active_badge_shows_minutes_rounded_up_from_one_minute")]
    [InlineData(720, "12 min")]
    [InlineData(61, "2 min")]
    [InlineData(60, "1 min")]
    [InlineData(3600, "60 min")]
    public void Format_AtLeastOneMinute_ReturnsMinutesRoundedUp(int remaining, string expected)
    {
        var result = SleepTimerBadgeFormatter.Format(true, remaining, Minutes, Seconds);

        Assert.Equal(expected, result);
    }

    [Theory(DisplayName = "when_timer_active_below_one_minute_badge_shows_seconds")]
    [InlineData(59, "59 s")]
    [InlineData(1, "1 s")]
    [InlineData(0, "0 s")]
    [InlineData(-3, "0 s")]
    public void Format_BelowOneMinute_ReturnsSeconds(int remaining, string expected)
    {
        var result = SleepTimerBadgeFormatter.Format(true, remaining, Minutes, Seconds);

        Assert.Equal(expected, result);
    }

    [Fact(DisplayName = "when_formats_are_provided_badge_uses_them")]
    public void Format_CustomFormats_AreRespected()
    {
        var result = SleepTimerBadgeFormatter.Format(true, 720, "{0} хв", "{0} с");
        var seconds = SleepTimerBadgeFormatter.Format(true, 45, "{0} хв", "{0} с");

        Assert.Equal("12 хв", result);
        Assert.Equal("45 с", seconds);
    }
}