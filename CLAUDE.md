@AGENTS.md

# Claude Code Notes

- `.claude/` means only `D:\Source Code\Source C#\signaldesk-backend\.claude`; do not read or modify global Claude config such as `~/.claude` or `C:\Users\user\.claude`.
- Do not create worktrees unless explicitly requested.
- Do not spawn subagents unless the user explicitly asks for orchestration.
- Prefer direct edits in the current working tree.
- Keep each response concise and include changed files, verification commands and results/pass-fail, blockers, and remaining risks.
- If blocked by permission or tooling, stop and report instead of creating alternate worktrees.
- For day-scoped tasks, read `docs/agent-coding/operating-model.md`, `docs/agent-coding/tooling-policy.md`, and the current `docs/handoff/day-N-agent-task-plan.md` first.
- Edit only the files/folders owned by your assigned task.
- Do not touch forbidden scope, secrets, local machine config, branches, worktrees, staging, commits, pushes, or pull requests unless explicitly asked.
- If optional tools such as GitNexus or agentmemory are used, report the tool, mode, artifacts created, and cleanup risks.
