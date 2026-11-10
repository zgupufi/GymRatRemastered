# Smart training: targets, effort, coach suggestions

Source: the research doc "GymRat UX priorities and niches research", ranked shortlist items 1, 2 and 3. Positioning: the free, web-first lifting log that tells you exactly what to lift next and why.

Three features ship together in one PR. They share one migration and one set of DTOs, so the code landed as one backend commit and one frontend commit; each feature section below is the review map:

1. Effort per set, add/remove sets mid-workout, copy last set (item 2, feeds the others)
2. Today's target per exercise by double progression (item 1)
3. Coach suggestions that the user confirms before anything changes (item 3)

Out of scope: pre-workout check-in, exercise swap, notes, PRs per rep range, CSV, PWA, plate calculator, mesocycles, time-based exercises, and any change to PR #25 files beyond what these features need.

The plan was challenged by a second agent. The decisions below are the agreed outcome. The "Decision log" at the end lists what changed and why.

## Shared decisions

- Effort is stored as reps in reserve (RIR), 0 to 3, optional. 3 means "3 or more". UI labels: Max (0), Hard (1), Good (2), Easy (3+). Logging a set never requires it.
- Each workout exercise has a rep range (`RepsMin`, `RepsMax`, default 8 to 12) and a weight step (`WeightStepKg`). The default step comes from the exercise's equipment: barbell 2.5, dumbbell 2, anything else 1.25.
- Weights are canonical kilograms in the database and API. Targets are rounded to the step in kg; the frontend converts and rounds for lb display through `lib/weight`.
- "Last time" means the latest finished session of the user that has sets for that exercise, in any workout. The current workout's rep range and step always win; history only supplies weights, reps and RIR, judged against today's range.
- Duplicate exercises in one workout (supersets) are matched by exercise id and occurrence index.
- The coach never writes anything on its own. It only creates pending suggestions; only the user's Apply changes a workout.
- `PUT` sets stays last-write-wins: one user, one open session, and the call is idempotent.
- Backend conventions: no code comments, `BusinessLogicException` with codes in `ErrorCodes` plus en and ro resx entries (all map to 400), Liquibase changeset, GUID v7 default from the database, `TimeProvider.System`, FluentValidation validators in `API/Validators` run by the global validation filter, `[Authorize]`, `response` and `userId` variables, endpoint integration tests only over HTTP.

## Feature 1: Effort per set and flexible sets

### Behaviour

- A done set shows an optional effort chip row (Easy, Good, Hard, Max). It can be changed until the exercise is saved.
- "Add set" appends a row prefilled from the previous row (copy last set). "Remove last" removes the last row if it is not done. The workout's planned set count does not change; only today's session does.
- "Last time" text shows effort when present.

### Data model

Migration `007-smart-training.xml`: `SessionSets.RepsInReserve integer null`, plus the served target columns from Feature 2.

### API

- `PUT /api/sessions/{id}/exercises/{exerciseId}/sets` body: `{ sets: [{ reps, weightKg, repsInReserve? }], target?: { weightKg?, reps, reason } }`.
- `SessionSetDto` gains `repsInReserve` (last-session and history).
- Validator: `RepsInReserve` 0 to 3 when present.

### UI

Approved mockup `docs/mockups/smart-training.png`, frame 1. `SetsCard` gets the effort row and the add/remove controls; `sets.ts` gets `effort`, `addDraft`, `removeLastDraft`.

### Tests

Integration: save with and without effort and read it back; effort -1 and 4 return 400 with a `RepsInReserve` error; saving more and fewer sets than planned persists exactly those. Validator unit tests. Frontend: `sets.test.ts` (add, remove, copy, effort in entries), `WorkoutExecutionPage` tests for add set, remove only an undone last set, choosing effort, and the PUT payload.

## Feature 2: Today's target per exercise

### Rules (double progression), in `ProgressionCalculator`

Inputs: today's settings (`TargetSets`, `RepsMin`, `RepsMax`, `WeightStepKg`) and the sets of the last two finished sessions for that exercise.

1. No history: `NoHistory`, no weight, reps = `RepsMin`, so the UI says "Pick a weight, aim for 8 to 12".
2. Working weight = the weight with the most sets in the last session; ties go to the heavier weight. Working sets = the sets at that weight. This ignores one-off typos and single heavier sets. Top-set-plus-backoff programs are a known limit.
3. Hold: any working set is at RIR 0 or below `RepsMin`. Same weight, reps = lowest working reps clamped to the range. A miss outranks an "Easy" label.
4. Stalled: Hold, and the session before it was also Hold at the same working weight. Same numbers as Hold; the UI shows "Stalled, ask your coach" and the coach sees it in `get_targets`. No automatic deload.
5. IncreaseWeight: every working set reached `RepsMax` and none is at RIR 0. Weight = working weight + step, rounded to the step; reps = `RepsMin`.
6. AddRep: otherwise. Same weight, reps = lowest working reps + 1, capped at `RepsMax`.
7. Bodyweight (working weight 0): never IncreaseWeight. Reps = lowest working reps + 1 with no cap, or Hold on RIR 0.
8. Sets = `TargetSets`.

### Served target

The client sends the target it showed with each sets PUT; it is stored on that exercise's rows as nullable `SessionSets.TargetWeightKg`, `TargetReps`, `TargetReason`. This lets us measure later whether people follow targets. It is never required.

### Data model

Same migration: `WorkoutExercises.RepsMin integer not null default 8`, `RepsMax integer not null default 12`, `WeightStepKg numeric(5,2) not null default 2.5`, check `RepsMin <= RepsMax`; `SessionSets.TargetWeightKg numeric(7,2) null`, `TargetReps integer null`, `TargetReason integer null`.

### API

- `GET /api/workouts/{id}/targets` returns one `ExerciseTargetDto { order, exerciseId, sets, repsMin, repsMax, weightKg, reps, reason, lastWeightKg, lastReps }` per position. `reason`: `NoHistory | Hold | Stalled | IncreaseWeight | AddRep`. Missing or foreign workout: `Workouts.NotFound`.
- `PUT /api/workouts/{id}/exercises`: `WorkoutExerciseRequest` gains optional `repsMin`, `repsMax`, `weightStepKg`. When omitted, the values of the same exercise at the same occurrence index are kept; for a new exercise the defaults (with the equipment step) are used.
- `WorkoutExerciseDto` gains `repsMin`, `repsMax`, `weightStepKg`.
- Validator: `RepsMin` and `RepsMax` 1 to 30, `RepsMax >= RepsMin`, `WeightStepKg` one of 1, 1.25, 2, 2.5, 5.

### UI

Approved mockup frames 1 to 3: target banner with reason chip and "Use target", target placeholders with last time as a secondary line, rep range inputs in the workout editor.

### Tests

Unit: golden table per rule (no history, mode with ties, typo set, hold on RIR 0, hold on miss, stalled, increase with rounding, add rep with cap, bodyweight), plus a generated grid theory over weights, reps, RIR and ranges asserting invariants: loaded targets never exceed `RepsMax`, the weight never drops, and the weight rises only on IncreaseWeight. Integration: each reason end to end with exact numbers; foreign workout; unfinished session ignored; only the latest sessions count; settings kept when omitted, including duplicates; invalid range or step returns 400; served target is accepted; kg and lb round trip through settings and targets. Frontend: banner per reason, "Use target", lb display rounding, editor range inputs and payload.

## Feature 3: Coach suggestions the user confirms

### Behaviour

- New coach tools: `get_exercise_history` (last sessions of one exercise with reps, weight and RIR), `get_targets`, and `propose_exercise_change`. The coach can propose new sets, rep range and step for one exercise in one of the user's workouts, with a rationale. It cannot apply.
- Each valid proposal becomes a pending suggestion returned with the reply. The chat renders a card with before and after, the rationale, and Apply / Not now. Apply changes the workout and announces the change; Not now dismisses. Both are final.
- Limits: at most one pending suggestion per workout exercise (a new one dismisses the old), at most 3 proposals per chat turn (later ones return a tool error), suggestions expire after 7 days (hidden from the list; apply returns `NotPending`).
- Bounds: sets change by at most 1 from the current value, reps 1 to 30 with min not above max, step from the allowed list, at least one value present, rationale required (trimmed to 500).
- Stale check: a suggestion stores the before-values it was made against. Apply fails with `CoachSuggestions.Stale` if any occurrence of that exercise in the workout differs from them. Apply updates every occurrence.
- Proposals are logged. Tool output stays data to the model; the worst case of a manipulated proposal is a card the user still has to confirm.
- The system prompt says changes are only proposed and the user confirms them.

### Data model

Same migration, `CoachSuggestions`: `Id` uuid v7, `UserId`, `WorkoutId`, `ExerciseId` (FKs cascade), `TargetSets`, `RepsMin`, `RepsMax`, `WeightStepKg` (null = unchanged), `BeforeTargetSets`, `BeforeRepsMin`, `BeforeRepsMax`, `BeforeWeightStepKg` (not null), `Rationale varchar(500)`, `Status integer` (0 pending, 1 applied, 2 dismissed), `CreatedAt`, `ResolvedAt`. Index `(UserId, Status)`.

### API

- `POST /api/chat/messages` response adds `suggestions: CoachSuggestionDto[]`.
- `GET /api/coach/suggestions`: pending, non-expired, standard filter and `PagedResult` (required for every list endpoint).
- `POST /api/coach/suggestions/{id}/apply` and `/dismiss` return 204. Errors: `CoachSuggestions.NotFound` (missing or foreign), `CoachSuggestions.NotPending` (resolved or expired), `CoachSuggestions.Stale`, `CoachSuggestions.ExerciseNotInWorkout`.

### UI

Approved mockup frame 4. Pending suggestions reload when the widget opens.

### Tests

Integration with `ScriptedChatClient`: a proposal returns a card and appears in the list while the workout is unchanged; apply changes it (read back with `GET /api/workouts/{id}`); dismiss leaves it unchanged; second apply returns `NotPending`; expired returns `NotPending`; another user gets `NotFound`; stale returns `Stale`; foreign workout, exercise not in workout, empty change, out-of-bound sets, invalid range, wrong-type and missing arguments, and a fourth proposal in one turn all give tool errors and no card; a second proposal for the same exercise dismisses the first; the history tool returns RIR. Unit: validators, filter, service. Frontend: card, apply and dismiss states, reload, error toast.

## Delivery

1. Plan and approved mockup.
2. F1, F2, F3 backend with tests; then frontend; coverage at or above 90 percent; `dotnet test`, `npm run lint`, `npm test`, `npm run build` clean; manual check in the preview browser.

## Decision log (challenge round)

| Topic | Outcome |
| --- | --- |
| Split into 3 PRs | One PR as asked; backend and frontend commits, reviewed per feature section |
| Working weight | Mode of weights, ties to heavier (was heaviest) |
| Deload | `Stalled` after two holds; coach proposes, no auto deload |
| Step | Default by equipment; reset to RepsMin kept (standard double progression) |
| Range precedence | Today's workout settings win |
| Served target | Stored in nullable session set columns now |
| Bodyweight, kg/lb | Reps-only progression at 0 kg; round-trip tests |
| PUT sets | Last-write-wins kept |
| RIR scale | 0 to 3, 3 = 3+ |
| Coach safety | 1 pending per exercise, 3 per turn, 7-day expiry, stale check per occurrence, ±1 set |
| Tool reliability | Tests for malformed and duplicate arguments |
| Paging | Kept, required by conventions |
| Tests | Golden tables and invariant grid; no e2e framework, manual browser check |
| Duplicates | Matched by occurrence index |
