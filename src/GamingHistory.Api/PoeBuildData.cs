using System.Text;
using System.Text.Json;

namespace GamingHistory.Api;

// Frozen observations keep the read API independent of the pending physical storage decision.
public sealed class PoeBuildData<TSupport>
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly Dictionary<string, PoeLatest<TSupport>> _latest;
    private readonly PoeSnapshot<TSupport>[] _snapshots;
    private readonly string _game;

    public PoeBuildData(string game)
    {
        _game = game;
        var path = Path.Combine(AppContext.BaseDirectory, "MockFixtures", $"{game}-builds.json");
        var fixtures = JsonSerializer.Deserialize<PoeBuildFixtures<TSupport>>(File.ReadAllText(path),
            JsonOptions)!;
        _latest = fixtures.Latest.ToDictionary(latest => latest.Build.Character.Id, StringComparer.Ordinal);
        _snapshots = [.. fixtures.Snapshots.OrderByDescending(snapshot => snapshot.Snapshot.ScheduledAt)
            .ThenBy(snapshot => snapshot.Snapshot.Id, StringComparer.Ordinal)];
    }

    public PoeLatest<TSupport>? ReadLatest(PoeRosterEntry entry)
    {
        if (!entry.BuildAvailable || !_latest.TryGetValue(entry.Character.Id, out var latest))
        {
            return null;
        }

        return latest with
        {
            Tracking = entry.Tracking,
            Refresh = new PoeRefreshState(null, null, entry.Tracking.Status == "archived" ? "archived" : "available")
        };
    }

    public PoeSnapshot<TSupport>? ReadSnapshot(string characterId, string snapshotId) =>
        _snapshots.SingleOrDefault(snapshot => snapshot.Build.Character.Id == characterId && snapshot.Snapshot.Id == snapshotId);

    public PoeSnapshots? ReadSnapshots(string characterId, int limit, string? cursor)
    {
        var snapshots = _snapshots.Where(snapshot => snapshot.Build.Character.Id == characterId).ToArray();
        var start = 0;

        if (cursor is not null)
        {
            var previous = Array.FindIndex(snapshots, snapshot => Cursor(characterId, snapshot.Snapshot.Id) == cursor);

            if (previous < 0)
            {
                return null;
            }

            start = previous + 1;
        }

        var page = snapshots.Skip(start).Take(limit).Select(snapshot => snapshot.Snapshot).ToArray();
        var next = start + page.Length < snapshots.Length ? Cursor(characterId, page[^1].Id) : null;

        return new PoeSnapshots(next, page);
    }

    private string Cursor(string characterId, string snapshotId) =>
        Convert.ToBase64String(Encoding.UTF8.GetBytes($"{_game}:{characterId}:{snapshotId}"));
}
