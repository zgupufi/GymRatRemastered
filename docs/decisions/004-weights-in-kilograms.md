# 004: Weights are stored and sent in kilograms

Status: accepted

## Context

Each user picks kilograms or pounds. History, targets, charts and the coach all compare weights, so they need one unit.

## Decision

Kilograms are the only unit in the database, the API, the coach tools and the CSV export. The frontend converts for display and input in `frontend/src/lib/weight.ts`. Progression targets are calculated in kilograms with the exercise's weight step, which is also in kilograms.

## Consequences

- Changing the unit preference never rewrites stored data.
- A screen that shows or reads a weight must go through `weight.ts`.
- Conversions round. `displayWeight` rounds to 0.5 lb or 0.25 kg, and `fromKilograms`, used for volume, rounds to a whole number. For pounds, `toKilograms` prefers a 0.25 kg value that displays as the typed number, and otherwise keeps two decimals. `restoredWeight` refills an input without changing the stored value.
- A target rounded to a kilogram step is not always a round number of pounds.
