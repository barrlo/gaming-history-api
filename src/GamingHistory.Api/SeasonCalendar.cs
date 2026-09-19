namespace GamingHistory.Api;

public sealed record SeasonDefinition(string Id, string Name, DateTimeOffset StartsAt, DateTimeOffset? EndsAt = null);

public sealed record Season(string Id, string Name, DateTimeOffset StartsAt, DateTimeOffset EndsAt, bool EndDateEstimated)
{
    public static string Region => "us";
}

// Dates describe configured seasons; an estimated end never invents the next season.
public sealed class SeasonCalendar
{
    private readonly Season[] _seasons;

    public SeasonCalendar(IEnumerable<SeasonDefinition> definitions)
    {
        var ordered = definitions.OrderBy(season => season.StartsAt).ToArray();

        if (ordered.Length == 0 || ordered.Any(season => string.IsNullOrWhiteSpace(season.Id) || string.IsNullOrWhiteSpace(season.Name)) ||
            ordered.Select(season => season.Id).Distinct(StringComparer.Ordinal).Count() != ordered.Length)
        {
            throw new ArgumentException("Season configuration requires named seasons with unique stable IDs.", nameof(definitions));
        }

        _seasons = new Season[ordered.Length];

        for (var index = 0; index < ordered.Length; index++)
        {
            var definition = ordered[index];
            var startsAt = definition.StartsAt.ToUniversalTime();
            var endsAt = definition.EndsAt?.ToUniversalTime() ?? startsAt.AddMonths(9);
            var nextStart = index + 1 < ordered.Length ? ordered[index + 1].StartsAt : (DateTimeOffset?)null;

            if (definition.EndsAt is null && nextStart is not null)
            {
                endsAt = nextStart.Value.ToUniversalTime();
            }

            if (endsAt <= startsAt || nextStart < endsAt)
            {
                throw new ArgumentException("Season dates must have a positive duration and cannot overlap confirmed dates.", nameof(definitions));
            }

            _seasons[index] = new Season(definition.Id, definition.Name, startsAt, endsAt, definition.EndsAt is null);
        }
    }

    public Season? GetCurrentSeason(DateTimeOffset timestamp) =>
        _seasons.SingleOrDefault(season => season.StartsAt <= timestamp && timestamp < season.EndsAt);

    // Reset stays at 15:00 UTC in both Central daylight and standard time.
    public static DateTimeOffset GetWeekStart(DateTimeOffset timestamp)
    {
        var instant = timestamp.ToUniversalTime();
        var daysSinceTuesday = ((int)instant.DayOfWeek - (int)DayOfWeek.Tuesday + 7) % 7;
        var boundary = new DateTimeOffset(instant.Date, TimeSpan.Zero).AddDays(-daysSinceTuesday).AddHours(15);

        return boundary > instant ? boundary.AddDays(-7) : boundary;
    }

    public static (DateTimeOffset StartsAt, DateTimeOffset EndsAt) GetSeasonWeek(Season season, DateTimeOffset timestamp)
    {
        if (timestamp < season.StartsAt || timestamp >= season.EndsAt)
        {
            throw new ArgumentOutOfRangeException(nameof(timestamp), "The requested instant must be inside the season.");
        }

        var startsAt = GetWeekStart(timestamp);
        var endsAt = startsAt.AddDays(7);

        return (startsAt < season.StartsAt ? season.StartsAt : startsAt, endsAt > season.EndsAt ? season.EndsAt : endsAt);
    }
}
