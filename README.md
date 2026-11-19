# GymRat

.NET 10 Web API (`backend`), React + Vite frontend (`frontend`), PostgreSQL with Liquibase migrations (`database`).

## Run with Docker

Run the commands from the `infra` folder, where `docker-compose.yml` lives. Before the first run, copy `infra/.env.example` to `infra/.env`:

```bash
cd infra
cp .env.example .env
docker compose up db
docker compose up backend
docker compose up frontend
docker compose up --build
```

- `up db` starts the local Postgres (`postgres` service) and runs Liquibase (`db` service) to create the schema and seed the exercise catalog.
- `up backend` starts the API and also starts `db` first if it isn't running. API: http://localhost:8080.
- `up frontend` starts the web app and also starts `backend` and `db`. Frontend: http://localhost:3000.
- `up --build` builds the backend and frontend images and starts everything.

Postgres is exposed on localhost:5432. The database connection for the backend is defined in `infra/docker-compose.yml` (`backend` service, `ConnectionStrings__DefaultConnection`).

The example file sets `ASPNETCORE_ENVIRONMENT=Development`, which lets the API start with the placeholder secrets. Without an `infra/.env` the API runs as `Production` and refuses to start, because the defaults in `docker-compose.yml` are publicly known.

`Development` is for your own machine only: it switches the startup checks off, accepts the public placeholder secrets and exposes `/openapi/v1.json`. When the compose stack runs anywhere else, remove the `ASPNETCORE_ENVIRONMENT` line from `infra/.env` or set it to `Production`, and set real values for `JWT_KEY`, `PASSWORD_PEPPER` and `POSTGRES_PASSWORD`.

## Required settings outside Development

In any environment other than `Development` and `Testing` the API validates its settings at startup and exits with a message that names the setting (never its value) when one of these rules is broken:

| Setting | Environment variable | Rule |
| --- | --- | --- |
| `JwtSettings:Key` | `JwtSettings__Key` (`JWT_KEY` in `infra/.env`) | At least 32 bytes, and not one of the development or example values from this repository. |
| `PasswordSettings:Pepper` | `PasswordSettings__Pepper` (`PASSWORD_PEPPER` in `infra/.env`) | Not empty, and not one of the development or example values from this repository. |
| `ConnectionStrings:DefaultConnection` | `ConnectionStrings__DefaultConnection` | Not empty. |
| `Cors:AllowedOrigins` | `Cors__AllowedOrigins__0`, `Cors__AllowedOrigins__1`, ... | At least one origin, unless `Cors:SameOrigin` (`Cors__SameOrigin`) is `true` because the web app and the API share one origin, as with the nginx proxy in `infra/docker-compose.yml`. |

Generate each secret separately, for example with `openssl rand -base64 48`. Changing the pepper invalidates every stored password.

## Changing the database

Add a new XML changeset under `database/changelog/changes`, include it in `db.changelog-master.xml`, and run `docker compose up db`. Never edit a changeset that has already been applied.

## AI coach

`POST /api/chat/messages` talks to an LLM through `Microsoft.Extensions.AI` (`IChatClient`). The default is `baidu/ernie-5.5-flash-preview:free` through OpenRouter's OpenAI-compatible endpoint, with `google/gemma-4-31b-it:free` as `AiCoach:FallbackModel` (used on 429, 5xx, timeout or an empty reply). The API key is never stored in the repo:

- Locally, in `backend/src/GymRatService.API`: `dotnet user-secrets set "AiCoach:ApiKey" "<your OpenRouter API key>"`
- Docker: set `AI_COACH_API_KEY` in `infra/.env`

Without a key the endpoint answers 400 `BLException_ChatService_ProviderUnavailable`. `AiCoach:Model` and `AiCoach:Endpoint` switch to any other OpenAI-compatible provider through configuration. Per-user limits: `AiCoach:RateLimitRequests` (5) per `RateLimitWindowSeconds` (60), `DailyRequestLimit` (100), `DailyTokenBudget` (30000); replies are capped at `MaximumOutputTokens` (600) and the tool loop at `MaximumToolIterations` (3).
