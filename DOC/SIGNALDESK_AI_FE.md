# SignalDesk AI — Frontend Source of Truth
## Multi-tenant Customer Engagement Platform — Frontend Architecture

> **File này là nguồn sự thật duy nhất cho phần Frontend của SignalDesk AI.**
> Dùng để onboard context mới khi chat window bị reset, hoặc khi bắt đầu task FE cụ thể.
> Phiên bản: `v1.0` — micro-frontend với Next.js + React, SSR/ISR/CSR rõ ràng per app
> File BE tương ứng: `SIGNALDESK_AI_BE.md`

---

## Table of Contents

1. [Executive Summary — FE](#1-executive-summary--fe)
2. [Micro-Frontend Architecture Overview](#2-micro-frontend-architecture-overview)
3. [App Boundaries & Rendering Strategy](#3-app-boundaries--rendering-strategy)
4. [Turborepo Monorepo Structure](#4-turborepo-monorepo-structure)
5. [Shared Packages Architecture](#5-shared-packages-architecture)
6. [App 1: Portal — Next.js SSR/ISR](#6-app-1-portal--nextjs-ssrisr)
7. [App 2: Workspace — Next.js SSR](#7-app-2-workspace--nextjs-ssr)
8. [App 3: Widget SDK — React CSR Embeddable](#8-app-3-widget-sdk--react-csr-embeddable)
9. [State Management Strategy](#9-state-management-strategy)
10. [Real-time Architecture (Socket.io)](#10-real-time-architecture-socketio)
11. [Auth & Permission Flow (FE)](#11-auth--permission-flow-fe)
12. [Component Architecture](#12-component-architecture)
13. [Performance Strategy](#13-performance-strategy)
14. [Testing Strategy](#14-testing-strategy)
15. [CI/CD Frontend](#15-cicd-frontend)
16. [Roadmap 10 Tuần — FE Tasks](#16-roadmap-10-tuần--fe-tasks)
17. [Definition of Done — FE](#17-definition-of-done--fe)
18. [CV Bullet Points & Interview Prep — FE](#18-cv-bullet-points--interview-prep--fe)
19. [Context Handoff](#19-context-handoff)

---

## 1. Executive Summary — FE

Frontend của SignalDesk AI là **3 ứng dụng độc lập** cùng tồn tại trong một Turborepo monorepo, chia sẻ code qua package nội bộ nhưng deploy và build riêng biệt:

| App | Framework | Rendering | Mục đích |
|-----|-----------|-----------|----------|
| `portal` | **Next.js 14** | ISR / SSG | Public help center, KB articles, SEO |
| `workspace` | **Next.js 14** | SSR | Agent + admin console, authenticated |
| `widget-sdk` | **React 18** (không Next.js) | CSR | Embeddable chat widget, nhúng vào site ngoài qua `<script>` |

Đây là mô hình **micro-frontend thực dụng** — không dùng Module Federation hay iframe phức tạp, mà dùng **package-based sharing** qua Turborepo: mỗi app là một unit deploy độc lập, shared UI và logic nằm trong `packages/`.

---

## 2. Micro-Frontend Architecture Overview

### 2.1 Tại Sao Đây Là Micro-Frontend (Và Tại Sao Không Dùng Module Federation)

```
┌─────────────────────────────────────────────────────────────┐
│                  signaldesk-fe (Turborepo)                  │
│                                                             │
│  apps/                                                      │
│  ├── portal/          Next.js         Deploy: VPS/Vercel   │
│  ├── workspace/       Next.js         Deploy: VPS/Vercel   │
│  └── widget-sdk/      React (Vite)    Deploy: CDN (JS file) │
│                                                             │
│  packages/   ← shared, không deploy riêng                  │
│  ├── ui/              React components (shadcn base)        │
│  ├── api-client/      Generated API client                  │
│  ├── types/           TypeScript types                      │
│  ├── auth/            Auth helpers + permission hooks       │
│  ├── realtime/        Socket.io hooks                       │
│  └── config/          ESLint, Tailwind, TS config           │
└─────────────────────────────────────────────────────────────┘
```

**Tại sao KHÔNG dùng Module Federation ngay từ đầu:**
- Module Federation thêm complexity về webpack config, versioning dependencies, và runtime loading — không phù hợp với solo dev trong 10 tuần
- Package-based sharing compile-time đủ mạnh: type-safe, tree-shaking tốt, không có runtime dependency hell
- Nếu sau này cần Module Federation (team lớn, deploy per feature independently), migration path rõ ràng vì boundaries đã được thiết lập

**Đặc điểm của micro-frontend thực dụng này:**
- Mỗi app có thể deploy độc lập mà không cần deploy app khác
- Mỗi app có thể có cadence release khác nhau (portal deploy ít hơn workspace)
- Shared packages được versioned; breaking change trong `packages/ui` phải bump version và cập nhật consumers
- `widget-sdk` là pure React — không phụ thuộc vào Next.js, có thể nhúng vào bất kỳ website nào (kể cả WordPress, Shopify)

### 2.2 Ranh Giới Giữa Các App

```
Customer Journey:
  1. Khách hàng vào https://help.taskflow.io  →  portal  (public, no auth)
  2. Khách hàng dùng chat widget trên taskflow.io  →  widget-sdk  (embeddable)
  3. Agent/Admin login tại https://app.taskflow.io  →  workspace  (auth required)

Không có giao tiếp trực tiếp giữa 3 apps tại runtime.
Giao tiếp duy nhất là qua:
  - Backend API (tất cả đều gọi cùng 1 gateway)
  - WebSocket (workspace + widget-sdk connect tới cùng 1 socket server)
  - Shared packages (compile-time, không runtime)
```

### 2.3 Next.js vs React — Khi Nào Dùng Cái Nào

| Tiêu chí | Next.js (portal, workspace) | React thuần (widget-sdk) |
|----------|----------------------------|--------------------------|
| SEO | ✅ SSR/ISR tốt | ❌ không cần |
| Auth-gated | ✅ server-side redirect | ❌ không cần |
| Bundle size | Lớn hơn (Next.js overhead) | Nhỏ hơn (critical cho embeddable) |
| Embeddable | ❌ không embed được | ✅ build ra 1 JS file |
| Routing | Next.js App Router | React Router v6 |
| Deploy | VPS / Vercel | CDN static file |

---

## 3. App Boundaries & Rendering Strategy

### 3.1 Portal — ISR cho Public Content

```
Mục đích: Public help center cho end-customer
URL: https://help.{tenant}.signaldesk.io hoặc https://help.taskflow.io

Rendering strategy per route:
  /                       → SSG (static, revalidate: false — landing page ít thay đổi)
  /help/[slug]            → ISR (revalidate: 3600, on-demand khi article published)
  /search                 → SSR (dynamic query params, không cache)
  /login                  → SSR (nếu portal có auth cho customer)

Tại sao ISR cho /help/[slug]:
  - KB articles thay đổi không thường xuyên (vài lần/ngày)
  - ISR cho phép page đã được built sẵn → latency thấp cho end-user (< 50ms TTFB)
  - On-demand revalidation: khi admin publish article → backend gọi
    Next.js revalidate API → page được rebuild ngay lập tức
  - Fallback: blocking (build on first request nếu chưa có)

SEO requirements:
  - <title> và <meta description> từ article metadata
  - Open Graph tags
  - Sitemap tự động (next-sitemap hoặc custom)
  - Structured data (FAQ schema, Article schema)
  - LCP < 2.5s, Lighthouse ≥ 90
```

### 3.2 Workspace — SSR cho Authenticated App

```
Mục đích: Agent + admin console, full lifecycle management
URL: https://app.{tenant}.signaldesk.io

Rendering strategy per route:
  /login                  → SSR (server-side auth check + redirect)
  /tickets                → SSR (server-side fetch với JWT, filter + pagination)
  /tickets/[id]           → SSR (server-side fetch, rồi hydrate với TanStack Query)
  /kb/*                   → SSR
  /analytics/*            → SSR với suspense (heavy data, stream progressively)
  /settings/*             → SSR

Tại sao SSR toàn bộ cho workspace:
  - Data luôn user-specific + tenant-specific → không cache tốt ở CDN
  - JWT cần thiết cho mọi request → server có thể đọc cookie httpOnly và fetch data
  - SEO không quan trọng (auth-gated) nhưng SSR vẫn tốt hơn CSR vì:
    → First paint có data sẵn (không flash empty state)
    → Redirect unauthorized ngay ở server (không để client flash rồi redirect)

Sau khi SSR hydrate, TanStack Query tiếp quản client-side:
  - Mutations: optimistic update + invalidate cache
  - Background refetch: staleTime: 300_000 (5 phút) cho ticket detail
  - Real-time: WebSocket override TanStack Query cache cho live updates
```

### 3.3 Widget SDK — CSR Embeddable

```
Mục đích: Embeddable chat widget cho customer
Embed: <script src="https://cdn.signaldesk.io/widget.js" data-tenant="taskflow"></script>

Build target: ES module + UMD bundle qua Vite
Output: dist/widget.js (gzip target < 80KB)
Rendering: CSR hoàn toàn (no server rendering)

Lifecycle:
  1. Script loader nhận data-tenant attribute
  2. Inject iframe hoặc shadow DOM vào page
  3. React app mount trong isolated container
  4. Connect tới SignalDesk WebSocket với tenantId
  5. Chat session tạo trong MongoDB (không yêu cầu login)

Tại sao KHÔNG dùng Next.js cho widget:
  - Next.js không build ra single embeddable JS file
  - Bundle size quá lớn cho use case embed (Next.js runtime ~100KB+)
  - Không cần SSR (widget là interactive element, không cần SEO)
  - Cần full control over shadow DOM / style isolation

Style isolation:
  - Dùng CSS Modules hoặc Tailwind với prefix để tránh conflict với host page
  - Hoặc Shadow DOM để hoàn toàn isolated
```

---

## 4. Turborepo Monorepo Structure

```
signaldesk-fe/
├── apps/
│   ├── portal/                        ← Next.js 14 (App Router)
│   │   ├── app/
│   │   │   ├── (public)/
│   │   │   │   ├── page.tsx           ← Home / help center (SSG)
│   │   │   │   ├── help/
│   │   │   │   │   └── [slug]/
│   │   │   │   │       ├── page.tsx   ← KB article (ISR)
│   │   │   │   │       └── loading.tsx
│   │   │   │   └── search/
│   │   │   │       └── page.tsx       ← Public search (SSR)
│   │   │   ├── api/
│   │   │   │   └── revalidate/
│   │   │   │       └── route.ts       ← On-demand ISR revalidation endpoint
│   │   │   └── layout.tsx
│   │   ├── components/
│   │   │   ├── ArticleCard.tsx
│   │   │   ├── SearchBar.tsx
│   │   │   └── BreadcrumbNav.tsx
│   │   ├── lib/
│   │   │   └── kb.ts                  ← fetch articles (server-side)
│   │   └── next.config.ts
│   │
│   ├── workspace/                     ← Next.js 14 (App Router)
│   │   ├── app/
│   │   │   ├── (auth)/
│   │   │   │   ├── login/
│   │   │   │   │   └── page.tsx       ← Login page (SSR)
│   │   │   │   └── layout.tsx
│   │   │   ├── (app)/
│   │   │   │   ├── layout.tsx         ← Root layout: Sidebar + Header + SocketProvider
│   │   │   │   ├── tickets/
│   │   │   │   │   ├── page.tsx       ← Ticket list (SSR + TanStack Query hydration)
│   │   │   │   │   └── [id]/
│   │   │   │   │       ├── page.tsx   ← Ticket detail (SSR)
│   │   │   │   │       └── loading.tsx
│   │   │   │   ├── customers/
│   │   │   │   │   └── page.tsx
│   │   │   │   ├── chat/
│   │   │   │   │   ├── page.tsx       ← Chat list
│   │   │   │   │   └── [sessionId]/
│   │   │   │   │       └── page.tsx   ← Chat detail (SSR + WebSocket)
│   │   │   │   ├── kb/
│   │   │   │   │   ├── page.tsx       ← Article list
│   │   │   │   │   ├── new/
│   │   │   │   │   │   └── page.tsx
│   │   │   │   │   └── [id]/
│   │   │   │   │       └── edit/
│   │   │   │   │           └── page.tsx  ← TipTap WYSIWYG editor
│   │   │   │   ├── campaigns/
│   │   │   │   │   └── page.tsx
│   │   │   │   ├── analytics/
│   │   │   │   │   └── page.tsx       ← Recharts dashboards (Suspense streaming)
│   │   │   │   └── settings/
│   │   │   │       ├── team/
│   │   │   │       ├── roles/
│   │   │   │       └── ai-config/
│   │   │   └── api/
│   │   │       └── auth/
│   │   │           └── [...nextauth]/
│   │   │               └── route.ts
│   │   ├── components/
│   │   │   ├── ticket/
│   │   │   │   ├── TicketList.tsx
│   │   │   │   ├── TicketCard.tsx
│   │   │   │   ├── TicketTimeline.tsx
│   │   │   │   ├── ReplyEditor.tsx        ← form với expectedVersion
│   │   │   │   ├── AISuggestPanel.tsx     ← manual trigger, không auto-fetch
│   │   │   │   └── CollisionBanner.tsx    ← hiển thị khi agent khác đang reply
│   │   │   ├── chat/
│   │   │   │   ├── ChatWindow.tsx
│   │   │   │   ├── MessageBubble.tsx
│   │   │   │   └── TypingIndicator.tsx
│   │   │   └── analytics/
│   │   │       ├── TicketVolumeChart.tsx
│   │   │       ├── AgentPerformanceTable.tsx
│   │   │       └── AIDeflectionWidget.tsx
│   │   ├── hooks/
│   │   │   ├── useTickets.ts          ← TanStack Query hooks
│   │   │   ├── useTicketDetail.ts
│   │   │   ├── useAISuggest.ts        ← manual trigger
│   │   │   └── useVersionConflict.ts  ← handle 409 Conflict
│   │   ├── store/
│   │   │   ├── authStore.ts           ← Zustand: user + tenant
│   │   │   ├── ticketStore.ts         ← Zustand: UI state (selected, filters)
│   │   │   └── chatStore.ts           ← Zustand: active chat UI state
│   │   └── next.config.ts
│   │
│   └── widget-sdk/                    ← React 18 + Vite (NO Next.js)
│       ├── src/
│       │   ├── embed/
│       │   │   ├── index.ts           ← <script> bootstrap loader
│       │   │   └── mount.tsx          ← ReactDOM.createRoot vào host page
│       │   ├── widget/
│       │   │   ├── ChatWidget.tsx     ← root component
│       │   │   ├── MessageList.tsx
│       │   │   ├── MessageBubble.tsx
│       │   │   ├── InputBar.tsx
│       │   │   └── TypingIndicator.tsx
│       │   ├── hooks/
│       │   │   ├── useChat.ts         ← gọi API + WebSocket
│       │   │   └── useSession.ts      ← tạo/restore session
│       │   └── api/
│       │       └── widgetClient.ts    ← API calls (no TanStack Query, lightweight)
│       ├── vite.config.ts
│       └── package.json
│
├── packages/
│   ├── ui/                            ← React component library
│   │   ├── src/
│   │   │   ├── components/
│   │   │   │   ├── Button/
│   │   │   │   │   ├── Button.tsx
│   │   │   │   │   └── Button.test.tsx
│   │   │   │   ├── Badge/
│   │   │   │   ├── DataTable/
│   │   │   │   ├── Modal/
│   │   │   │   ├── Toast/
│   │   │   │   └── StatusBadge/       ← ticket status colored badge
│   │   │   └── index.ts
│   │   ├── package.json
│   │   └── tsconfig.json
│   │
│   ├── api-client/                    ← typed API client
│   │   ├── src/
│   │   │   ├── tickets.ts
│   │   │   ├── knowledge.ts
│   │   │   ├── ai.ts
│   │   │   ├── auth.ts
│   │   │   ├── search.ts
│   │   │   └── index.ts
│   │   └── package.json
│   │
│   ├── types/                         ← shared TypeScript types
│   │   ├── src/
│   │   │   ├── ticket.types.ts
│   │   │   ├── user.types.ts
│   │   │   ├── tenant.types.ts
│   │   │   ├── kb.types.ts
│   │   │   └── index.ts
│   │   └── package.json
│   │
│   ├── auth/                          ← auth helpers + permission hooks
│   │   ├── src/
│   │   │   ├── guards/
│   │   │   │   └── PermissionGuard.tsx
│   │   │   ├── hooks/
│   │   │   │   ├── usePermission.ts   ← can("tickets:view_all")
│   │   │   │   └── useTenantContext.ts
│   │   │   └── session/
│   │   │       └── sessionStore.ts
│   │   └── package.json
│   │
│   ├── realtime/                      ← WebSocket hooks
│   │   ├── src/
│   │   │   ├── socket/
│   │   │   │   ├── SocketProvider.tsx
│   │   │   │   └── useSocket.ts
│   │   │   ├── presence/
│   │   │   │   └── usePresence.ts
│   │   │   └── notifications/
│   │   │       └── useNotifications.ts
│   │   └── package.json
│   │
│   └── config/                        ← shared tooling config
│       ├── eslint-config/
│       ├── tailwind-config/           ← shared Tailwind preset
│       └── tsconfig/
│
├── turbo.json                         ← pipeline config
├── package.json
└── .github/
    └── workflows/
        ├── ci.yml
        ├── deploy-portal.yml
        └── deploy-workspace.yml
```

---

## 5. Shared Packages Architecture

### 5.1 `packages/ui` — React Component Library

**Nguyên tắc:**
- Không phụ thuộc Next.js — pure React. Dùng được trong cả 3 apps
- Dựa trên shadcn/ui làm base, extend thêm domain-specific components
- Mỗi component có `.test.tsx` riêng (Vitest)
- Export named (không default export) để tree-shaking tốt

**Components phải có:**
```typescript
// Domain-specific components quan trọng
<StatusBadge status="Open|InProgress|Resolved|Closed" />
<PriorityBadge priority="Low|Normal|High|Urgent" />
<DataTable columns={columns} data={data} loading={isLoading} />
<AvatarGroup users={users} max={3} />
<EmptyState icon={...} title="..." action={...} />
<LoadingSpinner size="sm|md|lg" />
<ConfirmDialog title="..." onConfirm={...} />
```

### 5.2 `packages/api-client` — Typed API Client

**Nguyên tắc:**
- Wrap `fetch` với base URL + auth header injection
- Mỗi resource có typed functions rõ ràng
- Throw typed errors (ApiError với status code)
- Không dùng trong SSR của Next.js — chỉ dùng ở client side. Server component tự fetch với cookie

```typescript
// Ví dụ tickets.ts
export async function getTickets(params: GetTicketsParams): Promise<PaginatedResult<Ticket>> {
  return apiFetch('/api/tickets', { params })
}

export async function createTicket(
  body: CreateTicketBody,
  opts?: { idempotencyKey?: string }
): Promise<Ticket> {
  return apiFetch('/api/tickets', {
    method: 'POST',
    body,
    headers: opts?.idempotencyKey ? { 'Idempotency-Key': opts.idempotencyKey } : {}
  })
}

export async function addTicketMessage(
  ticketId: string,
  body: AddMessageBody & { expectedVersion: number }
): Promise<TicketMessage> {
  return apiFetch(`/api/tickets/${ticketId}/messages`, { method: 'POST', body })
}
```

### 5.3 `packages/auth` — Permission System

```typescript
// usePermission hook — check quyền từ Zustand store
export function usePermission(permission: string): boolean {
  const permissions = useAuthStore(state => state.permissions)
  return permissions.includes(permission)
}

// PermissionGuard component
export function PermissionGuard({
  permission,
  children,
  fallback = null
}: PermissionGuardProps) {
  const can = usePermission(permission)
  return can ? <>{children}</> : <>{fallback}</>
}

// Usage:
<PermissionGuard permission="tickets:assign">
  <AssignButton />
</PermissionGuard>
```

### 5.4 `packages/realtime` — WebSocket Abstraction

```typescript
// SocketProvider — wrap Socket.io, tự reconnect
export function SocketProvider({ children }: { children: React.ReactNode }) {
  const { token, tenantId } = useAuthStore()
  const [socket, setSocket] = useState<Socket | null>(null)

  useEffect(() => {
    if (!token) return
    const s = io(GATEWAY_URL, {
      auth: { token },
      query: { tenantId },
      reconnectionAttempts: 5,
      reconnectionDelay: 1000,
    })
    setSocket(s)
    return () => { s.disconnect() }
  }, [token, tenantId])

  return <SocketContext.Provider value={socket}>{children}</SocketContext.Provider>
}

// useSocket — consume context
export function useSocket(): Socket | null {
  return useContext(SocketContext)
}

// useTicketRoom — subscribe tới room của 1 ticket
export function useTicketRoom(ticketId: string, onUpdate: (data: TicketUpdate) => void) {
  const socket = useSocket()
  useEffect(() => {
    if (!socket || !ticketId) return
    socket.emit('join_ticket_room', ticketId)
    socket.on(`ticket:${ticketId}:update`, onUpdate)
    return () => {
      socket.emit('leave_ticket_room', ticketId)
      socket.off(`ticket:${ticketId}:update`, onUpdate)
    }
  }, [socket, ticketId, onUpdate])
}
```

---

## 6. App 1: Portal — Next.js SSR/ISR

### 6.1 Mục Đích

Public help center cho end-customer. Không yêu cầu login. Phải SEO-ready. Đây là "shop window" của tenant — người dùng tìm kiếm câu trả lời trước khi cần contact support.

### 6.2 Route Structure

```
/ (Home — SSG)
  → Featured categories + popular articles + search bar

/search?q=... (SSR)
  → Gọi search API, hiển thị kết quả articles + maybe tickets (nếu logged in customer)

/help/[slug] (ISR, revalidate 3600s)
  → Article content, breadcrumb, related articles
  → On-demand revalidation khi article được publish/update từ admin

/help/category/[slug] (ISR)
  → List articles trong category

/contact (SSR)
  → Form gửi ticket (không cần account)
```

### 6.3 On-Demand ISR Revalidation Flow

```
Admin publish article → knowledge-service
    │
    ├─ Outbox event: knowledge.article.published.v1
    ├─ search-service: index vào ES
    └─ portal revalidation:
         knowledge-service → POST https://help.taskflow.io/api/revalidate
         body: { slug: "how-to-reset-password", secret: REVALIDATE_SECRET }
         Next.js route handler gọi: revalidatePath(`/help/${slug}`)
         → ISR cache cleared → next request rebuild fresh page
```

```typescript
// apps/portal/app/api/revalidate/route.ts
import { revalidatePath } from 'next/cache'
import { NextRequest, NextResponse } from 'next/server'

export async function POST(request: NextRequest) {
  const { slug, secret } = await request.json()

  if (secret !== process.env.REVALIDATE_SECRET) {
    return NextResponse.json({ error: 'Invalid secret' }, { status: 401 })
  }

  revalidatePath(`/help/${slug}`)
  revalidatePath('/') // refresh home nếu featured articles thay đổi
  return NextResponse.json({ revalidated: true, slug })
}
```

### 6.4 Article Page — ISR Implementation

```typescript
// apps/portal/app/(public)/help/[slug]/page.tsx

// generateStaticParams: pre-build popular articles at build time
export async function generateStaticParams() {
  const articles = await getPublishedArticles({ limit: 50 }) // top 50
  return articles.map(a => ({ slug: a.slug }))
}

// generateMetadata: SEO per article
export async function generateMetadata({ params }: { params: { slug: string } }) {
  const article = await getArticleBySlug(params.slug)
  return {
    title: `${article.title} | ${article.tenantName} Help Center`,
    description: article.metaDescription || article.summary,
    openGraph: {
      title: article.title,
      description: article.summary,
      type: 'article',
      publishedTime: article.publishedAt,
    }
  }
}

// Page component: Server Component, runs at build/revalidate time
export default async function ArticlePage({ params }: { params: { slug: string } }) {
  const article = await getArticleBySlug(params.slug) // server-side fetch
  if (!article) notFound()

  return (
    <article>
      <Breadcrumb items={article.breadcrumb} />
      <h1>{article.title}</h1>
      <ArticleContent html={article.bodyHtml} />
      <RelatedArticles articleId={article.id} />
      <FeedbackWidget articleId={article.id} /> {/* Client Component */}
    </article>
  )
}

// revalidate: set ISR interval
export const revalidate = 3600 // 1 hour fallback; on-demand còn nhanh hơn
```

### 6.5 Tenant Context trên Portal

Portal phục vụ nhiều tenant. Tenant được xác định từ:
1. Subdomain: `help.taskflow.io` → `taskflow`
2. Hoặc path: `signaldesk.io/t/taskflow/help`

```typescript
// apps/portal/lib/tenant.ts
export async function getTenantFromRequest(request: NextRequest): Promise<string> {
  const host = request.headers.get('host') || ''
  const subdomain = host.split('.')[0]
  // Validate subdomain vs whitelist từ workspace-service
  return subdomain
}
```

---

## 7. App 2: Workspace — Next.js SSR

### 7.1 Auth Flow (SSR-first)

```
User truy cập /tickets (protected route)
    │
Next.js middleware check JWT từ cookie httpOnly
    │
    ├─ Không có JWT → redirect /login
    │
    └─ Có JWT → Server Component fetch data với JWT
         → render page với data sẵn (không flash loading)
         → hydrate → TanStack Query tiếp quản client-side mutations

/login:
  POST /api/auth/login → backend
  Set httpOnly cookie (refresh token)
  LocalStorage lưu access token (short-lived, 15min)
  TanStack Query prefetch user data
```

```typescript
// apps/workspace/middleware.ts
import { NextResponse } from 'next/server'
import type { NextRequest } from 'next/server'

export function middleware(request: NextRequest) {
  const accessToken = request.cookies.get('access_token')?.value
  const isAuthPage = request.nextUrl.pathname.startsWith('/login')

  if (!accessToken && !isAuthPage) {
    return NextResponse.redirect(new URL('/login', request.url))
  }
  if (accessToken && isAuthPage) {
    return NextResponse.redirect(new URL('/tickets', request.url))
  }
  return NextResponse.next()
}

export const config = {
  matcher: ['/((?!api|_next/static|_next/image|favicon.ico).*)'],
}
```

### 7.2 Ticket List Page — SSR + TanStack Query Hydration Pattern

```typescript
// apps/workspace/app/(app)/tickets/page.tsx
import { dehydrate, HydrationBoundary, QueryClient } from '@tanstack/react-query'
import { TicketListClient } from './TicketListClient'

// Server Component: fetch initial data server-side
export default async function TicketsPage({ searchParams }: PageProps) {
  const queryClient = new QueryClient()

  // Prefetch tại server — không có loading flash cho client
  await queryClient.prefetchQuery({
    queryKey: ['tickets', searchParams],
    queryFn: () => fetchTicketsServer(searchParams), // dùng server-side cookie
  })

  return (
    <HydrationBoundary state={dehydrate(queryClient)}>
      <TicketListClient defaultSearchParams={searchParams} />
    </HydrationBoundary>
  )
}
```

```typescript
// apps/workspace/app/(app)/tickets/TicketListClient.tsx
'use client'
import { useQuery } from '@tanstack/react-query'

export function TicketListClient({ defaultSearchParams }: Props) {
  const [filters, setFilters] = useFilters(defaultSearchParams)

  // Data đã có từ SSR hydration → không có loading state ban đầu
  const { data, isLoading } = useQuery({
    queryKey: ['tickets', filters],
    queryFn: () => getTickets(filters),
    staleTime: 60_000, // 1 phút trước khi background refetch
  })

  // Real-time update: subscribe WebSocket để invalidate cache khi có ticket mới
  const socket = useSocket()
  useEffect(() => {
    socket?.on('ticket:new', () => queryClient.invalidateQueries(['tickets']))
    return () => socket?.off('ticket:new')
  }, [socket])

  return <TicketList tickets={data?.items} loading={isLoading} filters={filters} onFilterChange={setFilters} />
}
```

### 7.3 Ticket Detail — Version Conflict Handling

```typescript
// apps/workspace/components/ticket/ReplyEditor.tsx
'use client'

export function ReplyEditor({ ticket }: { ticket: Ticket }) {
  const [content, setContent] = useState('')
  const [conflictData, setConflictData] = useState<Ticket | null>(null)

  const mutation = useMutation({
    mutationFn: (body: string) => addTicketMessage(ticket.id, {
      content: body,
      expectedVersion: ticket.rowVersion, // bắt buộc gửi lên
    }),

    onSuccess: () => {
      setContent('')
      queryClient.invalidateQueries(['ticket', ticket.id])
    },

    onError: (error: ApiError) => {
      if (error.status === 409) {
        // Version conflict: agent khác đã thay đổi ticket
        setConflictData(error.body.latestSnapshot)
        toast.error('Ticket was updated by someone else. Please review the latest changes.')
      }
    },
  })

  return (
    <div>
      {conflictData && (
        <ConflictBanner
          latestData={conflictData}
          onDismiss={() => {
            setConflictData(null)
            queryClient.setQueryData(['ticket', ticket.id], conflictData) // sync FE state
          }}
        />
      )}
      <TipTapEditor value={content} onChange={setContent} />
      <Button onClick={() => mutation.mutate(content)} loading={mutation.isPending}>
        Send Reply
      </Button>
    </div>
  )
}
```

### 7.4 AI Suggest Panel — Manual Trigger

```typescript
// apps/workspace/components/ticket/AISuggestPanel.tsx
'use client'

// AI suggest KHÔNG auto-fetch khi mở ticket (tránh chi phí AI không cần thiết)
// Agent phải click "Get AI Suggestion" mới trigger

export function AISuggestPanel({ ticketId }: { ticketId: string }) {
  const [enabled, setEnabled] = useState(false)

  const { data, isLoading } = useQuery({
    queryKey: ['ai-suggest', ticketId],
    queryFn: () => getAISuggestReply(ticketId),
    enabled, // chỉ fetch khi agent click
    staleTime: 5 * 60 * 1000, // cache 5 phút (đã cache ở Redis BE, FE cũng cache)
  })

  return (
    <div>
      {!enabled && (
        <Button variant="outline" onClick={() => setEnabled(true)}>
          ✨ Get AI Suggestion
        </Button>
      )}
      {isLoading && <Skeleton />}
      {data && (
        <div>
          <p className="text-sm text-muted">{data.suggestion}</p>
          <Button onClick={() => navigator.clipboard.writeText(data.suggestion)}>
            Use this reply
          </Button>
          <FeedbackButtons suggestionId={data.id} />
        </div>
      )}
    </div>
  )
}
```

### 7.5 Workspace Layout — SocketProvider + Notifications

```typescript
// apps/workspace/app/(app)/layout.tsx
import { SocketProvider } from '@signaldesk/realtime'
import { NotificationProvider } from '@/components/NotificationProvider'

export default function AppLayout({ children }: { children: React.ReactNode }) {
  return (
    <SocketProvider>
      <NotificationProvider>
        <div className="flex h-screen">
          <Sidebar />
          <main className="flex-1 overflow-auto">
            <Header />
            {children}
          </main>
        </div>
      </NotificationProvider>
    </SocketProvider>
  )
}
```

---

## 8. App 3: Widget SDK — React CSR Embeddable

### 8.1 Embed Mechanism

```html
<!-- Nhúng vào website của tenant -->
<script
  src="https://cdn.signaldesk.io/widget.js"
  data-tenant="taskflow"
  data-position="bottom-right"
  data-primary-color="#4F46E5"
  async
></script>
```

```typescript
// apps/widget-sdk/src/embed/index.ts
// Script này chạy trên website của tenant

(function() {
  const script = document.currentScript as HTMLScriptElement
  const tenantId = script.dataset.tenant
  const position = script.dataset.position || 'bottom-right'
  const primaryColor = script.dataset.primaryColor || '#4F46E5'

  if (!tenantId) {
    console.error('[SignalDesk] data-tenant is required')
    return
  }

  // Tạo isolated container
  const container = document.createElement('div')
  container.id = 'signaldesk-widget-root'
  container.style.cssText = `
    position: fixed;
    ${position === 'bottom-right' ? 'bottom: 24px; right: 24px;' : 'bottom: 24px; left: 24px;'}
    z-index: 999999;
  `
  document.body.appendChild(container)

  // Mount React app
  import('./mount').then(({ mountWidget }) => {
    mountWidget(container, { tenantId, primaryColor })
  })
})()
```

```typescript
// apps/widget-sdk/src/embed/mount.tsx
import { createRoot } from 'react-dom/client'
import { ChatWidget } from '../widget/ChatWidget'

export function mountWidget(container: HTMLElement, config: WidgetConfig) {
  const root = createRoot(container)
  root.render(<ChatWidget config={config} />)
}
```

### 8.2 Widget State Management (Lightweight)

Widget SDK KHÔNG dùng TanStack Query hay Zustand — quá nặng cho embeddable. Dùng React state + custom hooks.

```typescript
// apps/widget-sdk/src/hooks/useSession.ts
export function useSession(tenantId: string) {
  // Session persisted trong sessionStorage của browser tenant
  const [sessionId, setSessionId] = useState<string | null>(() =>
    sessionStorage.getItem(`sd_session_${tenantId}`)
  )

  const createSession = async (email?: string) => {
    const session = await widgetClient.createChatSession(tenantId, email)
    sessionStorage.setItem(`sd_session_${tenantId}`, session.sessionId)
    setSessionId(session.sessionId)
    return session
  }

  return { sessionId, createSession }
}

// apps/widget-sdk/src/hooks/useChat.ts
export function useChat(sessionId: string | null) {
  const [messages, setMessages] = useState<ChatMessage[]>([])
  const [isTyping, setIsTyping] = useState(false)
  const socketRef = useRef<Socket | null>(null)

  useEffect(() => {
    if (!sessionId) return

    const socket = io(GATEWAY_URL, { query: { sessionId } })
    socketRef.current = socket

    socket.on('message', (msg: ChatMessage) => {
      setMessages(prev => [...prev, msg])
    })
    socket.on('bot_typing', () => setIsTyping(true))
    socket.on('bot_response', () => setIsTyping(false))

    return () => socket.disconnect()
  }, [sessionId])

  const sendMessage = async (content: string) => {
    if (!sessionId) return
    // Optimistic: add message immediately
    const optimisticMsg: ChatMessage = {
      id: crypto.randomUUID(), role: 'customer', content, timestamp: new Date()
    }
    setMessages(prev => [...prev, optimisticMsg])
    // Then send to server
    await widgetClient.sendMessage(sessionId, content)
  }

  return { messages, isTyping, sendMessage }
}
```

### 8.3 Bundle Size Budget

```
Widget SDK build target:
  - Total bundle (gzip): < 80KB
  - React + React DOM: ~45KB gzip
  - Widget code: < 20KB
  - Socket.io client: ~14KB gzip

Để đạt được:
  - Không dùng TanStack Query, Zustand, Radix UI
  - Tailwind: dùng JIT và purge aggressively
  - Vite code splitting: lazy load ChatWindow component
  - Không import toàn bộ lodash (dùng native alternatives)
```

### 8.4 Vite Config cho Widget

```typescript
// apps/widget-sdk/vite.config.ts
import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'

export default defineConfig({
  plugins: [react()],
  build: {
    lib: {
      entry: 'src/embed/index.ts',
      name: 'SignalDeskWidget',
      fileName: 'widget',
      formats: ['iife'], // IIFE để auto-execute khi load
    },
    rollupOptions: {
      // Không externalize React — widget cần self-contained
    },
    minify: 'terser',
    target: 'es2018',
  },
})
```

---

## 9. State Management Strategy

### 9.1 Tổng Quan — 3 Loại State Riêng Biệt

```
┌─────────────────────────────────────────────────────┐
│ Server State (data từ API)                           │
│ → TanStack Query (workspace app)                    │
│   - cache, background refetch, optimistic update    │
│   - staleTime, gcTime per resource type             │
└─────────────────────────────────────────────────────┘

┌─────────────────────────────────────────────────────┐
│ UI State (local component state)                    │
│ → Zustand (workspace app)                           │
│   - auth: user, tenantId, permissions               │
│   - ticketUI: selectedTicketId, filters, view mode  │
│   - chatUI: active session, typing state            │
└─────────────────────────────────────────────────────┘

┌─────────────────────────────────────────────────────┐
│ Real-time State (WebSocket events)                  │
│ → Socket.io events → TanStack Query cache update   │
│   - ticket update → invalidateQueries               │
│   - new message → setQueryData (push vào cache)     │
│   - presence update → Zustand presence store        │
└─────────────────────────────────────────────────────┘
```

### 9.2 TanStack Query — Cấu Hình Per Resource

```typescript
// Ticket list: refetch mỗi 1 phút (background)
useQuery({
  queryKey: ['tickets', filters],
  queryFn: () => getTickets(filters),
  staleTime: 60_000,
  refetchInterval: 60_000,
})

// Ticket detail: stale sau 5 phút, real-time via WebSocket
useQuery({
  queryKey: ['ticket', ticketId],
  queryFn: () => getTicketById(ticketId),
  staleTime: 5 * 60_000,
})

// AI suggest: cache lâu hơn (đắt tiền để generate)
useQuery({
  queryKey: ['ai-suggest', ticketId],
  queryFn: () => getAISuggestReply(ticketId),
  enabled: userRequestedSuggestion,
  staleTime: 5 * 60_000,
  gcTime: 10 * 60_000,
})

// Dashboard stats: fresh mỗi 5 phút
useQuery({
  queryKey: ['analytics', 'overview'],
  queryFn: getAnalyticsOverview,
  staleTime: 5 * 60_000,
  refetchInterval: 5 * 60_000,
})
```

### 9.3 Zustand Stores

```typescript
// store/authStore.ts
interface AuthState {
  user: User | null
  tenantId: string | null
  permissions: string[]  // ['tickets:view_all', 'kb:publish', ...]
  isLoading: boolean
  setAuth: (user: User, tenantId: string, permissions: string[]) => void
  clearAuth: () => void
}

// store/ticketStore.ts — UI-only state, không phải server data
interface TicketUIState {
  selectedTicketId: string | null
  viewMode: 'list' | 'split' | 'detail'
  filters: TicketFilters
  setFilters: (filters: Partial<TicketFilters>) => void
  setViewMode: (mode: TicketUIState['viewMode']) => void
}
```

### 9.4 Optimistic Updates Strategy

Chỉ dùng optimistic updates ở nơi UX cần thiết và rollback ít phức tạp:

```typescript
// ✅ NÊN dùng optimistic: reply gửi tin nhắn (risk thấp)
const mutation = useMutation({
  mutationFn: addTicketMessage,
  onMutate: async (newMessage) => {
    await queryClient.cancelQueries({ queryKey: ['ticket', ticketId] })
    const previous = queryClient.getQueryData(['ticket', ticketId])

    queryClient.setQueryData(['ticket', ticketId], (old: Ticket) => ({
      ...old,
      messages: [...old.messages, { ...newMessage, id: 'optimistic', pending: true }]
    }))
    return { previous }
  },
  onError: (err, _, context) => {
    queryClient.setQueryData(['ticket', ticketId], context?.previous)
  },
  onSettled: () => {
    queryClient.invalidateQueries({ queryKey: ['ticket', ticketId] })
  },
})

// ❌ KHÔNG dùng optimistic: resolve ticket (status change phức tạp)
// ❌ KHÔNG dùng optimistic: assign agent (business rule phức tạp)
// → Những action này show loading state, wait for server response
```

---

## 10. Real-time Architecture (Socket.io)

### 10.1 Event Map — FE Side

```typescript
// Các events mà workspace app subscribe:

// Ticket events
socket.on('ticket:new', (data: { ticketId, tenantId }) => {
  queryClient.invalidateQueries(['tickets'])
  showNotification(`New ticket from ${data.customerEmail}`)
})

socket.on(`ticket:${ticketId}:update`, (data: TicketUpdate) => {
  queryClient.setQueryData(['ticket', ticketId], data)
})

socket.on(`ticket:${ticketId}:message`, (msg: TicketMessage) => {
  queryClient.setQueryData(['ticket', ticketId], (old: Ticket) => ({
    ...old,
    messages: [...old.messages, msg]
  }))
})

// Collaboration events
socket.on(`ticket:${ticketId}:lock`, (data: { agentId, agentName }) => {
  setCollisionState({ lockedBy: data.agentName, isLocked: true })
})

socket.on(`ticket:${ticketId}:lock_released`, () => {
  setCollisionState({ isLocked: false })
})

socket.on(`ticket:${ticketId}:typing`, (data: { agentId, agentName }) => {
  setTypingAgent(data.agentName)
})

// Presence
socket.on('presence:update', (data: PresenceMap) => {
  usePresenceStore.getState().update(data)
})
```

### 10.2 Heartbeat & Reconnection

```typescript
// packages/realtime/src/socket/SocketProvider.tsx

// Heartbeat để giữ presence alive (server TTL 30s)
useEffect(() => {
  if (!socket) return
  const heartbeat = setInterval(() => {
    socket.emit('heartbeat')
  }, 15_000)
  return () => clearInterval(heartbeat)
}, [socket])

// Reconnection: Socket.io tự reconnect với exponential backoff
// Sau reconnect: rejoin rooms và resync data
socket.on('connect', () => {
  // Rejoin all active rooms
  activeTicketRooms.forEach(ticketId => {
    socket.emit('join_ticket_room', ticketId)
  })
  // Resync data
  queryClient.invalidateQueries(['tickets'])
})
```

---

## 11. Auth & Permission Flow (FE)

### 11.1 JWT Flow — Access Token + Refresh Token

```
Login:
  POST /api/auth/login
  Server trả về:
    - access_token (JWT 15min) → lưu memory (Zustand) hoặc localStorage
    - refresh_token → set httpOnly cookie (XSS-safe)

Token refresh (tự động):
  TanStack Query mutation request gặp 401
  → api-client tự call POST /api/auth/refresh
  → nhận access_token mới
  → retry original request

Logout:
  POST /api/auth/logout
  Clear memory + cookie
  Redirect /login
```

### 11.2 Permission Check Pattern

```typescript
// Permission check theo 2 cách:

// 1. Hook (trong component logic)
const canAssign = usePermission('tickets:assign')
if (!canAssign) return null

// 2. Component guard (trong JSX)
<PermissionGuard permission="kb:publish" fallback={<span>View only</span>}>
  <PublishButton />
</PermissionGuard>

// 3. Server-side (trong Next.js server component)
const user = await getServerSession()
if (!user.permissions.includes('analytics:view')) {
  redirect('/tickets')
}
```

### 11.3 Tenant Context

```typescript
// Mỗi authenticated user thuộc về 1 tenant trong session hiện tại.
// tenantId được lưu trong JWT và Zustand store.

// Mọi API call tự động attach tenantId:
// api-client/src/fetcher.ts
async function apiFetch(path: string, options?: FetchOptions) {
  const { accessToken, tenantId } = useAuthStore.getState()
  return fetch(`${GATEWAY_URL}${path}`, {
    ...options,
    headers: {
      Authorization: `Bearer ${accessToken}`,
      'X-Tenant-Id': tenantId,  // Gateway sẽ validate với JWT claim
      'Content-Type': 'application/json',
      ...options?.headers,
    }
  })
}
```

---

## 12. Component Architecture

### 12.1 Phân Loại Components

```
Server Components (Next.js):
  - Page components (fetch data tại server, không có useState/useEffect)
  - Layout components (static structure)
  - Mọi component không cần interactivity

Client Components ('use client'):
  - Mọi component dùng useState, useEffect, event handlers
  - Socket subscribers
  - Form components
  - Rich text editor (TipTap)
  - Charts (Recharts)

Shared Components (packages/ui):
  - Pure React, không có 'use client' directive
  - Nhận data và callbacks qua props
  - Không gọi API trực tiếp
  - Không access context
```

### 12.2 Server vs Client Component Boundary Pattern

```typescript
// Đúng: Page là Server Component, chỉ phần interactive là Client
// apps/workspace/app/(app)/tickets/[id]/page.tsx (Server Component)
export default async function TicketDetailPage({ params }: { params: { id: string } }) {
  const ticket = await getTicketById(params.id) // server fetch

  return (
    <div>
      <TicketHeader ticket={ticket} />          {/* Server Component */}
      <TicketTimeline messages={ticket.messages} /> {/* Server Component */}
      <ReplyEditor ticket={ticket} />            {/* Client Component (có form) */}
      <AISuggestPanel ticketId={ticket.id} />   {/* Client Component (manual trigger) */}
    </div>
  )
}
```

### 12.3 Loading States & Error Boundaries

```typescript
// Mỗi page có loading.tsx (Next.js Suspense)
// apps/workspace/app/(app)/tickets/[id]/loading.tsx
export default function Loading() {
  return (
    <div className="animate-pulse">
      <div className="h-8 bg-gray-200 rounded w-1/3 mb-4" />
      <div className="h-4 bg-gray-200 rounded w-2/3 mb-2" />
      <div className="h-64 bg-gray-200 rounded" />
    </div>
  )
}

// Error boundary cho data fetch failures
// apps/workspace/app/(app)/tickets/[id]/error.tsx
'use client'
export default function Error({ error, reset }: { error: Error, reset: () => void }) {
  return (
    <EmptyState
      icon="alert-circle"
      title="Something went wrong"
      description={error.message}
      action={{ label: 'Try again', onClick: reset }}
    />
  )
}
```

---

## 13. Performance Strategy

### 13.1 Portal — SEO & Core Web Vitals

```
Target:
  - Lighthouse score ≥ 90 (performance, accessibility, SEO)
  - LCP < 2.5s
  - FID / INP < 100ms
  - CLS < 0.1

Techniques:
  - ISR: TTFB thấp vì page đã built sẵn
  - next/image: tự optimize + lazy load images
  - next/font: preload fonts, no layout shift
  - Article content: render HTML từ server, không hydrate heavy editor
  - Code splitting: dynamic() cho heavy components (search highlight, etc.)
```

### 13.2 Workspace — Bundle Optimization

```typescript
// Heavy components dùng dynamic import
const TipTapEditor = dynamic(() => import('@/components/TipTapEditor'), {
  loading: () => <Skeleton className="h-64" />,
  ssr: false,
})

const RechartsChart = dynamic(() => import('@/components/AnalyticsChart'), {
  loading: () => <Skeleton className="h-48" />,
  ssr: false,
})

// Code splitting theo route tự động với Next.js App Router
// Mỗi layout/page là bundle riêng
```

### 13.3 Widget SDK — Bundle Size Control

```
Mục tiêu: widget.js (gzip) < 80KB
Monitoring: Bundle analyzer (rollup-plugin-visualizer)

Kiểm tra thường xuyên:
  npm run build:analyze
  → mở dist/stats.html
  → identify modules > 10KB
  → xem xét lazy load hoặc thay thế

Lazy load ChatWindow:
  // Khi user chưa click open, chỉ load bubble button (< 5KB)
  // Khi click → lazy load full ChatWindow component
```

### 13.4 Image & Font Optimization

```typescript
// Portal: dùng next/image cho tất cả images
import Image from 'next/image'
<Image
  src={article.thumbnail}
  alt={article.title}
  width={800}
  height={400}
  priority={isAboveFold}  // true cho hình đầu tiên
  sizes="(max-width: 768px) 100vw, 800px"
/>

// Font: preload với next/font/google
import { Inter } from 'next/font/google'
const inter = Inter({
  subsets: ['latin'],
  display: 'swap',  // không block render
  preload: true,
})
```

---

## 14. Testing Strategy

### 14.1 Unit Tests — Vitest

```typescript
// packages/ui: component unit tests
// apps/workspace: hook tests, utility tests

// Ví dụ: test useVersionConflict hook
import { renderHook, act } from '@testing-library/react'
import { useVersionConflict } from '../hooks/useVersionConflict'

test('shows conflict banner when receiving 409', async () => {
  const { result } = renderHook(() => useVersionConflict())

  act(() => {
    result.current.handleApiError({ status: 409, body: { latestSnapshot: mockTicket } })
  })

  expect(result.current.hasConflict).toBe(true)
  expect(result.current.latestSnapshot).toEqual(mockTicket)
})
```

### 14.2 E2E Tests — Playwright

```typescript
// Critical user journeys:

// 1. Agent reply flow với collision detection
test('shows collision banner when another agent is replying', async ({ browser }) => {
  const agentA = await browser.newContext()
  const agentB = await browser.newContext()

  const pageA = await agentA.newPage()
  const pageB = await agentB.newPage()

  await pageA.goto('/tickets/TK-000001')
  await pageB.goto('/tickets/TK-000001')

  // Agent A starts typing
  await pageA.click('[data-testid="reply-editor"]')

  // Agent B should see collision banner
  await expect(pageB.locator('[data-testid="collision-banner"]')).toBeVisible()
})

// 2. RAG bot escalation
test('bot escalates low-confidence query to human', async ({ page }) => {
  await page.goto('https://help.taskflow.io')
  await page.click('[data-testid="chat-widget-button"]')
  await page.fill('[data-testid="chat-input"]', 'What is the refund policy for annual plan?')
  await page.press('[data-testid="chat-input"]', 'Enter')

  await expect(page.locator('[data-testid="escalation-message"]')).toBeVisible({ timeout: 5000 })
})

// 3. KB article ISR revalidation
test('article updates after publish within 5 seconds', async ({ page, request }) => {
  // Publish article via API
  await request.post('/api/kb/articles/test-article/publish', { ... })

  // Portal should show updated content within 5s (on-demand ISR)
  await page.goto('/help/test-article')
  await page.waitForTimeout(2000) // wait for ISR
  await page.reload()
  await expect(page.locator('h1')).toHaveText('Updated Article Title')
})
```

### 14.3 Testing Pyramid

```
E2E (Playwright): 10–15 critical flows
  - Login + auth redirect
  - Ticket create → reply → resolve
  - KB publish → portal ISR
  - Widget chat → bot response → escalation
  - Tenant isolation (try cross-tenant API)

Integration (Vitest + MSW): 20–30 tests
  - TanStack Query hooks với mocked API
  - Socket.io event handlers
  - Permission guards
  - Form validation

Unit (Vitest): 50+ tests
  - packages/ui components
  - Utility functions
  - Type validators
  - State logic
```

---

## 15. CI/CD Frontend

### 15.1 GitHub Actions Pipelines

```yaml
# .github/workflows/ci.yml — chạy trên mọi PR
name: CI
on: [pull_request]
jobs:
  lint-and-test:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-node@v4
        with: { node-version: '20', cache: 'npm' }
      - run: npm ci
      - run: npx turbo lint
      - run: npx turbo test
      - run: npx turbo build  # verify tất cả apps build được
```

```yaml
# .github/workflows/deploy-portal.yml
name: Deploy Portal
on:
  push:
    branches: [main]
    paths: ['apps/portal/**', 'packages/**']  # chỉ deploy khi portal hoặc packages thay đổi
jobs:
  deploy:
    runs-on: ubuntu-latest
    steps:
      - run: npx turbo build --filter=portal
      - run: ssh deploy@vps "docker pull && docker-compose up -d portal"
```

### 15.2 Turborepo Caching

```json
// turbo.json
{
  "pipeline": {
    "build": {
      "dependsOn": ["^build"],  // build packages trước apps
      "outputs": [".next/**", "dist/**"]
    },
    "test": {
      "outputs": ["coverage/**"]
    },
    "lint": {
      "outputs": []
    }
  },
  "remoteCache": {
    "enabled": true   // Vercel Remote Cache — CI nhanh hơn 10x sau lần đầu
  }
}
```

---

## 16. Roadmap 10 Tuần — FE Tasks

### Tuần 1: Monorepo Bootstrap

```
✓ Turborepo setup với 3 apps + packages skeleton
✓ Shared config: ESLint, Tailwind preset, TypeScript base config
✓ packages/ui: Button, Badge, DataTable, EmptyState, LoadingSpinner
✓ packages/types: Ticket, User, Tenant types
✓ packages/api-client: base fetcher với auth header injection
✓ CI: lint + test + build trên PR
```

### Tuần 2: Auth + Workspace Shell

```
✓ workspace: login page (SSR), middleware auth redirect
✓ workspace: root layout với Sidebar + Header
✓ Zustand authStore: user, tenantId, permissions
✓ packages/auth: usePermission hook, PermissionGuard component
✓ packages/realtime: SocketProvider skeleton (connect/disconnect)
✓ Workspace app deploy tới VPS (có URL live)
```

### Tuần 3: Ticket List + Detail

```
✓ workspace: ticket list page (SSR + TanStack Query hydration pattern)
✓ workspace: ticket detail page (SSR, messages timeline)
✓ ReplyEditor: form với expectedVersion, 409 Conflict handling
✓ CollisionBanner component
✓ TanStack Query: staleTime config per resource
✓ Optimistic update cho add message
```

### Tuần 4: Portal + KB Management

```
✓ portal: Home page (SSG), Article page (ISR)
✓ portal: On-demand ISR revalidate endpoint
✓ portal: Breadcrumb, Related Articles
✓ portal: generateMetadata per article (SEO)
✓ workspace: KB article list + TipTap WYSIWYG editor
✓ workspace: KB category tree management
✓ Lighthouse audit portal ≥ 85 (tuần này)
```

### Tuần 5: Search UI

```
✓ portal: Public search page (SSR) kết nối /api/search
✓ workspace: Global search bar với unified results (tickets + articles)
✓ Debounce + loading states
✓ Keyboard navigation trong search results
✓ Search highlight cho matched terms
```

### Tuần 6: Real-time Chat Widget

```
✓ widget-sdk: Vite setup, bundle size audit < 80KB
✓ widget-sdk: embed/index.ts script loader
✓ widget-sdk: ChatWidget component + MessageList + InputBar
✓ widget-sdk: useSession (sessionStorage persist) + useChat (WebSocket)
✓ widget-sdk: TypingIndicator, bot response rendering với citations
✓ workspace: Chat view cho agent (SSR + Socket.io)
✓ Collision detection UI: advisory lock banner, heartbeat
```

### Tuần 7: AI Integration UI

```
✓ workspace: AISuggestPanel (manual trigger, cache 5 phút)
✓ workspace: AI classification badge trên ticket (category + sentiment)
✓ workspace: Ticket summary section (lazy load)
✓ widget-sdk: Confidence-based escalation message UI
✓ FeedbackButtons component (accepted/rejected AI suggestion)
```

### Tuần 8: Analytics Dashboard

```
✓ workspace: analytics/page.tsx (SSR + Suspense streaming)
✓ Recharts: TicketVolumeChart, AgentPerformanceTable, AIDeflectionWidget
✓ Dynamic import cho Recharts (heavy, SSR: false)
✓ Date range picker
✓ Export button (CSV)
```

### Tuần 9: Campaign UI + Polish

```
✓ workspace: Campaign list + create form (nếu BE xong)
✓ Portal: Lighthouse final audit ≥ 90, fix CLS issues
✓ Workspace: Code splitting audit (identify > 50KB chunks)
✓ Error states + empty states đầy đủ mọi page
✓ Accessibility: keyboard navigation, ARIA labels
✓ Toast notifications system
✓ Responsive layout (workspace: tablet-friendly ít nhất)
```

### Tuần 10: Deploy + Polish + Portfolio

```
✓ Portal: sitemap.xml, robots.txt
✓ Widget: CDN deploy (Cloudflare Pages hoặc S3)
✓ Workspace: Sentry integration (FE error tracking)
✓ Web Vitals monitoring (next/web-vitals → analytics)
✓ Playwright E2E: 5 critical flows
✓ README với screenshots + architecture diagram
✓ Demo video: 5–7 phút, cover 4 scenarios
```

---

## 17. Definition of Done — FE

```
Portal:
  ✓ KB article load trong < 2s (ISR cache)
  ✓ Lighthouse ≥ 90 (performance, SEO, accessibility)
  ✓ LCP < 2.5s
  ✓ On-demand ISR revalidate trong < 5s sau publish
  ✓ generateMetadata đầy đủ (title, description, OG)
  ✓ Sitemap hoạt động

Workspace:
  ✓ Ticket list SSR: data hiện ngay khi load (không flash loading)
  ✓ 409 Conflict handling: CollisionBanner + refetch đúng
  ✓ AI Suggest: manual trigger, cache, feedback buttons
  ✓ WebSocket: auto-reconnect, rejoin rooms sau reconnect
  ✓ Permission guards đúng per role
  ✓ Bundle size per page < 200KB gzip (page JS, không gồm initial JS)

Widget SDK:
  ✓ widget.js gzip < 80KB
  ✓ Embed bằng 1 dòng <script>
  ✓ Session persist qua reload (sessionStorage)
  ✓ Bot response với citations hiển thị đúng
  ✓ Escalation message khi confidence thấp
  ✓ Typing indicator hoạt động

Shared:
  ✓ CI: lint + test + build < 5 phút
  ✓ Vitest: > 50 unit tests pass
  ✓ Playwright: 5 critical E2E pass
  ✓ TypeScript strict mode: 0 errors
```

---

## 18. CV Bullet Points & Interview Prep — FE

### CV Bullet Points (FE)

```
• Architected a micro-frontend system using Next.js 14 (App Router) and React 18
  across 3 independent deployable apps — a public ISR knowledge base portal
  (Lighthouse 90+, LCP < 2.5s), an SSR agent workspace with real-time collision
  detection, and a self-contained embeddable widget SDK (< 80KB gzip) — all sharing
  type-safe packages via Turborepo monorepo.

• Implemented TanStack Query with SSR hydration pattern to eliminate initial loading
  states in the agent workspace; integrated WebSocket (Socket.io) for live ticket
  updates and optimistic mutations with typed 409 version-conflict handling to
  preserve data integrity across concurrent agents.

• Engineered an embeddable React chat widget (no Next.js, Vite-built IIFE bundle)
  with session persistence, typing indicators, AI citation rendering, and
  confidence-based escalation UX — deployable on any third-party site via a
  single <script> tag.
```

---

### Câu Hỏi Phỏng Vấn FE & Câu Trả Lời Prepared

**Q: Micro-frontend của bạn khác gì so với Module Federation?**
A: Tôi dùng package-based sharing qua Turborepo — compile-time, type-safe, không có runtime dependency hell. Module Federation phù hợp khi nhiều team cần deploy feature independently mà không rebuild toàn bộ app. Với dự án solo trong 10 tuần, boundaries đã được thiết lập (portal/workspace/widget), code sharing qua packages là đủ và maintainable hơn. Migration path sang Module Federation rõ ràng khi cần thiết.

**Q: Tại sao widget-sdk dùng React thuần thay vì Next.js?**
A: Next.js không build ra single embeddable JS file — nó cần server runtime hoặc static export với nhiều files. Widget cần self-contained bundle < 80KB gzip, inject được vào bất kỳ website nào bằng 1 thẻ `<script>`. Vite build IIFE format cho phép control hoàn toàn bundle size. Next.js runtime overhead (~100KB+) không chấp nhận được cho use case này.

**Q: Tại sao portal dùng ISR thay vì SSG hoàn toàn hoặc SSR?**
A: SSG thuần: KB articles thay đổi thường xuyên (admin publish liên tục) nên không thể rebuild toàn bộ mỗi lần. SSR: TTFB cao vì phải fetch data mỗi request. ISR là compromise tốt nhất: page được build sẵn → TTFB thấp (CDN cache); on-demand revalidation khi article published → admin thấy thay đổi ngay lập tức; hourly fallback revalidation → không bao giờ quá stale.

**Q: Làm sao bạn handle 409 Conflict từ concurrent agent edits?**
A: Ticket detail page SSR trả về `rowVersion`. Khi agent submit reply, FE gửi kèm `expectedVersion`. Nếu backend trả 409 (version không khớp), FE: (1) hiển thị CollisionBanner với diff; (2) không overwrite editor content của agent; (3) update TanStack Query cache với `latestSnapshot` từ error response; (4) agent có thể review changes rồi retry. Redis advisory lock chỉ là UX signal "ai đó đang reply" — không phải source of truth, PostgreSQL row_version mới là guard cuối.

**Q: TanStack Query và WebSocket phối hợp thế nào?**
A: TanStack Query quản lý server state (fetch, cache, background refetch). WebSocket là real-time update channel. Khi WebSocket nhận event `ticket:update`, tôi gọi `queryClient.setQueryData` để push data mới vào cache — không cần refetch. Khi nhận `ticket:new`, tôi gọi `invalidateQueries(['tickets'])` để trigger background refetch list. Pattern này tránh polling nhưng vẫn consistent khi WebSocket disconnect.

**Q: SSR hydration pattern của bạn hoạt động thế nào?**
A: Server Component prefetch data vào QueryClient, serialize thành dehydrated state, truyền vào `HydrationBoundary`. Client component nhận state đã hydrate — TanStack Query có data sẵn ngay từ đầu, không có loading state ban đầu. Sau đó TanStack Query tự quản lý staleTime, background refetch, và mutations. Đây là pattern chính thức của Next.js + TanStack Query — tránh waterfall fetching và double fetch.

**Q: Widget bundle size 80KB kiểm soát thế nào?**
A: Build target là IIFE với Vite. Không externalize React (phải self-contained). Loại bỏ heavy dependencies: không TanStack Query, không Zustand, không Radix UI. Tailwind JIT + aggressive purge. Lazy load ChatWindow component (chỉ load khi user click open). Dùng `rollup-plugin-visualizer` sau mỗi build để check module breakdown. Hiện tại: React+DOM ~45KB, Socket.io client ~14KB, widget code ~15KB = ~74KB gzip.

**Q: Trong workspace app, bạn xử lý session timeout thế nào?**
A: Access token JWT 15 phút. api-client interceptor catch 401 → tự gọi POST /api/auth/refresh (refresh token trong httpOnly cookie) → nhận access token mới → retry original request. Nếu refresh cũng fail (cookie expired/revoked) → clear auth store → redirect /login. Pattern này transparent với component — component không cần biết token hết hạn.

---

## 19. Context Handoff

### Khi Mang File Này Sang Chat Window Mới

Paste file này vào context và dùng prompt:

> Đây là tài liệu source of truth frontend của project SignalDesk AI. Hãy dùng nó làm base và giúp tôi triển khai: **[ghi rõ task cụ thể]**.
>
> Context hiện tại: Tôi đang ở **Tuần [N]**, đã hoàn thành [liệt kê deliverables FE], đang implement [feature đang làm].

### Ví Dụ Task Prompt

> "Tôi đang implement ReplyEditor component trong workspace app. Component này cần: TanStack Query mutation gọi addTicketMessage API, gửi kèm expectedVersion từ ticket.rowVersion, xử lý 409 Conflict bằng cách show CollisionBanner với latestSnapshot. Tech: Next.js 14 App Router, TanStack Query v5, TypeScript strict mode, Tailwind + shadcn."

### Trạng Thái Hiện Tại

```
Phiên bản tài liệu: v1.0
Tuần hiện tại:      [ ]
Deliverables hoàn thành:
  - [ ]
Đang implement:
  - [ ]
Quyết định đã chốt:
  - Monorepo: Turborepo (package-based sharing, không Module Federation)
  - portal: Next.js 14 ISR
  - workspace: Next.js 14 SSR
  - widget-sdk: React 18 + Vite IIFE bundle
  - State: TanStack Query (server) + Zustand (UI) + Socket.io (real-time)
  - File BE tương ứng: SIGNALDESK_AI_BE.md
```

---

*Tài liệu này được duy trì bởi owner. Cập nhật `Trạng Thái Hiện Tại` khi bắt đầu mỗi tuần mới.*
