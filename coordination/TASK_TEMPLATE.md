# Task packet: `<JOB-ID>` — `<title>`

## Control

- Status: `DRAFT`
- Worker: `<worker-id>`
- Runtime: `<Codex | AGY CLI | Kimi CLI | Cursor | Claude Code | Claude Desktop>`
- Required model: `<exact model or alias>`
- Required effort: `<level>`
- Green base commit: `<full commit>`
- Branch: `<branch>`
- Worktree: `<absolute path>`
- Canonical ledger: `D:\FrontMission-RIMG\MIGRATION_LEDGER.md`
- Canonical plan: `D:\FrontMission-RIMG\MIGRATION_PLAN.md`

Do not begin unless this task is `READY` or `ACTIVE` in the canonical ledger and the
recorded owner matches this packet.

## Objective

`<one bounded outcome>`

## Evidence and context to read

1. Read the canonical plan completely.
2. Read the canonical ledger completely.
3. Read only these additional files:
   - `<path>`

## Allowed write scope

- `<path or exact glob>`

## Prohibited write scope

- `MIGRATION_PLAN.md`
- `MIGRATION_LEDGER.md`
- Anything not explicitly listed under allowed write scope

## Required behavior preservation

- `<invariant>`

## Non-goals

- `<explicitly excluded work>`

## Required checks

1. `<command and expected result>`

## Stop conditions

Stop and return `BLOCKED` without expanding scope if:

- A required change falls outside allowed paths.
- The green base is not reproducible.
- The requested transformation requires a behavior change.
- Required checks remain red after two focused repair attempts.
- Another worker modified the same owned path.

## Deliverables

- One implementation commit on the assigned branch.
- No unrelated formatting or cleanup.
- A handoff using `coordination/HANDOFF_TEMPLATE.md`.
