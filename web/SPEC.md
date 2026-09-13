# TradeLedger Web — Implementation Spec

## Summary

A React + TypeScript single-page application that puts a usable face on the
existing TradeLedger API: an equity dashboard, the trade journal with a
one-at-a-time review queue for the subjective half, analytics breakdowns, and
management screens for portfolio, plans, sync and settings.

The backend is already built and is treated as fixed. Exactly one backend change
is required for this cycle — a CORS policy — and it is specified separately in
[`docs/backend-changes-for-web.md`](../docs/backend-changes-for-web.md). That
file must be implemented **before** this one.

## Context

The API lives in `src/TradeLedger.Api` and exposes 13 route groups under `/api`.
This spec consumes 9 of them. The frontend is a new top-level `web/` directory,
a sibling of `src/` and `tests/`, with its own toolchain; it is not part of
`TradeLedger.slnx`.

What already exists and this spec depends on:

| Fact | Where |
|---|---|
| JWT bearer auth, 12h lifetime, no refresh token, no signup | `src/TradeLedger.Api/Shared/JwtTokenService.cs:19` |
| Enums serialised as strings both directions | `src/TradeLedger.Api/Program.cs:87` |
| `ProblemDetails` with `code`, `traceId`, optional `errors` | `src/TradeLedger.Api/Shared/Errors/ApiProblem.cs` |
| OpenAPI 3.1 document at `/openapi/v1.json`, **Development only** | `src/TradeLedger.Api/Program.cs:101` |
| `decimal` serialised as JSON numbers | default `System.Text.Json` behaviour |
| Equity comes from `BalanceSnapshot`, never from summing trades | `CLAUDE.md` §3 |

## Goals

- The trader can clear the review backlog quickly — the post-market loop of
  turning closed positions into journal rows is the product's reason to exist.
- Every number the dashboard shows comes from the server; the client never does
  money arithmetic.
- Works from 375px to desktop, so the review queue is usable on a phone.
- The API contract can never silently drift from the client's types.
- The whole stack — postgres, redis, api, web — comes up with `docker compose up`.

## Non-Goals

These are deliberately excluded. Each is a later cycle, not an oversight.

| Excluded | Why |
|---|---|
| **Backtests screens** (`/api/backtests/**`) | Rule-document editing and run analysis are their own product surface, comparable in size to the journal. |
| **Market Data screens** (`/api/market-data/**`) | Candle coverage, gap management and CSV import are a prerequisite for backtests, so they ship with them. |
| **Attachments / screenshots** | No endpoint accepts or serves file bytes. The `attachments` array in `TradeDetailResponse` is ignored entirely — no preview, no metadata list, no upload control. |
| **Editing profile or account settings** | No write endpoint exists for `TimeZoneId`, `StartingBalance`, `DefaultRiskPerTrade`, or for renaming/deactivating an `Account`. These render read-only. |
| **Editing or manually linking a plan** | The API offers create, list, abandon and calculate only. |
| **Signup, password reset, user management** | Public signup is disabled by design; the owner account is seeded at startup. |
| **Real-time updates** | No WebSocket track exists in the backend. Liveness is polling-only, and only while a sync run is active. |
| **i18n / RTL** | Single user, English, LTR. |
| **Server-side rendering, SEO, offline/PWA** | Authenticated single-user tool. |

## Requirements

Numbered and verifiable. "Renders X" means X is visible without further interaction.

### Auth and shell

1. `/login` accepts email + password, calls `POST /api/auth/login`, and stores
   `token` and `expiresAt` under the localStorage key `tradeledger.auth`.
2. A failed login renders "Email or password is incorrect" inline above the form
   and does not distinguish unknown email from wrong password.
3. Every route except `/login` is guarded: no stored token, or `expiresAt` in the
   past, redirects to `/login?next=<encoded pathname+search>`.
4. A successful login redirects to `next` when present, otherwise `/`.
5. Any API response with status 401 clears stored auth and redirects to `/login`,
   regardless of which query or mutation produced it.
6. The app shell renders a persistent navigation with: Dashboard, Journal,
   Review, Analytics, Portfolio, Plans, Settings.
7. The Review nav item renders a badge with the current unreviewed count from
   `GET /api/trades/inbox`; the badge is hidden when the count is 0.
8. A theme toggle switches Mantine's colour scheme and persists the choice to
   localStorage key `tradeledger.theme`. Default is `dark`.
9. Below 768px the navigation collapses into a burger-triggered drawer.

### Dashboard (`/`)

10. Renders an equity area chart from `GET /api/analytics/equity-curve`, x-axis
    `at`, y-axis `equity`.
11. Renders, below or overlaid on the curve, the drawdown figures from the same
    response: `maxDrawdown`, `maxDrawdownPercent`, `maxDrawdownAt`,
    `currentDrawdown`, `currentDrawdownPercent`.
12. Renders stat tiles from `GET /api/analytics/summary`: net PnL, win rate,
    total/winning/losing trades, profit factor, expectancy, average win, average
    loss, longest win streak, longest loss streak, average duration.
13. `profitFactor` is `null` when there are no losing trades; it renders as `—`,
    never as `∞` or `0`.
14. Renders a planned-vs-unplanned comparison from `plannedTradeCount`,
    `unplannedTradeCount`, `plannedNetProfitLoss`, `unplannedNetProfitLoss`.
15. Renders open positions — `GET /api/trades?pageSize=50` filtered client-side
    on `outcome === "Open"` — and the 10 most recent trades.
16. A date-range control (presets: 7d, 30d, 90d, YTD, All, plus a custom range)
    and an account selector drive `from`, `to` and `accountId` on all four
    analytics queries simultaneously. The selection is held in the URL query string.

### Journal (`/trades`)

17. Renders a paged table from `GET /api/trades` with columns: opened at, symbol,
    side, outcome, net PnL, achieved R, strategy, market session, review state,
    origin, rating, duration.
18. Filters for `accountId`, `symbol`, `reviewState`, `marketSession`, `from`,
    `to` are synced to the URL query string, so a filtered view is linkable and
    survives reload.
19. Paging uses `page` and `pageSize` (default 50, max 200) and renders
    `total` from `PagedResult`.
20. A row click navigates to `/trades/:id`.
21. `/trades/:id` renders the full `TradeDetailResponse`: mechanical fields, the
    executions table ordered by `executedAt`, subjective fields, market context,
    and tagged mistakes and trackings with their notes and estimated costs.
22. `/trades/:id` offers Edit, which opens the same journal form used by the
    review queue, pre-filled, submitting `PATCH /api/trades/{id}/journal`.
23. `/trades/:id` offers Delete only when `origin === "Manual"`; for
    `origin === "Synced"` the control is absent, not merely disabled.
24. `/trades/new` submits `POST /api/trades` for a manual trade and navigates to
    the created trade's detail page.

### Review queue (`/review`)

25. Loads `GET /api/trades/inbox` and renders one trade at a time, full width.
26. The mechanical half renders read-only beside (desktop) or above (mobile) the
    journal form.
27. The form fields are: strategy, timeframe, entry type, exit type, entry mental
    state, exit mental state, stop loss price, take profit price, mistakes
    (multi), trackings (multi), rating 1–5, memo, tag, post-trade tag, and the
    ten market-context free-text fields.
28. All taxonomy pickers are populated from a single `GET /api/taxonomy` call,
    grouped client-side by `kind`, active terms only.
29. **Save & next** submits `PATCH /api/trades/{id}/journal` with
    `markReviewed: true` and advances to the next trade in the queue.
30. **Skip** advances without submitting.
31. Keyboard: `Ctrl/Cmd+Enter` = Save & next, `Esc` = Skip, `1`–`5` set the
    rating when focus is not in a text input.
32. A progress indicator renders `n of m` and decrements as the queue drains.
33. An empty inbox renders an explicit "Nothing to review" state with a link to
    the journal — not a blank page.

### Analytics (`/analytics`)

34. A dimension selector drives `GET /api/analytics/breakdown/{dimension}` across
    all ten values: `Strategy`, `Symbol`, `Side`, `Timeframe`, `MarketSession`,
    `EntryMentalState`, `ExitMentalState`, `DayOfWeek`, `HourOfDay`, `Planned`.
35. Each breakdown renders as a sortable table (`key`, `tradeCount`, `winCount`,
    `winRate`, `netProfitLoss`, `averageR`) beside a bar chart of `netProfitLoss`
    by `key`.
36. `averageR` is nullable and renders as `—` when absent.
37. Renders mistake frequency and cost from `GET /api/analytics/mistakes`
    (`mistake`, `occurrences`, `totalCost`), worst first as returned.
38. The `MarketSession` dimension's keys are the derived session flags, rendered
    by `MarketSessionCalendar.Describe` — "Tokyo", "Tokyo / London overlap",
    "Off hours" — so they agree with the `marketSession` filter on the journal
    and with `marketSessionLabel` on the trade detail page. No caveat is shown.

### Portfolio (`/portfolio`)

39. Tabs for Holdings, Transfers and Snapshots.
40. Holdings render from `GET /api/holdings` with `kind`, `asset`, `quantity`,
    entry and current value, and — for `LiquidityPool`/`Farm` kinds — `poolName`,
    `farmApr` and `isFarmed`.
41. Row actions Reprice (`POST /api/holdings/{id}/reprice`) and Close
    (`POST /api/holdings/{id}/close`) open modals and invalidate the list on success.
42. New Holding submits `POST /api/holdings`.
43. Transfers render from `GET /api/transfers`; New Transfer submits
    `POST /api/transfers`. A transfer with `writeOff: true` renders a visible
    "Write-off" marker.
44. Record Snapshot submits `POST /api/snapshots`. There is no snapshot list
    endpoint, so the tab explains that the equity curve is the snapshot view and
    links to the dashboard.

### Plans (`/plans`)

45. Renders plans from `GET /api/plans` with status, symbol, side, planned entry,
    stop, take profit, risk fraction, planned R:R and `linkedTradeId`.
46. A plan with `status === "Active"` or `"Draft"` offers Abandon
    (`POST /api/plans/{id}/abandon`).
47. `/plans/new` includes a live position-size calculator: on every change to
    balance, entry, stop, risk fraction, R:R, leverage or fee rate, it calls
    `POST /api/plans/calculate` (debounced 300ms) and renders `quantity`,
    `orderValue`, `margin`, `takeProfitPrice`, `estimatedProfit`,
    `estimatedLoss`, `riskAmount` and both fee figures.
48. The calculator's `balance` defaults to `me.startingBalance` and
    `riskFraction` to `me.defaultRiskPerTrade` when present.
49. Create Plan submits `POST /api/plans` with the same values.

### Settings (`/settings`)

50. **Accounts** tab lists `GET /api/accounts` with `name`, `kind`, `venue`,
    `syncMode`, `quoteAsset`, `isActive`, `trackedFrom`, `apiKeyHint`,
    `credentialEnabled`, `lastVerifiedAt`, `verifiedViaEgress`.
51. Add Account submits `POST /api/accounts`.
52. Set Credentials submits `PUT /api/accounts/{id}/credentials`; the form warns,
    before submission, that the key is verified through the egress proxy and that
    the proxy must be configured first.
53. A `503` with `code === "proxy_required"` from credential verification renders
    a dedicated message pointing at the Proxy tab — not a generic toast.
54. Remove Credentials submits `DELETE /api/accounts/{id}/credentials` behind a
    confirmation.
55. **Proxy** tab renders `GET /api/proxy` (`configured`, `enabled`, `scheme`,
    `host`, `port`, `username`, `hasPassword`, `required`, `configurationFallback`)
    and submits `PUT /api/proxy` / `DELETE /api/proxy`.
56. The proxy password field is write-only: never pre-filled, with an explicit
    "Clear password" control mapping to `clearPassword: true`.
57. **Sync** tab renders `GET /api/sync/status` per account+endpoint and the run
    history from `GET /api/sync/runs`, and offers Run
    (`POST /api/sync/accounts/{id}/run`), Backfill
    (`POST /api/sync/accounts/{id}/backfill`) and Snapshot
    (`POST /api/sync/accounts/{id}/snapshot`).
58. While any run has `status === "Running"`, `GET /api/sync/runs` refetches
    every 3 seconds; polling stops when none is running.
59. A failed run renders its `error` string in full, not truncated.
60. **Profile** tab renders `GET /api/auth/me` read-only with a note that these
    values are configured server-side.

### Cross-cutting

61. Every list view has a distinct empty state, loading skeleton and error state.
    A failed query renders a retry control, never an indefinite spinner.
62. Validation failures (HTTP 400 with an `errors` object) map field-by-field
    onto the submitting form's inputs.
63. All other failures raise a toast containing the `title`, the `detail` when
    present, and the `traceId`.
64. All monetary values render via a shared formatter; no component calls
    `toFixed` directly.
65. All instants render in the user's `timeZoneId`, falling back to
    `Intl.DateTimeFormat().resolvedOptions().timeZone`.
66. Positive and negative PnL are distinguished by an explicit `+`/`−` sign as
    well as by colour.

## User Flows / UX

### Sign in

1. Unauthenticated visit to any route → redirect to `/login?next=…`.
2. Submit → `POST /api/auth/login`.
3. 200 → store `{token, expiresAt, email, displayName}`, redirect to `next` or `/`.
4. 401 → inline error, form stays populated except the password.
5. Network failure → inline error "Could not reach the API", with a retry.

### The review loop (the core flow)

1. Nav badge shows unreviewed count → user opens `/review`.
2. Queue loads `GET /api/trades/inbox` once and holds the list in memory.
3. Trade *n* renders: mechanical facts read-only, journal form focused on Strategy.
4. User fills the form; `Ctrl+Enter` saves.
5. `PATCH /api/trades/{id}/journal` with `markReviewed: true`.
6. On success: invalidate `["trades"]` and `["inbox"]`, advance the in-memory
   cursor, do **not** refetch the queue mid-session (a refetch would reorder it
   under the user's cursor).
7. On 400 with `errors`: mark the offending fields, stay on the trade.
8. On 422 (`DomainRuleException`, e.g. reviewing an open trade): toast the rule
   message, stay on the trade.
9. Cursor reaches the end → "Inbox cleared" state with a link to `/trades`.
10. Empty from the start → "Nothing to review" state.

### Filtering the journal

1. `/trades` loads page 1 with defaults.
2. Changing any filter writes to the URL query string and resets `page` to 1.
3. The query key includes every filter, so TanStack Query caches per-combination.
4. Zero results → "No trades match these filters" with a Clear filters control.

### Triggering a sync

1. Settings → Sync → Run on an account.
2. `POST /api/sync/accounts/{id}/run`; button enters a loading state.
3. `GET /api/sync/runs` begins 3s polling.
4. Run reaches `Succeeded` → polling stops, toast with `recordsWritten`,
   `["trades"]` and `["analytics"]` invalidated.
5. Run reaches `Failed` → polling stops, the `error` renders in the run row.
6. `503 proxy_required` → toast linking to the Proxy tab.

### States every screen must define

| State | Rendering |
|---|---|
| Loading (first) | Mantine `Skeleton` matching the final layout |
| Loading (refetch) | Existing data stays; a subtle top loading bar |
| Empty | Explicit message + the action that would produce data |
| Error | Title, detail, `traceId`, Retry |
| Partial | Nullable fields render `—`; a missing optional never blanks a row |
| Offline | Toast "Could not reach the API"; cached data stays rendered |

## Data Model

The client holds almost no state. Server state lives in TanStack Query; the only
persistent client state is auth and theme.

### Persistent client state

```ts
// localStorage key: "tradeledger.auth"
type StoredAuth = {
  token: string;
  expiresAt: string;   // ISO 8601, from LoginResponse
  email: string;
  displayName: string | null;
};

// localStorage key: "tradeledger.theme"
type StoredTheme = "dark" | "light";
```

### Query keys

Stable, hierarchical, so invalidation is coarse and predictable.

| Key | Source |
|---|---|
| `["me"]` | `GET /api/auth/me` |
| `["accounts"]` | `GET /api/accounts` |
| `["taxonomy"]` | `GET /api/taxonomy` |
| `["trades", filters]` | `GET /api/trades` |
| `["trades", id]` | `GET /api/trades/{id}` |
| `["inbox"]` | `GET /api/trades/inbox` |
| `["analytics", "summary", filters]` | `GET /api/analytics/summary` |
| `["analytics", "equity", filters]` | `GET /api/analytics/equity-curve` |
| `["analytics", "breakdown", dimension, filters]` | `GET /api/analytics/breakdown/{d}` |
| `["analytics", "mistakes", filters]` | `GET /api/analytics/mistakes` |
| `["holdings"]`, `["transfers"]` | portfolio lists |
| `["plans"]` | `GET /api/plans` |
| `["sync", "status"]`, `["sync", "runs"]` | sync |
| `["proxy"]` | `GET /api/proxy` |

Invalidation rules:

| Mutation | Invalidates |
|---|---|
| `PATCH /api/trades/{id}/journal` | `["trades"]`, `["inbox"]`, `["analytics"]` |
| `POST /api/trades`, `DELETE /api/trades/{id}` | `["trades"]`, `["analytics"]` |
| any `/api/sync/**` run reaching a terminal status | `["trades"]`, `["analytics"]`, `["sync"]` |
| holdings/transfers/snapshots mutations | own list + `["analytics"]` |
| plan mutations | `["plans"]` |
| credentials / proxy mutations | `["accounts"]`, `["proxy"]` |

### Wire-format rules

These are properties of the existing API and must be handled, not assumed away.

| Type | On the wire | Client handling |
|---|---|---|
| `decimal` | JSON number | `number`. Display only — never summed or compared for equality. |
| `DateTimeOffset` | ISO 8601 with offset | `string`, parsed at the render boundary. |
| `TimeSpan?` (`duration`, `averageDuration`) | `"HH:mm:ss"` or `"d.HH:mm:ss"` | A dedicated `parseTimeSpan` — `new Date()` cannot parse this. |
| enums | strings (`"Long"`, `"ExchangeFutures"`) | String-literal unions from the generated schema. |
| `MarketSession` (`[Flags]`) | `"Tokyo, London"` | Split on `", "` for chips. `marketSessionLabel` on trade detail is pre-formatted; prefer it. |
| nullable numbers | `null` | `—`, never `0` and never `NaN`. |

## Interfaces

### Endpoints consumed

| Method | Path | Screen |
|---|---|---|
| POST | `/api/auth/login` | Login |
| GET | `/api/auth/me` | Shell, Settings, Plans calculator |
| GET | `/api/accounts` | Settings, filters |
| POST | `/api/accounts` | Settings |
| PUT/DELETE | `/api/accounts/{id}/credentials` | Settings |
| GET | `/api/trades` | Journal, Dashboard |
| GET | `/api/trades/inbox` | Review, nav badge |
| GET | `/api/trades/{id}` | Trade detail |
| POST | `/api/trades` | Manual trade |
| PATCH | `/api/trades/{id}/journal` | Review, edit |
| DELETE | `/api/trades/{id}` | Trade detail (manual only) |
| GET | `/api/taxonomy` | Journal form pickers |
| GET | `/api/analytics/summary` | Dashboard |
| GET | `/api/analytics/equity-curve` | Dashboard |
| GET | `/api/analytics/breakdown/{dimension}` | Analytics |
| GET | `/api/analytics/mistakes` | Analytics |
| GET/POST | `/api/holdings`, `/api/transfers` | Portfolio |
| POST | `/api/holdings/{id}/reprice`, `/close` | Portfolio |
| POST | `/api/snapshots` | Portfolio |
| GET/POST | `/api/plans`, `/api/plans/calculate` | Plans |
| POST | `/api/plans/{id}/abandon` | Plans |
| GET | `/api/sync/status`, `/api/sync/runs` | Settings |
| POST | `/api/sync/accounts/{id}/run`, `/backfill`, `/snapshot` | Settings |
| GET/PUT/DELETE | `/api/proxy` | Settings |

Not consumed: `/api/backtests/**`, `/api/market-data/**`, `/health`.

### Type generation

```jsonc
// package.json
"scripts": {
  "api:types": "openapi-typescript http://localhost:5000/openapi/v1.json -o src/api/schema.d.ts"
}
```

`src/api/schema.d.ts` is **committed**. Regenerating it is a deliberate act whose
diff shows exactly what the contract change was. The API must be running in
Development for the script to work — the document is not served in Production.

### The fetch wrapper

```ts
// src/api/client.ts
export class ApiError extends Error {
  status: number;
  code: string;               // "validation_failed" | "not_found" | ...
  title: string;
  detail?: string;
  traceId?: string;
  errors?: Record<string, string[]>;
}

export async function apiFetch<T>(
  path: string,
  init?: RequestInit & { query?: Record<string, unknown> },
): Promise<T>;
```

Behaviour:

- Prefixes `import.meta.env.VITE_API_BASE_URL`.
- Attaches `Authorization: Bearer <token>` when a non-expired token is stored.
- Serialises `query`, dropping `undefined`/`null`/`""` entries.
- On a non-2xx response, parses the `ProblemDetails` body and throws `ApiError`.
- On 401, clears stored auth and dispatches the redirect before throwing.
- `204` and empty bodies resolve as `undefined`.

### Error code mapping

Taken from `GlobalExceptionHandler.cs` and `DomainExceptions.cs` — these are the
real codes and statuses.

| `code` | Status | Rendering |
|---|---|---|
| `validation_failed` | **400** | Inline field errors from `errors`. Note: validation is 400, not 422. |
| `malformed_request` | 400 | Toast. Indicates a client bug. |
| `invalid_argument` | 400 | Toast. |
| `missing_reference` | 400 | Toast "One of the selected items no longer exists"; refetch `["taxonomy"]` and `["accounts"]`. |
| `not_authenticated` | 401 | Clear auth, redirect to `/login`. |
| `not_found` | 404 | Route-level 404 state for detail pages; toast elsewhere. |
| `duplicate_record` | 409 | Toast. |
| `concurrent_update` | 409 | Toast "This changed while you were editing"; refetch the record. |
| *(any `DomainRuleException`)* | 422 | Toast with the rule message verbatim — it is written for the user. |
| `request_cancelled` | 499 | Silent. Never surfaced. |
| `engine_unavailable` | 501 | Not reachable — backtests are out of scope. |
| `proxy_required` | 503 | Dedicated message linking to Settings → Proxy. |
| `internal_error` | 500 | Toast with `traceId`. |

`POST /api/auth/login` returns the code `invalid_credentials` on a 401. The login
form treats any 401 from that endpoint as bad credentials without reading `code`,
so it is unaffected if that shaping changes again. Unknown email and wrong
password give the same response on purpose.

### Routes

| Path | Screen | Auth |
|---|---|---|
| `/login` | Sign in | public |
| `/` | Dashboard | required |
| `/trades` | Journal list | required |
| `/trades/new` | Manual trade | required |
| `/trades/:id` | Trade detail | required |
| `/review` | Review queue | required |
| `/analytics` | Breakdowns + mistakes | required |
| `/portfolio` | Holdings / Transfers / Snapshots | required |
| `/plans` | Plan list | required |
| `/plans/new` | New plan + calculator | required |
| `/settings` | Accounts / Proxy / Sync / Profile | required |
| `*` | Not found | — |

## Implementation Notes

### Directory layout

Feature folders, mirroring the backend's vertical slices.

```
web/
  Dockerfile
  nginx.conf
  index.html
  package.json
  tsconfig.json
  vite.config.ts
  playwright.config.ts
  .env.example
  src/
    main.tsx
    App.tsx
    routes.tsx
    api/
      schema.d.ts          # generated, committed
      client.ts            # apiFetch + ApiError
      problem.ts           # ProblemDetails → ApiError
      queries/             # one file per route group
        auth.ts  accounts.ts  trades.ts  taxonomy.ts
        analytics.ts  portfolio.ts  plans.ts  sync.ts  proxy.ts
    auth/
      AuthProvider.tsx  useAuth.ts  RequireAuth.tsx  tokenStorage.ts
    components/
      AppShell.tsx  PageHeader.tsx  StatTile.tsx
      Money.tsx  Pnl.tsx  Instant.tsx  Duration.tsx
      OutcomeBadge.tsx  SessionChips.tsx
      EmptyState.tsx  ErrorState.tsx  LoadingTable.tsx
    features/
      dashboard/  trades/  review/  analytics/
      portfolio/  plans/  settings/
    lib/
      format.ts        # money, price, percent, R
      time.ts          # parseTimeSpan, formatInstant, formatDuration
      filters.ts       # URLSearchParams ↔ filter objects
      theme.ts         # Mantine theme + colour scheme persistence
      notify.ts        # toast helpers keyed off ApiError
  tests/
    unit/              # Vitest
    e2e/
      review-loop.spec.ts
```

The journal form is **one component** used by both `/review` and the edit action
on `/trades/:id`. It is the largest single piece of UI in the cycle; build it once.

### Build order

Each step leaves the app runnable.

1. **Scaffold** — Vite + React + TS, Mantine, TanStack Query, React Router,
   ESLint + Prettier, `.env.example` with `VITE_API_BASE_URL=http://localhost:5000`.
2. **API plumbing** — `api:types` script, `client.ts`, `problem.ts`, the error
   code mapping, `notify.ts`.
3. **Auth** — `tokenStorage`, `AuthProvider`, `RequireAuth`, `/login`, the 401
   interceptor. Verify against the real API before going further.
4. **Shell** — `AppShell`, navigation, responsive drawer, theme toggle, the
   shared `Money` / `Pnl` / `Instant` / `Duration` primitives and `lib/format.ts`
   + `lib/time.ts`. Unit-test the formatters and `parseTimeSpan` here.
5. **Journal list** — table, URL-synced filters, paging. The first real screen and
   the proof that filters and query keys work.
6. **Trade detail** — read-only, including executions.
7. **Journal form + review queue** — the core loop. Taxonomy pickers, validation
   mapping, keyboard shortcuts, Save & next, then wire the same form into the
   detail page's edit action.
8. **Dashboard** — equity chart, drawdown, stat tiles, date-range control shared
   with analytics.
9. **Analytics** — dimension selector, breakdown table + bar chart, mistakes.
10. **Portfolio** — three tabs and their mutations.
11. **Plans** — list, abandon, and the debounced calculator.
12. **Settings** — accounts, credentials, proxy, sync with conditional polling,
    read-only profile.
13. **Manual trade** — `/trades/new`.
14. **Docker + compose** — multi-stage build, nginx, `web` service.
15. **Playwright** — the review-loop spec.

### Docker

Multi-stage: `node:22-alpine` builds, `nginx:alpine` serves. `nginx.conf` needs
`try_files $uri $uri/ /index.html;` so client-side routes deep-link. Because
`VITE_*` variables are baked at build time, `VITE_API_BASE_URL` is a build arg in
`compose.yaml`, not a runtime environment variable.

The `web` service is added to the existing `compose.yaml` alongside postgres and
redis, with `depends_on` the API if the API is added to compose, otherwise
pointing at the host.

## Dependencies

### npm

| Package | Purpose |
|---|---|
| `react`, `react-dom` (19) | Runtime |
| `typescript`, `vite`, `@vitejs/plugin-react` | Toolchain |
| `@mantine/core`, `@mantine/hooks` | UI kit |
| `@mantine/form` | Form state + field-level errors |
| `@mantine/dates` | Date-range control |
| `@mantine/notifications` | Toasts |
| `@mantine/charts` + `recharts` | Equity, drawdown, breakdown charts |
| `@tabler/icons-react` | Mantine's icon set |
| `@tanstack/react-query` | Server state |
| `react-router` (v7) | Routing |
| `openapi-typescript` (dev) | Type generation |
| `vitest`, `@testing-library/react`, `jsdom` (dev) | Unit tests |
| `@playwright/test` (dev) | E2E |
| `eslint`, `prettier` + configs (dev) | Lint/format |

### As built

Where the implementation differs from this spec, it is because of what the
registry actually resolved to. Nothing here changes a decision.

| Spec said | Built with | Why |
|---|---|---|
| pnpm | **npm** (lockfile committed) | pnpm is not installed on the dev machine and nothing in the project needs it. `web/.gitignore` re-includes `package-lock.json`, which the root `.gitignore` excludes for the newman collection. |
| React Router v7 | **v8** | The current major. Same `createBrowserRouter` / `useSearchParams` surface this spec uses. |
| Mantine (unversioned) | **v9** | `Grid` takes `gap`, not `gutter`; `@mantine/dates` exchanges `YYYY-MM-DD` strings rather than `Date` objects. Both are reflected in `lib/filters.ts`. |
| `@mantine/charts` + recharts 2 | recharts **3** | Mantine 9's charts peer-depend on it. |
| `schema.d.ts`, generated and committed | `src/api/types.ts`, transcribed from the DTOs | The OpenAPI document is Development-only and the API was not running when the client was built. The `api:types` script is in place; run it against a running API and reconcile. |

### Human-provisioned, before implementation starts

1. **The CORS policy must be merged first** —
   [`docs/backend-changes-for-web.md`](../docs/backend-changes-for-web.md).
   Without it every request from the Vite dev server fails at the browser.
   *(Merged.)*
2. A running API with user-secrets configured (`Encryption:KeyBase64`,
   `Jwt:SigningKey`, `Seed:OwnerEmail`, `Seed:OwnerPassword`) — the seeded
   owner's credentials are the only way to log in.
3. `docker compose up -d` for postgres and redis.
4. Node 22+ and npm.

## Edge Cases

| Case | Expected behaviour |
|---|---|
| No accounts exist yet | Dashboard renders an onboarding empty state linking to Settings → Accounts. |
| No trades at all | Equity curve renders an empty state, not a flat zero line. Summary tiles render `—`. |
| No losing trades | `profitFactor` is `null` → `—`. |
| A single balance snapshot | Curve renders one point; `maxDrawdown` is 0 and renders as such. |
| Journal has 10,000+ trades | Server-side paging caps at 200 rows per page; no client-side virtualisation needed. |
| Token expires mid-session | Next request 401s → auth cleared, redirect to `/login?next=…`. |
| Token expires while the review form is dirty | Redirect happens; unsaved input is lost. Accepted — the alternative is a draft store this cycle doesn't build. |
| Two tabs open, one logs out | The other 401s on its next request and follows the same path. No cross-tab storage listener. |
| Sub-satoshi price (e.g. `0.0000000123`) | Rendered with significant-digit formatting, not fixed 2dp. |
| `null` in any nullable decimal | `—`. Never `0`, never `NaN`. |
| `duration` of `"1.03:20:00"` | `parseTimeSpan` handles the `d.HH:mm:ss` form → "1d 3h 20m". |
| `marketSession` is `"None"` | Renders "No session" chip. |
| Trade with zero executions | Executions table renders its own empty state; the page still renders. |
| Attempting to delete a synced trade | The control is never rendered. If a 422 arrives anyway, its message is toasted. |
| Reviewing a trade that is still open | 422 `DomainRuleException` → toast, stay on the trade. |
| Taxonomy term retired mid-session | `missing_reference` 400 → toast + refetch `["taxonomy"]`. |
| Sync triggered with no proxy configured | `503 proxy_required` → message linking to the Proxy tab. |
| Sync run never finishes | Polling continues while `Running`. No client-side timeout; the run row shows elapsed time. |
| Two sync runs triggered at once | The backend's Redis lock rejects the second; whatever it returns is surfaced as a toast. |
| API unreachable | Toast "Could not reach the API"; cached data stays on screen; queries retry with backoff. |
| `/openapi/v1.json` unreachable when generating types | `api:types` fails loudly; the committed `schema.d.ts` is unaffected. |
| Viewport at 375px | Tables become stacked cards or scroll inside `overflow-x: auto`; the page body never scrolls horizontally. |

## Constraints

- **Performance** — first contentful paint under 2s on a local network; the
  journal list renders in under 500ms after its response arrives.
- **Bundle** — initial JS under 400KB gzipped. `/analytics`, `/portfolio`,
  `/plans` and `/settings` are lazy-loaded route chunks; charts load with them.
- **Security** — no secret is ever rendered: `ExchangeCredential` secrets and the
  proxy password are never returned by the API and must never be requested. The
  proxy password field is write-only. The JWT in localStorage is an accepted risk
  (single user, no third-party scripts, no untrusted content rendered); no
  third-party analytics, fonts or scripts may be added without revisiting it.
- **Compatibility** — current evergreen Chrome, Firefox, Safari. No IE, no
  legacy transpilation targets.
- **Accessibility** — Mantine defaults, semantic landmarks, every control
  keyboard-reachable, the review queue fully keyboard-operable, and colour never
  the only signal. No formal WCAG audit this cycle.
- **Responsive** — 375px to 2560px.
- **No deadline.**

## Decisions and Trade-offs

| Decision | Chosen | Rejected | Why |
|---|---|---|---|
| Framework | React + TS + Vite SPA | Blazor WASM, Next.js, SvelteKit | Best ecosystem for dense tables and charts; keeps the API frontend-agnostic per `CLAUDE.md` §7. Blazor would have shared DTOs but needs JS interop for charting anyway. |
| UI kit | Mantine | Tailwind + shadcn/ui, MUI | Batteries-included: DataTable, dates, notifications and form handling out of the box, and `@mantine/charts` inherits the theme for free. Accepts Mantine's design language and upgrade cycle. |
| Charts | `@mantine/charts` (Recharts) | ECharts, lightweight-charts | One design language, no second theming model. ECharts would handle far larger point counts; revisit if the equity curve outgrows Recharts. |
| Routing | React Router v7 | TanStack Router | Larger ecosystem and fewer unknowns. Loses typed search params, so `lib/filters.ts` does that mapping by hand. |
| API types | `openapi-typescript`, committed | Hand-written types, Orval | Types cannot drift from the API, and a contract change appears as a reviewable diff — without inheriting generated call sites. |
| Money | float64, display-only | `decimal.js` + string wire format | Every aggregate is already computed server-side, so the client never does arithmetic. Exact decimals would mean changing every DTO and every existing consumer. Risk is confined to displaying >17 significant digits, which no real quote has. |
| Token storage | localStorage | In-memory, sessionStorage, httpOnly cookies | Survives refresh, which matters most on mobile. XSS risk bounded by the single-user, no-third-party-script constraint. Cookies would mean changing the API's auth scheme and adding CSRF protection. |
| Serving | Separate origin + CORS | API `wwwroot`, reverse proxy | Keeps the two deployables independent. Costs one backend change (the CORS policy). |
| Review UX | Dedicated one-at-a-time queue | Drawer over the list, full detail page | The post-market backlog is the core loop; momentum and keyboard flow matter more than list context. |
| Liveness | Poll only while a run is active | Manual refresh, fixed interval | Feedback exactly when something is happening, no constant background traffic otherwise. |
| Time zone | `me.timeZoneId`, browser fallback | Browser-only, UTC-only | Session and hour-of-day analysis must agree with the journal regardless of where the trader is. |
| Repo location | `web/` at root | `src/TradeLedger.Web`, separate repo | One clone, atomic commits across a contract change. `src/` means "in `TradeLedger.slnx`", and a Node project is not. |

## Migration / Rollout

There is no existing frontend, so nothing migrates.

1. Merge the CORS change from `docs/backend-changes-for-web.md`.
2. `web/` lands incrementally in build order; each step is independently runnable
   against a locally running API.
3. `compose.yaml` gains the `web` service last, once the bundle builds cleanly.
4. `CLAUDE.md` §7 Layout gains a `web/` entry, and §7a a section on running the
   frontend. Do this in the same change as step 3.
5. Rollback is removing the `web` service from `compose.yaml`; the API is
   unaffected apart from the CORS policy, which is inert without a browser origin.

## Testing

### Unit (Vitest + React Testing Library)

- `lib/time.ts` — `parseTimeSpan` across `"00:05:00"`, `"02:14:33"`,
  `"1.03:20:00"`, `null`; `formatInstant` honouring `timeZoneId` and falling back.
- `lib/format.ts` — money, sub-satoshi prices, percentages, R multiples, `null` → `—`,
  signed PnL.
- `lib/filters.ts` — filters ↔ `URLSearchParams` round-trip; empty values omitted.
- `api/problem.ts` — every row of the error-code table maps to the right
  `ApiError`; a login 401 with no `code` is handled.
- `auth/` — expiry guard: expired `expiresAt` redirects; a 401 clears storage.
- The journal form — `errors` from a 400 land on the right fields.
- MSW mocks the API; no unit test touches a real server.

### End-to-end (Playwright)

One spec, `tests/e2e/review-loop.spec.ts`, against a running API and database:

1. Log in as the seeded owner.
2. Assert the Review badge count is *n*.
3. Open `/review`, fill strategy, mental state, rating and memo.
4. `Ctrl+Enter` → assert advance to the next trade and the count is *n−1*.
5. Navigate to `/trades`, filter to `reviewState=Reviewed`, assert the trade appears.
6. Open its detail page, assert the saved subjective fields render.

This is the one flow whose breakage makes the product useless, and the only test
that proves CORS, auth, contract and mutation-invalidation all work together.

### Not tested automatically

Visual regression, cross-browser rendering, and the Portfolio/Plans/Settings
mutations. Verified by hand this cycle.

## Open Questions

Each carries the default this spec assumes. None blocks implementation.

1. ~~**`MarketSession` breakdown groups on the wrong field.**~~ **Resolved.**
   `AnalyticsService` now groups on the derived `Trade.MarketSession` flags via
   `MarketSessionCalendar.Describe`. Requirement 38 describes the fixed
   behaviour and the client renders no caveat.
2. **Profile and account settings are read-only.** No endpoint writes
   `TimeZoneId`, `StartingBalance`, `DefaultRiskPerTrade`, or renames/deactivates
   an `Account`. *Default: render read-only with a note; endpoints tracked in
   `docs/backend-changes-for-web.md` §3.*
3. **Plans cannot be edited or manually linked to a trade.** *Default: build only
   create, list, abandon and calculate.*
4. **No snapshot list endpoint.** *Default: the Snapshots tab records only, and
   points at the dashboard for viewing.*
5. **`/openapi/v1.json` is Development-only.** *Default: acceptable — type
   generation is a developer-machine script, never part of the production build.*
6. ~~**Login's 401 carries no `code` or `traceId`.**~~ **Resolved.**
   `AuthEndpoints` shapes it through `ApiProblem` with the code
   `invalid_credentials`. The login form still treats any 401 from that endpoint
   as bad credentials without reading `code`, so it works either way, and
   unknown email and wrong password still return the same response.

7. **`TradeDetailResponse` carries taxonomy names, not ids.** The journal form
   needs ids to pre-select its pickers, so it matches each name back against
   `GET /api/taxonomy` within its kind. A term renamed since the trade was
   journalled will not resolve and the picker opens empty rather than showing a
   wrong selection. *Default: resolve by name; adding ids to the response is
   tracked in `docs/backend-changes-for-web.md` §6.*
