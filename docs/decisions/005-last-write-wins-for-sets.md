# 005: Saving sets replaces them, and the last write wins

Status: accepted

## Context

The app saves often during a workout. The [smart training plan](../SMART_TRAINING_PLAN.md) kept last-write-wins because there is one user with one open session, and the call is idempotent.

## Decision

`PUT /api/sessions/{id}/exercises/{exerciseId}/sets` carries the full list of sets for one exercise. In one transaction the repository deletes the old rows and inserts the new ones. There is no version or concurrency check. An empty list clears the exercise.

## Consequences

- Repeating a save gives the same sets, although the rows get new ids.
- Two tabs or devices in the same session overwrite each other without a warning.
- A client has to send saves in order. In `RunningWorkout.tsx`, saves made when a set is ticked or unticked, or when the effort of a ticked set changes, go through a queue. Next, Previous, Finish and the automatic move to the next exercise call the save directly, so they can overlap a queued one.
- No unique index covers session, exercise and set number, so the database would not reject duplicate rows from overlapping saves.
- A finished session refuses further saves.
