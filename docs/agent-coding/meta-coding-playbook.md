# SignalDesk Meta-Coding Playbook

Tài liệu này mô tả thứ tự vận hành khi làm các Day tiếp theo của SignalDesk
backend với Codex, Claude Code, worker agents, reviewer agents, hooks, memory và
các công cụ code graph như GitNexus hoặc Grapuco.

File này không thay thế `AGENTS.md`, `CLAUDE.md`,
`docs/agent-coding/operating-model.md`, hoặc
`docs/agent-coding/tooling-policy.md`. Các file đó vẫn là nguồn luật chính của
project. File này chỉ là playbook thực hành: nên làm gì trước, làm gì sau, và
khi nào mới nên gọi agent implement.

## Nguyên Tắc Gốc

Không xem `day-N-agent-task-plan.md` là nút "bắt đầu implement".

Vòng lặp đúng là:

```text
One-time setup
-> Generate Day plan
-> Manager review và approve plan
-> Chọn tooling mode cho Day đó
-> Gọi worker hoặc multi-worker implement
-> Manager review output
-> Chạy verification
-> Tạo final checklist
-> Lưu carry-over memory
```

Hãy chia workflow thành 3 lớp:

| Lớp | Vai trò |
| --- | --- |
| Setup layer | `.claude`, project rules, hooks, memory, graph tools. Làm một lần rồi bảo trì dần. |
| Planning layer | Tạo `docs/handoff/day-N-agent-task-plan.md`. Lặp lại mỗi Day. |
| Execution layer | Gọi worker hoặc multi-agent implement các task đã được approve. Chỉ chạy sau khi plan được review. |

Repo docs và current implementation luôn thắng agent memory, graph output hoặc
personal notes.

## Vai Trò Của Từng Tool Và File

| Tool hoặc file | Vai trò đúng |
| --- | --- |
| `AGENTS.md` | Luật project-level cho mọi agent. |
| `CLAUDE.md` | Ghi chú vận hành riêng cho Claude Code trong repo. |
| `day-planning-prompt.md` | Prompt dùng để tạo Day plan. Chỉ planning, không implement. |
| `day-N-agent-task-plan.md` | Contract thực thi của một Day. Định nghĩa owned files, forbidden scope, acceptance criteria, worker prompts và verification. |
| Codex | Manager và reviewer mặc định. Có thể implement task nhỏ nếu scope rõ. |
| Claude Code | Optional implementation worker khi bạn explicit approve orchestration. |
| Subagents | Worker chuyên môn hóa. Nhận task prompt, không tự invent scope. |
| Hooks | Safety checks local để warn/block hành động rủi ro. Không auto-edit code. |
| agentmemory | Optional continuity memory. Recall/remember context, nhưng không override repo docs. |
| GitNexus | Optional read-only code graph và impact-analysis helper. |
| Grapuco | Optional spike riêng cho graph visualization hoặc architecture evaluation. Không nằm trong daily mainline mặc định. |

Canonical paths trong repo hiện tại:

| Artifact | Path |
| --- | --- |
| Day planning prompt | `docs/agent-coding/day-planning-prompt.md` |
| Day task plan output | `docs/handoff/day-N-agent-task-plan.md` |
| Day final checklist output | `docs/handoff/day-N-final-checklist.md` |

Không tạo thêm `docs/handoff/day-planning-prompt.md` trừ khi team quyết định
move file prompt sang `docs/handoff/` và cập nhật toàn bộ references trong
`README.md`, `.claude/commands`, playbook, và các docs liên quan. Với trạng thái
repo hiện tại, `docs/agent-coding/day-planning-prompt.md` là canonical path.

## Giai Đoạn 0 - Chốt Operating Model

Trước khi thêm tool, cần chốt cách nghĩ về agent work.

Luật nền:

- Codex là manager và reviewer mặc định.
- Claude Code hoặc Codex worker chỉ implement các task slice đã được approve.
- Worker chỉ sửa file thuộc owned scope của task.
- Worker không tạo branch, worktree, commit, push, PR hoặc nested agent nếu chưa
  được explicit approve.
- Multi-agent chỉ dùng khi Day plan có disjoint write sets.
- Optional tools chỉ dùng khi chúng giảm một rủi ro cụ thể.
- Rule bền vững của project phải được promote vào tracked repo docs.

Done khi:

- Vai trò từng agent đã rõ.
- Biết file nào là source of truth cho project rules.
- Biết memory và graph tools chỉ là advisory, không phải luật.

## Giai Đoạn 1 - One-Time Setup

Giai đoạn này làm một lần, sau đó chỉ bảo trì. Nên hoàn thành các bước này trước
khi phụ thuộc nhiều vào multi-agent workflow.

### Bước 1 - Chuẩn Hóa `AGENTS.md` Và `CLAUDE.md`

`AGENTS.md` nên có:

- project overview
- source-of-truth docs
- architecture rules
- coding rules
- daily agent workflow
- verification commands
- day scope guardrails
- forbidden actions

`CLAUDE.md` nên có:

- rule riêng cho Claude Code
- không tạo branch/worktree/subagent nếu chưa được approve
- đọc current Day plan trước khi implement
- chỉ sửa owned files
- report changed files, verification commands và remaining risks
- report optional tool usage và local artifacts

Vì sao bước này phải làm trước:

- Day planning prompt và worker prompts đều yêu cầu agent đọc các file này đầu
  tiên.
- Nếu hai file này yếu, mọi agent call sau đó sẽ kém tin cậy.

Done khi:

- Agent mới vào repo biết cần đọc gì trước.
- Forbidden actions rõ ràng.
- Daily workflow được document.

### Bước 2 - Cấu Hình `.claude/` Baseline

`.claude/` là local-only agent harness configuration. Mục tiêu là giảm việc paste
prompt lặp lại, không phải thay thế project docs.

Trước khi tạo hoặc mở rộng `.claude/`, kiểm tra `.gitignore` có dòng:

```gitignore
.claude/
```

Repo này hiện đã ignore `.claude/`. Nếu một repo khác chưa có dòng này, thêm
vào trước khi tạo `settings.local.json`, hooks, agents, hoặc local worktree state
để tránh commit nhầm local agent config.

Cấu trúc target nên có:

```text
.claude/
  settings.local.json
  commands/
    plan.md
    review-day-plan.md
    implement-task.md
    review-diff.md
    verify.md
  agents/
    manager.md
    worker.md
    reviewer.md
  hooks/
    pre-task.ps1
    post-task.ps1
    guard-scope.ps1
```

Vai trò gợi ý cho slash commands:

| Command | Mục đích |
| --- | --- |
| `/plan` | Load `docs/agent-coding/day-planning-prompt.md`, điền Day number, tạo hoặc cập nhật `docs/handoff/day-N-agent-task-plan.md`, rồi dừng. |
| `/review-day-plan` | Đọc Day plan và kiểm tra task breakdown, owned files, forbidden scope, parallel safety, contracts và verification. |
| `/implement-task` | Chạy một worker prompt đã được approve từ Day plan. |
| `/review-diff` | Review worker diff theo architecture rules, scope, tests, tenant isolation, error envelope và outbox/inbox rules. |
| `/verify` | Chạy verification commands được định nghĩa trong task plan. |

Vì sao bước này làm sau `AGENTS.md` và `CLAUDE.md`:

- `.claude/` nên reference stable rules, không chứa toàn bộ luật.
- Commands và agent prompts có thể ngắn nếu project docs đã đủ tốt.

Done khi:

- Không còn phải paste cùng một system prompt dài nhiều lần.
- Manager, worker và reviewer flows dễ launch.
- `.claude/` vẫn local-only, đã được ignore, và không commit.

### Bước 3 - Thêm Subagent Prompts Một Cách Cẩn Thận

Ban đầu chỉ nên có 3 role gốc:

```text
.claude/agents/
  manager.md
  worker.md
  reviewer.md
```

Chỉ thêm specialized agents khi thật sự hữu ích:

```text
.claude/agents/
  dotnet-persistence-worker.md
  nestjs-worker.md
  gateway-worker.md
  contracts-docs-worker.md
  security-reviewer.md
```

Luật cho subagents:

- Không tự chọn scope.
- Không tự đọc timeline rồi invent task mới.
- Nhận `Task N.X` từ `day-N-agent-task-plan.md`.
- Stop sau 2 failed attempts và report root cause.
- Report files changed, tests changed, commands run, blockers và risks.

Vì sao bước này nên làm trước hooks:

- Agent roles cần rõ trước khi automated checks enforce behavior.

Done khi:

- Mỗi subagent có identity hẹp.
- Task-specific detail vẫn nằm trong Day plan.
- Không duplicate rule block quá lớn giữa các subagent prompts.

### Bước 4 - Cấu Hình Hooks

Hooks là optional, nhưng rất hữu ích khi bắt đầu dùng nhiều worker.

Với repo Windows này, nên ưu tiên PowerShell scripts:

```text
.claude/hooks/
  pre-task.ps1
  post-task.ps1
  guard-scope.ps1
```

Hook checks nên có:

- warn hoặc block edits vào `.env`, `.env.*`, `secrets/`, `*.pem`, `*.key`
- warn hoặc block edits ngoài owned files của worker
- warn hoặc block edits vào `infra/postgres/init/**` nếu Day không explicitly
  allow
- warn khi `git stage`, `git commit`, `git push`, branch creation hoặc worktree
  creation chưa được approve
- warn khi package install chưa được approve
- warn khi .NET request handlers publish trực tiếp lên RabbitMQ
- warn khi thêm Kafka, Kubernetes, Qdrant hoặc expansion infrastructure ngoài
  current Day scope

Hooks nên:

- fail rõ ràng
- report lý do
- yêu cầu worker stop và report

Hooks không nên:

- auto-edit source files
- auto-format phạm vi rộng
- âm thầm fix policy violations
- thay thế manager review

Vì sao hooks làm sau role setup:

- Hook chỉ enforce được workflow đã định nghĩa rõ.
- Hook sai có thể block việc đúng hoặc che mất nguyên nhân thật.

Done khi:

- Forbidden-file edits bị bắt.
- Out-of-scope edits nhìn thấy được.
- Manager vẫn là người quyết định.

### Bước 5 - Cấu Hình agentmemory

agentmemory là optional continuity memory. Nó hữu ích để carry lessons và context
qua nhiều session, nhưng không được trở thành project truth.

Setup tối thiểu theo hướng dẫn hiện tại của project agentmemory:

```powershell
# Terminal riêng: start memory server
npx -y @agentmemory/agentmemory

# Verify server health
curl http://localhost:3111/agentmemory/health

# Viewer local
# http://localhost:3113
```

Nếu chỉ cần MCP tools mà không cần REST API, viewer hoặc background jobs:

```powershell
npx -y @agentmemory/agentmemory mcp
# hoặc
npx -y @agentmemory/mcp
```

Với Codex CLI, có thể wire MCP bằng:

```powershell
codex mcp add agentmemory -- npx -y @agentmemory/mcp
```

Với Claude Code plugin flow, hướng dẫn hiện tại là:

```text
1. Chạy `npx -y @agentmemory/agentmemory` trong terminal riêng.
2. Trong Claude Code, chạy `/plugin marketplace add rohitg00/agentmemory`.
3. Chạy `/plugin install agentmemory`.
4. Verify bằng `curl http://localhost:3111/agentmemory/health`.
```

Không dùng `pip install agentmemory` cho `rohitg00/agentmemory` trừ khi upstream
chính thức đổi hướng dẫn. Bản hiện tại là Node/MCP-first, chạy qua `npx`.

Wrapper local tối giản có thể đặt trong `.claude/local/agentmemory.ps1` vì
`.claude/` đã được ignore:

```powershell
param(
  [Parameter(Mandatory = $true)]
  [ValidateSet("start", "health", "recall", "remember")]
  [string] $Command,

  [string] $Text
)

switch ($Command) {
  "start" {
    npx -y @agentmemory/agentmemory
  }
  "health" {
    curl http://localhost:3111/agentmemory/health
  }
  "recall" {
    if (-not $Text) { throw "Missing -Text for recall" }
    curl -X POST http://localhost:3111/agentmemory/smart-search `
      -H "Content-Type: application/json" `
      -d "{`"query`":`"$Text`"}"
  }
  "remember" {
    if (-not $Text) { throw "Missing -Text for remember" }
    Write-Host "Use the installed agentmemory MCP skill/tool to save: $Text"
    Write-Host "Do not save secrets, .env values, tokens, or private keys."
  }
}
```

Wrapper này chỉ standardize cách gọi local. Nó không biến memory thành source of
truth và không được commit nếu nằm trong `.claude/`.

Dùng agentmemory cho:

- prior Day carry-over
- architecture decisions lặp lại
- debugging lessons hữu ích
- personal workflow preferences

Không lưu:

- `.env` values
- secrets
- tokens
- private keys
- production data
- raw transcripts có thể chứa secrets

Cách dùng gợi ý:

```text
Before Day planning:
  recall "SignalDesk Day N-1 carry-over"

During planning:
  compare recalled context against repo docs and final checklist

End of Day:
  remember "Day N completed X. Verification Y. Carry-over Z. Important rule W."
```

Luật quan trọng:

```text
agentmemory recall != source of truth
repo docs + current implementation > memory
```

Nếu memory gợi ra một project rule bền vững, hãy promote rule đó vào tracked docs
trước khi kỳ vọng future agents làm theo.

Reference:

- https://github.com/rohitg00/agentmemory
- https://www.agent-memory.dev/

Done khi:

- Recall giúp bắt đầu một Day nhanh hơn.
- Memory không override `DOC/`, `docs/`, `AGENTS.md`, `CLAUDE.md` hoặc current
  code.
- Không lưu secrets.

### Bước 6 - Setup GitNexus Ở Read-Only Mode

GitNexus nên được dùng như optional impact-analysis helper, không phải daily
requirement.

Dùng GitNexus khi:

- Day đi qua nhiều service boundaries
- shared library change có blast radius chưa rõ
- worker cần biết file nào gọi class, route, function hoặc repository nào
- auth, gateway, outbox, event hoặc migration dependencies chưa rõ

Không cần dùng khi:

- task chỉ sửa docs
- task chỉ update test nhỏ
- owned files và dependencies đã rõ

Câu hỏi gợi ý:

```text
Which files call this service?
What depends on this repository?
What endpoints reach this handler?
What changes if this shared contract changes?
```

Luật:

- Dùng read-only mode.
- Không commit graph indexes hoặc generated tool state.
- Review output thủ công trước khi hành động.
- Nếu GitNexus output mâu thuẫn với source files, source files thắng.

Reference:

- https://yuv.ai/blog/gitnexus

Done khi:

- Tool trả lời được impact questions.
- Artifacts là local-only hoặc được ignore.
- GitNexus không bị xem là source of truth.

### Bước 7 - Đánh Giá Grapuco Bằng Một Spike Riêng

Grapuco không nên nằm trong default Day workflow cho đến khi được evaluate riêng.

Dùng một spike riêng cho:

- architecture graph visualization
- business/spec agent experiments
- giải thích service flows
- so sánh graph output với repo docs

Không dùng Grapuco trong daily implementation cho đến khi:

- data flow và upload behavior được hiểu rõ
- cloud mode và CLI mode được review
- local artifacts được xác định
- `docs/agent-coding/tooling-policy.md` được cập nhật nếu team adopt

Reference:

- https://grapuco.com/

Done khi:

- Có một spike report ngắn.
- Biết mode nào gửi dữ liệu nào đi đâu.
- Team quyết định có đưa vào workflow hay không.

## Giai Đoạn 1.5 - Readiness Check

Trước khi bắt đầu một Day mới, dùng checklist này:

- [ ] `AGENTS.md` có project overview, source-of-truth docs, architecture rules,
  coding rules, daily workflow, verification commands, day guardrails, và
  forbidden actions.
- [ ] `CLAUDE.md` có rule không tạo branch/worktree/subagent nếu chưa approve,
  không đụng forbidden scope, và phải report changed files/verification/risks.
- [ ] `docs/agent-coding/day-planning-prompt.md` tồn tại và có thể tạo
  decision-complete Day plan.
- [ ] `docs/handoff/day-agent-plan-template.md` tồn tại và khớp với workflow.
- [ ] `.gitignore` có `.claude/`.
- [ ] `.claude/commands/` có command cần thiết nếu team dùng Claude Code thường
  xuyên.
- [ ] `.claude/hooks/` chạy được nếu team đã bật hooks; nếu chưa bật, Day plan
  phải ghi hooks là optional/manual.
- [ ] agentmemory, nếu bật, có no-secrets rule và không override repo docs.
- [ ] GitNexus, nếu bật, chỉ chạy read-only và artifacts local-only hoặc ignored.
- [ ] Grapuco được giữ ngoài mainline implementation trừ khi đã có tooling spike
  approve.
- [ ] Multi-agent có conflict-control strategy: owned files disjoint, review sau
  từng worker, và plan rõ task nào serial/task nào parallel-safe.

Minimum readiness để tiếp tục:

```text
AGENTS.md + CLAUDE.md + day-planning-prompt.md là đủ để bắt đầu.
Hooks, memory, GitNexus và Grapuco giúp an toàn hơn nhưng không nhất thiết block progress.
```

## Giai Đoạn 2 - Daily Loop

Lặp lại vòng này cho mỗi backend Day.

### Bước 1 - Generate Day Plan

Input:

- `docs/agent-coding/day-planning-prompt.md`
- target Day number `N`
- previous Day final checklist
- source-of-truth docs
- current repo state

Output:

```text
docs/handoff/day-N-agent-task-plan.md
```

Planner phải:

- đọc required docs
- inspect repo
- xác định current implementation state
- detect gaps
- detect blockers
- identify contract changes
- define allowed và forbidden scope
- split tasks theo ownership boundary
- define verification commands
- viết worker prompts
- define tooling mode

Planner không được:

- implement code
- tạo branches hoặc worktrees
- stage, commit, push hoặc mở PR
- install packages
- mutate bất kỳ thứ gì ngoài Day plan file

Done khi Day plan trả lời được:

- Objective của Day là gì?
- File nào được sửa?
- File nào không được sửa?
- Contract nào thay đổi?
- Task nào block task nào?
- Task nào parallel-safe?
- Điều gì chứng minh Day đã xong?
- Worker phải làm gì nếu fail?

### Bước 2 - Manager Review Day Plan

Không đưa plan vừa generate trực tiếp cho implementation workers.

Codex manager nên review:

- task scope
- owned files
- forbidden files
- parallel-safe claims
- acceptance criteria
- verification commands
- contract changes
- missing tests
- hidden dependencies
- optional tooling needs

Manager nên output một trong hai kết luận:

```text
Approved for implementation
```

hoặc:

```text
Plan revision required before implementation
```

Câu hỏi review:

- Task nào có quá rộng không?
- Có 2 task nào sửa cùng files không?
- Worker có cần đụng forbidden scope để hoàn thành không?
- Acceptance criteria có observable không?
- Verification commands có thực tế với repo này không?
- GitNexus có hữu ích trước khi sửa code không?
- Day này có cần hooks không?
- Multi-agent có thật sự safe không?

Done khi:

- Execution order rõ ràng.
- Parallel groups rõ ràng.
- Plan edits cần thiết đã xong.

### Bước 3 - Chọn Tooling Mode Cho Day Này

Mỗi Day nên chọn một mode.

#### Mode A - Simple Day

Dùng khi scope hẹp và ownership rõ.

```text
Codex manager/reviewer
One worker
Serial implementation
No GitNexus
No Grapuco
Optional agentmemory recall
```

#### Mode B - Medium Day

Dùng khi có nhiều task nhưng dependencies còn quản lý được.

```text
Codex manager/reviewer
Claude Code hoặc Codex workers
Review after each task
Basic hooks
GitNexus read-only nếu dependency chưa rõ
Optional agentmemory recall and remember
```

#### Mode C - Large Or Cross-Service Day

Dùng khi tasks chạm nhiều services hoặc shared contracts.

```text
Codex manager/reviewer
Multiple workers
Disjoint owned files
Prefer separate worktrees for true parallel work nếu explicitly approved
GitNexus read-only before shared-code edits
Reviewer pass after each worker
Full integration verification
```

#### Mode D - Tooling Spike

Dùng khi evaluate tools, không implement product code.

```text
No product implementation
Evaluate GitNexus, Grapuco, hooks, hoặc memory setup
Record artifacts and cleanup risks
Update tooling policy only after review
```

Done khi:

- Day plan có declared tooling mode.
- Workers biết optional tools nào được phép dùng.
- Optional tool artifacts là local-only hoặc được ignore.

### Bước 4 - Worker Implementation

Chỉ sau khi manager approve thì worker mới bắt đầu implement.

Mỗi worker nhận:

- task name
- Day scope summary
- required reading
- owned files
- forbidden files
- implementation scope
- acceptance criteria
- verification commands
- failure protocol

Worker rules:

- đọc assigned docs trước
- chỉ sửa owned files
- không expand scope
- không chạy nested agents
- không tạo branches, worktrees, commits, pushes hoặc PRs
- không install packages nếu chưa có manager approval
- stop sau 2 failed attempts
- report files changed, tests changed, commands run, blockers và risks

Failure protocol chuẩn:

```text
1. Chạy lại verification commands liên quan để confirm lỗi.
2. Report root cause cụ thể: command nào fail, lỗi gì, file/scope nào liên quan.
3. Thử sửa lại tối đa 1 lần trong owned scope.
4. Nếu vẫn fail: STOP, report cho manager, không tự mở rộng scope để fix.
```

Diễn giải: worker có tổng cộng tối đa 2 attempts cho một lỗi quan trọng: attempt
ban đầu và 1 retry có kiểm soát. Sau đó manager quyết định tiếp.

Nếu dùng multi-agent:

- chỉ chạy parallel tasks khi owned files disjoint
- serialize tasks chạm cùng service hoặc shared library
- prefer separate worktrees cho true parallel editing, nhưng chỉ khi explicitly
  approved
- manager review từng output trước khi tiếp tục integration

Done khi:

- Worker output complete.
- Task-level verification đã chạy hoặc blocker được report.
- Changed files được liệt kê.

### Bước 5 - Manager Review Worker Output

Manager review sau mỗi worker trước khi cho overlapping work tiếp tục.

Review checklist:

- worker ở trong owned files
- không có forbidden files bị sửa
- không đụng secrets hoặc local machine config
- tenant isolation được giữ
- gateway vẫn mechanical
- error envelope đúng
- .NET domain events dùng outbox
- async consumers idempotent
- không direct RabbitMQ publish từ request handlers
- không thêm expansion infrastructure ngoài Day scope
- tests cover high-risk behavior
- docs và contracts được cập nhật khi cần

Nếu output fail review:

```text
1. Trả focused feedback cho worker.
2. Cho tối đa 2 revision attempts.
3. Nếu vẫn fail, stop và để manager/human owner quyết định.
4. Không cho worker patch vòng qua scope boundaries.
```

Done khi:

- Worker diff được accept.
- Rejected diffs được sửa hoặc bị stop.
- Integration có thể tiếp tục mà không có hidden scope drift.

### Bước 6 - Run Integration Verification

Chạy verification commands phù hợp với touched scope.

Common repo commands:

```powershell
npm run build:node
npm run test:node
powershell -ExecutionPolicy Bypass -File scripts/build-dotnet.ps1
docker compose -f infra/docker/docker-compose.local.yml config
```

Không phải Day nào cũng cần chạy mọi command, nhưng final checklist phải ghi:

- exact command đã chạy
- pass hoặc fail result
- lý do command bị skip
- exact blocker nếu command không chạy được

Done khi:

- Relevant verification pass, hoặc blockers được record rõ.
- Manager hiểu remaining risk.

### Bước 7 - Create End-Of-Day Checklist

Tạo hoặc cập nhật:

```text
docs/handoff/day-N-final-checklist.md
```

Chỉ tạo checklist này sau khi implementation và verification đã xong.

Checklist nên có:

- completed tasks
- changed files
- tests added hoặc modified
- verification commands và results
- contract/API/event/schema changes
- known gaps
- carry-over cho Day `N+1`
- optional tools used
- local artifacts và cleanup risks
- FE/API/event handoff notes khi relevant

Done khi:

- Final checklist có thể dùng làm input cho Day `N+1` planning.
- Carry-over rõ ràng.
- Không còn hidden assumptions chỉ nằm trong chat.

### Bước 8 - Save Memory Và Personal Notes

Sau khi final checklist đã viết xong, có thể save memory:

```text
remember "Day N completed X. Verification Y. Carry-over Z. Important rule W."
```

Dùng Obsidian hoặc personal notes cho learning notes.

Promote durable project rules vào tracked repo docs.

Done khi:

- agentmemory chứa context hữu ích.
- repo docs chứa durable rules.
- không lưu secrets.

## Giai Đoạn 3 - Khi Nào Dùng Multi-Agent

Chỉ dùng multi-agent khi tất cả điều kiện sau đúng:

- Day plan đã được approve
- mỗi task có owned file set rõ
- parallel tasks có disjoint write sets
- manager có thể review từng output
- verification có thể bắt integration regressions

Tránh multi-agent khi:

- domain hoặc schema design còn chưa ổn định
- tasks phụ thuộc mạnh vào cùng files
- tests đang fail vì nguyên nhân chưa rõ
- worker prompts còn mơ hồ
- Day plan chưa được review
- chưa có conflict-control strategy

Ví dụ split multi-agent tốt:

```text
Worker A: identity persistence
Worker B: gateway JWT verifier
Worker C: event contracts and docs
```

Ví dụ split multi-agent xấu:

```text
Worker A: modify AuthService
Worker B: modify AuthService
Worker C: modify tests that depend on AuthService shape
```

Shared working tree guidance:

- prefer serial edits cho overlapping service areas
- chỉ dùng parallel cho tasks rõ ràng disjoint
- stop nếu worker sửa ngoài ownership

True parallel guidance:

- chỉ dùng separate worktrees khi human owner explicitly approve
- merge hoặc integrate từng worker một
- chạy review và verification giữa các lần integration

## Giai Đoạn 4 - Thứ Tự Khuyến Nghị Cho Repo Này

Với SignalDesk backend workflow hiện tại, nên đi theo thứ tự này:

1. Giữ Grapuco ngoài daily mainline trước.
2. Không bắt đầu bằng aggressive multi-agent execution.
3. Thêm hoặc refine `.claude/commands` nếu dùng Claude Code thường xuyên.
4. Thêm local hooks cho forbidden files và out-of-scope edits.
5. Nếu dùng GitNexus, giữ read-only và ignore local graph artifacts.
6. Chạy Day tiếp theo với Codex manager và một worker serial.
7. Khi loop ổn định, mới cho phép 2 hoặc 3 workers với disjoint tasks.
8. Dùng agentmemory cho recall và end-of-Day carry-over.
9. Evaluate Grapuco trong tooling spike riêng nếu architecture visualization có
   giá trị.

## Standard Daily Workflow

Đây chỉ là quick reference. Canonical detail nằm ở **Giai Đoạn 2 - Daily Loop**;
nếu cần cập nhật workflow, cập nhật Giai Đoạn 2 trước rồi sync phần tóm tắt này.

```text
1. Generate docs/handoff/day-N-agent-task-plan.md
2. Manager review plan
3. Chọn tooling mode cho Day
4. Optionally run GitNexus read-only impact analysis
5. Run worker implementation for approved Task N.X
6. Manager review worker output
7. Repeat for serial tasks or approved parallel group
8. Manager review integrated diff
9. Run verification commands
10. Create docs/handoff/day-N-final-checklist.md
11. Save memory and personal notes
12. Use final checklist as input for Day N+1 planning
```

## Decision Table

| Tình huống | Hành động |
| --- | --- |
| Chưa có Day plan | Generate plan trước. Không implement. |
| Day plan có rồi nhưng chưa review | Manager review trước khi worker execute. |
| Owned files overlap | Chạy serial hoặc revise plan. |
| Worker cần forbidden files | Stop và escalate cho manager. |
| Cross-service impact chưa rõ | Dùng GitNexus read-only hoặc inspect thủ công trước khi edit. |
| Cần architecture visualization | Cân nhắc Grapuco spike, không đưa vào mainline Day execution. |
| Memory mâu thuẫn với repo docs | Repo docs thắng. Ghi discrepancy vào risks. |
| Tests fail sau 2 worker attempts | Stop và report root cause. |
| Tìm được durable rule hữu ích | Promote vào tracked docs. |

## Luật Cuối

Day plan là bản thiết kế. Manager approval là cổng kiểm soát. Worker execution là
phần implement. Verification và final checklist là bước đóng vòng.
