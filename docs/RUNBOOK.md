# Runbook

Operations for the free deployment (Cloudflare Pages, Render, Neon). Setup is in `docs/DEPLOY_FREE.md`.

## 1. Settings these workflows need

In GitHub: Settings > Secrets and variables > Actions.

| Name | Kind | Used by | Value |
| --- | --- | --- | --- |
| `DEPLOY_ENABLED` | variable | backup | `true` |
| `NEON_DUMP_URL` | secret | backup | `postgresql://<user>:<password>@<direct-host>/<db>?sslmode=require` (the direct host, without `-pooler`) |
| `BACKUP_PASSPHRASE` | secret | backup | a long random passphrase. Keep a copy in a password manager: without it no backup can be read |
| `API_HEALTH_URL` | variable | uptime | `https://<api-host>/health` |

The `backup` and `uptime` jobs are skipped while their settings are missing.

## 2. Backups

`Backup database` (`.github/workflows/backup.yml`) runs every night at 02:17 UTC and on demand. It runs `pg_dump` in the custom format, encrypts the file with AES-256 and uploads it as the `gymrat-backup` artifact, kept for 30 days. Failed runs send the repository owner a GitHub email.

Limits to know:
- The artifact lives in the same GitHub repository as the code, so it is protected by the passphrase and by repository access, not by being elsewhere. For a copy outside GitHub, download a recent artifact now and then and store it in a place you control.
- Scheduled workflows are disabled by GitHub after 60 days without repository activity. If the repository goes quiet, run the workflow by hand and re-enable the schedule.
- Neon's own point-in-time restore is short on the free plan, so treat this backup as the real one.

### Restore drill

Run this once after the first deploy and again after any change to the schema tooling. It restores into an empty database; never restore over the live one.

1. Download the newest `gymrat-backup` artifact from the Actions tab and unzip it.
2. Decrypt: `gpg --output gymrat.dump --decrypt gymrat-YYYYMMDD.dump.gpg` (enter the passphrase).
3. Create an empty target. Either a throwaway Neon branch (Neon console > Branches > Create, from an empty point) or a local container: `docker run --rm -d --name restore -e POSTGRES_PASSWORD=test -p 5433:5432 postgres:17`.
4. Restore: `pg_restore --no-owner --no-privileges --dbname "<target-url>" gymrat.dump`. A few warnings about extensions that already exist are normal; errors about missing tables or data are not.
5. Check it: `select count(*) from "Users";`, `select count(*) from "Workouts";` and `select max(dateexecuted) from databasechangelog;` (the exact table names are in `database/changelog`). Counts should match what the app showed on the backup date.
6. Optionally start the API against the restored database and log in with a known account.
7. Delete the throwaway branch or container and the decrypted `gymrat.dump`.

Write the date and result of each drill at the bottom of this file.

## 3. Uptime

`Uptime check` (`.github/workflows/uptime.yml`) requests `API_HEALTH_URL` every 10 minutes and fails if there is no successful answer within 90 seconds and two retries. A failing run emails the repository owner. It also keeps the free Render service awake. GitHub's scheduler can delay runs by several minutes, so this is a smoke alarm, not an SLA.

`GET /health` and `HEAD /health` return 200 when the API can reach PostgreSQL and 503 when the database check fails. The probe needs no credentials from the caller and has a four-second database timeout. Both the uptime workflow and the Render blueprint in PR #30 use this path. Repeated database failures can cause Render to restart the API.

If the site is down:
1. Open the Render dashboard, look at the latest deploy and the logs.
2. A deploy that failed at startup usually means a missing or invalid setting: the API refuses to start in Production without a real JWT key, pepper and CORS origin.
3. Check Neon for a paused or over-quota project.
4. If a release caused it, redeploy the previous successful deploy from the Render dashboard.

## 4. Error tracking

For an unexpected API error, take the `traceId` from the 500 response and search for it in the Production JSON console logs. The error entry records the exception type. The request entry records the method, route template, status, duration in milliseconds and authenticated user ID when available. If a download fails after it starts, the API closes the stream and records the failure under the same trace ID.

### External tracker (pending)

Not wired up in this change. It needs a new dependency in the frontend and the API (for example the Sentry free tier), which is an open decision in `docs/APP_REVIEW_REPORT.md` section 15. When approved, the requirements are:
- one release tag per deploy (the commit SHA) on both sides, so an error shows the release it came from;
- personal data scrubbing: no request bodies, no tokens, no emails, user id only;
- the API response trace ID attached to API events;
- a forced error in each of the frontend and the API shows up in the tracker with the release tag.

## 5. Release checklist

Before merging to `main` (which deploys the API and frontend):
- [ ] CI is green: backend, frontend.
- [ ] Database changes are a new changeset, never an edited one; the `Migrate database` workflow runs on merge when `database/` changed, so confirm it succeeded before the API deploy needs the new schema.
- [ ] New settings exist in Render (`sync: false` ones are not set by the blueprint) and in the Pages project.
- [ ] No secret in the diff. `JwtSettings__Key` and `PasswordSettings__Pepper` are never rotated casually: the first logs everyone out, the second breaks every password.
- [ ] `Cors__AllowedOrigins__0` is the exact production frontend origin.
- [ ] A backup from the last 24 hours exists (Actions > `Backup database`). For a risky migration, run it by hand first.

After deploy, smoke test on the live site:
- [ ] `/health` returns HTTP 200.
- [ ] Register or log in, open Home.
- [ ] Open a workout, start it, log a set, finish it.
- [ ] Open the coach and send a message.
- [ ] Render logs show no new errors.

If the smoke test fails: roll the API back in Render, and if the migration was the cause restore from the latest backup into a new database following the drill above, then point `ConnectionStrings__DefaultConnection` at it.

## 6. Staging and previews

The free tiers do not leave room for a second always-on API (750 Render hours per month cover one service). Until that changes:
- Cloudflare Pages already builds a preview for every pull request. A preview talks to the production API only if its origin is allowed by CORS, which is deliberately not the case, so previews are for visual review only.
- Verify backend and schema changes locally with `docker compose up --build` in `infra/` and in CI, then follow the release checklist above.
- A real staging environment (a Neon branch plus a second Render service) becomes possible on a paid Render plan or another host. Revisit when the site has users.

## Drill log

| Date | Backup used | Restored into | Result |
| --- | --- | --- | --- |
| | | | |
