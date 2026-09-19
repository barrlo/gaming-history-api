using System.Security.Cryptography;
using System.Text;

namespace GamingHistory.Api;

public static class ApiResponses
{
    public static IResult Fixture(HttpContext context, string body)
    {
        // Frozen fixture timestamps must never be presented as fresh production cache entries.
        context.Response.Headers.CacheControl = "no-store";
        var tag = $"\"{Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(body)))}\"";
        context.Response.Headers.ETag = tag;
        var matches = context.Request.Headers.IfNoneMatch.ToString().Split(',')
            .Select(value => value.Trim()).Any(value => value == "*" || value == tag || value == $"W/{tag}");

        return matches ? Results.StatusCode(StatusCodes.Status304NotModified) : Results.Text(body, "application/json");
    }

    public static IResult Problem(HttpContext context, string code, int status)
    {
        context.Response.Headers.CacheControl = "no-store";
        var title = status switch
        {
            400 => "Bad Request",
            404 => "Not Found",
            503 => "Service Unavailable",
            _ => "Internal Server Error"
        };

        return Results.Json(new
        {
            type = "about:blank",
            title,
            status,
            code,
            traceId = context.TraceIdentifier
        },
            contentType: "application/problem+json", statusCode: status);
    }
}
