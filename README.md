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

## Free web deployment

`docs/DEPLOY_FREE.md` walks through hosting the frontend on Cloudflare Pages, the API on Render and the database on Neon, all on free tiers. The startup checks described below apply there too.

## Changing the database

Add a new XML changeset under `database/changelog/changes`, include it in `db.changelog-master.xml`, and run `docker compose up db`. Never edit a changeset that has already been applied.

## Login and register limits

`POST /api/auth/login` and `POST /api/auth/register` are rate limited. A refused request answers 400 `BLException_AuthService_TooManyAttempts` with a `Retry-After` header in seconds. The limits live in the `AuthRateLimit` section and every value must be greater than zero, otherwise the API refuses to start:

- Per client IP: `LoginPermitLimit` (10) per `LoginWindowSeconds` (60) and `RegisterPermitLimit` (5) per `RegisterWindowSeconds` (60). IPv6 clients are grouped by their /64 prefix.
- Per email, from any IP: `FailedLoginLimit` (5) login attempts per `FailedLoginWindowSeconds` (900). A successful login clears the count for that email.

The counters are kept in memory, so they reset when the API restarts.

Behind a reverse proxy the API only sees the proxy's address, so every user would share one limit. List the proxy in `ForwardedHeaders:TrustedNetworks` (CIDR notation, for example `ForwardedHeaders__TrustedNetworks__0=10.0.0.5/32`) and the API takes the client address from the last `X-Forwarded-For` entry of requests that come from those networks. The list is empty by default, which ignores the header. Docker Compose gives the web container a fixed address and trusts only that one; if `172.28.0.0/24` is taken on your machine, or you run a second copy of the stack, set `NETWORK_SUBNET` and `PROXY_IP` in `infra/.env`.

## AI coach

`POST /api/chat/messages` talks to an LLM through `Microsoft.Extensions.AI` (`IChatClient`). The default is `baidu/ernie-5.5-flash-preview:free` through OpenRouter's OpenAI-compatible endpoint, with `google/gemma-4-31b-it:free` as `AiCoach:FallbackModel` (used on 429, 5xx, timeout or an empty reply). The API key is never stored in the repo:

- Locally, in `backend/src/GymRatService.API`: `dotnet user-secrets set "AiCoach:ApiKey" "<your OpenRouter API key>"`
- Docker: set `AI_COACH_API_KEY` in `infra/.env`

Without a key the endpoint answers 400 `BLException_ChatService_ProviderUnavailable`. `AiCoach:Model` and `AiCoach:Endpoint` switch to any other OpenAI-compatible provider through configuration. Per-user limits: `AiCoach:RateLimitRequests` (5) per `RateLimitWindowSeconds` (60), `DailyRequestLimit` (100), `DailyTokenBudget` (30000); replies are capped at `MaximumOutputTokens` (600) and the tool loop at `MaximumToolIterations` (3).
