namespace GamingHistoryApi.Models;

public class WorldOfWarcraftCharacterDetail
{
    public int Id { get; set; }
    public string Name { get; set; }
    public SeasonScore[] MythicPlusScoresBySeason { get; set; }
    public CurrentSeasonScore[] MythicPlusScoresCurrentSeason { get; set; }
}