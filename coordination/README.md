# External and multi-agent coordination

This directory holds physical task packets and handoff records for workers that do not
share the coordinator's conversational context.

Canonical control documents:

- `D:\FrontMission-RIMG\MIGRATION_PLAN.md` — durable process and safety gates
- `D:\FrontMission-RIMG\MIGRATION_LEDGER.md` — live status, ownership, commits, checks
- `D:\FrontMission-RIMG\coordination\tasks\` — one immutable assignment packet per job
- `D:\FrontMission-RIMG\coordination\handoffs\` — returned worker evidence
- `D:\FrontMission-RIMG\coordination\runs\` — CLI transcripts when a managed CLI runs

Only the coordinator edits the plan and ledger. A worker may edit its assigned product
files and its own handoff file, but no other coordination record.

## Worker lanes

### Coordinator-managed local workers

The coordinator can launch these without user relay:

- Codex subagents
- AGY CLI
- Kimi CLI
- Claude Code CLI

Cursor exposes a local `agent` entrypoint, but it may be used as coordinator-managed only
after its non-interactive invocation and requested model selection are verified.

### User-relayed UI workers

These require the user to open the assigned worktree and provide the physical task packet:

- Kimi Web UI, when the web interface is preferred over Kimi CLI
- Cursor IDE when a specific UI-only model such as Grok 4.6 is required
- Claude Desktop

The user does not need to explain or rewrite the task. Give the worker the exact task-file
path and instruct it to follow that file. When it finishes, report only the job ID and
commit hash, or the handoff-file path if it could not commit.

## Task lifecycle

1. Coordinator creates `coordination/tasks/<JOB-ID>-<slug>.md` from `TASK_TEMPLATE.md`.
2. Coordinator records the job, green base commit, worktree, branch, model, and exclusive
   write scope in `MIGRATION_LEDGER.md`.
3. Coordinator commits the task packet and ledger update before the worker starts.
4. Worker reads the plan, ledger, and task packet from physical disk.
5. Worker changes only its allowed paths.
6. Worker runs the packet's required checks.
7. Worker commits its implementation.
8. Worker writes `coordination/handoffs/<JOB-ID>.md` from `HANDOFF_TEMPLATE.md`, or returns
   the same fields in its final output when it cannot write the canonical checkout.
9. Coordinator reviews the diff, commit, checks, and handoff.
10. Coordinator integrates or rejects the commit and updates the ledger.

No worker starts from chat-only instructions.

## Manual UI handoff

For Cursor, Kimi Web UI, or Claude Desktop:

1. Open the exact worktree recorded in the task packet.
2. Attach or reference the task packet by absolute path.
3. Send: `Execute this task packet exactly. Read its required plan and ledger first. Do not expand scope.`
4. Do not give a second worker the same worktree.
5. When complete, tell the coordinator: `<JOB-ID> done, commit <hash>`.
6. If no commit was produced, tell the coordinator: `<JOB-ID> handoff at <absolute path>`.

The coordinator independently reviews and verifies all returned work.

## Managed CLI records

For AGY, Kimi, Claude Code, or a verified Cursor agent invocation:

- Launch from the assigned worktree.
- Use non-interactive mode.
- Pass the task packet as the complete job prompt.
- Avoid dangerous permission-bypass flags.
- Store stdout/stderr or structured output under `coordination/runs/<JOB-ID>/`.
- Record process/session identity in the ledger while active.
- Require a commit and structured handoff.

CLI availability does not authorize a job. The ledger must mark it `READY` and then
`ACTIVE` first.

## Conflict rule

Task packets are immutable after a worker starts. If scope must change, stop the worker,
record the reason in the ledger, create a revised task packet, and restart from a known-
green commit. Do not change requirements underneath a running worker.
