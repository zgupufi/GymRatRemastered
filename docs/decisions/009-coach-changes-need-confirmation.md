# 009: The coach proposes risky changes and the user applies them

Status: accepted (PR #26, extended in PR #58)

## Context

A language model can misread a request, and deleting or overwriting a workout by mistake loses the user's setup.

## Decision

To change an exercise's sets, rep range or weight step, to replace a workout's exercise list, or to delete a workout, the coach tools only create a pending `CoachSuggestions` row. The user applies or dismisses it. Applying checks that the suggestion is still pending and that what it changes is as it was when proposed.

Creating a workout (with or without exercises) and renaming one happen at once, without confirmation.

## Consequences

- A suggestion expires after seven days, or when applying it finds that the workout changed.
- A reply can make at most three proposals.
- A new tool that deletes or overwrites a user's setup should create a suggestion instead of writing.
