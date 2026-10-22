using System.Text.Json.Serialization;

namespace GamingHistory.Api;

public sealed record PoeAscendancy(string? Name, string Status);
public sealed record PoeIdentity(PoeAscendancy Ascendancy, [property: JsonPropertyName("class")] string ClassName, string Id, int Level, string Name);
public sealed record PoeLeague(DateTimeOffset? EndAt, string Id, string Name, PoeLeagueRule[] Rules, DateTimeOffset? StartAt);
public sealed record PoeLeagueRule(string Id, string Name);
public sealed record PoeTracking(DateTimeOffset FirstSeenAt, string? Reason, string Status);
public sealed record PoeRosterEntry(bool BuildAvailable, PoeIdentity Character, PoeLeague League, DateTimeOffset ObservedAt, PoeTracking Tracking);
public sealed record PoeRosterLeague(PoeRosterEntry[] Characters, string Id, string Name, DateTimeOffset? StartAt);
public sealed record PoeRoster(string Game, PoeRosterLeague[] Leagues)
{
    public PoeRoster Ordered() => this with
    {
        Leagues = Leagues.Where(league => league.Characters.Length > 0)
            .OrderByDescending(league => league.StartAt)
            .ThenBy(league => league.Id, StringComparer.Ordinal)
            .Select(league => league with
            {
                Characters = league.Characters.OrderByDescending(entry => entry.Character.Level)
                    .ThenBy(entry => entry.Character.Name, StringComparer.OrdinalIgnoreCase)
                    .ThenBy(entry => entry.Character.Id, StringComparer.Ordinal)
                    .ThenBy(entry => entry.League.Id, StringComparer.Ordinal).ToArray()
            }).ToArray()
    };
}

