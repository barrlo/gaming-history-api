using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Amazon.Runtime;
using GamingHistory.WowDiscovery;
using Xunit;

namespace GamingHistory.Api.Tests;

public sealed class DynamoDiscoveryTests
{
    [DynamoLocalFact]
    public async Task PublishedGenerationRetainsCharactersAndRejectsConcurrentReplacement()
    {
        using var client = new AmazonDynamoDBClient(new BasicAWSCredentials("local", "local"),
            new AmazonDynamoDBConfig { ServiceURL = "http://localhost:8000" });
        var table = $"discovery-test-{Guid.NewGuid():N}";
        var store = new DynamoDiscoveryStore(client, table);
        await store.InitializeAsync(CancellationToken.None);

        try
        {
            var initial = DiscoveryReconciliation.Reconcile(123,
                [new DiscoveredCharacter(1, 100, "Example", "realm", "Mage", 90, true, true)], null, 90);
            await store.SaveAsync(initial, null, CancellationToken.None);
            var saved = await store.ReadAsync(CancellationToken.None);

            Assert.NotNull(saved);
            Assert.Equal(initial.Generation, saved.Generation);
            Assert.Equal(initial.Characters, saved.Characters);

            var next = DiscoveryReconciliation.Reconcile(123, [], saved, 90);
            await store.SaveAsync(next, initial.Generation, CancellationToken.None);
            var stale = DiscoveryReconciliation.Reconcile(123, [], saved, 90);

            await Assert.ThrowsAsync<ConditionalCheckFailedException>(() => store.SaveAsync(stale, initial.Generation, CancellationToken.None));

            var current = await store.ReadAsync(CancellationToken.None);

            Assert.Equal(next.Generation, current!.Generation);
            Assert.Single(current.Characters);
            Assert.False(current.Characters[0].IsPresent);
            Assert.False(current.Characters[0].IsEligible);
        }
        finally
        {
            await client.DeleteTableAsync(table);
        }
    }
}

public sealed class DynamoLocalFactAttribute : FactAttribute
{
    public DynamoLocalFactAttribute()
    {
        if (Environment.GetEnvironmentVariable("DYNAMODB_LOCAL_TESTS") != "1")
        {
            Skip = "Start DynamoDB Local and set DYNAMODB_LOCAL_TESTS=1.";
        }
    }
}
