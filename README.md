# GymRat

.NET 10 Web API (`backend`), React + Vite frontend (`frontend`), PostgreSQL with Liquibase migrations (`database`).

## Run everything with Docker

```bash
cp .env.example .env
docker compose up --build
```

Frontend: http://localhost:3000, API: http://localhost:8080, Postgres: localhost:5432.

## Run the API locally against the Docker database

Start only the database and apply the Liquibase changelog:

```bash
docker compose up -d db liquibase
```

Point the API at it with user secrets (use the `POSTGRES_PASSWORD` from your `.env`):

```bash
cd backend/src/GymRatService.API
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Host=localhost;Port=5432;Database=gymrat;Username=gymrat;Password=<POSTGRES_PASSWORD>"
dotnet user-secrets set "JwtSettings:Key" "<at least 32 characters>"
dotnet run --launch-profile http
```

Then start the frontend with `npm run dev` in `frontend`; Vite proxies `/api` to http://localhost:5243.

## Changing the database

Add a new XML changeset under `database/changelog/changes`, include it in `db.changelog-master.xml`, and run `docker compose up liquibase`. Never edit a changeset that has already been applied.
