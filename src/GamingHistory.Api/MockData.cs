using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace GamingHistory.Api;

// This adapter deliberately replays approved fixtures. It is not a production provider or cache.
public sealed class MockData
{
    private readonly string _fixtureRoot;
    private readonly Season? _season;
    public string Scenario
    {
        get;
    }
    public string Clock
    {
        get;
    }
    public string Spec
    {
        get;
    }
    public string Roster
    {
        get;
    }
    public HashSet<string> CharacterIds
    {
        get;
    }
    public HashSet<string> FailedCurrentIds
    {
        get;
    }

    public MockData(IConfiguration configuration)
    {
        var contractRoot = Path.Combine(AppContext.BaseDirectory, "contracts", "v1");
        _fixtureRoot = Path.Combine(contractRoot, "fixtures");
        Scenario = configuration["Mock:Scenario"] ?? "populated";
        using var manifest = JsonDocument.Parse(File.ReadAllText(Path.Combine(_fixtureRoot, "manifest.json")));
        var entry = manifest.RootElement.GetProperty("scenarios").EnumerateArray()
            .FirstOrDefault(item => item.GetProperty("name").GetString() == Scenario);

        if (entry.ValueKind == JsonValueKind.Undefined)
        {
            throw new InvalidOperationException($"Unknown mock scenario '{Scenario}'. Select a scenario from contracts/v1/fixtures/manifest.json.");
        }

        Clock = entry.GetProperty("clock").GetString()!;
        Spec = File.ReadAllText(Path.Combine(contractRoot, "openapi.json"));
        var definitions = new List<SeasonDefinition>
        {
            new("demo-season", "Demo season", DateTimeOffset.Parse("2026-07-21T15:00:00Z", CultureInfo.InvariantCulture),
                DateTimeOffset.Parse("2026-10-20T15:00:00Z", CultureInfo.InvariantCulture))
        };

        // Scenarios are isolated: the gap intentionally has no configured successor.
        if (Scenario == "season-start")
        {
            definitions.Add(new SeasonDefinition("demo-season-2", "Demo season 2",
                DateTimeOffset.Parse("2026-10-20T15:00:00Z", CultureInfo.InvariantCulture)));
        }

        var calendar = new SeasonCalendar(definitions);
        _season = calendar.GetCurrentSeason(DateTimeOffset.Parse(Clock, CultureInfo.InvariantCulture));
        Roster = ApplySeason(File.ReadAllText(Path.Combine(_fixtureRoot, Scenario, "roster.json")));
        using var roster = JsonDocument.Parse(Roster);
        CharacterIds = roster.RootElement.GetProperty("characters").EnumerateArray()
            .Select(character => character.GetProperty("id").GetString()!).ToHashSet(StringComparer.Ordinal);
        FailedCurrentIds = configuration.GetSection("Mock:CurrentFailureCharacterIds").Get<string[]>()?.ToHashSet(StringComparer.Ordinal) ?? [];
    }

    public string ReadCharacter(string characterId, bool current) =>
        ApplySeason(File.ReadAllText(Path.Combine(_fixtureRoot, Scenario, $"{characterId}{(current ? "-current" : "")}.json")));

    private string ApplySeason(string fixture)
    {
        var response = JsonNode.Parse(fixture)!;

        if (response["season"]?["id"]?.GetValue<string>() != _season?.Id)
        {
            throw new InvalidOperationException("The mock fixture season does not match the configured season at its frozen clock.");
        }

        response["season"] = _season is null ? null : new JsonObject
        {
            ["endDateEstimated"] = _season.EndDateEstimated,
            ["endsAt"] = _season.EndsAt.UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture),
            ["id"] = _season.Id,
            ["name"] = _season.Name,
            ["region"] = Season.Region,
            ["startsAt"] = _season.StartsAt.UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture)
        };

        return response.ToJsonString();
    }
}
