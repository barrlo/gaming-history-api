using System.Text.Json.Serialization;

namespace GamingHistoryApi.Models;

public class MythicPlusData
{
    public WorldOfWarcraftCharacter Character { get; set; }

    [JsonPropertyName("current_mythic_rating")]
    public MythicPlusRating CurrentMythicRating { get; set; }
}