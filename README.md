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

Only read endpoints exist. No Blizzard credentials, external requests, cloud resources, login, database or collector are used. HTTP tests cover fixture responses, selector/character error precedence, per-character failures, conditional responses and Swagger delivery. They do not claim production projection or caching is implemented. No CORS policy is needed for the UI's same-origin Vite proxy.

## Scaffold verification (2026-09-18)

On .NET SDK 10.0.401: locked restore passed, Release build passed with zero warnings/errors, all 13 xUnit HTTP cases passed, and `dotnet format --verify-no-changes --no-restore` passed. The six scenario cases compare roster and all character responses to canonical fixtures. Swagger asset/spec delivery and error precedence are exercised through WebApplicationFactory. Cloud, persistence, production cache and collector behavior are not covered because they are not implemented.
