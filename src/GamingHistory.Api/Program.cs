using GamingHistory.Api;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddSingleton<MockData>();
builder.Services.AddSingleton<MockFailurePlan>();
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
app.MapWowRosterEndpoints();
app.Run();
