using System.Text.Json;
using System.Text.Json.Serialization;

namespace GamingHistory.Api;

public sealed record PoeSection<TEntry>(string Availability, TEntry[] Entries);
public sealed record PoeGem(int? Level, string Name, int? Quality);
public sealed record Poe2Support(string Name);
public sealed record PoeSkillGroup<TSupport>(string Id, PoeGem[] Skills, TSupport[] Supports);
public sealed record PoePassive(string DefinitionStatus, string Id, string? Name, string? SelectedEffectId, string[] SetIds, string[] Stats);
public sealed record PoeItemMod(string Description,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] Dictionary<string, bool>? Flags = null);
public sealed record PoeItemProperty(string Name, JsonElement[][] Values)
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? DisplayMode
    {
        get; init;
    }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Suffix
    {
        get; init;
    }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? Type
    {
        get; init;
    }
}

public sealed record PoeItem(string BaseType, string FrameTypeId, bool Identified, int Ilvl, string Name, string TypeLine)
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? Corrupted
    {
        get; init;
    }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string[]? EnchantMods
    {
        get; init;
    }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public PoeItemMod[]? ExplicitMods
    {
        get; init;
    }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string[]? FlavourText
    {
        get; init;
    }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Icon
    {
        get; init;
    }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Id
    {
        get; init;
    }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public PoeItemMod[]? ImplicitMods
    {
        get; init;
    }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? InventoryId
    {
        get; init;
    }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public PoeItemProperty[]? Properties
    {
        get; init;
    }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Rarity
    {
        get; init;
    }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public PoeItemProperty[]? Requirements
    {
        get; init;
    }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string[]? RuneMods
    {
        get; init;
    }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public PoeItem[]? SocketedItems
    {
        get; init;
    }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public PoeSocket[]? Sockets
    {
        get; init;
    }
}

public sealed record PoeSocket(int Group)
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Attr
    {
        get; init;
    }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Item
    {
        get; init;
    }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? SColour
    {
        get; init;
    }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Type
    {
        get; init;
    }
}

public sealed record PoeBuild<TSupport>(PoeSection<PoePassive> AscendancySkills, PoeIdentity Character,
    string? DefinitionVersion, PoeSection<PoeItem> Equipment, string Game, PoeLeague League,
    DateTimeOffset ObservedAt, PoeSection<PoePassive> PassiveSkills, PoeSection<PoeSkillGroup<TSupport>> Skills);
public sealed record PoeRefreshState(DateTimeOffset? NextAllowedAt, string? OperationId, string Status);
public sealed record PoeLatest<TSupport>(PoeBuild<TSupport> Build, PoeRefreshState Refresh, PoeTracking Tracking);
public sealed record PoeSnapshotSummary(DateOnly HistoryDate, string Id, PoeLeague League, int Level,
    DateTimeOffset ObservedAt, DateTimeOffset ScheduledAt);
public sealed record PoeSnapshot<TSupport>(PoeBuild<TSupport> Build, PoeSnapshotSummary Snapshot);
public sealed record PoeSnapshots(string? NextCursor, PoeSnapshotSummary[] Snapshots);
public sealed record PoeBuildCollection<TSupport>(PoeLatest<TSupport>[] Latest, PoeSnapshot<TSupport>[] Snapshots);
