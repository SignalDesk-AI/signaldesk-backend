# SignalDesk AI - Frontend Source of Truth
## Practical Micro-Frontend Architecture with Next.js, React SPA, and Embeddable Widget

> This document is the frontend source of truth for SignalDesk AI.
> It replaces the old 3-app `workspace` model with a 4-surface architecture:
> `portal`, `admin-portal`, `agent-workspace`, and `customer-widget`.
>
> Version: `v2.1-fe-techstack-mobile-pwa`
> Backend source of truth: `SIGNALDESK_AI_BE_v6_0.md`

---

## Table of Contents

1. [Executive Summary](#1-executive-summary)
2. [Frontend Architecture Overview](#2-frontend-architecture-overview)
3. [Micro-Frontend Rationale](#3-micro-frontend-rationale)
4. [App Responsibilities](#4-app-responsibilities)
5. [Shared Packages](#5-shared-packages)
6. [Repository Structure](#6-repository-structure)
7. [Routing and Composition Strategy](#7-routing-and-composition-strategy)
8. [Auth and Tenant Flow](#8-auth-and-tenant-flow)
9. [Data Fetching Strategy](#9-data-fetching-strategy)
10. [State Management Strategy](#10-state-management-strategy)
11. [Realtime Frontend Architecture](#11-realtime-frontend-architecture)
12. [Performance Strategy](#12-performance-strategy)
13. [Testing Strategy](#13-testing-strategy)
14. [Monitoring and Observability](#14-monitoring-and-observability)
15. [Implementation Roadmap](#15-implementation-roadmap)
16. [Interview and Portfolio Relevance](#16-interview-and-portfolio-relevance)
17. [Definition of Done](#17-definition-of-done)
18. [Context Handoff](#18-context-handoff)

---

## 1. Executive Summary

SignalDesk AI frontend is a practical micro-frontend system built around four independently deployable surfaces. Each surface is separated by user intent, rendering model, runtime constraints, and deploy cadence.

| Surface | Framework | Rendering | Primary User | Purpose |
|---|---|---|---|---|
| `portal` | Next.js 16.x App Router + React 19.x | SSG / ISR / selective SSR | Public customer | Help center, KB pages, SEO, public search, support entry |
| `admin-portal` | Next.js 16.x App Router + React 19.x | SSR | Admin / Manager | Dashboard, KB management, campaign management, settings, roles, AI config |
| `agent-workspace` | React 19.x SPA + Vite | CSR only | Agent / Senior Agent | Ticket queue, reply flow, live chat, presence, locks, realtime-heavy handling |
| `customer-widget` | React 19.x + Vite | CSR embeddable | End customer on tenant site | Chat widget, quick KB search, create ticket, AI support popup |

This is not just "one React app" or "one Next.js app". The frontend architecture intentionally uses different rendering models for different product surfaces:

- Public KB content uses Next.js ISR for SEO, fast TTFB, and on-demand revalidation.
- Admin workflows use Next.js SSR because pages are auth-gated, tenant-specific, and benefit from server-side session checks.
- Agent work uses a React SPA because the experience is websocket-heavy, optimistic, long-lived, and does not need SEO or SSR.
- The customer widget is a separate embeddable micro-frontend because it must run safely inside third-party websites with minimal bundle weight and style isolation.

The system uses a Turborepo monorepo with compile-time shared packages. It deliberately avoids runtime micro-frontend complexity such as Module Federation at this stage. The boundaries are still real: every app builds and deploys independently, owns its routes, and communicates with other surfaces through backend APIs, websocket events, and shared typed contracts.

---

## 2. Frontend Architecture Overview

### 2.1 High-Level Diagram

```text
signaldesk-fe/
|
|-- apps/
|   |-- portal/             Next.js 16.x App Router
|   |                       Public help center, KB, SEO, ISR
|   |
|   |-- admin-portal/       Next.js 16.x App Router
|   |                       Admin console, dashboard, settings, SSR
|   |
|   |-- agent-workspace/    React 19.x + Vite SPA
|   |                       Ticket queue, live chat, optimistic updates
|   |
|   `-- customer-widget/    React 19.x + Vite embeddable bundle
|                           Script/web-component wrapper for external sites
|
`-- packages/
    |-- ui/
    |-- design-tokens/
    |-- api-client/
    |-- auth-sdk/
    |-- telemetry/
    |-- realtime/
    |-- types/
    |-- utils/
    `-- config/
```

### 2.2 Runtime Communication

```text
portal
  -> API Gateway: public KB articles, public search, tenant config
  -> No websocket by default

admin-portal
  -> API Gateway: tenant admin APIs, KB, campaigns, roles, analytics
  -> Optional websocket: notifications and dashboard refresh

agent-workspace
  -> API Gateway: tickets, customers, AI suggestions, KB search
  -> WebSocket Gateway: ticket updates, typing, presence, locks, live chat

customer-widget
  -> API Gateway: widget config, KB quick search, create ticket
  -> WebSocket Gateway: chat session after AI escalation or live handoff
```

There is no direct runtime import from one app into another. Apps share only internal packages at build time and product state through backend-owned APIs/events.

### 2.3 Deploy Unit Boundaries

Each app is independently buildable and deployable:

```text
portal             -> Docker or Vercel, served at help.signaldesk.ai
admin-portal       -> Docker or Vercel, served at admin.signaldesk.ai
agent-workspace    -> Static Vite build behind Nginx, served at app.signaldesk.ai
customer-widget    -> CDN script bundle, served at cdn.signaldesk.ai/widget.js
```

Deploy isolation matters because:

- Portal content and SEO improvements should not force an agent workspace deploy.
- Agent workspace can ship frequent UX/realtime changes without touching admin.
- Customer widget needs strict bundle-size checks and CDN cache versioning.
- Admin portal has lower release frequency but stronger permission and audit needs.

### 2.4 Baseline Stack and Version Policy

As of 2026-05-05, new implementation should start from current stable frontend defaults and pin exact versions in `package.json` during Phase 1.

| Area | Default Choice | Business Reason |
|---|---|---|
| Web framework | Next.js 16.x App Router | SEO, ISR, RSC, SSR auth checks, deployment flexibility |
| React runtime | React 19.x | Current React baseline for Server Components and modern concurrent patterns |
| SPA build tool | Vite | Fast local dev and simple static deploy for agent/workspace/widget |
| Routing for SPA | React Router 7, Declarative or Data Mode | Mature client routing without forcing SSR where it adds no value |
| Monorepo | pnpm + Turborepo | Shared packages, cacheable builds, one source of truth |
| Language | TypeScript strict | Safer API contracts, tenant-aware domain types, better refactoring |
| Styling | Tailwind CSS + CSS variables | Fast UI delivery with token-based tenant theming |
| UI primitives | shadcn/ui + Radix UI or Base UI | Accessible primitives without inventing low-level components |
| Icons | lucide-react | Consistent tool/action icons with small bundle cost |
| Server state | TanStack Query v5 | Cache, retries, optimistic updates, realtime cache patching |
| Client UI state | Zustand | Lightweight state for filters, panels, local session context |
| Forms | React Hook Form + Zod | Typed validation and predictable API error mapping |
| Tables/lists | TanStack Table + TanStack Virtual | Admin tables, queue filters, large ticket/customer lists |
| Testing | Vitest, React Testing Library, MSW, Playwright | Unit, integration, mocked API, and critical user flows |
| Observability | Sentry, Web Vitals, request IDs | Connect frontend failures to backend traces |
| Docs/UI review | Storybook for `packages/ui` | Shared component QA, visual review, reusable design contracts |

Version policy:

- Prefer latest stable versions at project creation, then pin exact app/package versions.
- Run dependency upgrades on a planned cadence, not randomly during feature work.
- Avoid experimental framework features unless they clearly improve a product goal.
- Document every major stack exception in this file before implementation.

### 2.5 Mobile, PWA, and Native App Decision

Default mobile strategy is responsive web plus PWA, not a native app on day one.

Use PWA when the business need is:

- faster mobile access for agents/admins without App Store release overhead.
- installable support portal or agent workspace shortcut.
- cached public KB content.
- preserving draft replies during flaky network.
- push-style notifications where web push support is acceptable for target users.

Use Expo / React Native only when the business need is clearly native:

- reliable native push notifications across iOS and Android.
- app-store distribution for customer or agent mobile workflows.
- deeper device APIs such as camera attachment flow, file picker, biometrics, contacts, or native sharing.
- offline-first agent workflows with background sync beyond normal browser limits.

If native is required later, add a fifth surface:

```text
apps/mobile-agent/      Expo SDK 55+ / React Native 0.83+ / React 19.x
```

Mobile decision by surface:

| Surface | Mobile Plan | Reason |
|---|---|---|
| `portal` | Responsive PWA | Public KB/search should be fast and installable |
| `admin-portal` | Responsive web first | Admin work is usually desktop/tablet; native app is not justified initially |
| `agent-workspace` | Responsive PWA for MVP, Expo only if mobile agents become core | Realtime queue is feasible on web, but native push/offline may justify Expo later |
| `customer-widget` | Mobile-responsive embedded widget | It runs inside tenant websites, not as a standalone app |

---

## 3. Micro-Frontend Rationale

### 3.1 Why Split by Surface, Rendering Model, and Deploy Unit

The split is based on product behavior, not arbitrary component boundaries.

| Surface | Why Separate |
|---|---|
| `portal` | Public, SEO-sensitive, content-heavy, cacheable |
| `admin-portal` | Authenticated, permission-heavy, management workflows, SSR-friendly |
| `agent-workspace` | Long-lived SPA, websocket-heavy, optimistic, no SEO need |
| `customer-widget` | Embedded in external sites, strict size/style/runtime isolation |

This gives clear ownership:

- Route ownership is explicit.
- Rendering strategy is explicit.
- Performance budgets are app-specific.
- Deploy cadence matches actual change frequency.
- Recruiters can see architectural intent instead of a flat UI app.

### 3.2 Why Not Split by Tiny Components

Splitting by small UI components would create the wrong coupling:

- A button, modal, or table is not a deploy boundary.
- Feature-level runtime micro-frontends would add coordination overhead too early.
- Shared UI components should be package-level dependencies, not separately deployed remotes.

This project uses package-based sharing because it is type-safe, tree-shakable, simpler to debug, and appropriate for a solo or small-team build.

### 3.3 Why Not Module Federation Initially

Module Federation is useful when multiple teams need runtime-independent feature deployments. SignalDesk AI does not need that complexity yet.

Current choice:

```text
Turborepo shared packages
  -> compile-time sharing
  -> type-safe imports
  -> one lockfile
  -> clear app boundaries
  -> lower operational risk
```

Future migration path remains possible because the app boundaries are already clean.

### 3.4 Why Next.js for `portal`

`portal` uses Next.js because it needs:

- SEO for public help articles.
- SSG/ISR for KB pages.
- Dynamic metadata, sitemap, robots, structured data.
- SSR for search pages when query params and tenant context matter.

### 3.5 Why Next.js for `admin-portal`

`admin-portal` uses Next.js because it benefits from:

- Server-side session checks.
- Role-based redirects before rendering.
- SSR dashboard and management pages.
- App Router layouts for authenticated admin shell.
- Secure httpOnly cookie handling.

### 3.6 Why React SPA for `agent-workspace`

`agent-workspace` uses React + Vite SPA because it is:

- Internal only, no SEO.
- Long-lived and websocket-heavy.
- Optimistic and interaction-heavy.
- Better served by persistent client state.
- Easier to keep responsive with React Router, TanStack Query, Zustand, and Socket.io.

SSR would add complexity without improving the core agent experience.

### 3.7 Why `customer-widget` Is Separate

The widget is not just a component. It is an embeddable product surface.

Requirements:

- Load via one script tag or web component wrapper.
- Run on external tenant websites.
- Avoid style leaks with Shadow DOM or scoped CSS.
- Keep gzip bundle budget under 80KB.
- Avoid Next.js runtime overhead.
- Support anonymous/visitor sessions.

Example embed:

```html
<script
  src="https://cdn.signaldesk.ai/widget/v1/widget.js"
  data-tenant="taskflow"
  data-public-key="pk_live_xxx"
  data-position="bottom-right">
</script>
```

---

## 4. App Responsibilities

### 4.1 `portal`

```text
Goal:
  Public help center and support entry point.

Users:
  Anonymous customers and search-engine crawlers.

Route ownership:
  /
  /help
  /help/[category]
  /help/[category]/[slug]
  /search
  /contact-support
  /ticket/[publicToken]

Rendering:
  Landing/docs index        -> SSG
  KB article pages          -> ISR with on-demand revalidation
  Search                    -> SSR or CSR depending search backend
  Public ticket status      -> SSR with public token validation

Data dependencies:
  Public KB API, public search API, tenant branding config.

Auth:
  No user auth by default.
  Public ticket status uses signed token or public lookup token.

Deploy:
  Docker or Vercel.
  Domain: help.signaldesk.ai or help.{tenant}.signaldesk.ai.
```

### 4.2 `admin-portal`

```text
Goal:
  Admin and manager console for operating the tenant.

Users:
  Admin, Manager.

Route ownership:
  /login
  /dashboard
  /kb
  /kb/new
  /kb/[id]/edit
  /campaigns
  /campaigns/new
  /analytics
  /customers
  /settings/team
  /settings/roles
  /settings/ai-config
  /settings/billing

Rendering:
  Next.js SSR for authenticated pages.
  Client components only for interactive editors, charts, and forms.

Data dependencies:
  Tenant API, admin API, KB API, campaign API, analytics API, role/permission API.

Auth:
  httpOnly access/refresh cookies.
  Next.js Proxy guards Admin/Manager role redirects.
  Permission-aware UI rendering inside pages.

Deploy:
  Docker or Vercel.
  Domain: admin.signaldesk.ai.
```

### 4.3 `agent-workspace`

```text
Goal:
  Internal agent cockpit for ticket handling and live support.

Users:
  Agent, Senior Agent.

Route ownership:
  /login
  /tickets
  /tickets/:id
  /queue
  /chat
  /chat/:sessionId
  /customers/:id
  /kb-search
  /notifications

Rendering:
  React SPA, CSR only.
  React Router 7 handles all in-app navigation.

Data dependencies:
  Ticket API, message API, customer API, AI suggestion API, KB search API.

Auth:
  Access token in memory.
  Refresh token in httpOnly cookie.
  Route guards in React.
  Permission-aware actions and UI.

Deploy:
  Vite static build behind Nginx or CDN.
  Domain: app.signaldesk.ai.

Realtime:
  Heavy websocket use: ticket updates, typing indicators, advisory locks,
  presence, chat timeline, notifications, reconnect recovery.
```

### 4.4 `customer-widget`

```text
Goal:
  Customer-facing embedded support widget for tenant websites.

Users:
  Anonymous or identified customers visiting tenant websites.

Route ownership:
  No public app routes.
  Internal widget views:
    closed bubble
    home
    KB quick search
    chat
    create ticket
    ticket submitted

Rendering:
  React CSR mounted into Shadow DOM or web component.

Data dependencies:
  Widget config API, public KB search API, AI chat API, ticket creation API.

Auth:
  Tenant public key.
  Visitor/session token issued by backend.
  Optional customer identity via signed JWT from tenant site.

Deploy:
  CDN versioned JS bundle.
  Example: cdn.signaldesk.ai/widget/v1/widget.js.
```

---

## 5. Shared Packages

Shared packages are compile-time dependencies inside the monorepo. They should provide reusable primitives, typed contracts, and infrastructure wrappers. They should not hide product-specific workflows.

### 5.1 Package List

```text
packages/
|-- ui/                Shared React components
|-- design-tokens/     Color, spacing, radius, typography, CSS variables
|-- api-client/        Typed HTTP client, API errors, request ID handling
|-- auth-sdk/          Auth helpers, permission checks, route guard primitives
|-- telemetry/         Sentry, Web Vitals, event logging, correlation IDs
|-- realtime/          Socket client, providers, presence, reconnect helpers
|-- types/             Shared TypeScript domain types
|-- utils/             Pure utilities only
`-- config/            TSConfig, ESLint, Tailwind, Vitest, Playwright presets
```

### 5.2 `packages/ui`

Contains generic design-system components:

```text
Button
Input
Textarea
Select
Dialog
DropdownMenu
Badge
Avatar
DataTable
EmptyState
LoadingSpinner
Toast
Tabs
Tooltip
ErrorBoundary
```

Rules:

- No API calls.
- No app routing imports.
- No tenant-specific copy.
- No direct websocket access.
- Accept data and callbacks via props.

### 5.3 `packages/design-tokens`

Source of truth for visual primitives.

```typescript
export const tokens = {
  color: {
    primary: 'hsl(234, 89%, 60%)',
    success: 'hsl(142, 71%, 45%)',
    warning: 'hsl(38, 92%, 50%)',
    danger: 'hsl(0, 84%, 60%)',
    neutral: {
      50: 'hsl(210, 40%, 98%)',
      900: 'hsl(222, 47%, 11%)',
    },
  },
  spacing: {
    xs: '4px',
    sm: '8px',
    md: '16px',
    lg: '24px',
    xl: '40px',
  },
  radius: {
    sm: '4px',
    md: '8px',
    lg: '12px',
  },
} as const;
```

### 5.4 `packages/api-client`

Responsibilities:

- Base URL resolution per app.
- JSON fetch wrapper.
- Access token injection where appropriate.
- Refresh retry for SPA.
- Typed error classes.
- Correlation ID extraction from `x-request-id`.
- Consistent `tenantId` propagation.

```typescript
export class ApiError extends Error {
  constructor(
    public status: number,
    message: string,
    public body?: unknown,
    public requestId?: string,
  ) {
    super(message);
  }
}
```

### 5.5 `packages/auth-sdk`

Contains shared auth logic:

- `hasPermission(user, permission)`
- `hasRole(user, role)`
- `PermissionGuard`
- token refresh helpers
- tenant context helpers
- login redirect helpers

Do not put app-specific sidebar logic here.

### 5.6 `packages/realtime`

Contains websocket infrastructure:

- `SocketProvider`
- `useSocket`
- `useTicketRoom`
- `usePresence`
- `useTypingIndicator`
- reconnect and room rejoin helpers
- event type definitions

Product-specific UI such as `CollisionBanner` stays in `agent-workspace`.

### 5.7 `packages/telemetry`

Contains:

- Sentry init wrapper.
- Web Vitals reporting.
- custom event tracking.
- `setCorrelationId`.
- error capture helpers.
- session replay defaults with PII masking.

### 5.8 What Should Not Be Shared Too Much

Avoid over-sharing:

- Do not put feature workflows into `packages/ui`.
- Do not share page-level components across apps.
- Do not let `customer-widget` import heavy admin/agent packages.
- Do not make one mega `shared` package.
- Do not import app code from packages.

Coupling rule:

```text
apps/* can import packages/*
packages/* must not import apps/*
customer-widget should import only lightweight packages
utils must remain pure and dependency-light
```

### 5.9 Design System and UX Standards

The frontend should use a practical design system, not a custom component library built from scratch.

Default stack:

```text
Tailwind CSS
CSS variables for tokens and tenant themes
shadcn/ui component patterns
Radix UI or Base UI primitives for accessible behavior
lucide-react icons
Storybook for shared UI review
```

Design-system ownership:

| Layer | Location | Responsibility |
|---|---|---|
| Tokens | `packages/design-tokens` | colors, spacing, radius, typography, shadows, z-index, motion |
| Primitives | `packages/ui` | Button, Input, Dialog, Menu, Toast, Table, Tabs, Tooltip |
| Feature components | `apps/*/components` | ticket cards, KB editor, campaign forms, agent panels |
| App layout | `apps/*/app` or `apps/*/src/layout` | navigation, sidebars, shells, route composition |

Component requirements:

- Every shared component must support `disabled`, `loading`, `error`, and keyboard states where relevant.
- Form components must expose validation text and API error mapping.
- Dialog, dropdown, tooltip, select, and tabs must be accessible by keyboard.
- Buttons use icons for common actions such as save, edit, delete, reply, refresh, search, filter, settings, and close.
- Data-heavy screens use dense layouts, predictable navigation, sticky headers where useful, and clear empty/error states.
- Do not put product-specific API calls, copy, or routing inside `packages/ui`.

Accessibility baseline:

```text
Target: WCAG 2.2 AA
Keyboard: all primary workflows usable without a mouse
Focus: visible focus ring, no focus traps outside modal intent
Color: contrast checked for light and dark themes
Motion: respect prefers-reduced-motion
Screen readers: semantic landmarks, labels, descriptions, and live regions for toasts/errors
```

Tenant theming:

- Use CSS variables for tenant color, logo, radius, and optional dark mode.
- Keep widget theming isolated inside Shadow DOM or scoped CSS.
- Never allow tenant themes to break contrast requirements.
- Store brand config from the backend as data, not hardcoded theme variants.

### 5.10 PWA and Offline Shared Contracts

PWA work must serve a product goal, not exist as a checkbox.

Shared PWA contracts:

```text
packages/pwa/
  manifest helpers
  service-worker registration helpers
  offline fallback constants
  cache naming/version helpers
  draft persistence helpers
```

Use service workers only for:

- static asset caching.
- public KB page/article caching.
- offline fallback screens.
- preserving unsent reply/contact/ticket drafts.
- background retry for safe idempotent mutations if backend supports idempotency keys.

Do not cache:

- admin permission payloads longer than the session requires.
- sensitive ticket details in persistent storage unless encrypted/explicitly approved.
- tenant-private data for another tenant under the same browser profile.

---

## 6. Repository Structure

### 6.1 Full Repo Structure

```text
signaldesk-fe/
|-- apps/
|   |-- portal/
|   |-- admin-portal/
|   |-- agent-workspace/
|   `-- customer-widget/
|
|-- packages/
|   |-- ui/
|   |-- design-tokens/
|   |-- api-client/
|   |-- auth-sdk/
|   |-- telemetry/
|   |-- realtime/
|   |-- types/
|   |-- utils/
|   `-- config/
|
|-- e2e/
|   |-- flows/
|   |-- fixtures/
|   `-- playwright.config.ts
|
|-- package.json
|-- pnpm-workspace.yaml
|-- turbo.json
|-- tsconfig.base.json
`-- README.md
```

### 6.2 `portal` Structure

```text
apps/portal/
|-- app/
|   |-- (public)/
|   |   |-- page.tsx
|   |   |-- help/
|   |   |   |-- page.tsx
|   |   |   |-- [category]/
|   |   |   |   |-- page.tsx
|   |   |   |   `-- [slug]/page.tsx
|   |   |-- search/page.tsx
|   |   `-- contact-support/page.tsx
|   |-- ticket/[publicToken]/page.tsx
|   |-- api/revalidate/route.ts
|   |-- sitemap.ts
|   |-- robots.ts
|   `-- layout.tsx
|-- components/
|   |-- ArticleCard.tsx
|   |-- SearchBox.tsx
|   |-- CategoryNav.tsx
|   `-- PublicTicketStatus.tsx
|-- lib/
|   |-- kb.ts
|   |-- tenant.ts
|   `-- seo.ts
`-- next.config.ts
```

### 6.3 `admin-portal` Structure

```text
apps/admin-portal/
|-- app/
|   |-- (auth)/
|   |   |-- login/page.tsx
|   |   `-- layout.tsx
|   |-- (app)/
|   |   |-- layout.tsx
|   |   |-- dashboard/page.tsx
|   |   |-- kb/
|   |   |   |-- page.tsx
|   |   |   |-- new/page.tsx
|   |   |   `-- [id]/edit/page.tsx
|   |   |-- campaigns/
|   |   |   |-- page.tsx
|   |   |   `-- new/page.tsx
|   |   |-- analytics/page.tsx
|   |   |-- customers/page.tsx
|   |   `-- settings/
|   |       |-- team/page.tsx
|   |       |-- roles/page.tsx
|   |       |-- ai-config/page.tsx
|   |       `-- billing/page.tsx
|   `-- api/session/route.ts
|-- components/
|   |-- dashboard/
|   |-- kb/
|   |-- campaigns/
|   |-- analytics/
|   `-- settings/
|-- lib/
|   |-- server-auth.ts
|   |-- permissions.ts
|   `-- query.ts
|-- proxy.ts
`-- next.config.ts
```

### 6.4 `agent-workspace` Structure

```text
apps/agent-workspace/
|-- index.html
|-- vite.config.ts
|-- src/
|   |-- main.tsx
|   |-- App.tsx
|   |-- routes/
|   |   |-- index.tsx
|   |   |-- ProtectedRoute.tsx
|   |   `-- RoleRoute.tsx
|   |-- pages/
|   |   |-- LoginPage.tsx
|   |   |-- TicketsPage.tsx
|   |   |-- TicketDetailPage.tsx
|   |   |-- QueuePage.tsx
|   |   |-- ChatPage.tsx
|   |   |-- ChatSessionPage.tsx
|   |   |-- CustomerPage.tsx
|   |   `-- KBSearchPage.tsx
|   |-- components/
|   |   |-- layout/
|   |   |-- ticket/
|   |   |-- chat/
|   |   |-- customer/
|   |   `-- ai/
|   |-- hooks/
|   |   |-- useTickets.ts
|   |   |-- useTicketDetail.ts
|   |   |-- useOptimisticReply.ts
|   |   |-- useAISuggest.ts
|   |   `-- useAgentPresence.ts
|   |-- store/
|   |   |-- authStore.ts
|   |   |-- ticketStore.ts
|   |   |-- chatStore.ts
|   |   `-- notificationStore.ts
|   |-- api/
|   |-- telemetry/
|   `-- test/
```

### 6.5 `customer-widget` Structure

```text
apps/customer-widget/
|-- index.html
|-- vite.config.ts
|-- src/
|   |-- embed/
|   |   |-- index.ts
|   |   |-- mount.tsx
|   |   `-- define-custom-element.ts
|   |-- WidgetApp.tsx
|   |-- views/
|   |   |-- BubbleView.tsx
|   |   |-- HomeView.tsx
|   |   |-- SearchView.tsx
|   |   |-- ChatView.tsx
|   |   `-- TicketCreatedView.tsx
|   |-- components/
|   |-- hooks/
|   |   |-- useWidgetSession.ts
|   |   |-- useWidgetConfig.ts
|   |   |-- useWidgetChat.ts
|   |   `-- useWidgetSearch.ts
|   |-- styles/
|   `-- types.ts
`-- package.json
```

### 6.6 Naming and Module Boundaries

Naming:

- React components: `PascalCase.tsx`
- hooks: `useSomething.ts`
- stores: `somethingStore.ts`
- API modules: `tickets.api.ts`, `kb.api.ts`
- feature folders: lowercase nouns, e.g. `ticket`, `chat`, `settings`

Module boundary rule:

```text
feature components can import shared UI and local hooks
feature hooks can import api-client and types
stores should not call APIs directly
pages compose features but should stay thin
packages must not import app-specific code
```

---

## 7. Routing and Composition Strategy

### 7.1 Domain Strategy

Prefer subdomain-based separation:

```text
help.signaldesk.ai       -> portal
admin.signaldesk.ai      -> admin-portal
app.signaldesk.ai        -> agent-workspace
cdn.signaldesk.ai        -> customer-widget assets
```

Tenant-specific aliases are optional:

```text
help.{tenant}.signaldesk.ai
support.{tenant-domain.com}
```

### 7.2 Nginx Mapping

```nginx
server {
  server_name help.signaldesk.ai;
  location / {
    proxy_pass http://portal:3000;
  }
}

server {
  server_name admin.signaldesk.ai;
  location / {
    proxy_pass http://admin-portal:3001;
  }
}

server {
  server_name app.signaldesk.ai;
  location / {
    proxy_pass http://agent-workspace:3002;
  }
}

server {
  server_name cdn.signaldesk.ai;
  location /widget/ {
    proxy_pass http://cdn-bucket;
    add_header Cache-Control "public, max-age=31536000, immutable";
  }
}
```

### 7.3 Hard Navigation vs Soft Navigation

Hard navigation:

- Moving between `portal`, `admin-portal`, and `agent-workspace`.
- Login redirect based on role.
- Opening public help article from admin preview.

Soft navigation:

- Within `portal`: Next.js App Router.
- Within `admin-portal`: Next.js App Router.
- Within `agent-workspace`: React Router 7.
- Within `customer-widget`: internal view state, not browser routes.

### 7.4 Role-Based Entry

```text
After login:
  Admin / Manager -> https://admin.signaldesk.ai/dashboard
  Agent           -> https://app.signaldesk.ai/tickets

If Agent opens admin domain:
  redirect to app.signaldesk.ai/tickets

If Admin opens app domain:
  allow only if user also has agent permission, otherwise redirect to admin
```

### 7.5 Widget Mounting

Script bundle:

```html
<script src="https://cdn.signaldesk.ai/widget/v1/widget.js"
        data-tenant="taskflow"
        data-public-key="pk_live_xxx">
</script>
```

Web component option:

```html
<signaldesk-widget
  tenant="taskflow"
  public-key="pk_live_xxx">
</signaldesk-widget>
```

The widget mounts into Shadow DOM by default to prevent CSS collisions with the host site.

---

## 8. Auth and Tenant Flow

### 8.1 Shared Concepts

Every authenticated app request carries:

```text
tenantId
userId
role
permissions
correlationId
```

Tenant context is resolved from:

- authenticated session for admin/agent apps.
- subdomain or configured host for portal.
- public widget key for customer-widget.

### 8.2 `admin-portal` Auth

`admin-portal` uses httpOnly cookies and Next.js Proxy.

```typescript
export async function proxy(request: NextRequest) {
  const token = request.cookies.get('access_token')?.value;
  if (!token) return NextResponse.redirect(new URL('/login', request.url));

  const payload = await verifyJWT(token);
  if (!['admin', 'manager'].includes(payload.role)) {
    return NextResponse.redirect(new URL('/unauthorized', request.url));
  }

  const headers = new Headers(request.headers);
  headers.set('x-tenant-id', payload.tenantId);
  headers.set('x-user-role', payload.role);

  return NextResponse.next({ request: { headers } });
}
```

In Next.js 16+, use `proxy.ts` for route-level request checks and redirects. Do not use Proxy for slow data fetching or full authorization logic. Backend authorization remains the source of truth.

### 8.3 `agent-workspace` Auth

SPA auth pattern:

```text
1. POST /api/auth/login
2. Backend returns accessToken in JSON and sets refresh_token as httpOnly cookie
3. Store accessToken in memory only
4. API calls send Authorization: Bearer <accessToken>
5. On 401, call /api/auth/refresh with credentials: include
6. Retry original request
7. If refresh fails, clear memory state and redirect to /login
```

Do not store access tokens in `localStorage`.

### 8.4 Permission-Aware UI

Examples:

```typescript
const canManageRoles = hasPermission(user, 'settings.roles.write');
const canPublishKb = hasPermission(user, 'kb.article.publish');
const canAssignTicket = hasPermission(user, 'ticket.assign');
```

Permission-aware UI hides or disables actions, but backend authorization remains the source of truth.

### 8.5 Widget Auth

The widget does not use internal user JWTs.

Widget auth uses:

- tenant public key.
- backend-issued visitor session token.
- optional signed customer identity token from tenant website.

```typescript
type WidgetIdentity = {
  tenantId: string;
  visitorId: string;
  customerId?: string;
  sessionToken: string;
};
```

### 8.6 Auth.js / NextAuth.js Decision

Default SignalDesk auth is backend-owned because the backend already owns tenants, roles, refresh tokens, audit logs, and API Gateway authorization.

Use custom backend auth when:

- login is email/password or enterprise account managed by SignalDesk.
- access and refresh tokens are issued by the backend.
- tenant isolation and role permissions are enforced by backend services.
- FE only needs session checks, redirects, and permission-aware rendering.

Use Auth.js only when:

- OAuth/social login is required.
- SAML/OIDC enterprise login is required and Auth.js reduces integration work.
- the backend agrees on session ownership and token exchange boundaries.

If Auth.js is used, keep it inside `admin-portal` or a dedicated BFF route layer. Do not let it replace backend authorization checks.

---

## 9. Data Fetching Strategy

### 9.1 Tools by App

| App | Tooling | Reason |
|---|---|---|
| `portal` | Next.js fetch, ISR, server components, selective CSR | SEO and cacheability |
| `admin-portal` | Server fetch + Server Actions for simple forms + TanStack Query for interactive mutations | SSR shell with interactive forms |
| `agent-workspace` | TanStack Query v5 | SPA server state, cache, optimistic updates |
| `customer-widget` | Lightweight fetch hooks | Bundle size and simple flow |

### 9.2 Portal Fetching

```text
/help/[category]/[slug]
  -> fetch article on server
  -> render static article HTML
  -> revalidate every 3600 seconds
  -> on-demand revalidate after publish

/search
  -> dynamic query
  -> SSR for initial results or CSR for typeahead
```

### 9.3 Admin Fetching

Admin pages fetch initial data on the server when it improves UX:

```text
/dashboard       -> SSR summary cards
/kb              -> SSR article list
/settings/roles  -> SSR role matrix
```

Client components handle:

- form mutations.
- optimistic local field state.
- chart interaction.
- table filters.

### 9.4 Server Actions Decision

Server Actions are useful only when they simplify a concrete business workflow.

Use Server Actions in `admin-portal` for:

- login or logout if it is handled by a Next.js BFF layer.
- create/update/publish KB article forms.
- team invitation and role update forms.
- tenant settings and AI configuration forms.
- small mutations where progressive enhancement improves reliability.

Do not use Server Actions for:

- `agent-workspace`, because it is a Vite SPA calling the API Gateway.
- `customer-widget`, because it is an embedded bundle on third-party sites.
- realtime ticket reply flows, because TanStack Query mutations and websocket events provide better optimistic UX.
- long-running AI operations, because they need explicit status, cancellation, retries, and telemetry.

Rule:

```text
Server Actions are allowed for admin form mutations.
TanStack Query remains the default for interactive, optimistic, realtime, or cross-surface server state.
```

### 9.5 Agent Fetching

`agent-workspace` uses TanStack Query as the server-state source of truth.

```typescript
useQuery({
  queryKey: ['tickets', filters],
  queryFn: () => ticketsApi.list(filters),
  staleTime: 30_000,
  refetchOnWindowFocus: true,
});

useQuery({
  queryKey: ['ticket', ticketId],
  queryFn: () => ticketsApi.detail(ticketId),
  staleTime: 300_000,
});
```

Optimistic reply:

```typescript
useMutation({
  mutationFn: sendReply,
  onMutate: async (input) => {
    await queryClient.cancelQueries({ queryKey: ['ticket', input.ticketId] });
    const previous = queryClient.getQueryData(['ticket', input.ticketId]);

    queryClient.setQueryData(['ticket', input.ticketId], (old: Ticket) => ({
      ...old,
      messages: [...old.messages, createOptimisticMessage(input)],
    }));

    return { previous };
  },
  onError: (error, input, context) => {
    queryClient.setQueryData(['ticket', input.ticketId], context?.previous);
  },
  onSettled: (_data, _error, input) => {
    queryClient.invalidateQueries({ queryKey: ['ticket', input.ticketId] });
  },
});
```

### 9.6 API Gateway vs Direct Backend

Default:

```text
frontend apps -> API Gateway -> services
```

Direct backend calls are avoided from browser apps. The gateway owns:

- tenant routing.
- auth validation.
- rate limits.
- request IDs.
- consistent error shapes.

Only server-side Next.js code may call internal service URLs when deployed in the same private network.

### 9.7 Cache and Invalidation Policies

```text
KB article public page:
  ISR 1 hour + on-demand revalidate

Admin KB list:
  staleTime 60s, invalidate after create/update/publish

Ticket list:
  staleTime 30s, invalidate on ticket:new or filter change

Ticket detail:
  staleTime 5min, patch cache from websocket events

AI suggestion:
  manual trigger, staleTime 5min, no auto-fetch

Widget config:
  cache in session memory, re-fetch on widget boot
```

### 9.8 SEO, Metadata, Open Graph, and Structured Data

SEO work belongs mainly to `portal`. Admin, agent, and widget surfaces do not need public SEO.

Portal SEO requirements:

- Use `generateMetadata` for category and article pages.
- Include canonical URL per article.
- Include Open Graph title, description, image, type, URL, and site name.
- Include Twitter card metadata for shared KB articles.
- Generate `sitemap.ts` from published KB articles and public categories.
- Generate `robots.ts` with clear public/private crawl rules.
- Add JSON-LD for KB articles, breadcrumbs, and organization where relevant.
- Use `opengraph-image.tsx` only if dynamic article images provide clear sharing value.

Example metadata contract:

```typescript
type ArticleSeo = {
  title: string;
  description: string;
  canonicalUrl: string;
  ogImageUrl: string;
  publishedAt: string;
  updatedAt: string;
  locale: string;
};
```

### 9.9 i18n Decision

i18n is optional until the product has multi-language tenants or public KB content in more than one language.

If needed:

- Use `next-intl` for `portal` and `admin-portal`.
- Use route prefixes such as `/en/help`, `/vi/help`, `/th/help`.
- Store localized KB title, slug, summary, and article body in backend content models.
- Generate locale-aware metadata, canonical URLs, alternate links, and sitemap entries.
- Keep `agent-workspace` translation files lightweight because it is not SEO-sensitive.
- Keep `customer-widget` locale derived from tenant config, browser language, or explicit embed option.

Do not add i18n just to demonstrate a library. Add it when tenant/customer language support is a product requirement.

---

## 10. State Management Strategy

### 10.1 State Types

| State Type | Tool | Examples |
|---|---|---|
| Local UI state | React `useState` | modal open, active tab, input draft |
| Server state | TanStack Query | tickets, KB articles, customers, analytics |
| Form state | React Hook Form + Zod | login, KB editor metadata, campaign forms |
| Realtime state | Socket.io + Query cache patching | messages, locks, presence, typing |
| Ephemeral state | Zustand or component state | selected ticket, filters, panel open |
| Long-lived session state | Auth store + refresh flow | user, tenant, permissions |

### 10.2 Zustand Usage

Use Zustand only for client UI/session state:

```typescript
type TicketUIState = {
  selectedTicketId: string | null;
  filters: TicketFilters;
  aiPanelOpen: boolean;
  setFilters: (filters: TicketFilters) => void;
};
```

Do not duplicate server state in Zustand. Server data belongs in TanStack Query.

### 10.3 Form State

Use React Hook Form + Zod for:

- login form.
- KB article editor metadata.
- campaign builder.
- role editor.
- AI config form.
- ticket reply validation when needed.

### 10.4 Widget State

The widget intentionally avoids heavy state libraries.

Use:

- local React state.
- `sessionStorage` for widget session ID.
- lightweight custom hooks.
- no TanStack Query unless the widget grows substantially.

---

## 11. Realtime Frontend Architecture

### 11.1 WebSocket Connection

`agent-workspace` establishes one authenticated socket connection per browser session.

```typescript
const socket = io(WS_URL, {
  transports: ['websocket'],
  auth: { token: accessToken, tenantId },
  reconnection: true,
  reconnectionAttempts: Infinity,
  reconnectionDelay: 500,
  reconnectionDelayMax: 5000,
});
```

### 11.2 Rooms

```text
tenant:{tenantId}
agent:{agentId}
ticket:{ticketId}
chat:{sessionId}
queue:{queueId}
```

### 11.3 Events

```text
ticket:new
ticket:update
ticket:assigned
ticket:resolved
ticket:message_added
ticket:lock_acquired
ticket:lock_released
ticket:typing

chat:message
chat:typing
chat:escalated

presence:update
notification:new
```

### 11.4 Ticket Updates

Use websocket events to patch query cache:

```typescript
socket.on('ticket:update', (event) => {
  queryClient.setQueryData(['ticket', event.ticketId], event.ticket);
  queryClient.invalidateQueries({ queryKey: ['tickets'] });
});
```

### 11.5 Typing Indicators

Typing indicators are ephemeral:

- Debounce outgoing typing events.
- Expire remote typing state after 3-5 seconds.
- Never persist typing state in TanStack Query.

### 11.6 Presence

Presence is stored in a small realtime store:

```typescript
type PresenceState = {
  agents: Record<string, {
    status: 'online' | 'away' | 'offline';
    activeTicketId?: string;
    lastSeenAt: string;
  }>;
};
```

### 11.7 Advisory Locks

Advisory locks improve UX but are not the source of truth.

UX:

- Show "Alex is replying" banner.
- Warn before sending if another agent has lock.
- Allow override for senior agents if product requires.

Data integrity:

- Backend row/version check remains the final guard.
- FE sends `expectedVersion` with reply mutations.
- On 409, show conflict banner and refresh latest ticket snapshot.

### 11.8 Reconnect and Recovery

On reconnect:

```text
1. Refresh access token if needed.
2. Reconnect socket.
3. Rejoin active rooms.
4. Refetch active ticket detail.
5. Refetch queue summary.
6. Show subtle "Reconnected" toast if outage was visible.
```

---

## 12. Performance Strategy

### 12.1 Performance Budgets

```text
portal:
  Lighthouse >= 90
  LCP < 2.5s
  CLS < 0.1
  INP < 200ms

admin-portal:
  route JS < 250KB gzip for normal pages
  heavy charts lazy-loaded
  editor lazy-loaded

agent-workspace:
  initial JS < 300KB gzip
  ticket queue interaction INP < 200ms
  virtualize lists over 100 rows

customer-widget:
  bootstrap < 20KB gzip
  full widget < 80KB gzip
  no large UI libraries
```

### 12.2 Code Splitting

Use route-level splitting:

- Next.js App Router for `portal` and `admin-portal`.
- React Router lazy routes for `agent-workspace`.
- Lazy-load full widget panel after user opens the bubble.

```typescript
const TicketDetailPage = lazy(() => import('../pages/TicketDetailPage'));
const AnalyticsPage = lazy(() => import('../pages/AnalyticsPage'));
```

### 12.3 Heavy Component Boundaries

Lazy-load:

- TipTap editor.
- Recharts dashboards.
- campaign segment builder.
- AI suggestion panel if expensive.
- full widget chat view.

### 12.4 Hydration Cost

Portal:

- Prefer server-rendered article HTML.
- Avoid hydrating static article body.
- Hydrate only search box, feedback buttons, and widget mount.

Admin:

- Keep server components for layout and data fetch.
- Use client components only where interaction is required.

Agent:

- No hydration cost because it is a SPA, but bundle size and runtime interaction cost matter.

### 12.5 Virtualization

Use TanStack Virtual for:

- ticket queue.
- chat timeline when long.
- customer tables.
- audit logs.

### 12.6 Widget Bundle Size Thinking

Widget must avoid:

- full design system import.
- chart libraries.
- rich text editor.
- heavy date libraries.
- TanStack Query unless justified.

Use Rollup/Vite visualizer in CI:

```text
alert if widget gzip > 80KB
warn if widget gzip > 60KB
```

### 12.7 PWA and Offline Performance

PWA should be added only where it improves mobile reliability.

Portal PWA:

- cache app shell and static assets.
- cache recently viewed public KB articles.
- show an offline fallback with cached article links.
- keep Lighthouse PWA checks passing.

Agent PWA:

- preserve unsent reply drafts in browser storage.
- show explicit offline/reconnecting status.
- disable unsafe mutations while offline unless idempotency keys and retry logic exist.
- refetch active queue and ticket detail after reconnect.

Admin PWA:

- installable shell is optional.
- do not persist sensitive settings or permission data beyond normal browser/session behavior.

Widget:

- no full PWA behavior.
- preserve widget session and unsent chat draft where safe.
- never register a service worker from the widget on a tenant website.

---

## 13. Testing Strategy

### 13.1 Test Pyramid

```text
E2E tests:
  Playwright, 8-12 critical flows

Integration tests:
  React Testing Library + MSW

Unit tests:
  Vitest, pure functions, hooks, permission logic
```

### 13.2 Unit Tests

Targets:

- `packages/utils`
- `packages/auth-sdk`
- `packages/api-client`
- permission helpers
- query key helpers
- formatting utilities

Example:

```typescript
test('hasPermission returns true for direct permission', () => {
  expect(hasPermission(user, 'ticket.reply')).toBe(true);
});
```

### 13.3 Component and Integration Tests

Use React Testing Library + MSW for:

- ticket list filtering.
- reply editor optimistic updates.
- 409 conflict rendering.
- KB publish form.
- permission-aware buttons.
- widget chat flow.

### 13.4 Playwright Critical Flows

Required E2E flows:

```text
1. agent: login -> ticket list -> open detail -> send reply
2. agent: 409 conflict -> rollback -> conflict banner -> refresh
3. agent: live chat escalation -> reply -> customer receives message
4. admin: login -> KB management -> publish article
5. portal: KB article SEO metadata and JSON-LD
6. portal: on-demand ISR after article publish
7. widget: load on external test page -> KB search -> create ticket
8. tenant isolation: user from tenant A cannot see tenant B data
9. permissions: manager/admin actions appear only for allowed roles
10. realtime: reconnect -> rejoin room -> active ticket refreshes
```

### 13.5 Mock API

Use MSW for browser-level tests:

```typescript
export const handlers = [
  http.get('/api/tickets', () => HttpResponse.json(mockTicketList)),
  http.post('/api/tickets/:id/messages', () => HttpResponse.json(mockMessage)),
  http.post('/api/auth/login', () => HttpResponse.json(mockLoginResponse)),
];
```

### 13.6 Test Organization

```text
apps/agent-workspace/src/__tests__/
apps/admin-portal/src/__tests__/
apps/customer-widget/src/__tests__/
packages/*/src/__tests__/
e2e/flows/
e2e/fixtures/
```

### 13.7 Accessibility, Visual, and Design-System Tests

Frontend quality includes visual and accessibility checks, not only business logic tests.

Required checks:

- Storybook stories for shared `packages/ui` components.
- keyboard navigation tests for dialog, menu, select, tabs, ticket reply, and KB editor flows.
- accessibility checks with Playwright or axe for critical screens.
- visual review for light theme, dark theme, and one tenant-branded theme.
- mobile viewport screenshots for portal article, widget, ticket queue, and admin dashboard.
- reduced-motion check for animated UI.

Do not ship a shared UI component without states for default, hover, focus, disabled, loading, error, and empty where relevant.

---

## 14. Monitoring and Observability

### 14.1 Telemetry Package

`packages/telemetry` wraps Sentry, Web Vitals, custom events, and correlation IDs.

```typescript
export function initTelemetry(options: {
  dsn: string;
  app: 'portal' | 'admin-portal' | 'agent-workspace' | 'customer-widget';
  environment: 'production' | 'staging' | 'development';
  release?: string;
}) {
  Sentry.init({
    dsn: options.dsn,
    environment: options.environment,
    release: options.release,
    tracesSampleRate: 0.1,
    replaysSessionSampleRate: 0.01,
    replaysOnErrorSampleRate: 1.0,
    integrations: [
      Sentry.replayIntegration({
        maskAllText: true,
        blockAllMedia: false,
      }),
    ],
  });

  Sentry.setTag('app', options.app);
}
```

### 14.2 Web Vitals

Track:

- LCP
- INP
- CLS
- FCP
- TTFB

Portal and widget metrics are especially important because they affect public users.

### 14.3 Error Tracking

Capture:

- unhandled JS errors.
- React error boundary crashes.
- failed route loading.
- API errors with endpoint and status.
- websocket reconnect loops.
- widget mount failures on host websites.

### 14.4 Correlation ID Propagation

API Gateway returns `x-request-id`. The FE stores it in telemetry scope:

```typescript
const requestId = response.headers.get('x-request-id');
if (requestId) setCorrelationId(requestId);
```

This allows debugging:

```text
Sentry FE error -> correlation_id -> backend logs -> service trace
```

### 14.5 Product Events

Track recruiter-friendly product metrics:

```text
ticket_opened
reply_sent
conflict_encountered
ai_suggest_triggered
ai_suggest_accepted
ai_suggest_dismissed
widget_opened
widget_escalated
search_performed
kb_article_published
```

### 14.6 Observability Dashboard

| Metric | Target | Alert |
|---|---:|---:|
| Portal LCP | < 2.5s | > 3s |
| Portal CLS | < 0.1 | > 0.25 |
| Agent INP | < 200ms | > 500ms |
| JS error rate | < 0.1% | > 0.5% |
| API error rate from FE | < 1% | > 5% |
| 409 conflict rate | baseline | 3x baseline |
| WebSocket reconnect rate | < 1/session | > 5/session |
| Widget full gzip size | < 80KB | > 80KB |
| AI suggestion acceptance | tracked | no hard alert |

---

## 15. Implementation Roadmap

### Phase 1 - Monorepo and Shared Foundation

Deliver:

- Turborepo + pnpm workspace.
- `apps/portal`, `apps/admin-portal`, `apps/agent-workspace`, `apps/customer-widget`.
- Next.js 16.x, React 19.x, Vite, TypeScript strict.
- shared TSConfig, ESLint, Tailwind, Vitest.
- `packages/types`, `packages/ui`, `packages/design-tokens`.
- shadcn/ui + Radix/Base UI component foundation.
- Storybook for shared UI states.
- CI: lint, typecheck, test, build.

### Phase 2 - Auth and App Shells

Deliver:

- Admin login + Next.js Proxy route checks.
- Agent login + SPA refresh flow.
- Role-based redirect after login.
- Permission guards.
- Tenant context propagation.
- App shells for admin and agent.

### Phase 3 - Portal

Deliver:

- Public help center landing.
- Category and article pages.
- ISR article pages.
- on-demand revalidation endpoint.
- metadata, Open Graph, Twitter card, canonical URL, sitemap, robots, JSON-LD.
- public search entry.
- responsive PWA shell and offline fallback for cached public KB articles.
- locale-ready route/content model if multi-language support is required.

### Phase 4 - Admin Portal

Deliver:

- dashboard overview.
- KB management.
- campaign management.
- team and role settings.
- AI config page.
- analytics page with lazy-loaded charts.
- Server Actions for simple admin form mutations where they reduce client complexity.

### Phase 5 - Agent Workspace

Deliver:

- ticket queue with filters and virtualization.
- ticket detail timeline.
- reply editor.
- optimistic reply mutation.
- 409 conflict handling.
- AI suggestion panel.
- KB search for agents.
- PWA draft preservation and explicit offline/reconnecting UX if mobile agent use is prioritized.

### Phase 6 - Realtime

Deliver:

- socket provider.
- ticket rooms.
- typing indicators.
- presence.
- advisory locks.
- notifications.
- reconnect and recovery behavior.

### Phase 7 - Customer Widget

Deliver:

- script bootstrap.
- Shadow DOM mount.
- bubble and full panel.
- quick KB search.
- AI chat flow.
- create ticket flow.
- escalation to live agent.
- widget bundle analyzer and CI budget.

### Phase 8 - Testing and Hardening

Deliver:

- MSW integration tests.
- Playwright critical flows.
- Sentry integration.
- Web Vitals reporting.
- Lighthouse CI for portal.
- accessibility pass.
- Storybook visual review.
- mobile viewport checks.
- PWA checks for surfaces where PWA is enabled.
- performance budget enforcement.

### Phase 9 - Portfolio Polish

Deliver:

- architecture diagram in README.
- screenshots.
- demo script.
- recruiter-facing bullet points.
- interview Q&A.
- deployment URLs.

---

## 16. Interview and Portfolio Relevance

### 16.1 What Impresses Recruiters

SignalDesk AI frontend demonstrates:

- intentional micro-frontend boundaries.
- correct rendering model selection.
- real-world auth and tenant propagation.
- websocket-heavy collaboration UX.
- optimistic updates with conflict handling.
- embeddable widget engineering.
- performance budgets and observability.
- test strategy across unit, integration, and E2E.

### 16.2 Demo Scenarios

Strong demo sequence:

```text
1. Open portal KB article and show SEO metadata / fast load.
2. Admin publishes article and triggers ISR revalidation.
3. Customer opens widget on external demo site and asks AI question.
4. Low-confidence answer escalates to live agent.
5. Agent sees live chat/ticket appear in workspace.
6. Two agents open same ticket and advisory lock/conflict UX appears.
7. Admin dashboard shows ticket/AI metrics.
8. Sentry/Web Vitals dashboard shows frontend observability.
```

### 16.3 CV Bullets

```text
- Architected a 4-surface frontend system for SignalDesk AI using Next.js 16,
  React 19, Vite, and Turborepo, separating public SEO pages, admin SSR workflows,
  realtime agent operations, and an embeddable customer widget into independent
  deployable units.

- Built a realtime agent workspace with TanStack Query, Zustand, Socket.io,
  optimistic reply mutations, presence, typing indicators, advisory locks, and
  typed 409 conflict recovery for concurrent ticket handling.

- Engineered a lightweight React customer widget deployable through a single
  script tag, with Shadow DOM style isolation, KB quick search, AI support flow,
  ticket creation, and live escalation while maintaining a strict bundle budget.

- Implemented frontend observability with Sentry, Web Vitals, request correlation
  IDs, custom product events, and session replay masking to connect browser errors
  to backend traces.
```

### 16.4 Interview Questions to Prepare

Likely questions:

```text
1. Why split admin and agent into separate apps?
2. Why is agent-workspace a SPA instead of Next.js?
3. Why use ISR for the public portal?
4. How does the widget avoid CSS conflicts on host websites?
5. How do TanStack Query and WebSocket events work together?
6. How do you handle concurrent agent edits?
7. How does auth differ between Next.js admin and React SPA agent app?
8. Why avoid Module Federation?
9. How do you enforce tenant isolation on the frontend?
10. What frontend metrics would you monitor in production?
```

Prepared answer for the most important one:

```text
I split by surface, rendering model, and deploy unit. Portal needs SEO and ISR,
admin benefits from SSR and server-side auth checks, agent-workspace is a long-lived
realtime SPA with no SEO need, and the widget must be a lightweight embedded bundle.
That separation gives practical micro-frontend boundaries without the runtime
complexity of Module Federation.
```

---

## 17. Definition of Done

### Portal

```text
[ ] KB pages use ISR
[ ] article publish triggers on-demand revalidation
[ ] metadata, Open Graph, Twitter card, canonical URL, sitemap, robots, JSON-LD implemented
[ ] Lighthouse >= 90
[ ] LCP < 2.5s
[ ] public search works
[ ] PWA offline fallback works if portal PWA is enabled
[ ] locale-aware routes/metadata work if i18n is enabled
```

### Admin Portal

```text
[ ] login and SSR auth guard work
[ ] Admin/Manager role guard works
[ ] dashboard loads server data
[ ] KB management works
[ ] campaign management works
[ ] settings/team/roles/AI config work
[ ] permission-aware UI implemented
[ ] Server Actions used only where they simplify admin form mutations
[ ] Next.js Proxy redirects unauthorized users before rendering protected routes
```

### Agent Workspace

```text
[ ] SPA login and refresh flow work
[ ] ticket queue supports filters and virtualization
[ ] ticket detail patches from websocket events
[ ] reply flow supports optimistic updates
[ ] 409 conflict UX works
[ ] typing, presence, locks, notifications work
[ ] reconnect recovery refetches active data
[ ] offline/reconnecting status is visible
[ ] unsent reply drafts are preserved if mobile/PWA agent use is enabled
```

### Customer Widget

```text
[ ] loads by one script tag
[ ] mounts in Shadow DOM or equivalent isolation
[ ] bundle gzip < 80KB
[ ] quick KB search works
[ ] AI support flow works
[ ] create ticket works
[ ] escalation to live agent works
[ ] mobile host-page layout does not break widget interaction
[ ] widget does not register a service worker on tenant websites
```

### Shared and Quality

```text
[ ] TypeScript strict mode passes
[ ] lint passes
[ ] unit tests pass
[ ] integration tests pass
[ ] Playwright critical flows pass
[ ] Storybook shared UI states exist
[ ] accessibility checks pass for critical flows
[ ] mobile viewport checks pass
[ ] Sentry captures app-specific errors
[ ] Web Vitals reported
[ ] correlation IDs propagated
[ ] exact frontend framework/library versions are pinned
```

---

## 18. Context Handoff

When starting a new chat window, paste this:

```text
This is the frontend source of truth for SignalDesk AI.
Use it as the base for all frontend implementation decisions.

Current architecture:
- portal: Next.js 16 App Router, public help center, SSG/ISR/SEO
- admin-portal: Next.js 16 App Router, authenticated admin/manager console, SSR
- agent-workspace: React 19 + Vite SPA, realtime ticket handling
- customer-widget: React 19 embeddable micro-frontend, CDN script/web component

Current frontend baseline:
- Next.js 16.x App Router + React 19.x
- Vite for SPA/widget surfaces
- TanStack Query v5, Zustand, React Hook Form, Zod
- Tailwind CSS, shadcn/ui patterns, Radix/Base UI primitives, lucide-react
- PWA first for mobile unless native Expo requirements become clear

Shared packages:
ui, design-tokens, api-client, auth-sdk, telemetry, realtime, types, utils, config.

Current task:
[describe the concrete implementation task here]
```

Current project status:

```text
Version: v2.0-fe-merged
Current phase: [ ]
Completed:
  - [ ]
In progress:
  - [ ]
Open decisions:
  - [ ]
```
