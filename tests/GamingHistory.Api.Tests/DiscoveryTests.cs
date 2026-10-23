using System.Text.Json;
using GamingHistory.WowDiscovery;
using Xunit;

namespace GamingHistory.Api.Tests;

public sealed class DiscoveryTests
{
    private static DiscoveredCharacter Character(long identity, int level = 90) =>
        new(identity, 100, "Example", "example", "Mage", level, true, true);

    [Fact]
    public void ReconcileRetainsMissingHistoryAndBelowCapIdentities()
    {
        var previous = new DiscoverySnapshot(123, "previous", [Character(1), Character(2)]);
        var result = DiscoveryReconciliation.Reconcile(123, [Character(1, 89), Character(3)], previous, 90);

        Assert.Equal(3, result.Characters.Count);
        Assert.NotEqual(previous.Generation, result.Generation);
        Assert.False(result.Characters.Single(character => character.CharacterId == 1).IsEligible);
        Assert.True(result.Characters.Single(character => character.CharacterId == 1).IsPresent);
        Assert.False(result.Characters.Single(character => character.CharacterId == 2).IsPresent);
        Assert.False(result.Characters.Single(character => character.CharacterId == 2).IsEligible);
        Assert.True(result.Characters.Single(character => character.CharacterId == 3).IsEligible);
    }

    [Fact]
    public void ReconcileUsesConfiguredLevelAndPreservesIdentityAfterRename()
    {
        var previous = new DiscoverySnapshot(123, "previous", [Character(1)]);
        var result = DiscoveryReconciliation.Reconcile(123, [Character(1, 100) with { Name = "Renamed" }], previous, 100);

        Assert.Single(result.Characters);
        Assert.Equal("us:100:1", result.Characters[0].Identity);
        Assert.Equal("Renamed", result.Characters[0].Name);
        Assert.True(result.Characters[0].IsEligible);
    }

    [Fact]
    public void ReconcileRejectsDifferentOwnerAndDuplicateCharacters()
    {
        var previous = new DiscoverySnapshot(123, "previous", [Character(1)]);

        Assert.Throws<InvalidOperationException>(() => DiscoveryReconciliation.Reconcile(456, [], previous, 90));
        Assert.Throws<InvalidOperationException>(() => DiscoveryReconciliation.Reconcile(123, [Character(1), Character(1)], previous, 90));
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("{\"id\":123}")]
    [InlineData("{\"id\":123,\"wow_accounts\":[{}]}")]
    [InlineData("{\"id\":123,\"wow_accounts\":[{\"characters\":[{}]}]}")]
    public void ParseRejectsIncompleteResponses(string json)
    {
        using var document = JsonDocument.Parse(json);

        Assert.ThrowsAny<Exception>(() => BlizzardRosterClient.Parse(document.RootElement));
    }

    [Fact]
    public void ParseReadsAllAccountsAndAllowsAnExplicitEmptyRoster()
    {
        using var document = JsonDocument.Parse("""
            {"id":123,"wow_accounts":[{"characters":[{"id":1,"realm":{"id":100,"slug":"example"},"name":"First","playable_class":{"name":"Mage"},"level":90}]},{"characters":[{"id":2,"realm":{"id":200,"slug":"other"},"name":"Second","playable_class":{"name":"Warrior"},"level":80}]}]}
            """);
        using var empty = JsonDocument.Parse("""{"id":123,"wow_accounts":[]} """);
        var result = BlizzardRosterClient.Parse(document.RootElement);

        Assert.Equal(123, result.OwnerId);
        Assert.Equal(2, result.Characters.Count);
        Assert.Equal("us:100:1", result.Characters[0].Identity);
        Assert.Equal("us:200:2", result.Characters[1].Identity);
        Assert.Empty(BlizzardRosterClient.Parse(empty.RootElement).Characters);
    }
}
