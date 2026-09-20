# Gaming History API

.NET 10 mock-first scaffold with the approved OpenAPI 3.1 contract and Swagger UI.

```sh
dotnet restore --locked-mode
dotnet run --project src/GamingHistory.Api
```

API: http://localhost:5080. Swagger: http://localhost:5080/api/docs. Contract: http://localhost:5080/api/openapi/v1.json. The UI dev server proxies /api to this port.

The `http` launch profile opens Swagger automatically in IDEs that honor `launchBrowser`. For terminal development with automatic browser launch, use `dotnet watch --project src/GamingHistory.Api`. Plain `dotnet run` starts the API but does not launch a browser.

```sh
dotnet build --no-restore
dotnet test --no-restore
dotnet format --verify-no-changes --no-restore
```

## Mock data

Default scenario is populated at its frozen manifest clock (2026-09-15T12:00:00Z). All response data is fictional and dataMode is mock. Scores and fetchedAt are replayed exactly, never relabeled as fresh real-world data. Cache-Control is no-store in this development adapter. Production 30-minute current-score caching and weekly/season expiry caps remain a later checkpoint.

Choose a scenario using server configuration, never a public request parameter:

```sh
Mock__Scenario=season-start dotnet run --project src/GamingHistory.Api
Mock__CurrentFailureCharacterIds__0=char-aeloria dotnet run --project src/GamingHistory.Api
```

The second command simulates an independent current-score failure while history stays available. All six scenario names are in contracts/v1/fixtures/manifest.json. Invalid scenario configuration fails startup. Swagger UI assets are served locally by Swashbuckle.AspNetCore.SwaggerUI; the specification is the canonical file, not an inferred/generated replacement.

Season metadata in all mock responses now comes from `SeasonCalendar`, evaluated at the selected scenario's frozen clock. The independent fictional definitions preserve stable IDs and the approved fixtures. Starts are inclusive, ends exclusive; a configured gap returns a null season. Unknown ends use the next configured season start or, if there is none, nine calendar months with month-end clamping. Inferred dates stay marked estimated. Announced ends win; invalid durations, duplicate IDs, and confirmed overlaps fail configuration validation. Invalid configuration is not treated as an empty season gap. The mock catalog is configured at startup; mapping future runtime catalog failures to `season_unavailable` remains part of the persistence integration.

The calendar also provides Tuesday 15:00 UTC weekly boundaries and clips partial weeks to season dates. Weekly-history projection still replays fixtures; it will consume these calendar rules in the next checkpoint. No automatic page refresh or new response fields are introduced.

WoW read endpoints, PoE/PoE2 roster endpoints and health are implemented. The 0.4.0-review Swagger contract also describes planned PoE/PoE2 build, snapshot and refresh operations; those routes are not implemented yet. Their twenty standalone examples and schema index live in `contracts/v1/fixtures/poe-expansion/`, separately from the existing WoW mock scenarios. No Blizzard credentials, external requests, cloud resources, login, database or collector are used. HTTP tests cover fixture responses, selector/character error precedence, per-character failures, conditional responses and Swagger delivery. They do not claim production projection or caching is implemented. No CORS policy is needed for the UI's same-origin Vite proxy.

## Scaffold verification (2026-09-18)

On .NET SDK 10.0.401: locked restore passed, Release build passed with zero warnings/errors, all 13 xUnit HTTP cases passed, and `dotnet format --verify-no-changes --no-restore` passed. The six scenario cases compare roster and all character responses to canonical fixtures. Swagger asset/spec delivery and error precedence are exercised through WebApplicationFactory. Cloud, persistence, production cache and collector behavior are not covered because they are not implemented.

## PoE and PoE2 stored roster mocks

`GET /api/v1/poe/characters` and `GET /api/v1/poe2/characters` replay game-specific stored examples from `src/GamingHistory.Api/MockFixtures`, without fetching upstream. Responses preserve their observation timestamps. League families sort by start date descending (unknown last), then family ID; characters sort by level descending, case-insensitive name, character ID and league ID. Empty groups are omitted. Variant metadata, characters without an ascendancy, unavailable builds and archived participation remain intact. Discovery, permanent-league filtering and archival transitions belong to the future collector; these endpoints only read its stored results.

The populated example includes multiple league families, a temporary Hardcore variant, archived characters, missing builds and both ascended and unascended characters. Contract examples remain unchanged.

Configure games independently with `Mock__PoeScenario` or `Mock__Poe2Scenario`: `populated` (default), `empty`, `archived` or `unavailable`. The archived scenario retains the original temporary league and applies the approved archived example's tracking state. Unavailable simulates a storage failure and returns 503 Problem Details with `unexpected_error`; it never returns an empty success. Unknown scenario values fail initialization.

Successful PoE roster responses use the contract's `Cache-Control: no-cache`; problems use `no-store`. These mocks do not implement production storage, scheduling, cache freshness or provider integration. They do not add conditional ETags or claim build routes are already available.
