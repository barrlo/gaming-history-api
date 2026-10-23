using System.Net.Http.Headers;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication.OAuth;
using Microsoft.Extensions.Options;

namespace GamingHistory.WowDiscovery;

public sealed class BattleNetHandler(IOptionsMonitor<OAuthOptions> options, ILoggerFactory logger,
    UrlEncoder encoder) : OAuthHandler<OAuthOptions>(options, logger, encoder)
{
    protected override async Task<OAuthTokenResponse> ExchangeCodeAsync(OAuthCodeExchangeContext context)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, Options.TokenEndpoint);
        request.Headers.Authorization = new AuthenticationHeaderValue("Basic",
            Convert.ToBase64String(Encoding.UTF8.GetBytes($"{Options.ClientId}:{Options.ClientSecret}")));
        request.Content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["code"] = context.Code,
            ["grant_type"] = "authorization_code",
            ["redirect_uri"] = context.RedirectUri
        });
        using var response = await Backchannel.SendAsync(request, Context.RequestAborted);

        if (!response.IsSuccessStatusCode)
        {
            return OAuthTokenResponse.Failed(new InvalidOperationException("Battle.net token exchange failed."));
        }

        return OAuthTokenResponse.Success(await JsonDocument.ParseAsync(
            await response.Content.ReadAsStreamAsync(Context.RequestAborted), cancellationToken: Context.RequestAborted));
    }
}
