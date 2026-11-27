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

## Login and register limits

`POST /api/auth/login` and `POST /api/auth/register` are rate limited. A refused request answers 400 `BLException_AuthService_TooManyAttempts` with a `Retry-After` header in seconds. The limits live in the `AuthRateLimit` section and every value must be greater than zero, otherwise the API refuses to start:

- Per client IP: `LoginPermitLimit` (10) per `LoginWindowSeconds` (60), `RegisterPermitLimit` (5) per `RegisterWindowSeconds` (60) and `RefreshPermitLimit` (30) per `RefreshWindowSeconds` (60) for `POST /api/auth/refresh` and `POST /api/auth/logout`. IPv6 clients are grouped by their /64 prefix.
- Per email, from any IP: `FailedLoginLimit` (5) login attempts per `FailedLoginWindowSeconds` (900). A successful login clears the count for that email.

The counters are kept in memory, so they reset when the API restarts.

Behind a reverse proxy the API only sees the proxy's address, so every user would share one limit. List the proxy in `ForwardedHeaders:TrustedNetworks` (CIDR notation, for example `ForwardedHeaders__TrustedNetworks__0=10.0.0.5/32`) and the API takes the client address from the last `X-Forwarded-For` entry of requests that come from those networks. The list is empty by default, which ignores the header. Docker Compose gives the web container a fixed address and trusts only that one; if `172.28.0.0/24` is taken on your machine, or you run a second copy of the stack, set `NETWORK_SUBNET` and `PROXY_IP` in `infra/.env`.

## Sessions and refresh tokens

`POST /api/auth/login` returns a short access token (`JwtSettings:ExpiryMinutes`, 15) in the body and sets a rotating refresh token in an `HttpOnly` cookie. The web app keeps the access token in memory only. The cookie is scoped to `/api/auth`, is `Secure` outside `Development` and `Testing`, and the database stores only its SHA-256 hash in `RefreshTokens`.

- `POST /api/auth/refresh` swaps the cookie for a new one and returns a new access token. Every use extends the session by `RefreshToken:LifetimeDays` (30) from that moment, but never beyond `RefreshToken:AbsoluteLifetimeDays` (90) after the login, so a kept-alive stolen cookie cannot live forever.
- `POST /api/auth/logout` revokes the session of that cookie (every token rotated from the same login) and clears the cookie. Other devices stay signed in. If the call fails (for example offline) the browser forgets the session but the server keeps the token valid until it expires.
- Presenting a token that was already rotated, more than `RefreshToken:ReuseGraceSeconds` (10) after the rotation, is treated as theft: the whole session is revoked. The grace period absorbs two tabs refreshing at the same time; a token can be exchanged at most twice (the second only inside the grace period), enforced by one atomic database update. Rotated rows are kept until their expiry on purpose: they are what lets a replay, even weeks later, be recognised and revoke the session. Expired rows, and rows revoked more than a day ago, are deleted at the user's next login.
- `POST /api/users/me/password` revokes every refresh session of the user, the caller's own included, in the same database transaction as the password update, so a failure leaves both unchanged. Every device has to log in again with the new password. Access tokens already issued stay valid until they expire (up to `JwtSettings:ExpiryMinutes`, 15), so a stolen access token keeps working that long after a password change or an account deletion. The refresh cookie is scoped to `/api/auth` and never reaches the users endpoints, so the caller's session cannot be told apart; revoking all of them is the simple, safe choice. `DELETE /api/users/me` removes the user, and the refresh rows go with it through the cascading foreign key (an explicit revoke before it would add a failure mode and no protection).

| Setting | Environment variable | Default |
| --- | --- | --- |
| `RefreshToken:CookieName` | `RefreshToken__CookieName` | `gymrat_refresh` |
| `RefreshToken:LifetimeDays` | `RefreshToken__LifetimeDays` | `30` |
| `RefreshToken:AbsoluteLifetimeDays` | `RefreshToken__AbsoluteLifetimeDays` | `90` |
| `RefreshToken:ReuseGraceSeconds` | `RefreshToken__ReuseGraceSeconds` | `10` |
| `RefreshToken:SameSite` | `RefreshToken__SameSite` | `Strict` |

`Strict` works when the web app and the API share a site (the nginx proxy in `infra/docker-compose.yml`, the Vite dev proxy, or two subdomains of one registrable domain). When they are on different sites, for example `pages.dev` and `onrender.com`, browsers drop a `Strict` or `Lax` cookie on cross-site requests: either put both behind one origin or custom domain, or set `RefreshToken__SameSite=None` and list the web app in `Cors:AllowedOrigins` (the API allows credentials for those origins). `SameSite=None` only works over HTTPS, because the cookie is `Secure` outside `Development` and `Testing`. Some browsers block third-party cookies even then, so a shared parent domain is the dependable choice.

`POST /api/auth/refresh` and `POST /api/auth/logout` rely only on the cookie, so they refuse a request whose `Origin` header is not in `Cors:AllowedOrigins` (answer 400 `BLException_AuthService_OriginNotAllowed`). With `Cors:SameOrigin=true` the check is skipped and `SameSite` alone protects them.

The Docker Compose stack defaults to `ASPNETCORE_ENVIRONMENT=Production`, so the cookie is `Secure` while the stack is served over plain `http://localhost:3000`. Chrome and Firefox accept that on localhost; Safari and any other address (for example a LAN IP) drop the cookie and the session then does not survive a reload. Use `Development` in `infra/.env` for local runs, as the example file does.

## Security headers and HTTPS

The API sends `Strict-Transport-Security` (180 days, no subdomains, no preload) on HTTPS requests outside Development and Testing. Behind a TLS-terminating proxy the API only sees plain HTTP unless it trusts `X-Forwarded-Proto`, so the same `ForwardedHeaders:TrustedNetworks` list that controls `X-Forwarded-For` also controls the forwarded protocol; without it no HSTS header is sent. On Render the proxy is not a single fixed address: `render.yaml` (PR #30) sets `ForwardedHeaders__TrustedNetworks__0=10.0.0.0/8`, and if that range is wrong the API never sends HSTS and never sees client IPs. After deploying, run `curl -sI https://<api>/health` and confirm a `Strict-Transport-Security` header; if it is missing, take the proxy range from Render's request logs or docs. The API does not redirect HTTP to HTTPS: Render and Cloudflare do that at their edge, and a redirect inside the container would loop when the proxy speaks HTTP to it.

The static site sends a content security policy (scripts, styles and fonts from the site itself, `https://www.youtube.com` for the exercise video frame, `https://i.ytimg.com` for images, no framing), `X-Content-Type-Options`, `Referrer-Policy`, `Permissions-Policy` and `Cross-Origin-Opener-Policy`. `frontend/nginx.conf` covers Docker (its headers live in `frontend/security-headers.conf`, which every `location` must include: nginx drops server-level `add_header` lines from any `location` that sets its own `add_header`, so a location that adds cache headers must include that file too), where the API is proxied from the same origin, and `frontend/public/_headers` covers Cloudflare Pages, where `connect-src` also allows `https://*.onrender.com` for the API and HSTS is added. TODO once the API URL from PR #30 exists: replace the `*.onrender.com` wildcard in `_headers` with the exact API origin, because the wildcard lets a script reach any Render-hosted app. If the API lives on another host, add its origin to `connect-src` in `_headers`. Changing the policy: load every page with the browser console open and look for `Content Security Policy` errors.

## Static caching and compression

`frontend/nginx.conf` compresses text assets with gzip (the official nginx image has no brotli module) and leaves `/api/` uncompressed. Files under `/assets/` carry a content hash in their name, so they are cached for a year as `immutable`, and a missing one answers 404 instead of the app shell. `index.html` and every client route are sent with `no-cache`, so a deploy reaches users on their next load. The favicon is cached for a day.

`frontend/public/_headers` sets the same cache headers on Cloudflare Pages, which compresses with gzip or brotli by itself. Client routes other than `/` keep the Pages default for HTML (`max-age=0, must-revalidate`), because a `Cache-Control` under `/*` would be merged into the `/assets/*` one. `e2e/tests/static-caching.spec.ts` checks the nginx behaviour.

## Dependency and code scanning

The `audit` job in CI fails when a NuGet or npm package, direct or transitive, has a known high or critical vulnerability. Run the same checks locally:

```bash
scripts/audit/nuget.sh
(cd frontend && npm audit --audit-level=high)
(cd e2e && npm audit --audit-level=high)
```

A new advisory can turn the job red on a branch that did not touch any package. Fix it by updating the package. For npm, run `npm audit fix`. For a direct NuGet package, change its version in `backend/Directory.Packages.props`. For a transitive NuGet package, update the package that brings it in, or add a `<PackageVersion>` in `Directory.Packages.props` plus a `<PackageReference>` in the project that uses it. A `<PackageVersion>` alone does not change a transitive version, because transitive pinning is off.

`.github/dependabot.yml` opens weekly update pull requests for NuGet, npm, Docker, Docker Compose and GitHub Actions. `.github/workflows/codeql.yml` runs CodeQL on C# and TypeScript for every pull request, every push to `main` and once a week.

## AI coach

`POST /api/chat/messages` talks to an LLM through `Microsoft.Extensions.AI` (`IChatClient`). The default is `baidu/ernie-5.5-flash-preview:free` through OpenRouter's OpenAI-compatible endpoint, with `google/gemma-4-31b-it:free` as `AiCoach:FallbackModel` (used on 429, 5xx, timeout or an empty reply). The API key is never stored in the repo:

- Locally, in `backend/src/GymRatService.API`: `dotnet user-secrets set "AiCoach:ApiKey" "<your OpenRouter API key>"`
- Docker: set `AI_COACH_API_KEY` in `infra/.env`

Without a key the endpoint answers 400 `BLException_ChatService_ProviderUnavailable`. `AiCoach:Model` and `AiCoach:Endpoint` switch to any other OpenAI-compatible provider through configuration. Per-user limits: `AiCoach:RateLimitRequests` (5) per `RateLimitWindowSeconds` (60), `DailyRequestLimit` (100), `DailyTokenBudget` (30000); replies are capped at `MaximumOutputTokens` (600) and the tool loop at `MaximumToolIterations` (3). Usage is stored per user in the `CoachUsages` table, so the limits survive restarts and hold across instances; the daily counters reset at midnight UTC. Before the provider is called, the estimated input (prompt, history and tool definitions, about four characters per token) plus `MaximumOutputTokens` is reserved, and the request is refused with `BLException_ChatService_TokenBudgetExceeded` when that does not fit the daily budget. After the call the reservation is replaced by the tokens the provider reported for every model call, or by an estimate when it reports none; a call the caller cancels is charged its estimated input plus `MaximumOutputTokens`. Only the newest history messages that fit in `MaximumHistoryCharacters` (12000) are sent.

## End-to-end smoke test

`e2e/` holds a Playwright test that registers a user, builds a workout and split, runs the workout, resumes it after a reload and checks progression, on a desktop and a phone viewport. Start the stack from `infra`, then run it from `e2e`:

```
npm ci
npx playwright install chromium
E2E_BASE_URL=http://localhost:3000 npm test
```

CI runs it in the `e2e` job. The test does not leave the workout screen through an exercise link, because that still ends the session (BUG-02).

CI sets `ASPNETCORE_ENVIRONMENT=Development` for the `e2e` job on purpose. The refresh cookie is `Secure` outside Development and the job serves the stack over plain `http://localhost:3000`, so with `Production` the session would not survive the reload the test performs.
