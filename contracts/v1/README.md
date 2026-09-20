# Canonical API contract

OpenAPI 3.1.0, API version 0.4.0-review, copied unchanged from the approved planning workspace. This repository now owns the contract and fictional response fixtures. The UI pins a copy and checksum. Coordinate semantic changes before regenerating UI types.

Six unchanged WoW scenarios in fixtures/manifest.json include frozen clocks. That manifest retains its original 0.3.0-review fixture provenance; the WoW paths and schemas are unchanged in 0.4.0-review. These samples do not track real time. Observations are source data for later projection work; this scaffold serves the already-projected response files. No real provider, cache, persistence or scheduled collector is implemented yet.

## PoE and PoE2 contract foundation

The reviewed contract adds roster, latest build, historical snapshot, and asynchronous refresh operations for both games. These operations are **contract-only and are not implemented** by this scaffold. Swagger displays the planned API surface; it does not imply endpoint availability. The existing WoW and health endpoints are the only implemented routes.

Twenty fictional response examples are indexed by `fixtures/poe-expansion/manifest.json`, separately from the runtime WoW scenario manifest. Each entry names its component schema and game; paths are relative to this contract directory. These include archived and partial builds, refresh failure, concurrency, and cooldown responses. They are contract examples, not runnable mock scenarios.

The OpenAPI file is copied byte-for-byte from the validated planning draft. `openapi.sha256` records its SHA-256; verify from this directory with `shasum -a 256 -c openapi.sha256`. The UI must pin the same file and checksum. Hosted Swagger validation reported zero issues for [0.4.0-review](https://app.swaggerhub.com/apis/irunn36organization/gaming-history-api/0.4.0-review). Provider mapping, persistence, and physical build storage remain later checkpoints.

## Season calendar

Select the current season using `startsAt <= clock < endsAt`. Keep stable season IDs; a configured gap is a successful null season, not a missing-configuration fallback. Confirmed dates must have positive durations and cannot overlap.

When an end is unknown, use the next configured season's start as the end, even if it is later than nine months after the current start. With no configured successor, use nine calendar months, preserving UTC time and clamping to the destination month's last day. Both inferred end forms retain `endDateEstimated: true`; an announced end takes precedence. An estimated end never invents a successor's identity.

WoW North America resets Tuesday at 15:00 UTC throughout the year. Season weeks clip that weekly interval to the season start and end; exact reset instants belong to the new week. The mock adapter resolves season metadata at the manifest's frozen clock and refuses to relabel a fixture with a different season ID. History and score calculations remain fixture-backed until the projection checkpoint.
