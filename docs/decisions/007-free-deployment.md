# 007: Deploy on free tiers

Status: proposed. The setup is in PR #30, which was not merged when this was written. Check that PR before relying on this record.

## Context

The goal is to have the app online on free hosting plans.

## Decision

Cloudflare Pages serves the frontend, Render's free plan runs the API and Neon's free plan hosts PostgreSQL.

Already on `main`: the nightly encrypted database backup and the uptime check (each skipped until its settings exist), the [runbook](../RUNBOOK.md), and the Pages headers in `frontend/public/_headers`.

Only in PR #30: `render.yaml`, the `/health` endpoint, the Liquibase migration workflow, the Pages routing file and `docs/DEPLOY_FREE.md`. The runbook and the README already mention some of these.

## Consequences

- Render's free service sleeps when idle, so the first request after that is slow. Once its URL is configured, the uptime check also keeps the service awake.
- The free hours cover one always-on service, so there is no staging API.
- `pages.dev` and `onrender.com` are different sites, so browsers do not send the default `SameSite=Strict` refresh cookie. This needs a shared parent domain, one origin, or `RefreshToken__SameSite=None`, which some browsers still block. See the [README](../../README.md).
- Neon's point-in-time restore is short on the free plan, so the nightly dump is the backup to rely on.
