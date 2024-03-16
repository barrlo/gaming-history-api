using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using GamingHistoryApi.Models;

namespace GamingHistoryApi.Controllers;

[ApiController]
[Route("wow")]
public class WorldOfWarcraftController : ControllerBase
{
    private readonly ILogger<WorldOfWarcraftController> _logger;

    public WorldOfWarcraftController(ILogger<WorldOfWarcraftController> logger)
    {
        _logger = logger;
    }

    [HttpGet]
    [Route("characters")]
    public IEnumerable<WorldOfWarcraftCharacter> GetCharacters()
    {
        JsonSerializerOptions options = new(JsonSerializerDefaults.Web);
        return JsonSerializer.Deserialize<WorldOfWarcraftCharacter[]>(
            "[{\"id\": 194665345,\"name\": \"Barrlidan\",\"mythicPlusScore\": 3223.6477,\"itemLevel\": 485},{\"id\": 2,\"name\": \"Barrlo\",\"mythicPlusScore\": 2562.5,\"itemLevel\": 480}]",
            options
        );
    }

    // [HttpGet]
    // [Route("mythic-plus")]
    // public IEnumerable<MythicPlusData> GetMythicPlusData()
    // {
    //     JsonSerializerOptions options = new(JsonSerializerDefaults.Web);
    //     var mythicPlusData = JsonSerializer.Deserialize<MythicPlusData>(
    //         "{\"character\": {\"name\": \"Barrlidan\",\"id\": 194665345},\"current_mythic_rating\": {\"rating\": 3223.6477}}",
    //         options
    //     );
    //     return new MythicPlusData[] { mythicPlusData };
    // }
}