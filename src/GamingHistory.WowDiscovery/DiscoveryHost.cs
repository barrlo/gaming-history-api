using System.Net;
using System.Text.Encodings.Web;
using Amazon.DynamoDBv2;
using Amazon.Runtime;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OAuth;

namespace GamingHistory.WowDiscovery;

public static class DiscoveryHost
{
    public static void Configure(WebApplicationBuilder builder)
    {
        if (!builder.Environment.IsDevelopment())
        {
            throw new InvalidOperationException("The discovery tool only runs in Development on localhost.");
        }

        var clientId = builder.Configuration["BattleNet:ClientId"];
        var clientSecret = builder.Configuration["BattleNet:ClientSecret"];
        var maximumLevel = builder.Configuration.GetValue<int>("Expansion:MaximumLevel");

        if (string.IsNullOrWhiteSpace(clientId) || string.IsNullOrWhiteSpace(clientSecret) || maximumLevel <= 0)
        {
            throw new InvalidOperationException("Configure BattleNet client credentials and Expansion:MaximumLevel. See database/README.md.");
        }

        builder.Logging.ClearProviders();
        builder.Logging.AddConsole();
        builder.Logging.AddFilter("Microsoft.AspNetCore", LogLevel.None);
        builder.Logging.AddFilter("System.Net.Http.HttpClient", LogLevel.None);
        builder.Services.AddAntiforgery(options => options.Cookie.SecurePolicy = CookieSecurePolicy.Always);
        builder.Services.AddHttpClient<BlizzardRosterClient>(client => client.Timeout = TimeSpan.FromSeconds(30));
        builder.Services.AddSingleton<IAmazonDynamoDB>(_ => new AmazonDynamoDBClient(
            new BasicAWSCredentials("local", "local"), new AmazonDynamoDBConfig { ServiceURL = "http://localhost:8000" }));
        builder.Services.AddSingleton(provider => new DynamoDiscoveryStore(provider.GetRequiredService<IAmazonDynamoDB>(),
            "gaming-history-characters-local"));
        builder.Services.AddSingleton<IDiscoveryStore>(provider => provider.GetRequiredService<DynamoDiscoveryStore>());
        builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
            .AddCookie()
            .AddOAuth<OAuthOptions, BattleNetHandler>("BattleNet", options =>
            {
                options.AuthorizationEndpoint = "https://oauth.battle.net/authorize";
                options.CallbackPath = "/signin-battlenet";
                options.ClientId = clientId;
                options.ClientSecret = clientSecret;
                options.SaveTokens = false;
                options.Scope.Add("wow.profile");
                options.TokenEndpoint = "https://oauth.battle.net/token";
                options.Events.OnCreatingTicket = async context =>
                {
                    if (context.TokenResponse.Response?.RootElement.TryGetProperty("scope", out var scope) != true
                        || !scope.GetString()!.Split(' ').Contains("wow.profile") || string.IsNullOrEmpty(context.AccessToken))
                    {
                        throw new InvalidOperationException("Battle.net did not grant the required scope.");
                    }

                    var services = context.HttpContext.RequestServices;
                    var store = services.GetRequiredService<IDiscoveryStore>();
                    var previous = await store.ReadAsync(context.HttpContext.RequestAborted);
                    var roster = await services.GetRequiredService<BlizzardRosterClient>()
                        .FetchAsync(context.AccessToken, context.HttpContext.RequestAborted);
                    var snapshot = DiscoveryReconciliation.Reconcile(roster.OwnerId, roster.Characters, previous, maximumLevel);
                    await store.SaveAsync(snapshot, previous?.Generation, context.HttpContext.RequestAborted);
                    context.Properties.Items["eligible"] = snapshot.Characters.Count(character => character.IsEligible).ToString(System.Globalization.CultureInfo.InvariantCulture);
                };
                options.Events.OnTicketReceived = async context =>
                {
                    context.HandleResponse();
                    context.Response.ContentType = "text/plain";
                    await context.Response.WriteAsync($"Discovery saved. Eligible characters: {context.Properties?.Items["eligible"]}. You can close this window and stop the tool.");
                };
                options.Events.OnRemoteFailure = async context =>
                {
                    context.HandleResponse();
                    context.Response.StatusCode = StatusCodes.Status400BadRequest;
                    await context.Response.WriteAsync("Discovery failed or was cancelled. The active roster was not replaced. Start a new sync from the tool home page.");
                };
            });
    }

    public static void Map(WebApplication app)
    {
        app.Use(async (context, next) =>
        {
            if (!context.Request.IsHttps || context.Request.Host.Host != "localhost"
                || context.Connection.RemoteIpAddress is { } address && !IPAddress.IsLoopback(address))
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;

                return;
            }

            context.Response.Headers.CacheControl = "no-store";
            context.Response.Headers["Referrer-Policy"] = "no-referrer";
            context.Response.Headers.ContentSecurityPolicy = "default-src 'none'; form-action 'self'; frame-ancestors 'none'";
            await next(context);
        });
        app.UseAuthentication();
        app.MapGet("/", (HttpContext context, IAntiforgery antiforgery) =>
        {
            var tokens = antiforgery.GetAndStoreTokens(context);
            var token = HtmlEncoder.Default.Encode(tokens.RequestToken!);

            return Results.Content($"<!doctype html><html lang=\"en\"><title>WoW character discovery</title><h1>Sync your Battle.net characters</h1><p>The first successful sync binds this local database to your account. Later syncs must use the same account.</p><form action=\"/sync\" method=\"post\"><input type=\"hidden\" name=\"{tokens.FormFieldName}\" value=\"{token}\"><button>Authorize and sync</button></form></html>", "text/html");
        });
        app.MapPost("/sync", async (HttpContext context, IAntiforgery antiforgery) =>
        {
            try
            {
                await antiforgery.ValidateRequestAsync(context);
            }
            catch (AntiforgeryValidationException)
            {
                return Results.BadRequest("Start synchronization from the tool home page.");
            }

            return Results.Challenge(new AuthenticationProperties { RedirectUri = "/" }, ["BattleNet"]);
        });
    }
}
