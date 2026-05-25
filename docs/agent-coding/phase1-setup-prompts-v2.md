# Phase 1 - One-Time Setup Prompts (Project-Fit v2)

This file contains paste-ready prompts for **Giai Doan 1 - One-Time Setup** in
`docs/agent-coding/meta-coding-playbook.md`.

This version is optimized for the current SignalDesk backend repo:

- `.claude/` means the project-local folder only:
  `D:\Source Code\Source C#\signaldesk-backend\.claude`.
- Do not use or mutate global Claude config such as `~/.claude` or
  `C:\Users\user\.claude`.
- The repo already has `AGENTS.md`, `CLAUDE.md`, `.gitignore` with `.claude/`,
  and `.claude/settings.local.json`.
- The main missing project-local setup is expected to be:
  `.claude/commands/`, `.claude/agents/` with base + .NET/NestJS stack roles,
  `.claude/hooks/`, and optional `.claude/local/`.
- Optional tools are advisory. Do not install, start servers, wire MCP, or run
  external/cloud tooling unless the human owner explicitly approves that step.

## Template Quality Verdict

The original v2 template is more complete than the short prompt set because it
covers every playbook step with explicit gates and smoke tests. The main issues
were:

- it repeated the full preamble in every prompt, wasting context for a single
  long-running agent;
- some optional steps asked agents to install or run network tools immediately;
- `settings.local.json` creation did not account for the existing project file;
- the final gate referenced Day 1, while this repo is already around Day 4;
- hooks and commands were good, but needed stronger project-local/global-config
  wording and safer "patch if exists" behavior.

This file keeps the stronger step coverage from v2 and adds the missing safety
constraints from the shorter prompt set.

## Context Modes

Use one of these modes at the top of each prompt.

### Fresh Agent Mode

Use this when a new agent/thread starts.

```text
Project: SignalDesk Backend
Workspace root: D:\Source Code\Source C#\signaldesk-backend

Important:
- `.claude/` means only: D:\Source Code\Source C#\signaldesk-backend\.claude
- Do NOT read/write global Claude config such as ~/.claude or C:\Users\user\.claude
- Do NOT read or modify .env, secrets, keys, local machine config, branches,
  worktrees, staging, commits, pushes, or PRs
- Preserve existing dirty worktree changes. Do not revert user edits.
- Repo docs and current implementation are source of truth.
- agentmemory, GitNexus, Grapuco, graph output, and personal notes are advisory.

Read before acting:
- AGENTS.md
- CLAUDE.md
- docs/agent-coding/meta-coding-playbook.md
- docs/agent-coding/operating-model.md
- docs/agent-coding/tooling-policy.md
- docs/agent-coding/day-planning-prompt.md
- docs/handoff/day-agent-plan-template.md
- DOC/SIGNALDESK_AI_BE_v6_0.md
- DOC/SIGNALDESK_AI_BE_TIMELINE_2_4_WEEKS.md
- docs/api/error-envelope.md
- docs/handoff/gateway-route-map.md
- package.json
- nx.json
```

### Same Agent Continuation Mode

Use this if the same agent already completed the prior Phase 1 step in this
session.

```text
Use the context already loaded in this session.
Before acting, only re-read:
- the target file(s) for this step;
- any file changed by the previous step that this step depends on;
- git status --short.

Do not re-read the full architecture/timeline docs unless you see a conflict or
the target step asks for architecture-specific judgment.
All project-local/global-config and forbidden-action constraints still apply.
```

## Required Report Footer

Every prompt should end with:

```text
When done, report:
- Files changed
- Verification/discovery commands run
- Blockers
- Remaining risks
```

## Step 0 - Readiness Audit

Purpose: read-only audit before any setup changes.

```text
[Use Fresh Agent Mode]

Role:
You are the MANAGER agent. This is a READ-ONLY audit. Make zero changes.

Task:
Audit Phase 1 readiness for this repo and report each item as:
[EXISTS | MISSING | PARTIAL] - one-line summary.

Check:
1. AGENTS.md
   - Exists?
   - Contains project overview, source-of-truth docs, architecture rules,
     coding rules, daily workflow, verification commands, day guardrails,
     forbidden actions?
   - Report weak or missing items.

2. CLAUDE.md
   - Exists?
   - Contains no branch/worktree/subagent without approval, read Day plan,
     owned-files-only, report changed files/verification/risks, optional tool
     reporting, and project-local `.claude/` rule?

3. .gitignore
   - Contains `.claude/`?

4. Project-local .claude/
   - Exists at D:\Source Code\Source C#\signaldesk-backend\.claude?
   - List contents up to depth 3.
   - Confirm this is not global Claude config.
   - If `.claude/commands/` exists, check for plan, review-day-plan,
     implement-task, review-diff, and verify commands.
   - If `.claude/agents/` exists, check for manager, worker, dotnet-worker,
     nestjs-worker, reviewer, dotnet-reviewer, and nestjs-reviewer.
   - If `.claude/agents/` exists, check whether each agent file contains this
     exact scope line:
     "Task ownership and owned-files scope always come from docs/handoff/day-N-agent-task-plan.md. Do not infer scope from file patterns or service names."

5. Planning docs
   - docs/agent-coding/day-planning-prompt.md exists and is decision-complete?
   - docs/handoff/day-agent-plan-template.md exists?
   - docs/handoff/day-4-agent-task-plan.md exists?
   - docs/handoff/day-3-final-checklist.md exists?

6. Tooling policy and operating model
   - docs/agent-coding/tooling-policy.md exists?
   - docs/agent-coding/operating-model.md exists?
   - Summarize optional tool rules.

7. Current repo shape
   - List app/service folders.
   - List libs/contracts and building-blocks folders.
   - Run git status --short and call out existing dirty/untracked files.

Final verdict:
- Phase 1 steps already complete.
- Phase 1 steps still needed, ordered by priority.
- Minimum items required before continuing Day 4 implementation.
- Recommended but non-blocking items.

Constraints:
- Read-only.
- No file creation, edits, git operations, package installs, or network setup.
- If a file cannot be read, say so explicitly.

When done, report:
- Files changed: none
- Discovery commands run
- Blockers
- Remaining risks
```

## Step 1A - Review Rules Docs

Purpose: review `AGENTS.md` and `CLAUDE.md` together. This is more efficient
than separate reviewers because both files are small and strongly related.

```text
[Use Same Agent Continuation Mode if Step 0 ran in this same session; otherwise use Fresh Agent Mode]

Role:
You are a REVIEWER agent. This is READ-ONLY. Make zero changes.

Task:
Review AGENTS.md and CLAUDE.md against Phase 1 Step 1 in
docs/agent-coding/meta-coding-playbook.md.

AGENTS.md checklist:
- project overview
- source-of-truth docs
- architecture rules: gateway mechanical, tenant isolation, outbox, inbox,
  error envelope, no Kafka/Kubernetes/Qdrant during core unless asked
- coding rules and repo patterns
- daily agent workflow
- verification commands
- day scope guardrails
- forbidden actions: no branch/worktree, no stage/commit/push/PR, no nested
  agents, no package install, owned files only, no secrets

CLAUDE.md checklist:
- imports or references AGENTS.md
- project-local `.claude/` only; do not touch global Claude config
- no branch/worktree/subagent without explicit approval
- read current Day plan before implementation
- only edit owned files
- report changed files, verification commands/results, blockers, risks
- report optional tools and local artifacts

Output:
- Table with [READY | WEAK | MISSING] per item.
- Verdict:
  "Step 1 docs READY - no patch needed"
  OR
  "Step 1 docs need patch: [exact minimal additions]"

Constraints:
- Read-only.
- Do not edit AGENTS.md or CLAUDE.md.

When done, report:
- Files changed: none
- Discovery commands run
- Blockers
- Remaining risks
```

## Step 1B - Patch Rules Docs If Needed

Purpose: apply only the gaps found in Step 1A.

```text
[Use Same Agent Continuation Mode if Step 1A ran in this same session; otherwise use Fresh Agent Mode]

Role:
You are a WORKER agent.

Inputs:
Paste the exact Step 1A gaps here:
[PASTE GAPS HERE]

Owned files:
- AGENTS.md
- CLAUDE.md
- docs/agent-coding/tooling-policy.md only if Step 1A found a durable tooling
  policy gap that belongs there

Task:
Patch only the listed gaps.

Project-specific requirements:
- CLAUDE.md must explicitly say `.claude/` is project-local only and global
  Claude config must not be read or modified.
- AGENTS.md should remain the cross-agent project law.
- CLAUDE.md should stay concise and Claude-Code-specific.
- Do not duplicate the full architecture docs in CLAUDE.md.
- Do not invent new architecture rules unsupported by DOC/ or docs/.

Forbidden:
- Do not touch `.claude/`, apps/, libs/, infra/, .env, secrets, package files,
  branches, worktrees, staging, commits, pushes, or PRs.

Verify:
```powershell
rg -n "\.claude|global Claude|worktree|subagent|owned files|verification|optional tool|secrets" AGENTS.md CLAUDE.md docs/agent-coding/tooling-policy.md
git status --short
```

When done, report:
- Files changed
- Verification/discovery commands run
- Blockers
- Remaining risks
```

## Step 1C - Verify Rules Docs

```text
[Use Same Agent Continuation Mode if Step 1B ran in this same session; otherwise use Fresh Agent Mode]

Role:
You are the MANAGER agent. This is READ-ONLY.

Task:
Verify AGENTS.md and CLAUDE.md satisfy Phase 1 Step 1.

Output either:
- "Step 1 DONE - AGENTS.md and CLAUDE.md are ready"
- "Step 1 INCOMPLETE - remaining gaps: [exact list]"

Constraints:
- Read-only.

When done, report:
- Files changed: none
- Discovery commands run
- Blockers
- Remaining risks
```

## Step 2A - Verify `.gitignore` And Existing Settings

Purpose: this repo already has `.gitignore` and `.claude/settings.local.json`.
Review them before creating anything else.

```text
[Use Same Agent Continuation Mode if Step 1C ran in this same session; otherwise use Fresh Agent Mode]

Role:
You are a WORKER agent.

Owned files:
- .gitignore only if `.claude/` is missing
- .claude/settings.local.json only if it does not exist

Task:
1. Confirm .gitignore contains `.claude/`.
2. If `.claude/` is missing, add it under local tooling ignore rules.
3. Check .claude/settings.local.json.
4. If settings.local.json exists:
   - read and summarize it;
   - do not overwrite it;
   - flag unsafe broad permissions if present.
5. If settings.local.json is missing, create a minimal local-only file:
   {
     "notes": "Project-local Claude harness config. Do not commit. See .gitignore."
   }

Do not add broad permissions such as arbitrary git, package install, shell,
network install, or global config writes.

Verify:
```powershell
Select-String -Path .gitignore -Pattern "^\.claude/?$"
Get-Content -Raw .claude/settings.local.json
git status --short
```

When done, report:
- Files changed
- Verification/discovery commands run
- Blockers
- Remaining risks
```

## Step 2B - Create `.claude/commands/`

Purpose: create short project-local slash command prompts. These reference repo
docs instead of duplicating every rule.

```text
[Use Same Agent Continuation Mode if Step 2A ran in this same session; otherwise use Fresh Agent Mode]

Role:
You are a WORKER agent.

Owned files:
- .claude/commands/**

Task:
Create or patch these project-local files:
- .claude/commands/plan.md
- .claude/commands/review-day-plan.md
- .claude/commands/implement-task.md
- .claude/commands/review-diff.md
- .claude/commands/verify.md

If a file exists, patch it to align with this repo instead of skipping.
Keep each command short. Reference AGENTS.md, CLAUDE.md, operating-model.md,
tooling-policy.md, and the Day plan rather than copying their full text.

Required command behavior:

1. plan.md
   - Ask for Day number N.
   - Load docs/agent-coding/day-planning-prompt.md.
   - Create/update docs/handoff/day-N-agent-task-plan.md.
   - Do not implement code.
   - Stop after writing the plan.
   - If docs/handoff/day-{N-1}-final-checklist.md exists, read it for carry-over items before planning.

2. review-day-plan.md
   - Ask for N if unclear.
   - Review docs/handoff/day-N-agent-task-plan.md for task scope, owned files,
     forbidden files, parallel-safety, contracts, verification, test gaps,
     optional tooling, and failure protocols.
   - Output APPROVED or REVISION REQUIRED.
   - Parallel-safe tasks: verify owned file sets are strictly disjoint (no shared file across parallel tasks).

3. implement-task.md
   - Ask which Task N.X to implement.
   - Read owned files, forbidden files, acceptance criteria, verification, and
     failure protocol from the Day plan.
   - Edit only owned files.
   - No nested agents, package installs, branches, worktrees, commits, pushes,
     PRs, secrets, or global config.
   - Stop after 2 failed attempts.
   - After completing, always report:
      1. Files changed (each file + what changed)
      2. Verification commands run and results
      3. Remaining risks or blockers
   - On 2nd failure: STOP. Do not expand scope. Report root cause to manager and await instruction.

4. review-diff.md
   - Review the latest worker diff against Day plan ownership and project rules.
   - Check tenant isolation, gateway mechanical boundary, error envelope,
     outbox/inbox/idempotency, no direct RabbitMQ publish from .NET request
     handlers, no expansion infra, tests, docs/contracts.
   - Output DIFF APPROVED or DIFF REJECTED with focused feedback.

5. verify.md
   - Ask touched scope: node | dotnet | docker | all | custom.
   - Prefer Day plan verification commands.
   - Standard repo commands:
     npm run build:node
     npm run test:node
     powershell -ExecutionPolicy Bypass -File scripts/build-dotnet.ps1
     docker compose -f infra/docker/docker-compose.local.yml config
   - Report exact command, PASS/FAIL, output summary, and reason if skipped.

Verify:
```powershell
Get-ChildItem -Recurse -Force .claude/commands
rg -n "day-planning-prompt|owned files|forbidden|error-envelope|gateway-route-map|outbox|inbox|RabbitMQ|verify" .claude/commands
git status --short
```

When done, report:
- Files changed
- Verification/discovery commands run
- Blockers
- Remaining risks
```

## Step 2C - Create `.claude/agents/`

Purpose: create the base manager plus general and stack-specific worker/reviewer
role prompts for this mixed .NET/NestJS backend. These files define agent
behavior only; task ownership still comes from the Day plan.

```text
[Use Same Agent Continuation Mode if Step 2B ran in this same session; otherwise use Fresh Agent Mode]

Role:
You are a WORKER agent.

Owned files:
- .claude/agents/**

Task:
Create or patch:
- .claude/agents/manager.md
- .claude/agents/worker.md
- .claude/agents/dotnet-worker.md
- .claude/agents/nestjs-worker.md
- .claude/agents/reviewer.md
- .claude/agents/dotnet-reviewer.md
- .claude/agents/nestjs-reviewer.md

Do not duplicate large rule blocks across files. Reuse concise references to
AGENTS.md, CLAUDE.md, operating-model.md, tooling-policy.md, and the Day plan.
Do not omit required checks just to keep the files short.

AGENTS.md summary for these role prompts:
- SignalDesk is a mixed .NET/NestJS multi-tenant backend with gateway BFF,
  core business services, async events, search, notifications, AI/RAG, and
  campaign automation.
- Source-of-truth docs live in DOC/, docs/api/error-envelope.md,
  docs/handoff/gateway-route-map.md, package.json, nx.json, and relevant
  apps/libs/infra files.
- Core rules: gateway stays mechanical, tenant isolation is mandatory, .NET
  domain events use outbox, async consumers are idempotent, errors follow the
  error envelope, no secrets, no unapproved git/worktree/package/dependency
  actions, and no expansion infra unless Day scope explicitly includes it.
- Daily work must follow the Day plan with owned files, forbidden scope,
  acceptance criteria, verification commands, and final checklist.

Base worker/reviewer are for small, docs, contracts, or cross-cutting tasks.
Stack-specific workers/reviewers are used only when the Day plan assigns a
matching service stack.
No agent file may let the agent self-select scope. Task-specific ownership
must always come from `docs/handoff/day-N-agent-task-plan.md`.
Each agent file must contain this exact scope line:
"Task ownership and owned-files scope always come from docs/handoff/day-N-agent-task-plan.md. Do not infer scope from file patterns or service names."

manager.md must cover:
- review and approve Day plans before workers run;
- review worker outputs after each task;
- enforce owned files and conflict control;
- decide revision vs stop after max attempts;
- run/delegate verification;
- create day-N-final-checklist.md after implementation and verification.

SignalDesk-specific manager checks:
- Gateway tasks must stay mechanical: auth propagation, tenant context, rate
  limit, proxy, health, WebSocket entry only.
- .NET business services must publish domain events through outbox, never
  directly from request handlers.
- Async consumers must include idempotency/inbox/dedup strategy.
- Tenant isolation must be explicit in every data/cache/search/AI path.
- Do not allow Kafka, Kubernetes, Qdrant, or expansion infra unless the Day plan
  explicitly includes it.

worker.md must cover:
- read AGENTS.md, CLAUDE.md, operating-model.md, and assigned Day plan task;
- edit only owned files;
- do not create branches/worktrees/commits/pushes/PRs;
- do not spawn nested agents;
- do not install packages without manager approval;
- stop after 2 failed attempts;
- report files changed, tests changed, commands run, blockers, risks.

SignalDesk-specific worker rules:
- For gateway-bff changes, do not add business logic.
- For .NET service changes, keep Clean Architecture boundaries and use outbox
  for domain events.
- For NestJS consumers, include dedup/idempotency where the task touches async
  processing.
- For client-facing responses, follow docs/api/error-envelope.md.
- For gateway routes, follow docs/handoff/gateway-route-map.md.

dotnet-worker.md must cover:
- use for identity-service, workspace-service, support-service,
  knowledge-service, campaign-service, and libs/building-blocks/dotnet tasks;
- preserve Clean Architecture boundaries: Domain, Application, Infrastructure,
  API;
- keep tenant/correlation/current-user context explicit;
- use service-owned persistence/migrations when the Day plan allows schema work;
- use outbox for domain events and never publish directly from request handlers;
- preserve health endpoints and error-envelope behavior;
- verify with `powershell -ExecutionPolicy Bypass -File scripts/build-dotnet.ps1`
  plus focused tests when available.

nestjs-worker.md must cover:
- use for gateway-bff, notification-service, search-service, ai-service, and
  libs/building-blocks/nestjs tasks;
- keep NestJS module boundaries clear;
- for gateway-bff, keep behavior mechanical and route-map driven;
- preserve correlation ID and trusted tenant context propagation;
- use Redis/RabbitMQ helpers according to existing local patterns;
- include inbox/dedup/idempotency for async consumers touched by the task;
- verify with `npm run build:node` and `npm run test:node` or focused Nx
  commands from the Day plan.

reviewer.md must cover:
- review bugs, scope drift, forbidden files, secrets, tenant isolation, gateway
  boundary, error envelope, outbox/inbox/idempotency, direct RabbitMQ publish,
  expansion infra, tests, docs/contracts.
- output DIFF APPROVED or DIFF REJECTED.
- if only specific files are rejected, list those files explicitly.
- do not approve the whole diff if any file fails review.
- if the diff is too large for one reliable pass, output DIFF REJECTED with a
  request to split the review by file group or task slice.

Also check:
- tenant_id or trusted tenant context is required on business/data paths;
- Redis/cache keys are tenant-scoped when applicable;
- Elasticsearch/search/AI retrieval has mandatory tenant filter when applicable;
- identity refresh-token membership revoke behavior is preserved where relevant;
- no secrets/key material are committed.

dotnet-reviewer.md must cover:
- Clean Architecture boundary violations;
- EF Core mapping, migration, and transaction risks;
- tenant isolation in repositories/queries;
- outbox write path and no direct RabbitMQ publish from request handlers;
- refresh-token/security behavior for identity changes;
- CancellationToken propagation on async request/job paths;
- async/await correctness, including no `.Result` or `.Wait()` blocking in
  request/job paths;
- .NET build/tests and high-risk behavior coverage.

nestjs-reviewer.md must cover:
- NestJS module/controller/provider boundary issues;
- circular module/provider dependency risks and suspicious `forwardRef` usage;
- gateway mechanical boundary, route map, auth propagation, rate limiting, and
  tenant mismatch behavior;
- error envelope filters/interceptors;
- Redis/RabbitMQ idempotency/dedup behavior;
- search/AI tenant filters when touched;
- Node build/Jest coverage for high-risk behavior.

Verify:
```powershell
Get-ChildItem -Recurse -Force .claude/agents
rg -n "Task ownership and owned-files scope always come from docs/handoff/day-N-agent-task-plan.md|AGENTS.md|CLAUDE.md|owned files|2 failed|tenant isolation|outbox|inbox|RabbitMQ|forbidden|final-checklist|Clean Architecture|NestJS|gateway-bff|Elasticsearch|refresh-token|CancellationToken|\\.Result|\\.Wait\\(\\)|circular|forwardRef" .claude/agents
git status --short
```

When done, report:
- Files changed
- Verification/discovery commands run
- Blockers
- Remaining risks
```

## Step 2D - Verify `.claude/` Baseline

```text
[Use Same Agent Continuation Mode if Step 2C ran in this same session; otherwise use Fresh Agent Mode]

Role:
You are the MANAGER agent. This is READ-ONLY.

Task:
Verify baseline exists:
- .gitignore contains `.claude/`
- .claude/settings.local.json exists
- .claude/commands/plan.md
- .claude/commands/review-day-plan.md
- .claude/commands/implement-task.md
- .claude/commands/review-diff.md
- .claude/commands/verify.md
- .claude/agents/manager.md
- .claude/agents/worker.md
- .claude/agents/dotnet-worker.md
- .claude/agents/nestjs-worker.md
- .claude/agents/reviewer.md
- .claude/agents/dotnet-reviewer.md
- .claude/agents/nestjs-reviewer.md

Also check:
- no command or agent prompt tells agents to use global Claude config;
- no command or agent prompt permits branch/worktree/commit/push/PR by default;
- command prompts reference repo docs instead of duplicating large rule blocks.
- agent prompts do not let agents self-select task scope;
- stack-specific agents still defer owned files and forbidden scope to the Day
  plan.
- every agent prompt contains the exact scope line:
  "Task ownership and owned-files scope always come from docs/handoff/day-N-agent-task-plan.md. Do not infer scope from file patterns or service names."

Output either:
- "Step 2 DONE - project-local .claude baseline is complete"
- "Step 2 INCOMPLETE - missing or unsafe: [exact list]"

When done, report:
- Files changed: none
- Discovery commands run
- Blockers
- Remaining risks
```

## Step 3 - Optional Further Specialized Agents

Purpose: add narrower domain agents only after the base + stack-specific
workflow has been used at least once or a Day plan clearly benefits from them.

```text
[Use Fresh Agent Mode unless this is a direct continuation of Step 2D]

Role:
You are a WORKER agent.

Owned files:
- .claude/agents/**

Task:
Do not create specialized agents immediately.
First ask the human owner which domains are approved.
Do not recreate dotnet-worker, nestjs-worker, dotnet-reviewer, or
nestjs-reviewer; those stack-level roles are created in Step 2C.

Candidate domains for this repo:
- dotnet-persistence-worker: .NET EF Core, DbContext, migrations, repositories
- gateway-worker: gateway-bff auth/proxy/rate-limit/health only
- contracts-docs-worker: libs/contracts, docs/api, docs/handoff
- security-reviewer: auth, JWT, tenant isolation, RBAC, secret-sensitive review
- ai-search-reviewer: search/AI tenant filters, retrieval safety, projection
  freshness

For each approved domain, create .claude/agents/[name].md:
- short identity;
- required reading;
- typical owned file patterns;
- forbidden scope;
- domain verification commands;
- same failure protocol as worker.md.

Rules:
- Keep files short.
- Do not make specialized agents self-assign scope.
- Task-specific details must remain in the Day plan.
- No overlap that would encourage two agents to own the same file.

When done, report:
- Files changed
- Verification/discovery commands run
- Blockers
- Remaining risks
```

## Step 4A - Create PowerShell Hooks

Purpose: local safety guards for forbidden files and scope drift.

```text
[Use Same Agent Continuation Mode if possible; otherwise use Fresh Agent Mode]

Role:
You are a WORKER agent.

Owned files:
- .claude/hooks/**
- .claude/hooks/README.md if useful

Task:
Create or patch:
- .claude/hooks/pre-task.ps1
- .claude/hooks/post-task.ps1
- .claude/hooks/guard-scope.ps1

Hook rules:
- WARN writes a clear warning and exits 0.
- BLOCK writes a clear warning and exits 1.
- Every warning/block says: "Worker must stop and report to manager."
- Hooks do not auto-edit, auto-format, or silently fix anything.
- Hooks do not replace manager review.

pre-task.ps1:
- Parameter: -TaskContext string.
- BLOCK unapproved git commit, git push, git branch, git checkout -b,
  git worktree add, git merge, git rebase.
- WARN forbidden file references: .env, .env.*, secrets/, *.pem, *.key.
- WARN package install references unless text contains "manager-approved".
- WARN expansion infra references: kafka, kubernetes, k8s, qdrant.

post-task.ps1:
- Parameter: -OwnedFiles comma-separated list.
- Use git diff --name-only and git diff --cached --name-only to list changes.
- WARN sensitive file changes.
- WARN changed files outside owned files.
- Exit 0 always; manager decides action.

guard-scope.ps1:
- Parameters:
  -TaskFile path to day-N-agent-task-plan.md
  -ChangedFile path
  -TaskId optional, e.g. "4.1"
- Parse owned files from the matching task section when possible.
- Support exact paths and simple glob patterns.
- BLOCK ChangedFile outside owned files.
- BLOCK .env*, secrets/*, *.pem, *.key unconditionally.
- BLOCK infra/postgres/init/* unless task file contains
  "db-migration-approved: true" or the Day plan explicitly allows that path.

Verify syntax/help:
```powershell
powershell -ExecutionPolicy Bypass -File .claude/hooks/pre-task.ps1 -Help
powershell -ExecutionPolicy Bypass -File .claude/hooks/post-task.ps1 -Help
powershell -ExecutionPolicy Bypass -File .claude/hooks/guard-scope.ps1 -Help
Get-ChildItem -Recurse -Force .claude/hooks
git status --short
```

When done, report:
- Files changed
- Verification/discovery commands run
- Blockers
- Remaining risks
```

## Step 4B - Smoke Test Hooks

```text
[Use Same Agent Continuation Mode if Step 4A ran in this same session; otherwise use Fresh Agent Mode]

Role:
You are the MANAGER agent running local hook smoke tests.

Task:
Run these tests and report exact command, output, exit code, pass/fail.
Do not modify source files.

Test 1:
powershell -ExecutionPolicy Bypass -File .claude\hooks\pre-task.ps1 -TaskContext "git commit -m 'fix auth'"
Expected: exit code 1 and git operation warning.

Test 2:
powershell -ExecutionPolicy Bypass -File .claude\hooks\pre-task.ps1 -TaskContext "edit .env to add key"
Expected: exit code 0 and forbidden file warning.

Test 3:
powershell -ExecutionPolicy Bypass -File .claude\hooks\post-task.ps1 -OwnedFiles "apps/gateway-bff/src/app/auth/**"
Expected: advisory warnings for changed files outside owned scope if the working
tree has unrelated changes; otherwise note inconclusive.

Test 4:
Create .claude\local\test-task-plan.md with:
## Task 4.1
Owned files:
- apps/identity-service/src/**

Then run:
powershell -ExecutionPolicy Bypass -File .claude\hooks\guard-scope.ps1 -TaskFile ".claude\local\test-task-plan.md" -ChangedFile "apps/gateway-bff/src/app/auth/auth.service.ts" -TaskId "4.1"

Expected: exit code 1 and SCOPE VIOLATION.
Delete .claude\local\test-task-plan.md after the test.

When done, report:
- Files changed: only temporary .claude/local/test-task-plan.md, deleted after test
- Verification/discovery commands run
- Blockers
- Remaining risks
```

## Step 5 - Optional agentmemory Wrapper

Purpose: create local wrapper only. Do not install or start anything in this
setup prompt.

```text
[Use Fresh Agent Mode unless this is a direct continuation]

Role:
You are a WORKER agent.

Owned files:
- .claude/local/agentmemory.ps1
- .claude/local/README.md if useful

Task:
Create a local wrapper script only. Do NOT run npx. Do NOT start the server.
Do NOT wire Codex MCP or Claude plugin. Do NOT install packages.

Script requirements:
- Commands: start, health, recall, remember-help.
- start prints the npx command the human can run, or runs it only if a
  -Run switch is explicitly provided.
- health calls http://localhost:3111/agentmemory/health.
- recall requires -Text and calls smart-search.
- remember-help prints instructions and no-secrets warning.
- Script clearly states memory is advisory and repo docs/current code win.

Critical rules:
- Do not save .env values, secrets, tokens, private keys, production data, or
  raw transcripts that may contain secrets.
- Do not use pip install agentmemory for rohitg00/agentmemory unless upstream
  officially changes.

Verify:
```powershell
powershell -ExecutionPolicy Bypass -File .claude/local/agentmemory.ps1 -Help
Get-Content -Raw .claude/local/agentmemory.ps1
git status --short
```

When done, report:
- Files changed
- Verification/discovery commands run
- Blockers
- Remaining risks
```

## Step 6 - Optional GitNexus Read-Only Proposal

Purpose: evaluate whether to use GitNexus. Do not install or run it by default.

```text
[Use Fresh Agent Mode unless this is a direct continuation]

Role:
You are a REVIEWER agent.

Task:
Do not install GitNexus.
Do not run network commands unless the human owner explicitly approves browsing
or setup.

Review docs/agent-coding/tooling-policy.md and meta-coding-playbook.md.
Produce a recommendation:
- when GitNexus is useful for this repo;
- what "read-only mode" must mean;
- what artifacts must stay local/ignored;
- how to handle conflicts between graph output and source files;
- whether any .gitignore additions are likely needed after a future install.

Optional write scope if the human owner asks for a saved note:
- .claude/local/gitnexus-readonly-notes.md

When done, report:
- Files changed
- Verification/discovery commands run
- Blockers
- Remaining risks
```

## Step 7 - Optional Grapuco Spike Proposal

Purpose: keep Grapuco outside daily implementation until evaluated separately.

```text
[Use Fresh Agent Mode unless this is a direct continuation]

Role:
You are a REVIEWER agent.

Task:
Do not install Grapuco.
Do not run Grapuco.
Do not upload repo data.
Do not browse unless the human owner explicitly approves research.

Produce a spike plan:
- questions to answer before adoption: cloud/local modes, data upload, offline
  mode, artifacts, auth requirements, privacy risks;
- smallest safe repo subset for a future test;
- expected report path: .claude/local/grapuco-spike-report.md;
- criteria for "adopt", "defer", or "do not adopt";
- docs/agent-coding/tooling-policy.md changes needed only if adopted.

Optional write scope if the human owner asks for a saved note:
- .claude/local/grapuco-spike-plan.md

When done, report:
- Files changed
- Verification/discovery commands run
- Blockers
- Remaining risks
```

## Prompt R - Final Phase 1 Readiness Check

Run this before continuing with the next backend Day implementation.

```text
[Use Fresh Agent Mode unless this is a direct continuation]

Role:
You are the MANAGER agent. This is a READ-ONLY readiness audit.

Task:
Report each item as [DONE | SKIPPED (reason) | TODO - what's missing].

Minimum required:
- AGENTS.md contains project overview, source-of-truth docs, architecture rules,
  coding rules, daily workflow, verification commands, day guardrails,
  forbidden actions.
- CLAUDE.md contains project-local `.claude/` rule, no branch/worktree/subagent
  without approval, read Day plan, owned-files-only, report changed
  files/verification/risks, optional tool reporting.
- docs/agent-coding/day-planning-prompt.md exists and can generate a
  decision-complete Day plan.
- docs/handoff/day-agent-plan-template.md exists and matches workflow.

Recommended:
- .gitignore contains `.claude/`.
- .claude/settings.local.json exists and has no unsafe broad permissions.
- .claude/commands/ has plan, review-day-plan, implement-task, review-diff,
  verify.
- .claude/agents/ has manager, worker, dotnet-worker, nestjs-worker, reviewer,
  dotnet-reviewer, nestjs-reviewer.
- Every .claude/agents/*.md file contains the exact Day-plan ownership line:
  "Task ownership and owned-files scope always come from docs/handoff/day-N-agent-task-plan.md. Do not infer scope from file patterns or service names."
- .claude/hooks/ scripts exist and smoke tests passed, or hooks are documented
  as optional/manual in the Day plan.
- agentmemory, if enabled, has no-secrets rule and does not override docs.
- GitNexus, if enabled, is read-only and artifacts are local-only/ignored.
- Grapuco remains outside mainline implementation unless a spike is approved.

Multi-agent readiness:
- Day plan declares owned files.
- Parallel tasks have disjoint write sets.
- Manager reviews each worker output before integration continues.

Final output:
- "READY TO CONTINUE DAY WORK - minimum requirements met"
  plus non-blocking TODO items
OR
- "NOT READY - must fix before continuing: [exact list]"

Constraints:
- Read-only.

When done, report:
- Files changed: none
- Discovery commands run
- Blockers
- Remaining risks
```

## Quick Reference - Recommended Order

| # | Prompt | Required? | Notes |
|---|--------|-----------|-------|
| 0 | Readiness Audit | Yes | Read-only |
| 1A | Review Rules Docs | Yes | Read-only |
| 1B | Patch Rules Docs | If gaps | AGENTS/CLAUDE/tooling-policy only |
| 1C | Verify Rules Docs | Yes | Read-only gate |
| 2A | Verify `.gitignore` and settings | Yes | Existing repo already has both |
| 2B | Create `.claude/commands/` | Recommended if using Claude Code | Project-local only |
| 2C | Create `.claude/agents/` | Recommended if using Claude Code | Base + .NET/NestJS stack roles |
| 2D | Verify `.claude/` baseline | Yes after 2B/2C | Read-only gate |
| 3 | Further specialized agents | Optional later | Ask domain first |
| 4A | Create hooks | Recommended | PowerShell scripts |
| 4B | Smoke test hooks | Recommended after 4A | Local-only |
| 5 | agentmemory wrapper | Optional | No install/start by default |
| 6 | GitNexus proposal | Optional | No install by default |
| 7 | Grapuco proposal | Optional | Separate spike |
| R | Final readiness | Yes | Read-only gate |

Minimum readiness to continue backend Day work:

```text
AGENTS.md + CLAUDE.md + day-planning-prompt.md + day-agent-plan-template.md
```

The `.claude/commands`, `.claude/agents`, and hooks make the workflow safer and
less repetitive, but they should remain project-local and uncommitted.
