# GymRat UX review and improvement plan

Reviewed 2026-11-06 against the running app (docker compose in WSL), at 1280×800 and 390×844, with a fresh test account. Flows covered: guest landing, register, login (including empty-submit validation), dashboard, Settings, How to use, Workouts (create, add exercises, save), Training split (assign, save), Workout execution (start, log, rest timer, next, stop), Progression, Exercise library, Exercise details, and the AI coach widget.

No code was changed. Mockups for every visual change are in `docs/ux-review/mockups/` and need rafael's approval before any implementation. Note that `docs/FRONTEND_SPEC.md` describes an older build (Tailwind, JSX, colorful tiles, emoji, "ADD TO WORKOUT" modal, Dashboard "How to Use" panel). The shipped app is already calmer and mostly one-accent. This plan compares against the shipped app and the Apple design skill (wayfinding, agency, simplicity, familiarity, restraint), not against the old spec.

Rafael's standing preferences applied throughout: no goofy colors or emoji, "How to use" opens from a button, workout names show no exercise counts.

## Priority 1: fix first (broken or risky)

### 1. AI coach prints raw tool-call markup and does nothing
Asking "Build me a leg day" returned the literal text `<tool_call><function=create_workout><parameter=name>Leg Day …` with exercise GUIDs, and no workout was created. This is the worst thing a user can see in the app, and it is the coach's headline feature.
- Fix on the backend: detect model output containing tool-call markup, never return it as a reply, and either retry or return a friendly failure ("I couldn't build that. Try again?").
- Frontend safety net: if a reply starts with `<tool_call`, show the friendly error instead of the text.
- Also add a visible timeout/"still thinking" state. The typing indicator can spin for a long time with no way to cancel.

### 2. "End workout" has no confirmation and the back arrow silently drops the session
The unlabeled red square (icon only, aria-label "Stop workout") ends the workout instantly and shows a finish screen with only the time. The header Back button during a run leaves immediately and the session is gone (reopening shows the Start screen again). One mis-tap on a phone mid-set loses the session.
- Label the button "End" and add a confirm sheet; route the back arrow through the same sheet. See `GR-ux-3-workout-execution.png` (A, B).
- Completed sets are already saved per set, so the copy can say so.

### 3. Expired session shows the wrong error
With an expired token, Workouts shows "Could not load your workouts / Check your connection", which sends the user to debug their network. A 401 on any API call should log out and go to `/login` with a "Session expired, please log in again" notice (the login page already has a notice slot).

### 4. Dashboard promises things it can't deliver
A new account sees "Your workout is ready" and a big Start Workout button with no workouts and no split. Tapping it leads to a "Rest day" page. The hero copy ("enforce progressive overload", "crush your goals") is also generic and a little shouty.
- Make the hero state-aware: new account (setup steps), workout today (name and exercises), rest day (next workout). See `GR-ux-2-dashboard.png`.

## Priority 2: navigation and wayfinding

### 5. No persistent navigation; floating Back is unreliable
Every non-home page is reached only from the dashboard grid and left with a floating "Back" pill that uses browser history. Going Workouts → Split means Back, then tap again. On mobile the pill floats over page content, and on wide screens it sits far from the content (visible on every desktop screenshot). The Back button label also collides with the muscle group named "Back" in the library.
- Add a top nav on desktop and a 5-tab bottom bar on mobile (Home, Workouts, Split, Progress, Library); keep an in-header back arrow only on detail pages and the running workout. See `GR-ux-1-navigation.png`.
- Settings and "How to use" live in the header as buttons on both sizes. No panel on the page.
- Page titles: set `document.title` per route (currently always "GymRat"), which also fixes browser history and tab labels.

### 6. AI coach button covers content on mobile
The floating button sits over the dashboard card chevron, the last split card dropdown, and workout row controls on 390px screens. With the tab bar, move it above the bar and let it hide on scroll down, or move coach access into the nav as a sixth entry on desktop only.

## Priority 3: consistency and clarity

### 7. Workouts page
- Remove the exercise counts everywhere they appear: workout row pill ("3 exercises"), the Start screen ("3 exercises planned"), and the workout picker in the execution flow (rafael's rule).
- The rows also show muscle chips (CHEST, LEGS). Drop them from the row; a name is enough, and on 390px the name wraps to three lines next to the pill, chevron, edit and delete buttons.
- Creating a workout drops straight into an inline rename field with a lone check button, and the row shows a stale "0 exercises" while editing. Better: create, open the editor with the name selected, and show exercises below.
- Replace Save changes plus the collapse behavior with autosave and a "Saved" indicator.
- Delete is a red trash icon next to edit with no confirmation. Move rename and delete into a ⋯ menu and confirm delete.
- "Previous / Next" pagination shows for a single workout, which is noise. Hide it under one page.
- See `GR-ux-4-workouts-and-library.png` (A, B).

### 8. Exercise library
- Every card has the same dumbbell icon, which carries no information, and a redundant "View details" button on each card.
- Section header "Back" repeats the muscle in each card's subtitle; "Showing 24 of 89" with Load more means the first screen only covers Back and Biceps.
- Difficulty uses green, blue and orange pills (the "goofy color" risk). Plain text is enough.
- Proposal: the whole card opens details, with a + button to add to a workout from the list, and muscle filter chips instead of a dropdown. Keep search. See `GR-ux-4-workouts-and-library.png` (C).

### 9. Exercise details
- The YouTube embed rendered as a small clipped box (about 220×150) inside a large empty 16:9 frame in my run. Please check on a real network, but give the iframe `width:100%; aspect-ratio:16/9` regardless. Also show the "Demo video unavailable" fallback when the embed fails.
- The info tiles use blue, green and purple text for muscle, secondary muscles and equipment. Use one neutral color.
- "+ Add to workout" uses the plus-prefixed label and a modal; fine, but show the workout name it was added to in the success state.

### 10. How to use modal
- It is a 7-step list with 7 colors (grey, orange, red, cyan, blue, green, purple), which reads as rainbow decoration. Use one neutral number badge.
- Titles are vague ("Purpose & Scope", "Database Mastery") and the intro is hype. Shorten to 4 steps: Build a workout, Assign it to days, Log your sets, Watch progress. Mention the coach in one line.
- On a 800px-tall viewport the last step is cut off with no scroll affordance. Cap the height and show a fade at the bottom.

### 11. Training split
- Seven near-identical empty cards of mostly blank space; "Save split" is enabled even with no changes and no unsaved-changes cue. Use compact rows (day, workout select) and enable Save only when changed. Autosave would be better.
- No guidance when there are no workouts. The dropdown only offers "Rest day". Add "Create a workout first" with a link.
- Page title and subtitle sit too tight (title descenders touch the subtitle on Workouts, Split and Progression).

### 12. Progression
- First visit is a title and an empty search box. Pre-fill with the last exercise you trained, or show recent exercises below the search box.
- With exactly one session the page says "Not enough data" and shows nothing. Show the single point ("62.5 kg × 8, est. 1RM 79 kg") plus "log one more session to see the trend."
- The "FROM TODAY'S WORKOUT" shortcut chips only work when today has a workout.

### 13. Workout execution
- Finish summary: show time, sets, volume and best set per exercise, and a "View progression" link (`GR-ux-3`, C).
- "Last time: 60 kg × 8" is currently only a faint placeholder inside the input and "-" when empty. State it as text under the exercise name.
- No way to see upcoming exercises or jump; Previous and Next are the only controls. A small "1 of 3" tappable list would help.
- Add set / remove set is missing during a run (the landing mockup shows "Add set" and "End workout" buttons that don't exist in the real screen). Either add it or fix the landing mockup.

## Priority 4: polish and accessibility

- Landing page: the hero phone mockup shows "Add set" and "End workout" that the app doesn't have (see above). Content below the hero starts hidden until scrolled into view; it is fine for users, but a no-JS or print view shows blank bands.
- Registration is slow (the submit button sat on "Creating account…" for over 1.5 seconds). Add a progress state longer than the label change, and confirm the server-side hashing cost is intentional.
- Login empty-submit validation works well (inline errors under each field).
- Chat widget: tool/network errors after long waits give no retry button; add "Try again".
- Toasts are top-center and overlap the Back button region on small screens; with a tab bar move them above it.
- Accessibility: the icon-only buttons are labeled (good); keep that. Add visible focus rings on the card links, ensure the `role="status"` toast announces, check text contrast of `--text-3` (#7d879b) on `--surface` at 12–13px (borderline AA), and keep honoring reduced motion and reduced transparency (already in tokens).

## Suggested delivery order

1. Coach fix (backend plus frontend guard), end-workout confirm, 401 handling. Small, high value, no design approval needed beyond the confirm sheet.
2. Navigation (mockup 1) and state-aware dashboard (mockup 2).
3. Workouts and library changes (mockup 4), How to use trim.
4. Execution summary and "last time" text (mockup 3 C), then split and progression empty states.

## Mockups (need rafael's approval before any code)

| File | Shows |
|---|---|
| `mockups/GR-ux-1-navigation.png` | Desktop top nav, mobile bottom tab bar |
| `mockups/GR-ux-2-dashboard.png` | New account, workout today, rest day |
| `mockups/GR-ux-3-workout-execution.png` | Labeled End, confirm sheet, finish summary |
| `mockups/GR-ux-4-workouts-and-library.png` | Names-only workouts, autosave editor, library cards |

HTML sources sit next to each PNG; `build.mjs` regenerates them.
