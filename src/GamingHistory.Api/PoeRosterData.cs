using System.Text.Json;

namespace GamingHistory.Api;

// Frozen local examples stand in for stored collection results until persistence is implemented.
public sealed class PoeRosterData
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly Dictionary<string, PoeRoster?> _rosters;

    public PoeRosterData(IConfiguration configuration)
    {
        _rosters = new[] { "poe", "poe2" }.ToDictionary(game => game, game => ReadRoster(game,
            configuration[game == "poe" ? "Mock:PoeScenario" : "Mock:Poe2Scenario"] ?? "populated"));
    }

    public PoeRoster? Read(string game) => _rosters[game];

    private static PoeRoster? ReadRoster(string game, string scenario)
    {
        if (scenario is not ("populated" or "empty" or "archived" or "unavailable"))
        {
            throw new InvalidOperationException($"Unknown {game} roster mock scenario '{scenario}'. Use populated, empty, archived or unavailable.");
        }

        if (scenario == "unavailable")
        {
            return null;
        }

        if (scenario == "empty")
        {
            return new PoeRoster(game, []);
        }

        var root = Path.Combine(AppContext.BaseDirectory, "contracts", "v1", "fixtures", "poe-expansion");
        var rosterRoot = scenario == "archived" ? root : Path.Combine(AppContext.BaseDirectory, "MockFixtures");
        var roster = JsonSerializer.Deserialize<PoeRoster>(File.ReadAllText(Path.Combine(rosterRoot, $"{game}-roster.json")), JsonOptions)!;

        if (scenario == "archived")
        {
            using var archived = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, $"{game}-archived.json")));
            var tracking = archived.RootElement.GetProperty("tracking").Deserialize<PoeTracking>(JsonOptions)!;
            roster = roster with
            {
                Groups = roster.Groups.Select(group => group with
                {
                    Characters = group.Characters.Select(entry => entry with { Tracking = tracking }).ToArray()
                }).ToArray()
            };
        }

        return roster.Ordered();
    }
}

