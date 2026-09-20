namespace GamingHistory.Api;

public static class WowRosterEndpoints
{
    public static void MapWowRosterEndpoints(this WebApplication app)
    {
        app.MapGet("/api/v1/wow/characters", (HttpContext context, MockData data, MockFailurePlan failures) =>
            failures.FailRoster()
                ? ApiResponses.Problem(context, "data_unavailable", 503)
                : ApiResponses.Fixture(context, data.Roster));
        app.MapGet("/api/v1/wow/characters/{characterId}/history",
            (HttpContext context, string characterId, MockData data, MockFailurePlan failures) =>
                ReadCharacter(context, characterId, false, data, failures));
        app.MapGet("/api/v1/wow/characters/{characterId}/current-score",
            (HttpContext context, string characterId, MockData data, MockFailurePlan failures) =>
                ReadCharacter(context, characterId, true, data, failures));
    }

    private static IResult ReadCharacter(HttpContext context, string characterId, bool current, MockData data, MockFailurePlan failures)
    {
        var selector = context.Request.Query["season"];

        if (selector.Count > 1 || (selector.Count == 1 && selector[0] != "current"))
        {
            return ApiResponses.Problem(context, "invalid_season_selector", 400);
        }

        if (!data.CharacterIds.Contains(characterId))
        {
            return ApiResponses.Problem(context, "character_not_found", 404);
        }

        if (current && (data.FailedCurrentIds.Contains(characterId) || failures.FailCurrentScore(characterId)))
        {
            return ApiResponses.Problem(context, "data_unavailable", 503);
        }

        return ApiResponses.Fixture(context, data.ReadCharacter(characterId, current));
    }
}
