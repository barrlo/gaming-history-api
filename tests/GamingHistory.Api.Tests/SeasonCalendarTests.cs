using System.Globalization;
using Xunit;

namespace GamingHistory.Api.Tests;

public sealed class SeasonCalendarTests
{
    [Theory]
    [InlineData("2026-07-21T14:59:59.9999999Z", null)]
    [InlineData("2026-07-21T15:00:00Z", "first")]
    [InlineData("2026-10-20T14:59:59.9999999Z", "first")]
    [InlineData("2026-10-20T15:00:00Z", "second")]
    [InlineData("2027-01-19T15:00:00Z", null)]
    public void ShouldSelectSeasonAtInclusiveStartAndExclusiveEnd(string timestamp, string? expected)
    {
        var calendar = new SeasonCalendar([
            new SeasonDefinition("second", "Second", Instant("2026-10-20T15:00:00Z"), Instant("2027-01-19T15:00:00Z")),
            new SeasonDefinition("first", "First", Instant("2026-07-21T15:00:00Z"), Instant("2026-10-20T15:00:00Z"))
        ]);

        Assert.Equal(expected, calendar.GetCurrentSeason(Instant(timestamp))?.Id);
    }

    [Fact]
    public void ShouldReturnNoSeasonDuringConfiguredGap()
    {
        var calendar = new SeasonCalendar([
            new SeasonDefinition("first", "First", Instant("2026-07-21T15:00:00Z"), Instant("2026-10-20T15:00:00Z")),
            new SeasonDefinition("next", "Next", Instant("2026-11-03T15:00:00Z"))
        ]);

        Assert.Null(calendar.GetCurrentSeason(Instant("2026-10-27T15:00:00Z")));
    }

    [Theory]
    [InlineData("2023-05-31T15:00:00Z", "2024-02-29T15:00:00Z")]
    [InlineData("2024-05-31T15:00:00Z", "2025-02-28T15:00:00Z")]
    [InlineData("2026-10-20T10:00:00-05:00", "2027-07-20T15:00:00Z")]
    public void ShouldEstimateNineCalendarMonthsWithUtcTimeAndMonthEndClamping(string start, string end)
    {
        var calendar = new SeasonCalendar([new SeasonDefinition("stable-id", "Season", Instant(start))]);
        var season = calendar.GetCurrentSeason(Instant(start));

        Assert.NotNull(season);
        Assert.Equal(Instant(end), season.EndsAt);
        Assert.Equal(TimeSpan.Zero, season.StartsAt.Offset);
        Assert.True(season.EndDateEstimated);
        Assert.Null(calendar.GetCurrentSeason(Instant(end)));
    }

    [Theory]
    [InlineData("2026-10-20T15:00:00Z")]
    [InlineData("2027-07-20T15:00:00Z")]
    public void ShouldUseConfiguredSuccessorForUnknownEndAndKeepIdentity(string successorStart)
    {
        var start = Instant("2026-07-21T15:00:00Z");
        var nextStart = Instant(successorStart);
        var calendar = new SeasonCalendar([
            new SeasonDefinition("first", "First", start), new SeasonDefinition("next", "Next", nextStart)
        ]);
        var first = calendar.GetCurrentSeason(start);

        Assert.NotNull(first);
        Assert.Equal("first", first.Id);
        Assert.Equal(nextStart, first.EndsAt);
        Assert.True(first.EndDateEstimated);
        Assert.Equal("next", calendar.GetCurrentSeason(nextStart)?.Id);
    }

    [Fact]
    public void ShouldReplaceEstimateWithAnnouncedEndWithoutChangingIdentity()
    {
        var start = Instant("2026-07-21T15:00:00Z");
        var announcedEnd = start.AddMonths(3);
        var calendar = new SeasonCalendar([new SeasonDefinition("stable-id", "Season", start, announcedEnd)]);
        var season = calendar.GetCurrentSeason(start);

        Assert.NotNull(season);
        Assert.Equal("stable-id", season.Id);
        Assert.Equal(announcedEnd, season.EndsAt);
        Assert.False(season.EndDateEstimated);
    }

    [Fact]
    public void ShouldRejectInvalidConfigurationInsteadOfTreatingItAsGap()
    {
        var start = Instant("2026-07-21T15:00:00Z");

        Assert.Throws<ArgumentException>(() => new SeasonCalendar([]));
        Assert.Throws<ArgumentException>(() => new SeasonCalendar([new SeasonDefinition("", "Season", start)]));
        Assert.Throws<ArgumentException>(() => new SeasonCalendar([new SeasonDefinition("first", "", start)]));
        Assert.Throws<ArgumentException>(() => new SeasonCalendar([new SeasonDefinition("first", "First", start, start)]));
        Assert.Throws<ArgumentException>(() => new SeasonCalendar([new SeasonDefinition("first", "First", start, start.AddDays(-1))]));
        Assert.Throws<ArgumentException>(() => new SeasonCalendar([
            new SeasonDefinition("same", "First", start, start.AddMonths(1)), new SeasonDefinition("same", "Next", start.AddMonths(2))
        ]));
        Assert.Throws<ArgumentException>(() => new SeasonCalendar([
            new SeasonDefinition("first", "First", start, start.AddMonths(3)), new SeasonDefinition("next", "Next", start.AddMonths(2))
        ]));
        Assert.Throws<ArgumentException>(() => new SeasonCalendar([
            new SeasonDefinition("first", "First", start), new SeasonDefinition("next", "Next", start)
        ]));
    }

    [Theory]
    [InlineData("2026-09-15T14:59:59.9999999Z", "2026-09-08T15:00:00Z")]
    [InlineData("2026-09-15T15:00:00Z", "2026-09-15T15:00:00Z")]
    [InlineData("2026-09-15T15:00:00.0000001Z", "2026-09-15T15:00:00Z")]
    [InlineData("2026-07-14T10:00:00-05:00", "2026-07-14T15:00:00Z")]
    [InlineData("2026-01-13T09:00:00-06:00", "2026-01-13T15:00:00Z")]
    [InlineData("2026-03-10T09:59:59-05:00", "2026-03-03T15:00:00Z")]
    [InlineData("2026-11-03T08:59:59-06:00", "2026-10-27T15:00:00Z")]
    public void ShouldKeepTuesdayResetFixedInUtcAcrossDaylightSavingChanges(string timestamp, string expected)
    {
        var actual = SeasonCalendar.GetWeekStart(Instant(timestamp));

        Assert.Equal(Instant(expected), actual);
        Assert.Equal(TimeSpan.Zero, actual.Offset);
    }

    [Fact]
    public void ShouldClipPartialWeeksToSeasonBoundaries()
    {
        var start = Instant("2026-07-23T17:00:00Z");
        var end = Instant("2026-08-01T18:00:00Z");
        var calendar = new SeasonCalendar([new SeasonDefinition("partial", "Partial", start, end)]);
        var season = calendar.GetCurrentSeason(start)!;
        var first = SeasonCalendar.GetSeasonWeek(season, start);
        var last = SeasonCalendar.GetSeasonWeek(season, end.AddTicks(-1));

        Assert.Equal((start, Instant("2026-07-28T15:00:00Z")), first);
        Assert.Equal((Instant("2026-07-28T15:00:00Z"), end), last);
        Assert.Throws<ArgumentOutOfRangeException>(() => SeasonCalendar.GetSeasonWeek(season, start.AddTicks(-1)));
        Assert.Throws<ArgumentOutOfRangeException>(() => SeasonCalendar.GetSeasonWeek(season, end));
    }

    private static DateTimeOffset Instant(string value) => DateTimeOffset.Parse(value, CultureInfo.InvariantCulture);
}
