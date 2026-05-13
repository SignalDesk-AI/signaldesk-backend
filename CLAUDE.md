@AGENTS.md

# Claude Code Notes

- Do not create worktrees unless explicitly requested.
- Do not spawn subagents for small scoped tasks.
- Prefer direct edits in the current working tree.
- Keep each response concise and include changed files, verification commands, and remaining risks.
- If blocked by permission or tooling, stop and report instead of creating alternate worktrees.
- Keep Day 3 work focused on shared building blocks, request pipeline, proxy/health skeletons, and docs. Do not implement business logic.
- For Day 3 tasks, read `docs/handoff/day-3-agent-task-plan.md` first and stay inside the assigned task scope.
