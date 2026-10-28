# GymRat

Gym workout tracking app, rebuilt from the old GymRatService app: .NET 10 Web API backend, React + TypeScript frontend and PostgreSQL, in one repo.

Backend rules live in `backend/CLAUDE.md`, frontend rules in `frontend/CLAUDE.md`.

## Repo layout
```
/backend    .NET 10 Web API, N-layer (Controller → Service → Repository)
/frontend   React + TypeScript + Vite
/database   Liquibase changelog (schema, functions, seed data)
/infra      docker-compose.yml and .env.example for local Docker runs
/docs       specs (FRONTEND_SPEC.md), architecture decisions, API contract
```

## Commands
Docker (run from `/infra`):
- Database only (Postgres + Liquibase update): `docker compose up db`
- API on http://localhost:8080 (starts `db` too): `docker compose up backend`
- Web app on http://localhost:3000 (starts everything): `docker compose up frontend`
- Rebuild images and start everything: `docker compose up --build`
- Local overrides (DB password, JWT key): copy `.env.example` to `.env`.

Database changes: add a new XML changeset under `database/changelog/changes`, include it in `db.changelog-master.xml`, run `docker compose up db`. Never edit a changeset that has already been applied.

## General rules
- No code comments or XML doc summaries, in backend or frontend code.
- A review comment applies everywhere it fits: propagate it to all similar code, not only the line it was left on.
- Don't add third-party libraries for auth or password hashing; use what .NET provides.

## Testing
- Every new feature or change needs tests. Run them before finishing the task.
- Backend details are in `backend/CLAUDE.md`, frontend details in `frontend/CLAUDE.md`.

## Git workflow
- Tasks live in GitLab (`gymrat-group/tasks`); code lives on GitHub (`zgupufi/GymRatRemastered`).
- One branch per task: `feature/GR-123-short-kebab-description` or `bugfix/GR-123-short-kebab-description` (GR number = GitLab issue number).
- Commits start with the issue: `GR-123: short description`.
- PR description follows `.github/pull_request_template.md` and links the GitLab issue.
- Rebase on `main` before opening the PR and before asking for review.
- Merge with squash, and only after review approval:
  - Backend PRs: the user reviews the code and approves.
  - Frontend PRs: see `frontend/CLAUDE.md`.

## Definition of done
- Tests added or updated and passing.
- Build and lint clean, no unrelated file changes.
- PR opened against `main` with the issue linked.

## Never
- Push directly to `main` or force-push.
- Merge before review approval.
- Commit secrets, connection strings or API keys (use user secrets or `infra/.env`).
- Add business logic to controllers or repositories.
- Touch files outside the task's scope without calling it out in the PR.
