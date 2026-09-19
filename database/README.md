# Persistence boundary

Future DynamoDB Local setup runs through Rancher Desktop. Characters, seasons and season-scoped historical observations are separate planned tables. No database is required by this fixture-only host; schema, seed scripts and integration tests will arrive at the persistence checkpoint. Current requests must never overwrite scheduled history.
