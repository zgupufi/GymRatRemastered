# 002: Integration tests run on SQLite, with a PostgreSQL check in CI

Status: accepted

## Context

Integration tests call real endpoints and need a database. With SQLite, `dotnet test` runs without a database server, but SQLite is not the production database.

## Decision

`GymRatApiFactory` uses a temporary SQLite file per factory, with the schema created from the EF model (`EnsureCreated`). When `GYMRAT_TEST_POSTGRES` is set it uses PostgreSQL instead and expects the schema that Liquibase created. The CI `postgres` job applies the changelog to an empty database and runs the tests marked `Category=Postgres`. On pull requests it does so only when `backend/`, `database/` or the CI workflow changed.

## Consequences

- SQLite needs substitutes: the factory registers `uuid_generate_v7` as a function and stores `DateTimeOffset` as a number.
- The database is a file rather than one shared in-memory connection, so parallel requests each get their own connection (PR #47).
- A passing SQLite run says nothing about the Liquibase changesets or PostgreSQL-specific SQL. Changes that depend on either need a `Category=Postgres` test.
