using System.Net;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace GamingHistory.Api.Tests;

public sealed class ApiTests
{
    [Theory]
    [InlineData("populated")]
    [InlineData("empty-roster")]
    [InlineData("zero-and-ties")]
    [InlineData("season-gap")]
    [InlineData("weekly-rollover")]
    [InlineData("season-start")]
    public async Task ShouldMatchEveryApprovedScenario(string scenario)
    {
        await using var factory = CreateFactory(scenario);
        using var client = factory.CreateClient();
        var root = Path.Combine(AppContext.BaseDirectory, "contracts", "v1", "fixtures", scenario);
        var response = await client.GetAsync("/api/v1/wow/characters");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(response.Headers.CacheControl!.NoStore);

        var roster = JsonNode.Parse(await response.Content.ReadAsStringAsync())!;

        Assert.True(JsonNode.DeepEquals(JsonNode.Parse(await File.ReadAllTextAsync(Path.Combine(root, "roster.json"))), roster));

        foreach (var character in roster["characters"]!.AsArray())
        {
            var id = character!["id"]!.GetValue<string>();

            foreach (var (suffix, fileSuffix) in new[] { ("history", ""), ("current-score", "-current") })
            {
                var actual = await client.GetAsync($"/api/v1/wow/characters/{id}/{suffix}?season=current");

                Assert.Equal(HttpStatusCode.OK, actual.StatusCode);
                Assert.Equal("application/json", actual.Content.Headers.ContentType!.MediaType);
                Assert.True(JsonNode.DeepEquals(JsonNode.Parse(await File.ReadAllTextAsync(Path.Combine(root, $"{id}{fileSuffix}.json"))), JsonNode.Parse(await actual.Content.ReadAsStringAsync())));
            }
        }
    }

    [Theory]
    [InlineData("history")]
    [InlineData("current-score")]
    public async Task ShouldValidateSelectorBeforeCharacterLookup(string endpoint)
    {
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();

        foreach (var query in new[] { "season=old", "season=", "season=current&season=current" })
        {
            await AssertProblem(await client.GetAsync($"/api/v1/wow/characters/missing/{endpoint}?{query}"), 400, "invalid_season_selector");
        }

        await AssertProblem(await client.GetAsync($"/api/v1/wow/characters/missing/{endpoint}"), 404, "character_not_found");
    }

    [Fact]
    public async Task ShouldPreserveHistoryAndOtherCharactersWhenCurrentRequestFails()
    {
        await using var factory = CreateFactory(failedId: "char-aeloria");
        using var client = factory.CreateClient();

        await AssertProblem(await client.GetAsync("/api/v1/wow/characters/char-aeloria/current-score"), 503, "data_unavailable");
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/v1/wow/characters/char-aeloria/history")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/v1/wow/characters/char-thalren/current-score")).StatusCode);
    }

    [Theory]
    [InlineData("/api/v1/wow/characters")]
    [InlineData("/api/v1/wow/characters/char-aeloria/history")]
    [InlineData("/api/v1/wow/characters/char-aeloria/current-score")]
    public async Task ShouldReturnBodylessConditionalGetAndRetainEtag(string path)
    {
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();
        var first = await client.GetAsync(path);
        using var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.TryAddWithoutValidation("If-None-Match", $"\"other\", W/{first.Headers.ETag}");
        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.NotModified, response.StatusCode);
        Assert.Equal(first.Headers.ETag, response.Headers.ETag);
        Assert.Empty(await response.Content.ReadAsByteArrayAsync());
    }

    [Fact]
    public async Task ShouldServeCanonicalSpecAndLocalSwaggerAssets()
    {
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();

        Assert.Contains("swagger-ui", await client.GetStringAsync("/api/docs"));
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/docs/swagger-ui-bundle.js")).StatusCode);
        Assert.Equal(await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, "contracts", "v1", "openapi.json")), await client.GetStringAsync("/api/openapi/v1.json"));
        Assert.Equal("ok", JsonNode.Parse(await client.GetStringAsync("/health"))!["status"]!.GetValue<string>());
    }

    private static WebApplicationFactory<Program> CreateFactory(string scenario = "populated", string? failedId = null) =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder => builder.ConfigureAppConfiguration((_, configuration) =>
            configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Mock:Scenario"] = scenario,
                ["Mock:CurrentFailureCharacterIds:0"] = failedId
            })));

    private static async Task AssertProblem(HttpResponseMessage response, int status, string code)
    {
        Assert.Equal(status, (int)response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType!.MediaType);
        Assert.True(response.Headers.CacheControl!.NoStore);

        var body = JsonNode.Parse(await response.Content.ReadAsStringAsync())!;

        Assert.Equal(code, body["code"]!.GetValue<string>());
        Assert.Equal(status, body["status"]!.GetValue<int>());
        Assert.NotEmpty(body["traceId"]!.GetValue<string>());
    }
}
