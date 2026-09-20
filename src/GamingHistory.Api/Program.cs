using GamingHistory.Api;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddSingleton<MockData>();
var app = builder.Build();
var data = app.Services.GetRequiredService<MockData>();
ApiLog.MockMode(app.Logger, data.Scenario, data.Clock);
app.Use(async (context, next) =>
{
    try
    {
        await next(context);
    }
    catch (Exception exception) when (!context.Response.HasStarted)
    {
        ApiLog.RequestFailed(app.Logger, exception);
        await ApiResponses.Problem(context, "unexpected_error", 500).ExecuteAsync(context);
    }
});

app.UseSwaggerUI(options =>
{
    options.RoutePrefix = "api/docs";
    options.SwaggerEndpoint("/api/openapi/v1.json", "Gaming History API 0.4.0-review");
    options.DocumentTitle = "Gaming History API — mock development";
});

app.MapGet("/health", () => Results.Json(new { status = "ok" }));
app.MapGet("/api/openapi/v1.json", () => Results.Text(data.Spec, "application/json"));
app.MapGet("/api/v1/wow/characters", (HttpContext context) => ApiResponses.Fixture(context, data.Roster));
app.MapGet("/api/v1/wow/characters/{characterId}/history", (HttpContext context, string characterId) => ReadCharacter(context, characterId, false));
app.MapGet("/api/v1/wow/characters/{characterId}/current-score", (HttpContext context, string characterId) => ReadCharacter(context, characterId, true));
app.Run();

return;

IResult ReadCharacter(HttpContext context, string characterId, bool current)
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

    if (current && data.FailedCurrentIds.Contains(characterId))
    {
        return ApiResponses.Problem(context, "data_unavailable", 503);
    }

    return ApiResponses.Fixture(context, data.ReadCharacter(characterId, current));
}
