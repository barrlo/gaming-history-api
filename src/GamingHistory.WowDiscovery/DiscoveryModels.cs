namespace GamingHistory.WowDiscovery;

public sealed record DiscoveredCharacter(long CharacterId, long RealmId, string Name, string RealmSlug,
    string ClassName, int Level, bool IsPresent, bool IsEligible)
{
    public string Identity => $"us:{RealmId}:{CharacterId}";
}

public sealed record DiscoverySnapshot(long OwnerId, string Generation, IReadOnlyList<DiscoveredCharacter> Characters);

public interface IDiscoveryStore
{
    Task<DiscoverySnapshot?> ReadAsync(CancellationToken cancellationToken);
    Task SaveAsync(DiscoverySnapshot snapshot, string? previousGeneration, CancellationToken cancellationToken);
}

public static class DiscoveryReconciliation
{
    public static DiscoverySnapshot Reconcile(long ownerId, IReadOnlyList<DiscoveredCharacter> discovered,
        DiscoverySnapshot? previous, int maximumLevel)
    {
        if (ownerId <= 0 || maximumLevel <= 0 || previous is not null && previous.OwnerId != ownerId)
        {
            throw new InvalidOperationException("The authorized account does not match the saved owner.");
        }

        if (discovered.Select(character => character.Identity).Distinct().Count() != discovered.Count)
        {
            throw new InvalidOperationException("The account roster contains duplicate character identities.");
        }

        var characters = previous?.Characters.ToDictionary(character => character.Identity,
            character => character with { IsEligible = false, IsPresent = false }) ?? [];

        foreach (var character in discovered)
        {
            characters[character.Identity] = character with
            {
                IsEligible = character.Level == maximumLevel,
                IsPresent = true
            };
        }

        return new DiscoverySnapshot(ownerId, Guid.NewGuid().ToString("N"), characters.Values.ToArray());
    }
}
