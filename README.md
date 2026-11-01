# GymRat

.NET 10 Web API (`backend`), React + Vite frontend (`frontend`), PostgreSQL with Liquibase migrations (`database`).

## Run with Docker

Run the commands from the `infra` folder, where `docker-compose.yml` lives:

```bash
cd infra
docker compose up db
docker compose up backend
docker compose up frontend
docker compose up --build
```

- `up db` starts the local Postgres (`postgres` service) and runs Liquibase (`db` service) to create the schema and seed the exercise catalog.
- `up backend` starts the API and also starts `db` first if it isn't running. API: http://localhost:8080.
- `up frontend` starts the web app and also starts `backend` and `db`. Frontend: http://localhost:3000.
- `up --build` builds the backend and frontend images and starts everything.

Postgres is exposed on localhost:5432. The database connection for the backend is defined in `infra/docker-compose.yml` (`backend` service, `ConnectionStrings__DefaultConnection`). The defaults are for local development only. To use your own password or JWT key, copy `infra/.env.example` to `infra/.env` and change them.

## Changing the database

Add a new XML changeset under `database/changelog/changes`, include it in `db.changelog-master.xml`, and run `docker compose up db`. Never edit a changeset that has already been applied.

## AI coach

`POST /api/chat/messages` talks to an LLM through `Microsoft.Extensions.AI` (`IChatClient`). The default is Gemini 2.5 Flash-Lite through Google's OpenAI-compatible endpoint. The API key is never stored in the repo:

- Locally, in `backend/src/GymRatService.API`: `dotnet user-secrets set "AiCoach:ApiKey" "<your Gemini API key>"`
- Docker: set `AI_COACH_API_KEY` in `infra/.env`

Without a key the endpoint answers 400 `BLException_ChatService_ProviderUnavailable`. `AiCoach:Model` and `AiCoach:Endpoint` switch to any other OpenAI-compatible provider through configuration. Limits are `AiCoach:RateLimitRequests` per `AiCoach:RateLimitWindowSeconds` per user and `AiCoach:DailyTokenBudget` tokens per user per day.
