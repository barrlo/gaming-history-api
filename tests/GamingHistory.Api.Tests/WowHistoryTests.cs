using System.Globalization;
using Xunit;

namespace GamingHistory.Api.Tests;

public sealed class WowHistoryTests
{
    private static readonly Season Season = new("season", "Season", At("2026-07-21T15:00:00Z"),
        At("2026-08-18T15:00:00Z"), false);

    [Fact]
    public void ShouldKeepLeadingMissingWeeksNullAndCarryAnObservedZero()
    {
        WowObservation[] observations = [new("character", "season", 0m, At("2026-08-03T11:00:00Z"))];
        var history = WowHistory.Project("character", Season, Season.EndsAt, observations);

        Assert.Equal(4, history.Weeks.Count);
        Assert.Equal("unobserved", history.Weeks[0].Status);
        Assert.Null(history.Weeks[0].Score);
        Assert.Null(history.Weeks[0].SourceObservedAt);
        Assert.Equal("observed", history.Weeks[1].Status);
        Assert.Equal(0m, history.Weeks[1].Score);
        Assert.Equal("carriedForward", history.Weeks[2].Status);
        Assert.Equal(0m, history.Weeks[2].Score);
        Assert.Equal("2026-08-03T11:00:00Z", history.Weeks[3].SourceObservedAt);
        Assert.Equal(new WowWeeklySummary(0m, 0m, "2026-08-03T11:00:00Z"), history.WeeklySummary);
    }

    [Fact]
    public void ShouldUseLatestObservationWithinEachClosedWeekAndPreserveDecreases()
    {
        WowObservation[] observations =
        [
            new("character", "season", 900.25m, At("2026-07-27T11:00:00Z")),
            new("character", "season", 300m, At("2026-07-22T11:00:00Z")),
            new("character", "season", 850.5m, At("2026-08-03T11:00:00Z")),
            new("character", "season", 1000m, At("2026-08-04T15:00:00Z")),
            new("character", "season", 2000m, At("2026-08-10T11:00:00Z"))
        ];
        var history = WowHistory.Project("character", Season, At("2026-08-04T15:00:00Z"), observations);

        Assert.Equal(2, history.Weeks.Count);
        Assert.Equal(900.25m, history.Weeks[0].Score);
        Assert.Equal(850.5m, history.Weeks[1].Score);
        Assert.Equal(new WowWeeklySummary(850.5m, -49.75m, "2026-08-03T11:00:00Z"), history.WeeklySummary);
    }

    [Fact]
    public void ShouldCloseTheSavedWeekExactlyAtResetAndAssignBoundaryObservationToTheNextWeek()
    {
        WowObservation[] observations =
        [
            new("character", "season", 800m, At("2026-07-28T11:00:00Z")),
            new("character", "season", 900m, At("2026-07-28T15:00:00Z"))
        ];
        var before = WowHistory.Project("character", Season, At("2026-07-28T14:59:59Z"), observations);
        var boundary = WowHistory.Project("character", Season, At("2026-07-28T15:00:00Z"), observations);
        var next = WowHistory.Project("character", Season, At("2026-08-04T15:00:00Z"), observations);

        Assert.Empty(before.Weeks);
        Assert.Null(before.WeeklySummary.LatestScore);
        Assert.Single(boundary.Weeks);
        Assert.Equal(800m, boundary.Weeks[0].Score);
        Assert.False(boundary.Weeks[0].IsProvisional);
        Assert.Null(boundary.WeeklySummary.WeeklyChange);
        Assert.Equal(900m, next.Weeks[1].Score);
        Assert.Equal(100m, next.WeeklySummary.WeeklyChange);
    }

    [Fact]
    public void ShouldIgnoreOtherCharactersSeasonsAndObservationsOutsideTheSeason()
    {
        WowObservation[] observations =
        [
            new("other", "season", 100m, At("2026-07-27T11:00:00Z")),
            new("character", "previous-season", 200m, At("2026-07-27T11:00:00Z")),
            new("character", "season", 300m, At("2026-07-21T14:59:59Z")),
            new("character", "season", 400m, Season.EndsAt)
        ];
        var history = WowHistory.Project("character", Season, Season.EndsAt.AddDays(7), observations);

        Assert.Equal(4, history.Weeks.Count);
        Assert.All(history.Weeks, week =>
        {
            Assert.Equal("unobserved", week.Status);
            Assert.Null(week.Score);
            Assert.Null(week.SourceObservedAt);
        });
        Assert.Equal(new WowWeeklySummary(null, null, null), history.WeeklySummary);
    }

    [Fact]
    public void ShouldClipPartialSeasonWeeksAndStopAtTheSeasonEnd()
    {
        var partial = new Season("season", "Partial", At("2026-07-23T18:00:00Z"),
            At("2026-08-01T18:00:00Z"), false);
        WowObservation[] observations =
        [
            new("character", "season", 100m, partial.StartsAt),
            new("character", "season", 200m, partial.EndsAt.AddSeconds(-1)),
            new("character", "season", 300m, partial.EndsAt)
        ];
        var beforeEnd = WowHistory.Project("character", partial, partial.EndsAt.AddSeconds(-1), observations);
        var history = WowHistory.Project("character", partial, partial.EndsAt.AddDays(7), observations);

        Assert.Single(beforeEnd.Weeks);
        Assert.Equal(2, history.Weeks.Count);
        Assert.Equal(1, history.Weeks[0].Index);
        Assert.Equal("2026-07-23T18:00:00Z", history.Weeks[0].StartsAt);
        Assert.Equal("2026-07-28T15:00:00Z", history.Weeks[0].EndsAt);
        Assert.Equal(2, history.Weeks[1].Index);
        Assert.Equal("2026-07-28T15:00:00Z", history.Weeks[1].StartsAt);
        Assert.Equal("2026-08-01T18:00:00Z", history.Weeks[1].EndsAt);
        Assert.Equal(200m, history.WeeklySummary.LatestScore);
    }

    [Fact]
    public void ShouldReturnNoCompletedWeeksDuringAGapOrAtSeasonStart()
    {
        WowObservation[] observations = [new("character", "season", 100m, Season.StartsAt)];
        var gap = WowHistory.Project("character", null, Season.EndsAt, observations);
        var start = WowHistory.Project("character", Season, Season.StartsAt, observations);

        Assert.Empty(gap.Weeks);
        Assert.Empty(start.Weeks);
        Assert.Equal(new WowWeeklySummary(null, null, null), gap.WeeklySummary);
        Assert.Equal(new WowWeeklySummary(null, null, null), start.WeeklySummary);
    }

    private static DateTimeOffset At(string instant) => DateTimeOffset.Parse(instant, CultureInfo.InvariantCulture);
}
