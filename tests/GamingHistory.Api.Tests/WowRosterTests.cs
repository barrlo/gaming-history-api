using System.Net;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace GamingHistory.Api.Tests;

public sealed class WowRosterTests
{
    [Fact]
    public async Task ShouldRecoverOnlyTheRetriedCharacterWithoutChangingRosterOrHistory()
    {
        await using var factory = CreateFactory(new Dictionary<string, string?>
        {
            ["Mock:CurrentFailureAttempts:char-aeloria"] = "1",
            ["Mock:CurrentFailureAttempts:char-korren"] = "2"
        });
        using var client = factory.CreateClient();
        var rosterBefore = await client.GetStringAsync("/api/v1/wow/characters");
        var historyBefore = await client.GetStringAsync("/api/v1/wow/characters/char-aeloria/history");
        var failed = await client.GetAsync("/api/v1/wow/characters/char-aeloria/current-score");

        Assert.Equal(HttpStatusCode.ServiceUnavailable, failed.StatusCode);
        Assert.True(failed.Headers.CacheControl!.NoStore);
        Assert.Null(failed.Headers.ETag);
        Assert.Equal("data_unavailable", JsonNode.Parse(await failed.Content.ReadAsStringAsync())!["code"]!.GetValue<string>());
        Assert.Equal(rosterBefore, await client.GetStringAsync("/api/v1/wow/characters"));
        Assert.Equal(historyBefore, await client.GetStringAsync("/api/v1/wow/characters/char-aeloria/history"));
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/v1/wow/characters/char-thalren/current-score")).StatusCode);

        var recovered = await client.GetAsync("/api/v1/wow/characters/char-aeloria/current-score");
        var score = JsonNode.Parse(await recovered.Content.ReadAsStringAsync())!;

        Assert.Equal(HttpStatusCode.OK, recovered.StatusCode);
        Assert.Equal(2540, score["score"]!.GetValue<int>());
        Assert.Equal(178, score["weeklyChange"]!.GetValue<int>());
        Assert.Equal(HttpStatusCode.ServiceUnavailable, (await client.GetAsync("/api/v1/wow/characters/char-korren/current-score")).StatusCode);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, (await client.GetAsync("/api/v1/wow/characters/char-korren/current-score")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/v1/wow/characters/char-korren/current-score")).StatusCode);
    }

    [Fact]
    public async Task ShouldRecoverRosterAfterConfiguredFailuresWithoutReturningAnEmptySuccess()
    {
        await using var factory = CreateFactory(new Dictionary<string, string?> { ["Mock:RosterFailureAttempts"] = "1" });
        using var client = factory.CreateClient();
        var failed = await client.GetAsync("/api/v1/wow/characters");

        Assert.Equal(HttpStatusCode.ServiceUnavailable, failed.StatusCode);
        Assert.Equal("application/problem+json", failed.Content.Headers.ContentType!.MediaType);
        Assert.True(failed.Headers.CacheControl!.NoStore);

        var recovered = JsonNode.Parse(await client.GetStringAsync("/api/v1/wow/characters"))!;

        Assert.Equal(4, recovered["characters"]!.AsArray().Count);
    }

    [Fact]
    public async Task ShouldNotConsumeCurrentFailureForInvalidSelectorsOrHistoryRequests()
    {
        await using var factory = CreateFactory(new Dictionary<string, string?> { ["Mock:CurrentFailureAttempts:char-aeloria"] = "1" });
        using var client = factory.CreateClient();

        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync("/api/v1/wow/characters/char-aeloria/current-score?season=old")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/api/v1/wow/characters/missing/current-score")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/v1/wow/characters/char-aeloria/history")).StatusCode);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, (await client.GetAsync("/api/v1/wow/characters/char-aeloria/current-score")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/v1/wow/characters/char-aeloria/current-score")).StatusCode);
    }

    [Fact]
    public async Task ShouldConsumeExactlyConfiguredFailuresUnderConcurrentRequests()
    {
        await using var factory = CreateFactory(new Dictionary<string, string?> { ["Mock:CurrentFailureAttempts:char-aeloria"] = "3" });
        using var client = factory.CreateClient();
        var requests = new Task<HttpResponseMessage>[12];

        for (var index = 0; index < requests.Length; index++)
        {
            requests[index] = client.GetAsync("/api/v1/wow/characters/char-aeloria/current-score");
        }

        var responses = await Task.WhenAll(requests);

        try
        {
            Assert.Equal(3, responses.Count(response => response.StatusCode == HttpStatusCode.ServiceUnavailable));
            Assert.Equal(9, responses.Count(response => response.StatusCode == HttpStatusCode.OK));
        }
        finally
        {
            foreach (var response in responses)
            {
                response.Dispose();
            }
        }
    }

    [Theory]
    [InlineData("Mock:RosterFailureAttempts")]
    [InlineData("Mock:CurrentFailureAttempts:char-aeloria")]
    public void ShouldRejectNegativeFailureCounts(string setting)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { [setting] = "-1" }).Build();

        Assert.Throws<InvalidOperationException>(() => new MockFailurePlan(configuration));
    }

    private static WebApplicationFactory<Program> CreateFactory(Dictionary<string, string?> settings) =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder => builder.ConfigureAppConfiguration((_, configuration) =>
            configuration.AddInMemoryCollection(settings)));
}
