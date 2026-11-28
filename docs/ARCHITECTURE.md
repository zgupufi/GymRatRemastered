# Architecture

A gym workout tracker: a React single-page app, a .NET 10 Web API and PostgreSQL, with the schema managed by Liquibase. Setup and settings are in the [README](../README.md), operations in the [runbook](RUNBOOK.md), and the main choices in [decisions](decisions/README.md).

## Layers

```mermaid
flowchart TD
    subgraph backend["backend/src"]
        API["GymRatService.API<br/>controllers, filters, middleware, validators, DI"]
        Service["GymRatService.Service<br/>business rules, security, coach"]
        Repository["GymRatService.Repository<br/>EF Core, entities, queries"]
        Common["GymRatService.Common<br/>DTOs, error codes, filters, settings, enums"]
        API --> Service --> Repository
        API --> Common
        Service --> Common
        Repository --> Common
    end
    Web["frontend/<br/>pages and components, then src/api"] -->|"HTTP, /api"| API
    Repository --> DB[("PostgreSQL")]
    Liquibase["database/changelog<br/>Liquibase"] -->|"owns the schema"| DB
    Service -->|"IChatClient"| LLM["LLM provider"]
```

Calls go `API -> Service -> Repository`, never back. Controllers only map HTTP to a service call. Services depend on repository interfaces, not on `DbContext`. Repositories hold data access. Entities stay inside the backend; the API returns DTOs. `AddServiceLayer` registers the repository layer, so the API project does not reference it directly. The API project builds the LLM client (`AiCoachExtensions`).

`infra/docker-compose.yml` runs Postgres, a one-off Liquibase update, the API and nginx. nginx serves the built frontend and proxies `/api`, so both share one origin. The Vite dev server proxies `/api` too.

## Request flow

```mermaid
flowchart TD
    Request --> Edge["Forwarded headers, HSTS, request language (en, ro)"]
    Edge --> Errors["BusinessLogicExceptionMiddleware"]
    Errors --> Gate["CORS, auth rate limiter, JWT authentication and authorization"]
    Gate --> Filter["ValidationActionFilter<br/>FluentValidation on the action arguments"]
    Filter --> Controller --> Svc["Service"] --> Repo["Repository"] --> Db[("PostgreSQL")]
```

- Errors: a service throws `BusinessLogicException` with a code, or the validation filter throws `ValidationException`. The middleware answers 400 with a localized `{ code, message }`, plus `errors` for validation ([003](decisions/003-business-errors-return-400.md)).
- Validators live in `API/Validators`. Coach tools do not pass through the HTTP filter, so they call `IInputValidator` themselves.
- Identity: controllers are `[Authorize]` by default, and anonymous actions opt out. Controllers pass `User.GetUserId()` to the service, which checks ownership. Another user's resource gets the same "not found" code as a missing one.
- Frontend: API calls go through `src/api/client.ts`. It keeps the access token in memory and, on a 401, refreshes the session once and retries. The refresh token is an `HttpOnly` cookie (see "Sessions and refresh tokens" in the README).
- Coach: `POST /api/chat/messages` runs a tool loop whose tools call the same services with the signed-in user's id ([009](decisions/009-coach-changes-need-confirmation.md)).

## Data model

```mermaid
erDiagram
    Users ||--o{ Workouts : owns
    Users ||--o{ SplitDays : plans
    Users ||--o{ WorkoutSessions : logs
    Users ||--o{ RefreshTokens : "signs in with"
    Users ||--o| CoachUsages : "is limited by"
    Users ||--o{ CoachSuggestions : receives
    Workouts ||--o{ WorkoutExercises : contains
    Workouts ||--o{ SplitDays : "is scheduled on"
    Workouts ||--o{ CoachSuggestions : "is target of"
    Workouts o|--o{ WorkoutSessions : "is performed as"
    WorkoutSessions ||--o{ SessionSets : has
    Exercises ||--o{ WorkoutExercises : "is used in"
    Exercises ||--o{ SessionSets : "is logged as"
    Exercises o|--o{ CoachSuggestions : "may concern"
    Exercises ||--o{ ExerciseSecondaryMuscles : trains
    Exercises ||--o{ ExerciseTranslations : "is described by"
```

- `Exercises` (entity `ExerciseCard`) is a shared catalog seeded by Liquibase. The other tables hold user data, owned directly or through a parent row, and it is deleted with the account.
- Deleting a workout keeps its sessions and their sets: `WorkoutSessions.WorkoutId` becomes null.
- A user has at most one unfinished session (a partial unique index) and at most one workout per split day.
- Ids are UUID v7 ([008](decisions/008-uuid-v7-ids.md)) and weights are kilograms ([004](decisions/004-weights-in-kilograms.md)). The two exercise child tables are keyed by the exercise and a value instead of an id.

## Tests and CI

Backend unit tests mock the repository interfaces. Integration tests call the endpoints, normally on SQLite, and CI also runs a smaller set against PostgreSQL ([002](decisions/002-sqlite-for-tests.md)). The frontend uses Vitest and React Testing Library. `e2e/` holds a Playwright smoke test. The CI jobs are in `.github/workflows/ci.yml`.
