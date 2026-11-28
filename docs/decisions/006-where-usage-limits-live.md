# 006: Coach limits live in the database, login limits in memory

Status: accepted. For the coach, it replaces the earlier in-memory limits (PR #61).

## Context

The coach calls an LLM provider with one API key for the whole app, so each user has limits. While they were kept in memory, they reset on every restart and each API instance counted separately. Login, registration and refresh have their own limits.

## Decision

- Coach usage is one `CoachUsages` row per user, covering the rate window, the daily request count and the daily token budget. A `Version` column makes check-and-reserve atomic. Tokens are reserved before the provider call and corrected after it.
- The auth limits stay in memory: the ASP.NET rate limiter per client IP for login, registration and refresh, and `LoginAttemptLimiter` for login attempts per email.

## Consequences

- Coach limits survive restarts and hold across instances. The daily counters reset at midnight UTC.
- Auth limits reset when the API restarts, and each extra instance would allow the full limit again. Move them to shared storage before relying on them with more than one instance.
- Behind a reverse proxy, per-IP limits only separate clients when the proxy is listed in `ForwardedHeaders:TrustedNetworks`.

The limit values are in the [README](../../README.md).
