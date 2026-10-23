# Local WoW character discovery

This owner-only tool discovers US retail characters and saves identities in DynamoDB Local through Rancher Desktop. It is separate from the public mock API and is not deployed by the API workflow. It does not fetch scores or write weekly history yet.

## Setup

1. Start Rancher Desktop with the Moby container engine.
2. From the repository root, start storage:

   ```sh
   docker --context rancher-desktop compose -f database/compose.yaml up -d
   ```

3. Ensure the .NET HTTPS development certificate is trusted (`dotnet dev-certs https --trust`). Complete any operating-system trust prompt yourself.
4. Add `https://localhost:5091/signin-battlenet` to your existing Battle.net client's redirect URIs.
5. Configure `BattleNet:ClientId` and `BattleNet:ClientSecret` in .NET User Secrets for `src/GamingHistory.WowDiscovery`. Rider's Manage User Secrets action opens the separate local secrets file. Its JSON should have these keys, filled locally:

   ```json
   {
     "BattleNet:ClientId": "YOUR_CLIENT_ID",
     "BattleNet:ClientSecret": "YOUR_CLIENT_SECRET"
   }
   ```

   The project UserSecretsId is `gaming-history-wow-discovery`. Do not put real values in this example, appsettings, launch profiles, shell scripts, fixtures, or commits. User Secrets is local development storage, not an encrypted production vault.

6. Run `dotnet run --project src/GamingHistory.WowDiscovery --launch-profile discovery`, then open `https://localhost:5091/` yourself. The tool does not automatically open a browser.
7. Choose **Authorize and sync**, sign in to your Battle.net account, and grant `wow.profile`. The first successful discovery binds this local database to that account. Later attempts with a different account fail without replacing the active roster.
8. Stop the tool when finished. Stop storage with `docker --context rancher-desktop compose -f database/compose.yaml stop`. The named volume preserves data; do not remove it unless you intend to discard local discovery data.

The tool only runs in Development, listens on HTTPS localhost port 5091, rejects other hosts/non-loopback clients, and requires an antiforgery-protected POST before OAuth. Framework OAuth state/correlation validation protects the callback. Client credentials are exchanged server-side using HTTP Basic authentication. The access token is used for the discovery request only, never saved in a cookie or DynamoDB. Authentication and HTTP request logging are disabled for the tool to avoid recording callback codes or tokens. The public API remains unchanged at port 5080.

## Eligibility and identity

`Expansion:MaximumLevel` is configured as 90. Retain below-cap characters so later profile checks can recognize leveling; that scheduled profile-check integration is future work. Eligible means present in the latest complete discovery and exactly at the configured maximum level. A new sync can reactivate previously missing characters. Names are mutable; local identity uses US region, realm ID, and character ID. Realm transfers may change upstream identity and need separate reconciliation later.

Missing characters become inactive without deleting their stored identities or season history. Invalid or incomplete upstream responses are rejected before writes. An explicit empty `wow_accounts` array is a valid empty roster. The API cannot detect upstream omissions that Blizzard returns as an otherwise complete success.

## Storage and consistency

The local table is `gaming-history-characters-local`, with string `PK` and `SK`. It is created if absent; existing tables are never replaced.

- Pointer: `PK=SCOPE#personal#WOW#US`, `SK=DISCOVERY`; stores owner ID and active generation.
- Character rows: `PK=SCOPE#personal#WOW#US#GEN#<generation>`, `SK=CHAR#us:<realm-id>:<character-id>`.

Each generation contains the reconciled roster, including inactive identities. Write every row first, then publish the pointer with a conditional write against the previously read generation and owner. Readers query the published generation with consistent reads and pagination. Concurrent losers or failed writes cannot replace the active snapshot. Immutable generations avoid exposing partial batches and avoid DynamoDB's transaction item-count limit for large accounts.

Old and abandoned generations are retained in this local milestone; retention/cleanup must be implemented before cloud deployment. This refines the previously proposed single-scope character partition to give discovery atomic publication. It is separate from the future season and history tables. Expansions and seasons remain configuration/design work beyond this discovery slice.

## Tests

`dotnet test --configuration Release` runs parser, reconciliation, and local host tests without credentials. To include the real DynamoDB Local storage test, start the container and run:

```sh
DYNAMODB_LOCAL_TESTS=1 dotnet test --configuration Release
```

The integration test uses a unique table and deletes only that table afterward. CI starts DynamoDB Local and enables the same test. Live Battle.net authorization still requires owner verification; synthetic tests do not establish the actual account response shape.

## Production boundary

Use AWS Secrets Manager at runtime with narrowly scoped IAM roles. GitHub Actions uses OIDC and does not fetch the Blizzard client secret during builds. Terraform manages the resource and permissions, not secret values. This tool currently uses only local DynamoDB with dummy local credentials; production configuration and deployment are a later PR. Local data does not automatically migrate to AWS.
