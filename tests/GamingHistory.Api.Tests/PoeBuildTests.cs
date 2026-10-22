using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace GamingHistory.Api.Tests;

public sealed class PoeBuildTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [Theory]
    [InlineData("poe")]
    [InlineData("poe2")]
    public async Task ShouldReturnEveryAvailableBuildWithTheStoredRosterIdentity(string game)
    {
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();
        var roster = await client.GetFromJsonAsync<PoeRoster>($"/api/v1/{game}/characters");
        var fixtures = JsonNode.Parse(await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, "MockFixtures", $"{game}-builds.json")))!;

        foreach (var entry in roster!.Leagues.SelectMany(league => league.Characters).Where(entry => entry.BuildAvailable))
        {
            using var response = await client.GetAsync($"/api/v1/{game}/characters/{entry.Character.Id}/build");
            var body = JsonNode.Parse(await response.Content.ReadAsStringAsync())!;
            var expected = fixtures["latest"]!.AsArray().Single(latest => latest!["build"]!["character"]!["id"]!.GetValue<string>() == entry.Character.Id);
            var normalized = game == "poe"
                ? JsonSerializer.SerializeToNode(expected.Deserialize<PoeLatest<PoeGem>>(JsonOptions), JsonOptions)
                : JsonSerializer.SerializeToNode(expected.Deserialize<PoeLatest<Poe2Support>>(JsonOptions), JsonOptions);
            var build = body["build"]!;
            var identity = build["character"]!.Deserialize<PoeIdentity>(JsonOptions);
            var tracking = body["tracking"]!.Deserialize<PoeTracking>(JsonOptions);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.True(response.Headers.CacheControl!.NoStore);
            Assert.Equal("application/json", response.Content.Headers.ContentType!.MediaType);
            Assert.True(JsonNode.DeepEquals(normalized, body));
            Assert.Equal(entry.Character, identity);
            Assert.Equal(entry.Tracking, tracking);
            Assert.Equal(entry.ObservedAt, build["observedAt"]!.GetValue<DateTimeOffset>());
            Assert.Equal(entry.League.Id, build["league"]!["id"]!.GetValue<string>());

            var history = await client.GetFromJsonAsync<PoeSnapshots>($"/api/v1/{game}/characters/{entry.Character.Id}/snapshots");

            Assert.All(history!.Snapshots, summary => Assert.True(summary.ObservedAt <= entry.ObservedAt));
        }
    }

    [Theory]
    [InlineData("poe", "c")]
    [InlineData("poe2", "d")]
    public async Task ShouldPageSnapshotsNewestFirstAndKeepHistoricalIdentityAndCollectionTimes(string game, string identifier)
    {
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();
        var characterId = new string(identifier[0], 64);
        var path = $"/api/v1/{game}/characters/{characterId}/snapshots";
        using var firstResponse = await client.GetAsync($"{path}?limit=1");
        var first = await firstResponse.Content.ReadFromJsonAsync<PoeSnapshots>();
        var firstSummary = Assert.Single(first!.Snapshots);
        using var secondResponse = await client.GetAsync($"{path}?limit=1&cursor={Uri.EscapeDataString(first.NextCursor!)}");
        var second = await secondResponse.Content.ReadFromJsonAsync<PoeSnapshots>();
        var secondSummary = Assert.Single(second!.Snapshots);
        using var snapshotResponse = await client.GetAsync($"{path}/{secondSummary.Id}");
        using var snapshot = JsonDocument.Parse(await snapshotResponse.Content.ReadAsStringAsync());
        var build = snapshot.RootElement.GetProperty("build");
        var identity = build.GetProperty("character");
        var latest = await client.GetFromJsonAsync<JsonElement>($"/api/v1/{game}/characters/{characterId}/build");

        Assert.Equal(HttpStatusCode.OK, firstResponse.StatusCode);
        Assert.True(firstResponse.Headers.CacheControl!.NoCache);
        Assert.True(secondResponse.Headers.CacheControl!.NoCache);
        Assert.NotNull(first.NextCursor);
        Assert.Null(second.NextCursor);
        Assert.True(firstSummary.ScheduledAt > secondSummary.ScheduledAt);
        Assert.NotEqual(firstSummary.Id, secondSummary.Id);
        Assert.Equal(HttpStatusCode.OK, snapshotResponse.StatusCode);
        Assert.True(snapshotResponse.Headers.CacheControl!.NoCache);
        Assert.Equal("none", identity.GetProperty("ascendancy").GetProperty("status").GetString());
        Assert.Equal("known", latest.GetProperty("build").GetProperty("character").GetProperty("ascendancy").GetProperty("status").GetString());
        Assert.Equal(secondSummary.Level, identity.GetProperty("level").GetInt32());
        Assert.Equal(secondSummary.League.Id, build.GetProperty("league").GetProperty("id").GetString());
        Assert.Equal(secondSummary.ObservedAt, build.GetProperty("observedAt").GetDateTimeOffset());
        Assert.Equal(new DateOnly(2026, 9, 18), secondSummary.HistoryDate);
        Assert.Equal(TimeSpan.FromHours(1), secondSummary.ObservedAt - secondSummary.ScheduledAt);
        Assert.False(snapshot.RootElement.TryGetProperty("tracking", out _));
        Assert.False(snapshot.RootElement.TryGetProperty("refresh", out _));
        Assert.Equal("notApplicable", build.GetProperty("ascendancySkills").GetProperty("availability").GetString());
        Assert.Equal("available", build.GetProperty("equipment").GetProperty("availability").GetString());
        Assert.Empty(build.GetProperty("equipment").GetProperty("entries").EnumerateArray());
    }

    [Theory]
    [InlineData("poe")]
    [InlineData("poe2")]
    public async Task ShouldPreservePartialSectionsAndGameSpecificGemValues(string game)
    {
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();
        var latest = await client.GetFromJsonAsync<JsonElement>($"/api/v1/{game}/characters/{new string('e', 64)}/build");
        var build = latest.GetProperty("build");
        var skills = build.GetProperty("skills");
        var group = skills.GetProperty("entries")[0];
        var support = group.GetProperty("supports")[0];
        var passive = build.GetProperty("passiveSkills").GetProperty("entries")[0];

        Assert.Equal("partial", skills.GetProperty("availability").GetString());
        Assert.Equal(JsonValueKind.Null, group.GetProperty("skills")[0].GetProperty("quality").ValueKind);
        Assert.Equal("unavailable", passive.GetProperty("definitionStatus").GetString());
        Assert.Equal("unresolved-example", passive.GetProperty("id").GetString());
        Assert.Equal(JsonValueKind.Null, passive.GetProperty("name").ValueKind);
        Assert.Equal(game == "poe", support.TryGetProperty("level", out _));
        Assert.Equal(game == "poe", support.TryGetProperty("quality", out _));

        if (game == "poe")
        {
            Assert.Equal(0, support.GetProperty("quality").GetInt32());
        }
    }

    [Theory]
    [InlineData("poe")]
    [InlineData("poe2")]
    public async Task ShouldDistinguishNoBuildFromEmptyHistoryAndUnknownCharacter(string game)
    {
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();
        var path = $"/api/v1/{game}/characters/{new string('1', 64)}";
        using var missing = await client.GetAsync($"{path}/build");
        using var emptyResponse = await client.GetAsync($"{path}/snapshots");
        var empty = await emptyResponse.Content.ReadFromJsonAsync<PoeSnapshots>();
        using var unknown = await client.GetAsync($"/api/v1/{game}/characters/unknown/build");

        await AssertProblem(missing, HttpStatusCode.ServiceUnavailable, "build_unavailable");
        Assert.Equal(HttpStatusCode.OK, emptyResponse.StatusCode);
        Assert.Empty(empty!.Snapshots);
        Assert.Null(empty.NextCursor);
        await AssertProblem(unknown, HttpStatusCode.NotFound, "character_not_found");
    }

    [Theory]
    [InlineData("poe")]
    [InlineData("poe2")]
    public async Task ShouldKeepArchivedBuildsInTheirOriginalLeague(string game)
    {
        await using var factory = CreateFactory(game, "archived");
        using var client = factory.CreateClient();
        var path = $"/api/v1/{game}/characters/{new string('a', 64)}";
        var latest = await client.GetFromJsonAsync<JsonElement>($"{path}/build");
        var summaries = await client.GetFromJsonAsync<PoeSnapshots>($"{path}/snapshots");
        var summary = Assert.Single(summaries!.Snapshots);
        var snapshot = await client.GetFromJsonAsync<JsonElement>($"{path}/snapshots/{summary.Id}");

        Assert.Equal("archived", latest.GetProperty("tracking").GetProperty("status").GetString());
        Assert.Equal("permanentMigration", latest.GetProperty("tracking").GetProperty("reason").GetString());
        Assert.Equal("archived", latest.GetProperty("refresh").GetProperty("status").GetString());
        Assert.Equal("demo-emberfall", latest.GetProperty("build").GetProperty("league").GetProperty("id").GetString());
        Assert.Equal("demo-emberfall", snapshot.GetProperty("build").GetProperty("league").GetProperty("id").GetString());
    }

    [Theory]
    [InlineData("poe", "c", "poe2")]
    [InlineData("poe2", "d", "poe")]
    public async Task ShouldScopeSnapshotsAndCursorsToTheirCharacterAndGame(string game, string identifier, string otherGame)
    {
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();
        var characterId = new string(identifier[0], 64);
        var path = $"/api/v1/{game}/characters/{characterId}";
        var page = await client.GetFromJsonAsync<PoeSnapshots>($"{path}/snapshots?limit=1");
        var otherPath = $"/api/v1/{game}/characters/{new string('a', 64)}";
        using var foreignSnapshot = await client.GetAsync($"{otherPath}/snapshots/{page!.Snapshots[0].Id}");
        using var foreignCursor = await client.GetAsync($"{otherPath}/snapshots?cursor={Uri.EscapeDataString(page.NextCursor!)}");
        using var foreignCharacter = await client.GetAsync($"/api/v1/{otherGame}/characters/{characterId}/build");
        var sharedPath = $"/api/v1/{otherGame}/characters/{new string('a', 64)}";
        using var foreignGameSnapshot = await client.GetAsync($"{sharedPath}/snapshots/{game}-a-2026-09-19");
        using var foreignGameCursor = await client.GetAsync($"/api/v1/{otherGame}/characters/{new string('e', 64)}/snapshots?cursor={Uri.EscapeDataString(page.NextCursor!)}");

        await AssertProblem(foreignSnapshot, HttpStatusCode.NotFound, "snapshot_not_found");
        await AssertProblem(foreignCursor, HttpStatusCode.BadRequest, "invalid_request");
        await AssertProblem(foreignCharacter, HttpStatusCode.NotFound, "character_not_found");
        await AssertProblem(foreignGameSnapshot, HttpStatusCode.NotFound, "snapshot_not_found");
        await AssertProblem(foreignGameCursor, HttpStatusCode.BadRequest, "invalid_request");
    }

    [Theory]
    [InlineData("limit=0")]
    [InlineData("limit=101")]
    [InlineData("limit=no")]
    [InlineData("limit=1.5")]
    [InlineData("limit=1&limit=2")]
    [InlineData("cursor=garbage")]
    [InlineData("cursor=")]
    [InlineData("cursor=one&cursor=two")]
    public async Task ShouldRejectInvalidPaginationWithProblemDetails(string query)
    {
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();
        using var response = await client.GetAsync($"/api/v1/poe/characters/{new string('a', 64)}/snapshots?{query}");

        await AssertProblem(response, HttpStatusCode.BadRequest, "invalid_request");
    }

    [Theory]
    [InlineData("build")]
    [InlineData("snapshots")]
    [InlineData("snapshots/unknown")]
    public async Task ShouldPreserveStorageFailuresAndMissingCharacterErrors(string suffix)
    {
        await using var unavailableFactory = CreateFactory("poe", "unavailable");
        using var unavailableClient = unavailableFactory.CreateClient();
        using var unavailable = await unavailableClient.GetAsync($"/api/v1/poe/characters/unknown/{suffix}");
        await using var emptyFactory = CreateFactory("poe", "empty");
        using var emptyClient = emptyFactory.CreateClient();
        using var missing = await emptyClient.GetAsync($"/api/v1/poe/characters/{new string('a', 64)}/{suffix}");

        await AssertProblem(unavailable, HttpStatusCode.ServiceUnavailable, "unexpected_error");
        await AssertProblem(missing, HttpStatusCode.NotFound, "character_not_found");
    }

    private static async Task AssertProblem(HttpResponseMessage response, HttpStatusCode status, string code)
    {
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(status, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType!.MediaType);
        Assert.True(response.Headers.CacheControl!.NoStore);
        Assert.Equal(code, body.GetProperty("code").GetString());
        Assert.Equal((int)status, body.GetProperty("status").GetInt32());
        Assert.NotEmpty(body.GetProperty("traceId").GetString()!);
    }

    private static WebApplicationFactory<Program> CreateFactory(string game = "poe", string scenario = "populated") =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder => builder.ConfigureAppConfiguration((_, configuration) =>
            configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                [game == "poe" ? "Mock:PoeScenario" : "Mock:Poe2Scenario"] = scenario
            })));
}
