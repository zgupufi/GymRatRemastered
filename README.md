# GymRat

.NET 10 Web API (`backend`), React + Vite frontend (`frontend`), PostgreSQL with Liquibase migrations (`database`).

## Run everything with Docker

```bash
docker compose up --build
```

Frontend: http://localhost:3000, API: http://localhost:8080, Postgres: localhost:5432.

The database connection for the API is defined in `docker-compose.yml` (`api` service, `ConnectionStrings__DefaultConnection`). The defaults are for local development only. To use your own password or JWT key, copy `.env.example` to `.env` and change them.

## Changing the database

Add a new XML changeset under `database/changelog/changes`, include it in `db.changelog-master.xml`, and run `docker compose up liquibase`. Never edit a changeset that has already been applied.
