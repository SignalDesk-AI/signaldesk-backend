# Day N Agent Task Plan Template

Use this template to create `docs/handoff/day-N-agent-task-plan.md` before
implementation starts.

## Day Goal

State the day goal in one paragraph. Reference the exact section of
`DOC/SIGNALDESK_AI_BE_TIMELINE_2_4_WEEKS.md` and any relevant source-of-truth
architecture rules from `DOC/SIGNALDESK_AI_BE_v6_0.md`.

## Required Reading

- `AGENTS.md`
- `CLAUDE.md`
- `docs/agent-coding/operating-model.md`
- `docs/agent-coding/tooling-policy.md`
- `DOC/SIGNALDESK_AI_BE_v6_0.md`
- `DOC/SIGNALDESK_AI_BE_TIMELINE_2_4_WEEKS.md`
- `docs/api/error-envelope.md`
- `docs/handoff/gateway-route-map.md`
- previous day final checklist
- files in the owned implementation scope

## Allowed Scope

List files, folders, services, schemas, events, or docs that workers may change.
Keep the list concrete enough that workers know where to stop.

## Forbidden Scope

List service areas, infrastructure, secrets, business flows, or generated
artifacts that must not be touched on this day.

## Tooling Mode

State whether workers may use:

- Claude Code
- Codex workers
- GitNexus read-only analysis
- agentmemory local memory
- Grapuco spike/evaluation
- branches or worktrees
- hooks

Default: no branches/worktrees, no nested agents, no optional tools unless the
task contract explicitly allows them.

## Task Slices

### Task A - Name

Agent type: Codex CLI or Claude Code

Owned files/folders:

- `path/or/folder`

Must read:

- `path/or/doc`

Implementation:

- Concrete behavior to add or change.
- Public interfaces, schemas, or migrations affected.
- Error handling and tenant/correlation requirements.

Forbidden:

- Scope boundaries specific to this task.

Acceptance criteria:

- Observable behavior or tests that prove this task is done.

Verification:

```powershell
command goes here
```

Task contract note:

- Do not include a separate worker prompt inside the task.
- The full task section is the contract to paste into `<TASK_CONTRACT>` in
  Prompt 4 from `docs/agent-coding/daily-loop-prompts.md`.
- Keep each task section decision-complete: agent type, owned files, required
  reading, implementation scope, forbidden scope, acceptance criteria,
  verification commands, and any task-specific blocker/stop expectations.

## Integration And Review

State who integrates worker output and what the reviewer must check:

- architecture rule compliance
- tenant isolation
- outbox/inbox/idempotency rules
- API/error envelope
- tests and docs
- unexpected file changes

## Final Checklist Requirements

Create or update `docs/handoff/day-N-final-checklist.md` with:

- implementation checklist
- changed files
- verification commands and results
- known gaps before Day N+1
- FE/API/event handoff notes
- agent/tooling notes, including optional tools used
