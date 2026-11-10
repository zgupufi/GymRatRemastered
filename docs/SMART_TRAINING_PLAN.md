# Smart training: targets, effort, coach suggestions

Source: the research doc "GymRat UX priorities and niches research", ranked shortlist items 1, 2 and 3. Positioning: the free, web-first lifting log that tells you exactly what to lift next and why.

This plan covers three features that ship together in one PR, in this order because each feeds the next:

1. Effort per set, add/remove sets mid-workout, copy last set (item 2, feeds the others)
2. Today's target per exercise by double progression (item 1)
3. Coach suggestions that the user confirms before anything changes (item 3)

Out of scope: pre-workout check-in, exercise swap, notes, PRs per rep range, CSV, PWA, plate calculator, mesocycles, and any change to PR #25 files beyond what these features need.

## Shared decisions

- Effort is stored as reps in reserve (RIR), 0 to 5, optional. The UI labels it Max (0), Hard (1), Good (2), Easy (3+). 3+ stores 3. Nothing is required to log a set.
- Each workout exercise gets a rep range (`RepsMin`, `RepsMax`, default 8 to 12) and a weight step (`WeightStepKg`, default 2.5). These are what double progression and the coach read and change.
- All weights are kilograms in the database and API, converted at the edge by the existing `lib/weight`.
- "Last time" means the latest finished session of the user that has sets for that exercise, in any workout. Sessions without `FinishedAt` are ignored.
- The coach never writes anything on its own. It can only create pending suggestions. Only an explicit user action on a suggestion changes a workout.
- Backend conventions: no code comments, `BusinessLogicException` with codes in `ErrorCodes` plus en and ro resx entries, Liquibase changeset (never edit an applied one), GUID v7 default from the database, `TimeProvider.System`, FluentValidation validators in `API/Validators` run by the global validation filter, `[Authorize]`, `response` and `userId` variables in actions, endpoint integration tests only through HTTP.

## Feature 1: Effort per set and flexible sets

### Behaviour

- Each set row has an optional effort control. It appears on a set once it is checked off and can be changed until the exercise is saved.
- Add set: a button under the sets card appends a row. It copies weight and reps of the previous row as a draft (copy last set). Remove set: a small control on the last row if it is not done. The workout's planned set count is not changed by this; only today's session is.
- Saved sets carry effort. "Last time" text shows it when present (for example "62.5 x 8 @ Hard").

### Data model

Migration `007-smart-training.xml`:

- `SessionSets.RepsInReserve integer null`

Entity `SessionSet.RepsInReserve int?`; EF configuration without a default.

### API

- `PUT /api/sessions/{id}/exercises/{exerciseId}/sets` body: `{ sets: [{ reps, weightKg, repsInReserve? }] }`. Add and remove need no endpoint because the call replaces all sets of the exercise.
- `GET /api/workouts/{id}/last-session` and exercise progress: `SessionSetDto` gains `repsInReserve`.
- Validator: `RepsInReserve` inclusive 0 to 5 when present.

### UI

- Mockup: `docs/mockups/smart-training.html` and `.png`, frames "Executing with target" and "Effort picker".
- `SetsCard`: effort chip row under a done set, "Add set" and "Remove" controls.
- `sets.ts`: `SetDraft.effort: number | null`, `addDraft`, `removeLastDraft`, `toEntries` includes effort.

### Tests

Backend integration (`SessionsEndpointsTests`): save sets with and without effort then read last-session and assert effort values and nulls; effort -1 and 6 return 400 `BLException_Validation_InvalidRequest` with a `RepsInReserve` error; replacing with fewer and more sets than the planned count persists exactly that many. Validator unit tests for the range. Frontend: `sets.test.ts` for add, remove, copy last, effort in entries; `SetsCard` and `WorkoutExecutionPage` tests for adding a set, removing only an undone last set, choosing effort and the PUT payload.

## Feature 2: Today's target per exercise

### Behaviour (double progression)

Per exercise, using the sets of the last finished session for it and the exercise's settings (`RepsMin`, `RepsMax`, `WeightStepKg`):

1. No history: reason `NoHistory`, no weight, target reps are the range, so the UI shows "Pick a weight, aim for 8 to 12".
2. Working weight = heaviest weight logged in that session. Working sets = sets at that weight.
3. Hold (`Hold`): any working set has `RepsInReserve = 0`, or any working set has fewer than `RepsMin` reps. Same weight, target reps = lowest reps among working sets, clamped to the range.
4. Increase weight (`IncreaseWeight`): every working set reached `RepsMax` and none is at RIR 0. Weight = working weight + step, rounded to the step, target reps = `RepsMin`.
5. Add reps (`AddRep`): otherwise. Same weight, target reps = lowest working reps + 1, capped at `RepsMax`.
6. Target sets = the workout exercise's `TargetSets`.

The logic lives in one pure service class, `ProgressionCalculator`, so every branch is unit-testable.

### Data model

Same migration: `WorkoutExercises.RepsMin integer not null default 8`, `RepsMax integer not null default 12`, `WeightStepKg numeric(5,2) not null default 2.5`. Entity and configuration updated, check constraint `RepsMin <= RepsMax`.

### API

- `GET /api/workouts/{id}/targets` returns a list of `ExerciseTargetDto { exerciseId, sets, repsMin, repsMax, weightKg, reps, reason, lastWeightKg, lastReps }`. `reason` is `NoHistory | Hold | IncreaseWeight | AddRep`. 404 `Workouts.NotFound` for missing or foreign workouts.
- `PUT /api/workouts/{id}/exercises`: `WorkoutExerciseRequest` gains optional `repsMin`, `repsMax`, `weightStepKg`. When omitted, the existing values for that exercise in that workout are kept, otherwise defaults are used, so the editor and the coach tools cannot reset them by accident.
- `GET /api/workouts/{id}` and the summary: `WorkoutExerciseDto` gains `repsMin`, `repsMax`, `weightStepKg`.
- Validator: `RepsMin` 1 to 100, `RepsMax` 1 to 100 and not below `RepsMin`, `WeightStepKg` 0.25 to 50 with precision 2.

### UI

- Execution page: a target banner above the sets ("Today: 62.5 kg x 9", with a reason chip: New weight, Add a rep, Hold, First time) and a "Use target" button that fills empty drafts. Placeholders in the weight and reps inputs use the target instead of last time when a target exists; last time stays visible as a secondary line.
- Workout editor: rep range inputs next to the set count.
- Mockup frames "Executing with target" and "Workout editor reps range".

### Tests

Unit (`ProgressionCalculatorTests`): no history, all sets at max reps, one set at RIR 0, one set below min, mixed weights (only the heaviest counts), reps in range below max, weight rounding to step, cap at max. Integration: complete a session through endpoints then assert `GET targets` per branch with exact numbers; foreign workout 404; workout with a never-performed exercise; two sessions where only the latest counts; unfinished session ignored; replacing exercises without range keeps a changed range; invalid range 400. Frontend: banner and "Use target" rendering per reason, placeholder logic, editor range inputs and payload.

## Feature 3: Coach suggestions the user confirms

### Behaviour

- The coach chat gets read tools `get_exercise_history` (last sessions with reps, weight and RIR for one exercise) and `get_targets`, and one write-intent tool, `propose_exercise_change`. It can propose changing sets, rep range and weight step of one exercise in one of the user's workouts, with a short rationale (a stall, a deload, too easy). It cannot apply anything.
- Each proposal is stored as a pending suggestion and returned with the chat reply. The chat shows a card with the change ("Bench Press: 4 to 3 sets, reps 8-12 to 6-10"), the rationale, and Apply or Not now.
- Apply changes the workout exercise, marks the suggestion applied, and announces the workout change so open lists refresh. Not now marks it dismissed. Both are one-way: a resolved suggestion cannot be applied again.
- The existing guard against raw tool-call text in replies stays; the system prompt is updated to say changes are only proposed and the user confirms them.

### Data model

Migration: `CoachSuggestions` (`Id` uuid v7 default, `UserId` FK cascade, `WorkoutId` FK cascade, `ExerciseId` FK cascade, `TargetSets int null`, `RepsMin int null`, `RepsMax int null`, `WeightStepKg numeric(5,2) null`, `Rationale varchar(500) not null`, `Status integer not null` (0 pending, 1 applied, 2 dismissed), `CreatedAt`, `ResolvedAt null`). Index on `(UserId, Status)`.

### API

- `POST /api/chat/messages` response adds `suggestions: CoachSuggestionDto[]` created during that turn.
- `GET /api/coach/suggestions` returns the user's pending suggestions (a list endpoint, so it uses the standard filter, `PagedResult`, validator and database paging).
- `POST /api/coach/suggestions/{id}/apply` and `POST /api/coach/suggestions/{id}/dismiss` return 204. Errors: `CoachSuggestions.NotFound` (missing or foreign), `CoachSuggestions.NotPending`, `Workouts.NotFound`/`WorkoutExercise.NotFound` if the target vanished.
- `CoachSuggestionDto { id, workoutId, workoutName, exerciseId, exerciseName, targetSets, repsMin, repsMax, weightStepKg, rationale, status }` plus the current values so the card can show before and after.
- Tool validation: workout owned by the user, exercise is in that workout, at least one value present, values pass the same validator as the editor, rationale required and trimmed to 500. Tool errors return `{ error: code }` to the model like the existing tools.
- Usage limits from `ChatUsageLimiter` apply unchanged.

### UI

- `ChatWidget`: suggestion card inside the assistant bubble with Apply and Not now, then an Applied or Dismissed state. Pending suggestions reload when the widget opens so a reload does not lose them.
- Mockup frame "Coach suggestion card".

### Tests

Integration through `ScriptedChatClient`: scripted tool call to `propose_exercise_change` returns a suggestion in the reply and in `GET /api/coach/suggestions`, the workout is unchanged until apply; apply changes the workout (read with `GET /api/workouts/{id}`) and the suggestion leaves the pending list; dismiss leaves the workout unchanged; apply twice returns `NotPending`; another user gets `NotFound` for apply, dismiss and sees nothing in the list; proposal for a foreign workout, an exercise not in the workout, an empty change, an invalid range, all return a tool error and no suggestion; history tool returns RIR. Unit: validators, filter. Frontend: card rendering, apply and dismiss calls and states, reload of pending, error toast.

## Delivery order and commits

1. This plan and the mockups (reviewed and approved before any UI code).
2. Migration, entities, repositories, ProgressionCalculator, services, controllers, resx, backend tests.
3. Frontend api layer, execution page, editor, chat card, frontend tests.
4. Backend coverage stays at or above 90 percent, `dotnet test`, `npm run lint`, `npm test`, `npm run build` clean.

## Risks

- The RIR scale is unfamiliar to beginners. Mitigation: plain labels (Max, Hard, Good, Easy) and optional everywhere, with the target logic falling back to reps only.
- Heaviest-weight rule can mislead after a drop set. Accepted for the first version; the coach explains stalls and the user can ignore any target.
- LLM proposals can be wrong. Mitigation: confirmation, validated ranges, and a before and after view.
