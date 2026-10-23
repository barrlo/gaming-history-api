using System.Text.Json;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;

namespace GamingHistory.WowDiscovery;

public sealed class DynamoDiscoveryStore(IAmazonDynamoDB client, string tableName) : IDiscoveryStore
{
    private const string Scope = "SCOPE#personal#WOW#US";

    private static Dictionary<string, AttributeValue> Key(string partition, string sort) => new()
    {
        ["PK"] = new AttributeValue(partition),
        ["SK"] = new AttributeValue(sort)
    };

    public async Task InitializeAsync(CancellationToken cancellationToken)
    {
        try
        {
            await client.CreateTableAsync(new CreateTableRequest
            {
                AttributeDefinitions = [new("PK", ScalarAttributeType.S), new("SK", ScalarAttributeType.S)],
                BillingMode = BillingMode.PAY_PER_REQUEST,
                KeySchema = [new("PK", KeyType.HASH), new("SK", KeyType.RANGE)],
                TableName = tableName
            }, cancellationToken);
        }
        catch (ResourceInUseException)
        {
            // A local table already exists; never replace its data.
        }
    }

    public async Task<DiscoverySnapshot?> ReadAsync(CancellationToken cancellationToken)
    {
        var metadata = await client.GetItemAsync(new GetItemRequest
        {
            ConsistentRead = true,
            Key = Key(Scope, "DISCOVERY"),
            TableName = tableName
        }, cancellationToken);

        if (metadata.Item is null || metadata.Item.Count == 0)
        {
            return null;
        }

        var generation = metadata.Item["Generation"].S;
        var ownerId = long.Parse(metadata.Item["OwnerId"].N, System.Globalization.CultureInfo.InvariantCulture);
        var characters = new List<DiscoveredCharacter>();
        Dictionary<string, AttributeValue>? cursor = null;

        do
        {
            var page = await client.QueryAsync(new QueryRequest
            {
                ConsistentRead = true,
                ExclusiveStartKey = cursor,
                ExpressionAttributeValues = new() { [":partition"] = new($"{Scope}#GEN#{generation}") },
                KeyConditionExpression = "PK = :partition",
                TableName = tableName
            }, cancellationToken);

            foreach (var item in page.Items ?? [])
            {
                characters.Add(JsonSerializer.Deserialize<DiscoveredCharacter>(item["Character"].S)
                    ?? throw new InvalidOperationException("Invalid stored character."));
            }

            cursor = page.LastEvaluatedKey;
        } while (cursor is { Count: > 0 });

        return new DiscoverySnapshot(ownerId, generation, characters);
    }

    public async Task SaveAsync(DiscoverySnapshot snapshot, string? previousGeneration,
        CancellationToken cancellationToken)
    {
        foreach (var character in snapshot.Characters)
        {
            var item = Key($"{Scope}#GEN#{snapshot.Generation}", $"CHAR#{character.Identity}");
            item["Character"] = new AttributeValue(JsonSerializer.Serialize(character));
            await client.PutItemAsync(new PutItemRequest { Item = item, TableName = tableName }, cancellationToken);
        }

        var metadata = Key(Scope, "DISCOVERY");
        metadata["Generation"] = new AttributeValue(snapshot.Generation);
        metadata["OwnerId"] = new AttributeValue
        {
            N = snapshot.OwnerId.ToString(System.Globalization.CultureInfo.InvariantCulture)
        };
        await client.PutItemAsync(new PutItemRequest
        {
            ConditionExpression = previousGeneration is null ? "attribute_not_exists(PK)" : "Generation = :previous AND OwnerId = :owner",
            ExpressionAttributeValues = previousGeneration is null ? null : new()
            {
                [":owner"] = metadata["OwnerId"],
                [":previous"] = new AttributeValue(previousGeneration)
            },
            Item = metadata,
            TableName = tableName
        }, cancellationToken);
    }
}
