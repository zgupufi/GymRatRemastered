# 003: Business errors return 400 with a code

Status: accepted. The app review backlog item BE-08 asks whether to change it, and that is not decided.

## Context

Clients need to tell errors apart and show a message in the user's language. This has been the behaviour since the first version (PR #1). The reason for using one status was not written down.

## Decision

Every `BusinessLogicException`, including "not found", ownership failures and rate limits, returns 400 with `{ code, message }`. The code names the error (`BLException_<Area>_<Name>`, in `ErrorCodes`), and the message comes from the middleware's en and ro resource files. Validation failures return 400 `BLException_Validation_InvalidRequest` with an `errors` list. A `RateLimitedException` (login, registration and refresh limits) also sends `Retry-After`.

## Consequences

- Clients tell these errors apart by `code`, not by status.
- A new error needs a constant and a message in both languages.
- Proxies and monitoring cannot tell "not found" or "rate limited" from bad input.
- This covers errors raised by the application. The framework still answers some requests itself, such as 401 for a missing or expired access token, which the web client uses to trigger a session refresh.
