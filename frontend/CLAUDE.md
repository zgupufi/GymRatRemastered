# Frontend

## Design first
- Every UI change starts with a Figma mockup made with the Apple design skill (`.claude/skills/AppleDesignSkill.md`). Show it to the user and get it approved before writing any code.
- Behaviour and copy come from `docs/FRONTEND_SPEC.md` (the old app); the visual design is redone. GymRat Mode (strict mode, red theme) is dropped.
- Use the design tokens from the design system (colours, spacing, radii, motion); no hardcoded colours. Each page sets its own `--accent`.

## Coding conventions
- TypeScript strict mode on.
- Function components with hooks; no class components.
- API calls go through the typed client layer in `src/api`, never ad-hoc `fetch` in components.
- Co-locate a component's styles and tests with the component.

## Commands (run from `/frontend`)
- Install: `npm install`
- Dev server: `npm run dev`
- Tests: `npm test`
- Lint: `npm run lint`
- Build: `npm run build`

## Testing
- Vitest + React Testing Library for components and hooks that contain logic.

## Review and merge
- The user reviews only the design mockup, not the code.
- When the PR opens, and after every new push, a separate reviewer agent checks it in its own worktree:
  - `npm run lint`, `npm test` and `npm run build`
  - this file, the root `CLAUDE.md`, the design skill and `docs/FRONTEND_SPEC.md`
  - the running UI against the approved mockup
  - that the branch is rebased on `main`
- The reviewer posts its findings as a PR comment and never approves, merges or pushes fixes. The author applies them, and the reviewer looks again after each push.
- Once the reviewer has seen the latest commits and nothing more needs applying, the author squash-merges without asking the user again.
