using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace GamingHistory.Api.Tests;

public sealed class PoeRosterTests
{
    private static readonly DateTimeOffset ObservedAt = new(2026, 9, 20, 8, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData("poe")]
    [InlineData("poe2")]
    public async Task ShouldReturnStoredRosterWithoutChangingObservationTime(string game)
    {
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();
        using var response = await client.GetAsync($"/api/v1/{game}/characters");
        var actual = await response.Content.ReadFromJsonAsync<PoeRoster>();
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var fixture = await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, "MockFixtures", $"{game}-roster.json"));
        var expected = JsonSerializer.Deserialize<PoeRoster>(fixture, new JsonSerializerOptions(JsonSerializerDefaults.Web));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(response.Headers.CacheControl!.NoCache);
        Assert.Equal("application/json", response.Content.Headers.ContentType!.MediaType);
        Assert.Equal(JsonSerializer.Serialize(expected!.Ordered()), JsonSerializer.Serialize(actual));
        Assert.Equal(game, actual!.Game);
        Assert.True(body.RootElement.TryGetProperty("game", out _));
        Assert.True(body.RootElement.GetProperty("groups")[0].GetProperty("characters")[0].GetProperty("character").TryGetProperty("class", out _));
        Assert.Equal(ObservedAt, actual.Groups[0].Characters[0].ObservedAt);
    }

    [Theory]
    [InlineData("poe", "poe2")]
    [InlineData("poe2", "poe")]
    public async Task ShouldKeepGameScenariosIndependent(string game, string otherGame)
    {
        await using var factory = CreateFactory(game, "empty");
        using var client = factory.CreateClient();
        var empty = await client.GetFromJsonAsync<PoeRoster>($"/api/v1/{game}/characters");
        var populated = await client.GetFromJsonAsync<PoeRoster>($"/api/v1/{otherGame}/characters");

        Assert.Empty(empty!.Groups);
        Assert.NotEmpty(populated!.Groups);
    }

    [Theory]
    [InlineData("poe")]
    [InlineData("poe2")]
    public async Task ShouldPreserveArchivedParticipationAndOriginalTemporaryLeague(string game)
    {
        await using var factory = CreateFactory(game, "archived");
        using var client = factory.CreateClient();
        var roster = await client.GetFromJsonAsync<PoeRoster>($"/api/v1/{game}/characters");
        var entry = Assert.Single(Assert.Single(roster!.Groups).Characters);

        Assert.Equal("archived", entry.Tracking.Status);
        Assert.Equal("permanentMigration", entry.Tracking.Reason);
        Assert.Equal("demo-emberfall", entry.League.Id);
        Assert.Equal(ObservedAt, entry.ObservedAt);
        Assert.True(entry.BuildAvailable);
    }

    [Theory]
    [InlineData("poe")]
    [InlineData("poe2")]
    public async Task ShouldDistinguishStorageFailureFromAnEmptyRoster(string game)
    {
        await using var factory = CreateFactory(game, "unavailable");
        using var client = factory.CreateClient();
        using var response = await client.GetAsync($"/api/v1/{game}/characters");
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.True(response.Headers.CacheControl!.NoStore);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType!.MediaType);
        Assert.Equal("unexpected_error", body.RootElement.GetProperty("code").GetString());
        Assert.NotEmpty(body.RootElement.GetProperty("traceId").GetString()!);
    }

    [Fact]
    public void ShouldOrderLeagueFamiliesAndOmitEmptyGroups()
    {
        var entry = Entry("Ash", "aaa", 18);
        var roster = new PoeRoster("poe", [
            new([entry], "unknown", "Unknown", null),
            new([entry], "old", "Old", ObservedAt.AddMonths(-3)),
            new([entry], "zzz", "Same start Z", ObservedAt),
            new([], "empty", "Empty", ObservedAt.AddDays(1)),
            new([entry], "aaa", "Same start A", ObservedAt)
        ]);
        var ordered = roster.Ordered();

        Assert.Equal(["aaa", "zzz", "old", "unknown"], ordered.Groups.Select(group => group.Id));
        Assert.Equal(5, roster.Groups.Length);
    }

    [Fact]
    public void ShouldOrderCharactersAcrossVariantsAndRetainUnavailableBuildsAndMissingAscendancy()
    {
        var lower = Entry("Low", "low", 18);
        var higher = Entry("High", "high", 90);
        var tieLast = Entry("Zed", "zzz", 50);
        var tieName = Entry("ASH", "bbb", 50);
        var tieId = Entry("ash", "aaa", 50);
        var variant = tieId with
        {
            League = tieId.League with
            {
                Id = "hardcore",
                Rules = [new("hardcore", "Hardcore")]
            }
        };
        var roster = new PoeRoster("poe", [new([lower, tieLast, tieName, variant, tieId, higher], "family", "Family", ObservedAt)]);
        var entries = roster.Ordered().Groups[0].Characters;

        Assert.Equal([higher, tieId, variant, tieName, tieLast, lower], entries);
        Assert.Equal("none", lower.Character.Ascendancy.Status);
        Assert.Null(lower.Character.Ascendancy.Name);
        Assert.False(lower.BuildAvailable);
        Assert.Single(entries[2].League.Rules);
    }

    [Fact]
    public void ShouldRejectInvalidScenarioConfiguration()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Mock:PoeScenario"] = "../invalid"
        }).Build();

        Assert.Throws<InvalidOperationException>(() => new PoeRosterData(configuration));
    }

    private static PoeRosterEntry Entry(string name, string identifier, int level) => new(false,
        new PoeIdentity(new PoeAscendancy(null, "none"), "Witch", identifier, level, name),
        new PoeLeague(null, "default", "League", [], ObservedAt), ObservedAt,
        new PoeTracking(ObservedAt, null, "active"));

    private static WebApplicationFactory<Program> CreateFactory(string game = "poe", string scenario = "populated") =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder => builder.ConfigureAppConfiguration((_, configuration) =>
            configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                [game == "poe" ? "Mock:PoeScenario" : "Mock:Poe2Scenario"] = scenario
            })));
}
