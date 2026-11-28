# 001: Liquibase owns the database schema

Status: accepted

## Context

The first version had an EF Core migration and an in-app seeder. Both were replaced by Liquibase (PR #3). The reason for the switch was not written down.

## Decision

The schema and the catalog seed are XML changesets in `database/changelog`. The API never changes the schema: there is no `Migrate` call at startup and there are no EF migrations. EF Core only maps and queries.

## Consequences

- A schema change needs a new changeset, an include in `db.changelog-master.xml` and the matching EF configuration. An applied changeset is never edited.
- The EF model and the changelog are written separately and can drift. `LiquibaseChangelogTests` checks the changelog files, and `SchemaParityTests` compares columns and foreign keys on real PostgreSQL in CI. It does not compare indexes or default values.
- Migrations run as their own step (the `db` service in Docker Compose), before the API starts.
