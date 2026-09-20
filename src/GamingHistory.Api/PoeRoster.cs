using System.Text.Json.Serialization;

namespace GamingHistory.Api;

public sealed record PoeAscendancy(string? Name, string Status);
public sealed record PoeIdentity(PoeAscendancy Ascendancy, [property: JsonPropertyName("class")] string ClassName, string Id, int Level, string Name);
public sealed record PoeLeague(DateTimeOffset? EndAt, string Id, string Name, PoeLeagueRule[] Rules, DateTimeOffset? StartAt);
public sealed record PoeLeagueRule(string Id, string Name);
public sealed record PoeTracking(DateTimeOffset FirstSeenAt, string? Reason, string Status);
public sealed record PoeRosterEntry(bool BuildAvailable, PoeIdentity Character, PoeLeague League, DateTimeOffset ObservedAt, PoeTracking Tracking);
public sealed record PoeRosterGroup(PoeRosterEntry[] Characters, string Id, string Name, DateTimeOffset? StartAt);
public sealed record PoeRoster(string Game, PoeRosterGroup[] Groups)
{
    public PoeRoster Ordered() => this with
    {
        Groups = Groups.Where(group => group.Characters.Length > 0)
            .OrderByDescending(group => group.StartAt)
            .ThenBy(group => group.Id, StringComparer.Ordinal)
            .Select(group => group with
            {
                Characters = group.Characters.OrderByDescending(entry => entry.Character.Level)
                    .ThenBy(entry => entry.Character.Name, StringComparer.OrdinalIgnoreCase)
                    .ThenBy(entry => entry.Character.Id, StringComparer.Ordinal)
                    .ThenBy(entry => entry.League.Id, StringComparer.Ordinal).ToArray()
            }).ToArray()
    };
}

