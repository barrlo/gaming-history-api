using System.Globalization;

namespace GamingHistory.Api;

public static class PoeBuildEndpoints
{
    public static void MapPoeBuildEndpoints(this WebApplication app)
    {
        MapGame<PoeGem>(app, "poe");
        MapGame<Poe2Support>(app, "poe2");
    }

    private static void MapGame<TSupport>(WebApplication app, string game)
    {
        var routes = app.MapGroup($"/api/v1/{game}/characters/{{characterId}}");
        routes.MapGet("/build", (HttpContext context, PoeRosterData rosters, PoeBuildData<TSupport> builds, string characterId) =>
        {
            var error = FindCharacter(context, rosters, game, characterId, out var character);

            if (error is not null)
            {
                return error;
            }

            var latest = builds.ReadLatest(character!);
            context.Response.Headers.CacheControl = "no-store";

            return latest is null ? ApiResponses.Problem(context, "build_unavailable", 503) : Results.Json(latest);
        });

        routes.MapGet("/snapshots", (HttpContext context, PoeRosterData rosters, PoeBuildData<TSupport> builds, string characterId) =>
        {
            var error = FindCharacter(context, rosters, game, characterId, out _);

            if (error is not null)
            {
                return error;
            }

            var query = context.Request.Query;
            var limit = 30;

            if ((query.TryGetValue("limit", out var values) &&
                 (values.Count != 1 || !int.TryParse(values[0], NumberStyles.None, CultureInfo.InvariantCulture, out limit))) ||
                limit is < 1 or > 100 || (query.TryGetValue("cursor", out var cursors) && cursors.Count != 1))
            {
                return ApiResponses.Problem(context, "invalid_request", 400);
            }

            var page = builds.ReadSnapshots(characterId, limit, query.ContainsKey("cursor") ? query["cursor"].ToString() : null);

            if (page is null)
            {
                return ApiResponses.Problem(context, "invalid_request", 400);
            }

            context.Response.Headers.CacheControl = "no-cache";

            return Results.Json(page);
        });

        routes.MapGet("/snapshots/{snapshotId}", (HttpContext context, PoeRosterData rosters,
            PoeBuildData<TSupport> builds, string characterId, string snapshotId) =>
        {
            var error = FindCharacter(context, rosters, game, characterId, out _);

            if (error is not null)
            {
                return error;
            }

            var snapshot = builds.ReadSnapshot(characterId, snapshotId);

            if (snapshot is null)
            {
                return ApiResponses.Problem(context, "snapshot_not_found", 404);
            }

            context.Response.Headers.CacheControl = "no-cache";

            return Results.Json(snapshot);
        });
    }

    private static IResult? FindCharacter(HttpContext context, PoeRosterData rosters, string game,
        string characterId, out PoeRosterEntry? character)
    {
        var roster = rosters.Read(game);
        character = roster?.Leagues.SelectMany(league => league.Characters)
            .FirstOrDefault(entry => entry.Character.Id == characterId);

        if (roster is null)
        {
            return ApiResponses.Problem(context, "unexpected_error", 503);
        }

        return character is null ? ApiResponses.Problem(context, "character_not_found", 404) : null;
    }
}
