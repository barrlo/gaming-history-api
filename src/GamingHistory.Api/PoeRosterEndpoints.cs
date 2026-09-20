namespace GamingHistory.Api;

public static class PoeRosterEndpoints
{
    public static void MapPoeRosterEndpoints(this WebApplication app)
    {
        foreach (var game in new[] { "poe", "poe2" })
        {
            app.MapGet($"/api/v1/{game}/characters", (HttpContext context, PoeRosterData data) =>
            {
                var roster = data.Read(game);

                if (roster is null)
                {
                    return ApiResponses.Problem(context, "unexpected_error", 503);
                }

                context.Response.Headers.CacheControl = "no-cache";

                return Results.Json(roster);
            });
        }
    }
}
