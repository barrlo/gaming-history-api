using System.Globalization;

namespace GamingHistory.Api;

public sealed record WowObservation(string CharacterId, string SeasonId, decimal Score, DateTimeOffset ObservedAt);

public sealed record WowHistoryWeek(int Index, string StartsAt, string EndsAt, bool IsProvisional,
    string Status, decimal? Score, string? SourceObservedAt);

public sealed record WowWeeklySummary(decimal? LatestScore, decimal? WeeklyChange, string? LastObservedAt);

public sealed record WowHistory(IReadOnlyList<WowHistoryWeek> Weeks, WowWeeklySummary WeeklySummary)
{
    // Only scheduled observations belong here; on-demand scores remain independent.
    public static WowHistory Project(string characterId, Season? season, DateTimeOffset clock,
        IEnumerable<WowObservation> observations)
    {
        var weeks = new List<WowHistoryWeek>();
        WowObservation? latest = null;

        if (season is not null)
        {
            var scheduled = observations.Where(observation => observation.CharacterId == characterId &&
                observation.SeasonId == season.Id && observation.ObservedAt <= clock).ToArray();
            var cursor = season.StartsAt;

            while (cursor < season.EndsAt)
            {
                var (startsAt, endsAt) = SeasonCalendar.GetSeasonWeek(season, cursor);

                if (endsAt > clock)
                {
                    break;
                }

                var observed = scheduled.Where(observation => observation.ObservedAt >= startsAt &&
                    observation.ObservedAt < endsAt).MaxBy(observation => observation.ObservedAt);
                latest = observed ?? latest;
                weeks.Add(new WowHistoryWeek(weeks.Count + 1, Format(startsAt), Format(endsAt), false,
                    observed is not null ? "observed" : latest is null ? "unobserved" : "carriedForward",
                    latest?.Score, latest is null ? null : Format(latest.ObservedAt)));
                cursor = endsAt;
            }
        }

        var change = weeks.Count < 2 ? null : weeks[^1].Score - weeks[^2].Score;

        return new WowHistory(weeks, new WowWeeklySummary(latest?.Score, change,
            latest is null ? null : Format(latest.ObservedAt)));
    }

    private static string Format(DateTimeOffset instant) =>
        instant.UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture);
}
