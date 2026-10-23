using System.Net.Http.Headers;
using System.Text.Json;

namespace GamingHistory.WowDiscovery;

public sealed class BlizzardRosterClient(HttpClient client)
{
    public async Task<(long OwnerId, IReadOnlyList<DiscoveredCharacter> Characters)> FetchAsync(string accessToken,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get,
            "https://us.api.blizzard.com/profile/user/wow?namespace=profile-us&locale=en_US");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        using var response = await client.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
        using var document = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(cancellationToken),
            cancellationToken: cancellationToken);

        return Parse(document.RootElement);
    }

    public static (long OwnerId, IReadOnlyList<DiscoveredCharacter> Characters) Parse(JsonElement root)
    {
        var ownerId = root.GetProperty("id").GetInt64();
        var accounts = root.GetProperty("wow_accounts");
        var characters = new List<DiscoveredCharacter>();

        foreach (var account in accounts.EnumerateArray())
        {
            foreach (var character in account.GetProperty("characters").EnumerateArray())
            {
                var characterId = character.GetProperty("id").GetInt64();
                var realm = character.GetProperty("realm");
                var realmId = realm.GetProperty("id").GetInt64();
                var name = character.GetProperty("name").GetString();
                var realmSlug = realm.GetProperty("slug").GetString();
                var className = character.GetProperty("playable_class").GetProperty("name").GetString();
                var level = character.GetProperty("level").GetInt32();

                if (characterId <= 0 || realmId <= 0 || level <= 0 || string.IsNullOrWhiteSpace(name)
                    || string.IsNullOrWhiteSpace(realmSlug) || string.IsNullOrWhiteSpace(className))
                {
                    throw new InvalidOperationException("The account roster contains an incomplete character.");
                }

                characters.Add(new DiscoveredCharacter(characterId, realmId, name, realmSlug, className, level, true, false));
            }
        }

        if (ownerId <= 0)
        {
            throw new InvalidOperationException("The account roster has no valid owner identity.");
        }

        return (ownerId, characters);
    }
}
