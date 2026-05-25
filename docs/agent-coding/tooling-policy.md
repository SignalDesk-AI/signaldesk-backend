# Agent Coding Tooling Policy

This document defines which agent-support tools are allowed in the SignalDesk
backend workflow and how they should be used safely.

## Core Tools

- Codex: default manager, reviewer, and implementation agent.
- Claude Code: optional implementation worker when the user explicitly asks for
  agent orchestration.
- `rg`, targeted file reads, build scripts, and test commands: default repo
  discovery and verification tools.
- Existing docs under `DOC/`, `docs/`, `AGENTS.md`, and `CLAUDE.md`: source of
  truth for planning and task prompts.

## Optional Tools

- GitNexus: allowed as a read-only code graph or impact-analysis helper. Review
  generated output before keeping it. Do not treat generated rules as source of
  truth until they are manually reconciled with `AGENTS.md` and architecture docs.
- agentmemory: allowed as a local/private memory experiment for personal agent
  continuity. Do not store secrets, tokens, `.env` values, or private production
  data. Do not make it required for implementation.
- Obsidian: allowed as a personal learning and decision journal. Use it for
  durable human notes such as Day lessons, architecture decisions, interview
  prep, debugging stories, and personal skill tracking. Do not make Obsidian a
  required input for agents; promote stable, project-relevant notes into tracked
  repo docs when they should guide future sessions.
- Grapuco: use only after a separate spike/evaluation. Because it may send
  architecture metadata to a hosted service, it is not part of the default Day
  workflow.

## Memory And Notes Policy

Use repo docs, Obsidian, and agentmemory for different layers of memory:

- Repo docs are the project source of truth. Agents must prefer `DOC/`,
  `docs/`, `AGENTS.md`, and `CLAUDE.md` over personal notes or memory recalls.
- Obsidian is for human understanding and long-term personal learning. It is a
  good place to record why a decision was made, what was learned on a Day, and
  what to practice next.
- agentmemory is for agent continuity across sessions. It can help avoid
  re-explaining repo patterns, prior decisions, and workflow preferences, but it
  must remain optional and local/private by default.

Recommended Day-end flow:

1. Update the tracked `docs/handoff/day-N-final-checklist.md`.
2. Add personal lessons to Obsidian if useful.
3. Let agentmemory capture or recall context only if it is already enabled
   locally and does not include secrets.
4. Promote any durable rule from Obsidian or agentmemory into tracked repo docs
   before expecting future agents to follow it.

## Local-Only Artifacts

Do not commit local tool state or generated memory/graph artifacts:

- `.claude/`
- `.gitnexus/`
- local memory databases or vector stores
- agent transcripts containing secrets
- generated graph indexes
- secrets, keys, `.env`, `.env.*.local`

If a tool generates useful instructions, copy only the reviewed, stable guidance
into tracked docs under `docs/` or root agent instruction files.

## Hooks And Automation

Hooks may be used for warning or blocking high-risk actions:

- attempts to edit `.env`, `secrets/`, or local machine config
- attempts to edit `infra/postgres/init` for service-owned Day 4+ tables
- direct RabbitMQ publishing from .NET request handlers
- adding Kafka, Kubernetes, Qdrant, or other expansion infrastructure outside
  current scope
- broad format or codegen runs that touch unowned files

Hooks should not auto-edit source files. They should fail loudly and ask the
worker to report the blocker.

## Tool Escalation Rule

Use the smallest tool that answers the question:

1. Read source-of-truth docs and current files.
2. Use `rg` and local build/test output.
3. Use optional code graph or memory tools only when they materially reduce risk.
4. Ask the human owner before enabling tools that write outside the repo,
   install dependencies, create branches/worktrees, or persist memory.

## Reporting

Any worker that uses optional tooling must include it in the task report:

- tool name
- mode used: read-only, local memory, hook, code graph, or write automation
- files or artifacts created
- whether artifacts are tracked or local-only
- risks or cleanup needed
