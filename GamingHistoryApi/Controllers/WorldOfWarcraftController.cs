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

    [HttpGet]
    [Route("character/{id}")]
    public WorldOfWarcraftCharacterDetail GetCharacterData(int id)
    {
        JsonSerializerOptions options = new(JsonSerializerDefaults.Web);
        var data = JsonSerializer.Deserialize<WorldOfWarcraftCharacterDetail[]>(
            "[{\"id\": 194665345,\"name\": \"Barrlidan\",\"mythicPlusScoresBySeason\": [{\"id\": 11,\"name\": \"Dragonflight Season 3\",\"score\": 3223.6477},{\"id\": 10,\"name\": \"Dragonflight Season 2\",\"score\": 3082.3},{\"id\": 9,\"name\": \"Dragonflight Season 1\",\"score\": 2816.9},{\"id\": 8,\"name\": \"Shadowlands Season 4\",\"score\": 680.7},{\"id\": 7,\"name\": \"Shadowlands Season 3\",\"score\": 2589.2},{\"id\": 6,\"name\": \"Shadowlands Season 2\",\"score\": 2024.4},{\"id\": 5,\"name\": \"Shadowlands Season 1\",\"score\": 0.0}],\"mythicPlusScoresCurrentSeason\": [{\"id\": 11,\"name\": \"Dragonflight Season 3\",\"date\": 1710392400,\"score\": 3223.6477},{\"id\": 11,\"name\": \"Dragonflight Season 3\",\"date\": 1710219600,\"score\": 3220.5},{\"id\": 11,\"name\": \"Dragonflight Season 3\",\"date\": 1710050400,\"score\": 3200},{\"id\": 11,\"name\": \"Dragonflight Season 3\",\"date\": 1709618400,\"score\": 3116.67}]},{\"id\": 12,\"name\": \"Barrlo\",\"mythicPlusScoresBySeason\": [{\"id\": 11,\"name\": \"Dragonflight Season 3\",\"score\": 2562.5},{\"id\": 10,\"name\": \"Dragonflight Season 2\",\"score\": 2124.8},{\"id\": 9,\"name\": \"Dragonflight Season 1\",\"score\": 2631.4},{\"id\": 8,\"name\": \"Shadowlands Season 4\",\"score\": 2034},{\"id\": 7,\"name\": \"Shadowlands Season 3\",\"score\": 3021.5},{\"id\": 6,\"name\": \"Shadowlands Season 2\",\"score\": 2410.9},{\"id\": 5,\"name\": \"Shadowlands Season 1\",\"score\": 1416.6}],\"mythicPlusScoresCurrentSeason\": [{\"id\": 11,\"name\": \"Dragonflight Season 3\",\"date\": 1710392400,\"score\": 2562.5}]}]",
            options
        );
        return data.FirstOrDefault(character => character.Id == id);
    }
}