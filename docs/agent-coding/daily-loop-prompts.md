# SignalDesk Daily Loop Prompts

Use these prompts after the one-time agent setup is complete. They are designed
for the Phase 2 Daily Loop in `docs/agent-coding/meta-coding-playbook.md`.

Placeholders:

- `{N}`: target Day number.
- `{N-1}`: previous Day number.
- `{TASK_ID}`: task id from the Day plan, for example `Task A` or `Task 4.1`.
- `{TASK_NAME}`: task name from the Day plan.

Recommended sequence:

1. Prompt 1: generate the Day plan.
2. Prompt 2: manager review the Day plan.
3. Prompt 3: choose tooling mode, multi-agent policy, and local agent assignment.
4. Prompt 4: launch one implementation task.
5. Prompt 5: review that task output.
6. Prompt 6: controlled revision, only when review requires changes.
7. Prompt 7: integration verification after accepted tasks.
8. Prompt 8: final checklist.

## Prompt 1 - Generate Day Plan

````text
Tôi đã hoàn tất Day {N-1}. Hãy chuẩn bị kế hoạch Day {N} theo agent-coding workflow cho SignalDesk backend.

Repository context:
- Workspace: SignalDesk backend
- OS/shell thường dùng: Windows + PowerShell
- Monorepo layout: apps/, libs/, infra/, docs/, DOC/
- Không implement code trong prompt này. Chỉ inspect, analyze, plan, rồi tạo/cập nhật file plan.

Read first, không skip:
- AGENTS.md
- CLAUDE.md
- docs/agent-coding/operating-model.md
- docs/agent-coding/tooling-policy.md
- docs/agent-coding/day-planning-prompt.md
- docs/handoff/day-agent-plan-template.md
- DOC/SIGNALDESK_AI_BE_v6_0.md
- DOC/SIGNALDESK_AI_BE_TIMELINE_2_4_WEEKS.md
- docs/api/error-envelope.md
- docs/handoff/gateway-route-map.md
- docs/handoff/day-{N-1}-final-checklist.md

If the previous day final checklist is missing:
- record `prior_checklist: not found`
- continue without asking

Discover current repo state using non-mutating commands only.
Prefer rg and targeted file reads. Do not write, edit, create branches, create worktrees, stage, commit, push, open PRs, install packages, or modify anything except the Day plan file in the final output step.

Inspect `.claude/agents/` before writing task slices:
- If `.claude/agents/` exists, inspect only agent filenames plus short
  role/title/description/task-specialty hints needed to choose a worker.
- Do not read unrelated `.claude` state, transcripts, memory DBs, or local
  secrets.
- Do not treat `.claude/agents/` as source of truth for product scope; repo docs
  and current implementation still win.
- If no matching local agent exists, use `Codex worker` as the recommended
  fallback and record why.

Analyze:
- Day {N} scope from DOC/SIGNALDESK_AI_BE_TIMELINE_2_4_WEEKS.md
- current implementation gaps
- blockers and dependencies
- available `.claude/agents` and which one best fits each task's owned scope
- conflicts between repo state, docs, API contracts, gateway route map, and architecture intent
- carry-over from Day {N-1}
- dirty/untracked files that workers must not revert

Create or update:
- docs/handoff/day-{N}-agent-task-plan.md

The plan must include:
- Objective
- Success criteria
- Allowed scope
- Forbidden scope
- Contract changes
- Tooling mode
- Task slices
- Recommended `.claude/agents` assignment for every task slice, including
  recommended agent, backup agent or fallback, source file, and short rationale
- Decision-complete task contracts
- Integration plan
- End-of-day checklist
- Risks and Day {N+1} handoff

Each task slice must include an agent assignment block:
- Recommended agent: exact `.claude/agents/<file>` name when available, or
  `Codex worker` fallback
- Backup agent: exact `.claude/agents/<file>` name when available, or fallback
- Agent rationale: why this agent fits the owned files and implementation risk
- Agent caveat: selected agent must obey the task contract, owned files,
  forbidden scope, and manager gates

Hard constraints:
- NEVER implement product code.
- NEVER create branch/worktree/commit/stage/push/PR.
- NEVER install packages or add package references.
- NEVER edit .env, secrets, credentials, or local machine config.
- NEVER ask questions that can be answered from repo/docs.
- ALWAYS make the plan decision-complete enough for a worker to run.

After writing the file:
- summarize the file created/updated
- list discovery commands run
- stop
````

## Prompt 2 - Manager Review Day Plan

````text
Review docs/handoff/day-{N}-agent-task-plan.md as SignalDesk manager/reviewer.

Context:
- Prompt 1 already generated the Day plan from source-of-truth docs.
- Do not re-read long source docs unless a concrete review question requires it.
- Use the current conversation context and the Day plan as primary inputs.

Do not:
- implement code
- launch workers
- choose local agents
- create branches/worktrees
- stage, commit, push, or open PRs
- install packages or add package references
- edit files except docs/handoff/day-{N}-agent-task-plan.md if revision is required

Review:
- Day objective matches the timeline scope for Day {N}
- success criteria are concrete and observable
- allowed scope is specific enough
- forbidden scope includes secrets, .env, git actions, package installs, out-of-day services, and expansion infra
- each task has clear owned files/folders
- each task proposes a recommended `.claude/agents` worker or explicit
  `Codex worker` fallback
- each task's agent recommendation includes a source file/name, backup/fallback,
  and rationale tied to the owned scope
- task dependencies and serial/parallel safety are explicit
- contract changes are documented: REST, event schema, DB schema, gateway route, FE handoff
- task sections are decision-complete contracts
- task sections are ready to paste into Prompt 4 `<TASK_CONTRACT>` without requiring a separate worker prompt
- verification commands are realistic for the touched scope
- tenant isolation is preserved
- gateway remains mechanical
- .NET domain events go through outbox
- async consumers remain idempotent
- no task requires forbidden scope to complete
- dirty/untracked repo files are acknowledged so workers do not revert unrelated changes

Output exactly:

## Plan Review Findings

List findings ordered by severity.

Use:
- [P0/P1/P2/P3] finding title
  Explanation:
  Required plan change:

If no findings, write:
No blocking findings.

## Scope Review

- Day scope correct: yes/no
- Allowed scope clear: yes/no
- Forbidden scope clear: yes/no
- Owned files clear: yes/no
- Recommended agent present for every task: yes/no
- Agent rationale and backup present: yes/no
- Task dependencies clear: yes/no
- Parallel-safety claims credible: yes/no
- Task contracts decision-complete: yes/no
- Full task contract pattern ready for Prompt 4: yes/no

## Contract Review

- REST/API changes documented: yes/no/not applicable
- Event schema changes documented: yes/no/not applicable
- DB/schema changes documented: yes/no/not applicable
- Gateway route changes documented: yes/no/not applicable
- FE handoff documented: yes/no/not applicable
- Error envelope impact documented: yes/no/not applicable

## Verification Review

- Commands are realistic: yes/no
- Commands cover touched Node scope: yes/no/not applicable
- Commands cover touched .NET scope: yes/no/not applicable
- Commands cover infra/config scope: yes/no/not applicable
- Missing verification:

## Verdict

Choose exactly one:
- Approved for implementation
- Plan revision required before implementation

Reason:
[brief reason]

If verdict is "Plan revision required before implementation":
- Patch only docs/handoff/day-{N}-agent-task-plan.md.
- Do not implement product code.
- Do not launch workers.
- After patching, stop and say: "Plan revised; rerun Prompt 2 before Prompt 3."

If verdict is "Approved for implementation":
- Do not patch files.
- Say: "Ready for Prompt 3."
````

## Prompt 3 - Tooling Mode, Multi-Agent Gate, And Local Agent Assignment

````text
Choose the execution/tooling mode for Day {N} before implementation starts.

Context:
- The Day {N} plan was already generated from the required source-of-truth docs.
- The manager review from Prompt 2 was already completed and returned "Approved for implementation" plus "Ready for Prompt 3."
- Do NOT re-read long source docs unless a required fact is missing or unclear.
- Use the current conversation context, the approved Day plan, and the Prompt 2 verdict as primary inputs.
- `.claude/` is local-only tooling. It may help choose an agent, but it does not override the approved Day plan, AGENTS.md, CLAUDE.md, repo docs, or current implementation.

Target plan:
- docs/handoff/day-{N}-agent-task-plan.md

Precondition:
- If Prompt 2 did not approve the plan, STOP and output:
  "BLOCKED: Day {N} plan must be approved before choosing execution mode."
- If Prompt 2 revised the plan and said to rerun Prompt 2, STOP and output:
  "BLOCKED: rerun Prompt 2 after plan revision before choosing execution mode."
- If approval status is unclear, treat it as not approved.
- If the plan is missing full task contracts or owned files for implementation tasks, return `BLOCKED` and explain what must be fixed in the Day plan.
- If any implementation task is missing a recommended agent, backup/fallback,
  source/name, or agent rationale, return `BLOCKED` and explain what must be
  fixed in the Day plan.

Allowed lightweight local-tool inspection:
- If `.claude/agents/` exists, inspect only:
  - agent filenames
  - short role/title/description sections
  - task-specialty hints
- Do NOT deeply read long agent files unless the role is ambiguous.
- Do NOT read unrelated `.claude` state, transcripts, memory DBs, or local secrets.
- If `.claude/agents/` is missing or unclear, fall back to generic labels:
  - Codex worker
  - Claude Code worker
  - manager/reviewer

Do not:
- implement code
- edit files
- launch workers
- mutate `.claude/` files or local tool state
- create branches/worktrees
- stage, commit, push, or open PRs
- install packages or add package references
- treat `.claude/` as source of truth
- expand task scope based on local agent capabilities

Goal:
- choose tooling mode
- choose execution verdict
- decide whether multi-agent is allowed
- decide whether parallel execution is allowed
- define exact task order
- validate or refine the Day plan's recommended local agent and backup agent for
  each task
- define optional tools allowed/forbidden
- define review gates
- define stop rules
- decide whether Prompt 4 can be launched

Tooling mode:
- Mode A: Simple Day, one worker, serial, no optional tools by default.
- Mode B: Medium Day, reviewed worker slices, usually serial.
- Mode C: Large/cross-service Day, multiple workers only with disjoint write sets and strong review gates.
- Mode D: Tooling spike only, no product implementation.

Execution verdict:
- SINGLE_WORKER_SERIAL: one worker handles all implementation tasks, one at a time.
- SERIAL_MULTI_WORKER: multiple worker task launches may be used, but only one worker runs at a time; manager reviews after each task.
- PARALLEL_ALLOWED: two or more workers may run at the same time because write sets are disjoint and dependencies are stable.
- BLOCKED: do not call implementation workers yet.

Apply Phase 3 - Multi-Agent Gate:
Answer every item explicitly with yes/no/unknown and a short reason:
- Day plan approved
- Each task has clear owned files
- Each task has recommended agent, backup/fallback, source/name, and rationale
- Forbidden scope is clear
- Task dependencies are clear
- Parallel candidates have disjoint write sets
- Shared contracts/interfaces are stable enough for parallel work
- Manager can review each worker output before integration
- Verification can catch integration regressions
- Dirty-file or ownership risk is clear
- Separate worktrees are required for true parallel work
- Human explicitly approved branches/worktrees if required
- Any task overlaps the same service/project/contract/shared files
- Any task needs package install, package reference change, branch, commit, staging, push, PR, or external tool approval

Apply Phase 4 - Repo Recommendation:
- Keep Grapuco out of daily mainline unless this is a separate tooling spike.
- Do not start with aggressive multi-agent execution.
- Prefer Codex manager/reviewer plus serial reviewed workers until the loop is stable.
- Use GitNexus only read-only if dependency impact is unclear.
- Use agentmemory only as optional no-secrets advisory memory.
- Use hooks only as warn/block safety checks; no auto-edit.
- Repo docs/current implementation beat memory, graph output, local agent descriptions, or personal notes.

Agent assignment rules:
- Start from the agent recommendations already written into the Day plan.
- Confirm each task is matched to the most specific available `.claude/agents`
  role for its owned files and risk profile.
- If Prompt 3 changes a Day-plan recommendation, explain why and keep the task
  contract's owned files and forbidden scope unchanged.
- If no specific role exists, use generic `Codex worker`.
- If a task is too broad for one local agent role, recommend splitting or running sub-parts serially.
- Do not assign two agents to run concurrently unless Execution Verdict is `PARALLEL_ALLOWED`.
- If Execution Verdict is `SERIAL_MULTI_WORKER`, multiple agents may be selected, but only one runs at a time.
- The selected agent must still obey the task owned files and forbidden scope from the Day plan.

Output exactly:

## Selected Tooling Mode

Mode: [A/B/C/D]
Reason:
[Explain briefly.]

## Execution Verdict

Verdict: [SINGLE_WORKER_SERIAL / SERIAL_MULTI_WORKER / PARALLEL_ALLOWED / BLOCKED]
Reason:
[Say clearly whether multi-agent is allowed and whether parallel execution is allowed.]

## Local Agents Detected

| Agent | Source | Role summary | Suitable for |
| --- | --- | --- | --- |
| [agent name or generic fallback] | [.claude/agents/file.md or fallback] | ... | ... |

If no `.claude/agents/` are available, output:
`No local .claude agents detected; using generic worker labels.`

## Phase 3 Gate Checklist

| Check | Result | Reason |
| --- | --- | --- |
| Day plan approved | yes/no/unknown | ... |
| Each task has clear owned files | yes/no/unknown | ... |
| Each task has recommended agent | yes/no/unknown | ... |
| Forbidden scope is clear | yes/no/unknown | ... |
| Task dependencies are clear | yes/no/unknown | ... |
| Parallel candidates have disjoint write sets | yes/no/unknown | ... |
| Shared contracts/interfaces are stable enough | yes/no/unknown | ... |
| Manager can review each worker output | yes/no/unknown | ... |
| Verification can catch regressions | yes/no/unknown | ... |
| Dirty-file ownership risk is clear | yes/no/unknown | ... |
| Separate worktrees required | yes/no/unknown | ... |
| Human approved worktrees if required | yes/no/unknown | ... |
| Overlapping service/project/contract files exist | yes/no/unknown | ... |
| Any dependency/package/git approval needed | yes/no/unknown | ... |

## Task Dependency Order

List the exact execution order:

1. Task {N}.X - [name]
   - Blocked by:
   - Blocks:
   - Worker type:
   - Recommended agent:
   - Backup agent:
   - Parallel-safe: yes/no/conditional
   - Reason:

## Agent Assignment Matrix

| Task | Recommended agent | Backup agent | Why this agent | Must run serial? | Notes |
| --- | --- | --- | --- | --- | --- |
| Task {N}.X | ... | ... | ... | yes/no | ... |

## Parallel Decision

Choose one:
- No parallel execution allowed.
- Parallel execution conditionally allowed only after [specific task/review].
- Parallel execution allowed for [specific task IDs] because [specific disjoint write sets].

If conditional, state the exact condition that must be true first.

## Recommended Execution Queue

Give the exact workflow:
1. Launch Prompt 4 for [first task] using [recommended agent].
2. Run Prompt 5 review for that task.
3. If review requires changes, run Prompt 6 revision.
4. Only after accepted, launch the next task using its recommended agent.
5. Continue until all implementation tasks are accepted.
6. Run integration verification.
7. Create final checklist.

## Worker Launch Policy

State:
- one worker repeatedly or different workers serially
- Codex worker allowed yes/no
- Claude Code worker allowed yes/no
- local `.claude/agents` allowed yes/no
- reviewer is manager-run or separate
- nested agents forbidden yes/no
- branches/worktrees forbidden yes/no
- commits/staging/push/PR forbidden yes/no
- package installs/package refs forbidden unless explicitly approved yes/no

## Optional Tools

| Tool | Decision | Mode | Notes |
| --- | --- | --- | --- |
| GitNexus | allowed/forbidden/conditional | read-only only | ... |
| agentmemory | allowed/forbidden/conditional | no-secrets advisory | ... |
| hooks | allowed/forbidden/conditional | warn/block only | ... |
| Grapuco | allowed/forbidden | spike only | ... |
| branches/worktrees | allowed/forbidden | human approval only | ... |
| package installs/package refs | allowed/forbidden | human approval only | ... |

## Review Gates

After each worker, manager/reviewer must check:
- owned files only
- no forbidden files
- tenant isolation
- gateway remains mechanical
- error envelope compliance
- .NET events through outbox
- async idempotency
- no expansion infra outside Day scope
- tests/docs/contracts updated where required
- verification result reviewed
- selected agent did not expand scope beyond the Day plan

## Stop Rules

Stop before launching the next worker if:
- worker edited outside owned files
- reviewer finds architecture violation
- tests fail after allowed retry attempts
- task needs forbidden scope
- task needs package install or package reference change
- task needs branch/worktree/commit/staging/push/PR
- contract/schema design becomes unclear
- implementation conflicts with source-of-truth intent
- recommended agent is missing and no backup is suitable
- task appears too broad for one worker and needs manager re-splitting

## Prompt 4 Readiness

Output one:
- READY_FOR_PROMPT_4: [first task ID] using [recommended agent]
- NOT_READY_FOR_PROMPT_4: [reason]
````

## Prompt 4 - Launch One Worker Task

Use this prompt by pasting the full task section from the Day plan into
`<TASK_CONTRACT>`.

````text
You are the implementation worker for Day {N} in the SignalDesk backend repo.

Precondition:
- Prompt 3 or the latest Prompt 5 review must have returned
  `READY_FOR_PROMPT_4: {TASK_ID}`.
- If neither Prompt 3 nor the latest Prompt 5 review marked this exact task
  ready, STOP and report to manager.

Assigned task:
- {TASK_ID} - {TASK_NAME}

Use the full task contract pasted below as the authoritative scope for this task.
Do not rely on a separate short worker prompt; the full pasted task section is the contract.

<TASK_CONTRACT>
Paste the entire task section here, starting from:

### {TASK_ID} - {TASK_NAME}

Include:
- Agent type
- Recommended agent
- Backup agent
- Agent rationale
- Owned files/folders
- Must read / Required reading
- Implementation
- Forbidden
- Acceptance criteria
- Verification
- Task contract note, if present
</TASK_CONTRACT>

Before editing:
0. Confirm that the pasted task contract contains recommended agent, backup
   agent or fallback, agent rationale, owned files/folders, forbidden scope,
   acceptance criteria, and verification commands. If it does not, STOP and
   report to manager.
1. Read:
   - AGENTS.md
   - CLAUDE.md
   - docs/handoff/day-{N}-agent-task-plan.md, but only the assigned task section and global guardrails unless more context is needed
   - every file listed under Must read / Required reading in the task contract
2. Restate:
   - recommended agent and backup/fallback
   - owned files/folders
   - forbidden scope
   - acceptance criteria
   - verification commands
3. If any of those are missing, ambiguous, or conflict with the pasted task contract, STOP and report the issue to manager.

Execution rules:
- Edit only the owned files/folders listed in the task contract.
- Do not touch forbidden scope.
- Do not expand scope based on assumptions.
- Do not create branches or worktrees.
- Do not stage, commit, push, or open PRs.
- Do not install packages or add package references.
- Do not run nested agent CLIs.
- Do not edit .env, secrets, credentials, or local machine config.
- Client-facing errors must follow docs/api/error-envelope.md.
- Gateway-facing routes must follow docs/handoff/gateway-route-map.md.
- .NET domain events must go through outbox, not direct RabbitMQ publishing.
- Async consumers must remain idempotent where relevant.
- Repo docs and current implementation beat memory, graph output, local notes, or assumptions.

Implementation:
- Follow the Implementation section of the pasted task contract exactly.
- Meet every Acceptance Criteria item in the pasted task contract.
- If the readiness marker's agent assignment conflicts with the pasted task
  contract, the pasted task contract wins and the worker must report the
  mismatch.
- If completing the task requires editing outside owned files, STOP and report to manager.

Verification:
- Run the Verification commands listed in the task contract if possible.
- If a command cannot run, report the exact command, error, and blocker.
- Do not substitute unrelated verification unless the manager approves.

Failure protocol:
- If blocked by dependency approval, missing tooling, sandbox/network limits, failing verification, or a need to edit outside owned files, report the exact blocker.
- Try at most one controlled fix inside owned scope.
- After 2 failed attempts total, STOP and report to manager.
- Do not make alternate edits outside owned files.

When done, report:
- Files changed
- Tests added or modified
- Verification commands run and result
- Blockers found
- Remaining risks
````

## Prompt 5 - Review Worker Output

Use this after a worker finishes one task. Paste the same full task contract used
for Prompt 4 and the worker final report.

````text
You are the reviewer for Day {N} {TASK_ID} - {TASK_NAME} in the SignalDesk backend repo.

Do not edit files, except for the accepted-task readiness update explicitly
allowed below in `docs/handoff/day-{N}-agent-task-plan.md`.
Do not implement fixes.
Do not stage, commit, push, or open PRs.
Do not create branches or worktrees.
Do not install packages or add package references.
Use review mode only.

Use the full task contract below as the authoritative scope:

<TASK_CONTRACT>
Paste the same full task section used for Prompt 4 here.
</TASK_CONTRACT>

Worker report:
<WORKER_REPORT>
Paste the worker's final report here:
- Files changed:
- Tests added or modified:
- Verification commands run and result:
- Blockers:
- Remaining risks:
</WORKER_REPORT>

Review inputs:
- Inspect current git status and the current diff for the files changed by this task using non-mutating commands only.
- Read only the assigned task section, affected files, Day plan task order /
  dependency order, and directly relevant docs if needed.
- Do not re-read long source docs unless a concrete review question requires it.
- If the worker report is incomplete, inspect the diff anyway and mark missing report fields as `unknown`.

Review checklist:
- Worker edited only owned files/folders from the task contract.
- No forbidden files or forbidden scope were touched.
- No .env, secrets, credentials, local config, branches, worktrees, commits, staging, pushes, or PRs.
- No packages or package references were added without approval.
- Tenant isolation is preserved.
- Gateway remains mechanical if gateway files are involved.
- Client-facing errors follow docs/api/error-envelope.md.
- Gateway routes follow docs/handoff/gateway-route-map.md.
- .NET domain events go through outbox, not direct RabbitMQ publish.
- Async/idempotency rules are preserved where relevant.
- Acceptance criteria from the task contract are met.
- Verification commands are appropriate and results are credible.
- Tests/docs/contracts were updated where required.

Output exactly:

## Findings

List findings first, ordered by severity.
Use tight file/line references wherever possible.

Use this format:
- [P0/P1/P2/P3] file/path:line - finding title
  Explanation:
  Required fix:

If no findings, write:
No blocking findings.

## Scope Review

- Owned files only: yes/no
- Forbidden scope touched: yes/no
- Dependency/package/git action detected: yes/no
- Notes:

## Acceptance Criteria Review

For each acceptance criterion from the task contract:
- [pass/fail/unknown] criterion text - reason

## Verification Review

| Command | Worker result | Reviewer assessment | Notes |
| --- | --- | --- | --- |
| ... | ... | credible/not credible/unknown | ... |

## Verdict

Choose one:
- ACCEPTED
- REVISION_REQUIRED
- BLOCKED_MANAGER_DECISION

Reason:
[brief reason]

## Revision Instructions

If REVISION_REQUIRED, give focused instructions limited to the task owned files.
If BLOCKED_MANAGER_DECISION, state exactly what manager/human must decide.

## Next Prompt 4 Readiness

If Verdict is ACCEPTED and another implementation task is unblocked by this
accepted task:
1. Update `docs/handoff/day-{N}-agent-task-plan.md` before responding:
   - Mark the reviewed task as accepted.
   - Add or update the readiness marker in the next task section:
     `READY_FOR_PROMPT_4: [next task ID]`
   - Use the exact next task ID from the Day plan.
   - Do not add a readiness marker for the current task.
   - Do not mark a task ready if it is missing, ambiguous, already completed, or
     blocked by unresolved dependencies; return `BLOCKED_MANAGER_DECISION`
     instead.
2. Then output exactly:
- READY_FOR_PROMPT_4: [next task ID] using [recommended agent]

If Verdict is ACCEPTED and no implementation task remains before integration
verification:
1. Update `docs/handoff/day-{N}-agent-task-plan.md` before responding:
   - Mark the reviewed task as accepted.
   - Add or update the readiness marker for integration verification:
     `READY_FOR_PROMPT_7: all implementation tasks accepted`
2. Then output exactly:
- READY_FOR_PROMPT_7: all implementation tasks accepted

If Verdict is REVISION_REQUIRED or BLOCKED_MANAGER_DECISION, output exactly:
- NOT_READY_FOR_PROMPT_4: [reason]
````

## Prompt 6 - Controlled Revision

Use this only when Prompt 5 returns `REVISION_REQUIRED`.

````text
You are the worker revising Day {N} {TASK_ID} - {TASK_NAME}.

Precondition:
- Prompt 5 must have returned `REVISION_REQUIRED` for this task.
- If Prompt 5 returned `ACCEPTED`, STOP.
- If Prompt 5 returned `BLOCKED_MANAGER_DECISION`, STOP and wait for manager/human decision.

Use the full task contract below as the authoritative scope:

<TASK_CONTRACT>
Paste the same full task section used for Prompt 4 here.
</TASK_CONTRACT>

Reviewer findings:
<REVIEW_FINDINGS>
Paste Prompt 5 findings and revision instructions here.
</REVIEW_FINDINGS>

Rules:
- Address only the reviewer findings.
- Do not address unrelated cleanup or improvements.
- Edit only owned files/folders from the task contract.
- Do not expand scope.
- Do not touch forbidden files or forbidden scope.
- Do not create branch/worktree/commit/stage/push/PR.
- Do not install packages or add package references.
- Do not run nested agent CLIs.
- Do not edit .env, secrets, credentials, or local machine config.
- If a finding requires edits outside owned files, STOP and report to manager.

Before editing:
- Restate the specific findings you will fix.
- Restate the owned files you may edit.
- If anything is ambiguous, STOP and ask manager.

Verification:
- Run the task verification commands from the task contract after the revision.
- If verification fails, try at most one controlled fix inside owned scope.
- After 2 failed attempts total, STOP and report exact blocker.

When done, report:
- Findings addressed
- Files changed
- Tests added or modified
- Verification commands run and result
- Findings not addressed and why
- Remaining risks
````

## Prompt 7 - Integration Verification

Use this after all implementation tasks for the Day are accepted.

````text
You are running Day {N} integration verification for the SignalDesk backend repo.

Do not implement fixes.
Do not edit files.
Do not stage, commit, push, open PRs, create branches, or create worktrees.
Do not install packages or add package references.

Inputs:
- docs/handoff/day-{N}-agent-task-plan.md
- Accepted task list:
  - [paste accepted tasks here]
- Any worker/reviewer known risks:
  - [paste known risks here]

Goal:
Run the verification commands required by the Day {N} plan and record exact results.

Before running commands:
- Inspect git status with a non-mutating command.
- Confirm all implementation tasks are accepted. If any task is not accepted, STOP and output `NOT_READY_FOR_FINAL_CHECKLIST`.
- Identify the Day-level verification commands from the task plan.
- Do not re-read long source docs unless a verification command or blocker is unclear.

Recommended commands may include, depending on Day plan:
- npm run build:node
- npm run test:node
- powershell -ExecutionPolicy Bypass -File scripts/build-dotnet.ps1
- docker compose -f infra/docker/docker-compose.local.yml config

Run only commands relevant to the touched scope unless the Day plan requires all.
Do not skip a Day-plan-required command unless it is impossible to run; record the exact reason.

If a command fails:
- Do not start broad fixes.
- Re-run only if needed to confirm the failure.
- Record exact command, failure summary, likely owner/task, and whether it blocks Day completion.

Output exactly:

## Verification Summary

| Command | Result | Blocks Day {N}? | Notes |
| --- | --- | --- | --- |
| ... | pass/fail/skipped | yes/no | ... |

## Git Status Summary

Summarize current modified/untracked files relevant to Day {N}.
Do not stage anything.

## Failure Details

For each failed command:
- Exact command:
- Key error lines:
- Likely owner/task:
- Recommended next action:

If no failures, write:
No verification failures.

## Day {N} Completion Readiness

Choose one:
- READY_FOR_FINAL_CHECKLIST
- NOT_READY_FOR_FINAL_CHECKLIST

Reason:
[brief reason]
````

## Prompt 8 - Final Checklist

Use this only after Prompt 7 returns `READY_FOR_FINAL_CHECKLIST`, or after the
manager explicitly decides to close the Day with documented blockers.

````text
Create or update the Day {N} final checklist for SignalDesk backend.

Target file:
- docs/handoff/day-{N}-final-checklist.md

Precondition:
- Prompt 7 returned `READY_FOR_FINAL_CHECKLIST`, OR the manager explicitly decided to close Day {N} with documented blockers.
- If neither is true, STOP and output: "BLOCKED: Day {N} is not ready for final checklist."

Inputs:
- docs/handoff/day-{N}-agent-task-plan.md
- accepted task list
- worker reports
- reviewer verdicts
- Prompt 7 integration verification results
- known risks and carry-over items

Do not implement product code.
Do not edit app/lib/infra implementation files.
Do not edit any file except docs/handoff/day-{N}-final-checklist.md unless the manager explicitly asks.
Do not stage, commit, push, open PRs, create branches, or create worktrees.
Do not install packages or add package references.

The final checklist must include:
- Day goal summary
- implementation checklist
- completed tasks
- changed files
- tests added or modified
- verification commands and results
- API/contract/event/schema changes
- FE/API/event handoff notes when relevant
- known gaps before Day {N+1}
- carry-over for Day {N+1}
- optional tools used
- local artifacts and cleanup risks
- any commands skipped and why
- any blockers that remain

Do not claim verification passed unless it actually passed.
If verification failed but the manager decided to close the Day, record the failure clearly and mark the Day as closed with blockers.

When done, report:
- final checklist file path
- key completed items
- verification status
- carry-over for Day {N+1}
````
