namespace GamingHistory.Api;

public static class PoeRosterEndpoints
{
    public static void MapPoeRosterEndpoints(this WebApplication app)
    {
        app.MapGet("/api/v1/poe/characters", (HttpContext context, PoeRosterData data) => ReadRoster(context, data, "poe"));
        app.MapGet("/api/v1/poe2/characters", (HttpContext context, PoeRosterData data) => ReadRoster(context, data, "poe2"));
    }

    private static IResult ReadRoster(HttpContext context, PoeRosterData data, string game)
    {
        var roster = data.Read(game);

        if (roster is null)
        {
            return ApiResponses.Problem(context, "unexpected_error", 503);
        }

        context.Response.Headers.CacheControl = "no-cache";

        return Results.Json(roster);
    }
}
