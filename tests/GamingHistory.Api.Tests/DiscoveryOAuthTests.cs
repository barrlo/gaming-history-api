using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using GamingHistory.WowDiscovery;
using Microsoft.AspNetCore.Authentication.OAuth;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace GamingHistory.Api.Tests;

public sealed class DiscoveryOAuthTests
{
    private sealed class Store : IDiscoveryStore
    {
        public DiscoverySnapshot? Snapshot
        {
            get; private set;
        }

        public Task<DiscoverySnapshot?> ReadAsync(CancellationToken cancellationToken) => Task.FromResult(Snapshot);

        public Task SaveAsync(DiscoverySnapshot snapshot, string? previousGeneration, CancellationToken cancellationToken)
        {
            Snapshot = snapshot;

            return Task.CompletedTask;
        }
    }

    private sealed class Upstream(bool isDenied) : HttpMessageHandler
    {
        public string? TokenAuthorization
        {
            get; private set;
        }
        public string? TokenBody
        {
            get; private set;
        }
        public string? RosterAuthorization
        {
            get; private set;
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (request.RequestUri!.Host == "oauth.battle.net")
            {
                TokenAuthorization = request.Headers.Authorization?.ToString();
                TokenBody = await request.Content!.ReadAsStringAsync(cancellationToken);

                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("""{"access_token":"test-token","token_type":"bearer","expires_in":86399,"scope":"wow.profile"}""")
                };
            }

            RosterAuthorization = request.Headers.Authorization?.ToString();

            return new HttpResponseMessage(isDenied ? HttpStatusCode.Unauthorized : HttpStatusCode.OK)
            {
                Content = new StringContent("""{"id":123,"wow_accounts":[{"characters":[{"id":1,"realm":{"id":100,"slug":"example"},"name":"First","playable_class":{"name":"Mage"},"level":90}]}]}""")
            };
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CallbackExchangesCodeServerSideAndOnlyPublishesSuccessfulDiscovery(bool isDenied)
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
        var store = new Store();
        using var upstream = new Upstream(isDenied);
        using var backchannel = new HttpClient(upstream, false);
        builder.Services.AddSingleton<IDiscoveryStore>(store);
        builder.Services.AddSingleton(new BlizzardRosterClient(backchannel));
        builder.Services.PostConfigure<OAuthOptions>("BattleNet", options => options.Backchannel = backchannel);
        await using var app = builder.Build();
        DiscoveryHost.Map(app);
        await app.StartAsync();
        using var client = app.GetTestClient();
        client.BaseAddress = new Uri("https://localhost");
        using var home = await client.GetAsync("/");
        var html = await home.Content.ReadAsStringAsync();
        var token = WebUtility.HtmlDecode(Regex.Match(html, "name=\"__RequestVerificationToken\" value=\"([^\"]+)\"").Groups[1].Value);
        var cookies = home.Headers.GetValues("Set-Cookie").Select(cookie => cookie.Split(';')[0]);
        client.DefaultRequestHeaders.Add("Cookie", string.Join("; ", cookies));
        using var challenge = await client.PostAsync("/sync", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = token
        }));
        var location = challenge.Headers.Location!;
        var query = QueryHelpers.ParseQuery(location.Query);
        var correlation = challenge.Headers.GetValues("Set-Cookie").Select(cookie => cookie.Split(';')[0]);
        client.DefaultRequestHeaders.Remove("Cookie");
        client.DefaultRequestHeaders.Add("Cookie", string.Join("; ", correlation));
        using var callback = await client.GetAsync(QueryHelpers.AddQueryString("/signin-battlenet", new Dictionary<string, string?>
        {
            ["code"] = "test-code",
            ["state"] = query["state"].ToString()
        }));
        var result = await callback.Content.ReadAsStringAsync();

        Assert.Equal("https://oauth.battle.net/authorize", location.GetLeftPart(UriPartial.Path));
        Assert.Equal("wow.profile", query["scope"]);
        Assert.Equal("https://localhost/signin-battlenet", query["redirect_uri"]);
        Assert.Equal($"Basic {Convert.ToBase64String(Encoding.UTF8.GetBytes("test-client:test-secret"))}", upstream.TokenAuthorization);
        Assert.Contains("code=test-code", upstream.TokenBody);
        Assert.DoesNotContain("client_secret", upstream.TokenBody);
        Assert.Equal("Bearer test-token", upstream.RosterAuthorization);
        Assert.DoesNotContain("test-token", result);
        Assert.False(callback.Headers.Contains("Set-Cookie") && callback.Headers.GetValues("Set-Cookie").Any(cookie => cookie.Contains(".AspNetCore.Cookies=")));

        if (isDenied)
        {
            Assert.Equal(HttpStatusCode.BadRequest, callback.StatusCode);
            Assert.Null(store.Snapshot);
        }
        else
        {
            Assert.Equal(HttpStatusCode.OK, callback.StatusCode);
            Assert.Single(store.Snapshot!.Characters);
            Assert.True(store.Snapshot.Characters[0].IsEligible);
        }
    }
}
