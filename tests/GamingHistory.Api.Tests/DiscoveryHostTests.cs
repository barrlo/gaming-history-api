using System.Net;
using GamingHistory.WowDiscovery;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace GamingHistory.Api.Tests;

public sealed class DiscoveryHostTests
{
    private static WebApplication CreateApp()
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Development" });
        builder.WebHost.UseTestServer();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["BattleNet:ClientId"] = "test-client",
            ["BattleNet:ClientSecret"] = "test-secret",
            ["Expansion:MaximumLevel"] = "90"
        });
        DiscoveryHost.Configure(builder);
        var app = builder.Build();
        DiscoveryHost.Map(app);

        return app;
    }

    [Fact]
    public async Task HomeProvidesFormWithoutExposingCredentials()
    {
        await using var app = CreateApp();
        await app.StartAsync();
        using var client = app.GetTestClient();
        client.BaseAddress = new Uri("https://localhost");
        using var response = await client.GetAsync("/");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Authorize and sync", body);
        Assert.Contains("__RequestVerificationToken", body);
        Assert.DoesNotContain("test-secret", body);
        Assert.True(response.Headers.CacheControl!.NoStore);
    }

    [Theory]
    [InlineData("http://localhost")]
    [InlineData("https://example.com")]
    public async Task RejectsUntrustedHostOrPlainHttp(string address)
    {
        await using var app = CreateApp();
        await app.StartAsync();
        using var client = app.GetTestClient();
        client.BaseAddress = new Uri(address);
        using var response = await client.GetAsync("/");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task SyncRequiresAntiforgeryAndCallbackRequiresCorrelation()
    {
        await using var app = CreateApp();
        await app.StartAsync();
        using var client = app.GetTestClient();
        client.BaseAddress = new Uri("https://localhost");
        using var sync = await client.PostAsync("/sync", new FormUrlEncodedContent([]));
        using var callback = await client.GetAsync("/signin-battlenet?code=not-a-real-code&state=invalid");

        Assert.Equal(HttpStatusCode.BadRequest, sync.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, callback.StatusCode);
        Assert.Contains("roster was not replaced", await callback.Content.ReadAsStringAsync());
    }
}
