# Day Planning Prompt

Use this prompt when starting a new SignalDesk backend Day planning session.
Replace `{N}` with the target day number and `{N-1}` with the previous day
number before sending it to the manager agent.

````text
Tôi đã hoàn tất Day {N-1}. Chuẩn bị kế hoạch Day {N} theo agent-coding workflow cho SignalDesk backend.

Repository context:
- Workspace: SignalDesk backend
- OS/shell thường dùng: Windows + PowerShell
- Monorepo layout: apps/, libs/, infra/, docs/, DOC/
- Không implement code trong prompt này. Chỉ inspect, analyze, plan, rồi tạo/cập nhật file plan.

---

## PHASE 1 — READ (không skip, không reorder)

Đọc theo đúng thứ tự này. Không bắt đầu Phase 2 cho đến khi đọc xong tất cả.

1. `AGENTS.md`
2. `CLAUDE.md`
3. `docs/agent-coding/operating-model.md`
4. `docs/agent-coding/tooling-policy.md`
5. `docs/handoff/day-agent-plan-template.md`
6. `DOC/SIGNALDESK_AI_BE_v6_0.md`
7. `DOC/SIGNALDESK_AI_BE_TIMELINE_2_4_WEEKS.md`
8. `docs/api/error-envelope.md`
9. `docs/handoff/gateway-route-map.md`
10. `docs/handoff/day-{N-1}-final-checklist.md`
    - IF file không tồn tại → ghi nhận `prior_checklist: not found` và tiếp tục.
    - Không dừng.
    - Không hỏi.

---

## PHASE 2 — DISCOVER (non-mutating only)

Chạy các lệnh đọc/search non-mutating phù hợp với Windows/PowerShell. Ưu tiên `rg`. Không ghi, không sửa, không tạo file trong phase này.

Gợi ý lệnh cho repo này:

```powershell
# Repo tree cấp cao
Get-ChildItem -Force | Select-Object Name,Mode,Length

# Files chính, bỏ qua bin/obj/node_modules/.git
rg --files -g "!node_modules/**" -g "!**/bin/**" -g "!**/obj/**" -g "!.git/**" | Select-Object -First 200

# Services/apps/libs hiện có
Get-ChildItem -Path apps,libs,infra,docs,DOC -Force | Select-Object FullName,Mode

# Manifests/config
Get-Content -Path package.json -Raw
Get-Content -Path nx.json -Raw
Get-Content -Path global.json -Raw -ErrorAction SilentlyContinue

# Test/build scripts
Get-Content -Path scripts/build-dotnet.ps1 -Raw -ErrorAction SilentlyContinue
rg -n "test|build|lint|jest|xunit|dotnet test|nx" package.json nx.json apps libs scripts -g "!node_modules/**" -g "!**/bin/**" -g "!**/obj/**"

# Database/migrations/bootstrap
rg -n "migration|Migrations|CREATE TABLE|CREATE SCHEMA|DbContext|outbox|inbox" apps libs infra docs -g "!**/bin/**" -g "!**/obj/**"

# TODOs/stubs liên quan đến scope Day {N}
rg -n "TODO|FIXME|STUB|NOT_IMPLEMENTED|placeholder|not_checked" apps libs docs DOC -g "!node_modules/**" -g "!**/bin/**" -g "!**/obj/**"

# Test files hiện có
rg --files apps libs tests -g "*test*" -g "*spec*" -g "*.cs" -g "*.ts" -g "!**/bin/**" -g "!**/obj/**"

# Current git state
git status --short
```

Nếu lệnh nào không phù hợp hoặc file không tồn tại:
- Dùng lệnh equivalent phù hợp repo.
- Không dừng.
- Ghi nhận trong risks nếu missing file đó ảnh hưởng tới plan.

Sau khi discover xong, ghi nhận internally:

- Danh sách services/modules đã tồn tại.
- Test framework đang dùng.
- Build/verification commands thực tế.
- Migration strategy hiện có.
- TODOs/stubs trong scope Day {N}.
- Carry-over items từ `day-{N-1}-final-checklist.md` chưa done.
- Untracked/modified files hiện có để tránh đạp lên thay đổi của user.

---

## PHASE 3 — ANALYZE

Thực hiện 4 phân tích này theo thứ tự.

### 3.1 Scope Day {N}

Từ `DOC/SIGNALDESK_AI_BE_TIMELINE_2_4_WEEKS.md`, liệt kê từng deliverable được assign cho Day {N}.

### 3.2 Gap Detection

Gap = deliverable trong scope Day {N} mà:

- Chưa có file/module/test tương ứng trong repo, HOẶC
- Có nhưng implementation diverge khỏi spec trong `DOC/SIGNALDESK_AI_BE_v6_0.md`, HOẶC
- Có từ Day trước nhưng checklist ghi là known gap/carry-over.

### 3.3 Blocker + Dependency Detection

Blocker = thứ Day {N} cần mà chưa sẵn sàng, ví dụ:

- service skeleton chưa có
- contract chưa định nghĩa
- migration path chưa rõ
- package/tooling chưa có
- prior-day deliverable chưa xong
- local verification command có khả năng fail vì missing tool/dependency

Dependency = service/module/contract mà ít nhất một task Day {N} phải đọc hoặc ghi vào.

### 3.4 Conflict Detection

Tìm nơi implementation hiện tại mâu thuẫn với:

- `docs/api/error-envelope.md`
- `docs/handoff/gateway-route-map.md`
- `docs/handoff/service-port-map.md`
- `DOC/SIGNALDESK_AI_BE_v6_0.md`
- `AGENTS.md`
- current implementation

IF không tìm thấy bằng chứng trong repo/doc:
- ghi `"not found in repo — needs clarification"` trong risks.
- Không suy đoán file path, API contract, schema, hoặc behavior.

---

## PHASE 4 — PLAN

Xuất plan theo cấu trúc sau. Không bỏ mục nào.

---

### OBJECTIVE

Một đoạn tối đa 4 câu:
- Day {N} deliver gì
- Vì sao quan trọng cho timeline tổng thể
- Day {N} unblock gì cho Day {N+1}

---

### SUCCESS CRITERIA

Danh sách điều kiện cụ thể, verifiable bằng lệnh hoặc test.

Không dùng mô tả mơ hồ như:
- "hoạt động đúng"
- "được implement"
- "đầy đủ"

---

### ALLOWED SCOPE

Liệt kê files/folders được phép thay đổi trong Day {N}.

Yêu cầu:
- Càng cụ thể càng tốt.
- Nếu một folder rộng được phép, ghi rõ sub-scope bên trong.
- Tách rõ code, tests, docs, contracts, infra nếu có.

---

### FORBIDDEN SCOPE

Liệt kê files/folders không được chạm tới.

Bắt buộc ghi rõ:
- `.env`
- `secrets/`
- local machine config
- branch/worktree/git operations
- service hoặc module ngoài scope Day {N}
- infra/backlog tech không thuộc Day {N}

Ghi lý do kỹ thuật nếu có.

---

### CONTRACT CHANGES

Nếu Day {N} thay đổi bất kỳ mục nào dưới đây, liệt kê chi tiết:

- REST endpoint mới hoặc modified
- gRPC/proto contract
- Event/message schema
- Database schema/migration
- Error code mới theo `docs/api/error-envelope.md`
- Gateway route theo `docs/handoff/gateway-route-map.md`
- FE handoff contract

IF Day {N} có FE handoff, output contract phải bao gồm:
- endpoint paths
- request/response shape (JSON schema hoặc TypeScript interface)
- token lifetime và refresh strategy
- error codes và HTTP status mapping

IF không có thay đổi:
- ghi `"No public contract changes in Day {N}"`.

---

### TOOLING MODE

Tự đánh giá Day {N} có cần tool nào không. Không bật mặc định.

| Tool | Cách dùng trong Day {N} |
|------|--------------------------|
| Codex | Default manager/reviewer. Có thể làm worker nếu task nhỏ hoặc cần review chặt. |
| Claude Code | Worker implementation only nếu user yêu cầu orchestration. |
| Reviewer | Review diff sau mỗi task: tenant isolation, error envelope, outbox usage, scope drift, test gaps. Có thể là Codex hoặc Claude Code instance riêng. |
| agentmemory | Recall context từ ngày trước only. Không override repo/spec docs. Không lưu secrets. |
| Obsidian | Personal learning notes only. Không bắt buộc để implement. |
| GitNexus | Read-only impact/code graph only. Chỉ đề xuất nếu Day {N} có cross-service dependency, unknown dependency, hoặc refactor risk đủ lớn. |
| Grapuco | Spike/evaluation riêng only. Không đưa vào mainline Day {N}. |

Nếu đề xuất GitNexus hoặc Grapuco:
- nêu rõ lý do
- mode dùng
- artifact có thể tạo ra
- cách tránh commit artifact/tool state

---

### TASKS

Với mỗi task, dùng format sau. Không rút gọn bất kỳ trường nào.

#### Task {N}.X — [tên task]

| Field | Value |
|-------|-------|
| Agent type | [Codex manager / Claude Code worker / Codex worker / reviewer] |
| Owned files | [đường dẫn cụ thể] |
| Required reading | [files phải đọc trước khi bắt đầu task này] |
| Complexity | [S / M / L] |
| Parallel-safe | [yes / no] |

**Parallel-safe rule:** `yes` nghĩa là disjoint write set với tất cả task khác. Vẫn chạy serial trong cùng working tree trừ khi user duyệt worktree riêng cho worker.

**Implementation scope**
[Mô tả chính xác task phải làm gì, step-by-step nếu cần]

**Forbidden scope**
[Task này không được làm gì, không được chạm file nào]

**Acceptance criteria**
- [ ] [criterion 1]
- [ ] [criterion 2]

**Verification commands**
```powershell
# lệnh cụ thể
```

**Task contract note**
- Không viết thêm `Worker prompt` riêng bên trong task.
- Toàn bộ section của task này chính là contract để paste vào `<TASK_CONTRACT>` của Prompt 4 trong `docs/agent-coding/daily-loop-prompts.md`.
- Mỗi task section phải đủ decision-complete: agent type, owned files, required reading, implementation scope, forbidden scope, observable acceptance criteria, verification commands, dependency/blocker notes, và failure/stop expectations nếu có rủi ro đặc biệt.
- Không viết task chung chung như "implement feature" hoặc "fix tests"; mỗi worker phải biết chính xác nơi được sửa, nơi phải dừng, và cách chứng minh task đã xong.

---

### INTEGRATION PLAN

Liệt kê:

- Thứ tự integrate tasks.
- Task nào block task nào.
- Task nào parallel-safe.
- Manager review worker output như thế nào.
- Integration tests cần chạy sau khi tất cả tasks xong.
- Cách xử lý nếu worker chạm file ngoài owned scope.
- `Parallel-safe = yes` nghĩa là disjoint write set; vẫn chạy serial trong cùng working tree trừ khi user duyệt worktree riêng cho worker.
- Manager tạo `day-{N}-final-checklist.md` SAU khi tất cả task xong và verification pass — không tạo trước, không tạo song song với worker đang chạy.

---

### END-OF-DAY CHECKLIST

```text
- [ ] Task {N}.1 acceptance criteria passed
- [ ] Task {N}.2 acceptance criteria passed
      [thêm dòng cho mỗi task]
- [ ] Integration tests green
- [ ] No regression trên endpoints/behavior của Day {N-1}
- [ ] Contract changes documented
- [ ] docs/handoff/day-{N}-agent-task-plan.md created or updated
- [ ] Manager created docs/handoff/day-{N}-final-checklist.md AFTER all tasks finished and verification passed
- [ ] Changed files listed
- [ ] Verification commands and results recorded
- [ ] Known gaps before Day {N+1} recorded
```

---

### RISKS + DAY {N+1} HANDOFF

**Risks**
[Rủi ro kỹ thuật hoặc dependency có thể block Day {N}]

**Potential carry-over**
[Items từ Day {N} có thể chưa xong, cần xử lý ở Day {N+1}]

**Day {N+1} preconditions**
[Những gì Day {N} phải deliver để Day {N+1} không bị block]

---

### OBSIDIAN NOTES _(optional)_

IF trong quá trình planning phát hiện insight kỹ thuật đáng ghi nhớ:
- đề xuất 2-3 note titles
- mỗi note có key points ngắn gọn

ELSE:
- bỏ qua mục này hoàn toàn.

---

## PHASE 5 — OUTPUT

Tạo hoặc cập nhật file:

```text
docs/handoff/day-{N}-agent-task-plan.md
```

Theo template:

```text
docs/handoff/day-agent-plan-template.md
```

Thay `{N}` bằng số ngày thực tế trong tên file.

Sau khi tạo/cập nhật file xong:
- Tóm tắt ngắn file đã tạo.
- Liệt kê verification/discovery commands đã chạy.
- Dừng lại.
- Không implement code.

---

## HARD CONSTRAINTS — áp dụng suốt toàn bộ prompt này

```text
NEVER create branch / worktree / commit / stage / push / PR
NEVER modify .env, secrets, hoặc production config
NEVER implement code
NEVER run mutating commands ngoài việc tạo/cập nhật file plan ở Phase 5
NEVER hỏi những gì có thể tự discover từ repo hoặc docs
NEVER suy đoán file path hoặc API contract không có trong repo/doc
      → nếu không tìm thấy: ghi "not found — needs clarification" vào risks
NEVER nest agent CLI (Codex, Claude Code, opencode) from inside a worker task
ALWAYS agentmemory recall ≠ source of truth. Khi recall mâu thuẫn repo/spec, repo thắng và phải ghi discrepancy vào risks
ALWAYS repo docs + current implementation > Obsidian/agentmemory recall
ALWAYS plan phải decision-complete: worker nhận full task section qua Prompt 4 là chạy được ngay
ALWAYS each task section phải có: required reading, owned files, forbidden files, acceptance criteria observable, verification commands, dependency/blocker notes, và stop expectations nếu có rủi ro đặc biệt. Không viết chung chung.
```
````
