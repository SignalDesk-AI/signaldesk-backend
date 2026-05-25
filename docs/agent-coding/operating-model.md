# Agent Coding Operating Model

This document defines how SignalDesk backend days should be planned and
implemented when using coding agents. It complements the architecture and
timeline docs; it does not replace them.

## Roles

- Human owner: chooses the day, scope, tool preferences, and whether branches,
  worktrees, or external tools are allowed.
- Manager agent: reads source-of-truth docs, inspects current repo state, creates
  the day task plan, assigns scoped work, reviews changes, and owns the final
  checklist.
- Worker agent: implements one bounded task from the day plan, edits only its
  owned files, runs task-level verification, and reports changed files, commands,
  and risks.
- Reviewer agent: performs a review pass focused on bugs, scope drift, tenant
  isolation, architecture rules, test gaps, and docs/handoff completeness.

Codex is the default manager/reviewer. Claude Code or Codex workers may be used
for implementation tasks when the user explicitly asks for agent orchestration.

## Daily Workflow

Every new backend day should follow this loop:

1. Read `AGENTS.md`, `CLAUDE.md`, this operating model, the tooling policy, the
   backend source-of-truth docs, and the previous day's final checklist.
2. Inspect the current repo state before asking questions. Use `rg`, targeted
   file reads, and existing build/test config to discover facts.
3. Create or update `docs/handoff/day-N-agent-task-plan.md` from
   `docs/handoff/day-agent-plan-template.md`.
4. Split work by ownership boundary, not by vague theme. Each worker must have a
   disjoint write set where practical.
5. Give each worker an explicit prompt with required reading, owned files,
   forbidden scope, acceptance criteria, and verification commands.
6. Implement in small slices. Prefer one worker at a time in a shared working
   tree unless the user has explicitly approved branches or worktrees.
7. Review the diff against source-of-truth docs and current implementation.
8. Run verification commands that match the touched scope.
9. Create or update `docs/handoff/day-N-final-checklist.md` with completed work,
   commands run, remaining risks, and handoff notes for the next day.

## Conflict Control

- Do not run nested CLI agents from inside an implementation task unless the user
  explicitly asks for orchestration.
- Do not create branches or worktrees unless the user explicitly requests them.
- If multiple workers are used in one working tree, serialize edits that touch the
  same project or file family.
- Workers must not revert or rewrite changes outside their owned scope.
- Generated code, formatters, migrations, and package changes must be called out
  before they are run.

## Manager Checklist

Before assigning work, the manager must lock:

- Day goal and success criteria.
- Allowed files/folders and forbidden scope.
- Public API, event contract, schema, or migration changes.
- Verification commands and expected handoff docs.
- Tooling mode: local-only, read-only analysis, or approved write automation.

Before finalizing a day, the manager must report:

- Changed files.
- Verification commands run and results.
- Remaining risks or follow-up work.
- Next-day blockers or docs that should be imported in the next session.
