using System.Text.Json;

namespace GamingHistory.Api;

// This adapter deliberately replays approved fixtures. It is not a production provider or cache.
public sealed class MockData
{
    private readonly string _fixtureRoot;
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
        Roster = File.ReadAllText(Path.Combine(_fixtureRoot, Scenario, "roster.json"));
        using var roster = JsonDocument.Parse(Roster);
        CharacterIds = roster.RootElement.GetProperty("characters").EnumerateArray()
            .Select(character => character.GetProperty("id").GetString()!).ToHashSet(StringComparer.Ordinal);
        FailedCurrentIds = configuration.GetSection("Mock:CurrentFailureCharacterIds").Get<string[]>()?.ToHashSet(StringComparer.Ordinal) ?? [];
    }

    public string ReadCharacter(string id, bool current) =>
        File.ReadAllText(Path.Combine(_fixtureRoot, Scenario, $"{id}{(current ? "-current" : "")}.json"));
}
