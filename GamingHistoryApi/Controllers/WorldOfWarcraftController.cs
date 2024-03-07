using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using GamingHistoryApi.Models;

namespace GamingHistoryApi.Controllers;

[ApiController]
[Route("wow")]
public class WorldOfWarcraftController : ControllerBase
{
    private static readonly string[] Summaries = new[]
    {
        "Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching"
    };

    private readonly ILogger<WorldOfWarcraftController> _logger;

    public WorldOfWarcraftController(ILogger<WorldOfWarcraftController> logger)
    {
        _logger = logger;
    }

    [HttpGet(Name = "GetMythicPlusData")]
    public IEnumerable<MythicPlusData> GetMythicPlusData()
    {
        JsonSerializerOptions options = new(JsonSerializerDefaults.Web);
        var mythicPlusData = JsonSerializer.Deserialize<MythicPlusData>(
            "{\"character\": {\"key\": {\"href\": \"https://us.api.blizzard.com/profile/wow/character/hellscream/barrlidan?namespace=profile-us\"},\"name\": \"Barrlidan\",\"id\": 194665345,\"realm\": {\"key\": {\"href\": \"https://us.api.blizzard.com/data/wow/realm/53?namespace=dynamic-us\"},\"name\": \"Hellscream\",\"id\": 53,\"slug\": \"hellscream\"}},\"current_mythic_rating\": {\"color\": {\"r\": 255,\"g\": 128,\"b\": 0,\"a\": 1},\"rating\": 3223.6477}}",
            options
        );
        return new MythicPlusData[] { mythicPlusData };
    }
}