# HO-XCT-002 — Cross-cutting and Technical (frontend)

## 1. Identity

| Field | Value |
|---|---|
| Handover ID | **HO-XCT-002** |
| Title | Cross-cutting and Technical — frontend slice |
| Date | 2026-09-22 |
| Author | Claude Opus 5 |
| Backlog | [`docs/USER_STORIES.md`](../USER_STORIES.md) v2.4 §17 — **US-XCT-01, US-XCT-02, US-XCT-05, US-XCT-06** (browser halves). **US-XCT-03, US-XCT-04, US-XCT-07** are standards every frontend handover already applies; this slice **verifies** them across the whole app (§3.4) rather than building them |
| SRS | **NFR-AVL-3, NFR-AVL-5**, **NFR-PRF-1**, **NFR-CMP-2**, NFR-CMP-1, C-3, D-8, D-9, D-11; **UI-1, UI-3, UI-4, UI-6**; NFR-USA-4, NFR-USA-5, NFR-USA-6, NFR-SEC-2, NFR-SEC-4, C-9; **AC-19**, AC-20 |
| Pairs with | [`HO-XCT-001`](HO-XCT-001.md) — **§12.4 there is the contract**. On any disagreement about the wire, HO-XCT-001 wins (§20.4) |
| Builds on | [`HO-CAT-002`](HO-CAT-002.md) (the `web/` scaffold, stack, layering, accessibility rules), [`HO-ACC-002`](HO-ACC-002.md) (session model, `client.ts`, route groups, guards, idle lock, error mapping, the staff customer page), [`HO-ADM-002`](HO-ADM-002.md) (admin section shell, active branch, `lib/format`, the settings screen that renders HO-XCT-001's new keys for free), [`HO-APT-002`](HO-APT-002.md) (front desk, which gains the offline banner) and [`HO-NTF-002`](HO-NTF-002.md) |
| Delivery order | Section 17 of 17 in [`README.md`](README.md#delivery-order), the last one |
| Approved design | **None.** No design artifact exists for these screens. Build to §11 with the shadcn/ui tokens HO-CAT-002 establishes |
| Baseline commit | `4007225` |
| Amended | *(none. Append rows here)* |
| Amendments this slice needs in other handovers | §5.3. **Not written into those files** — the README owner records each as an Amended row before implementation (HO-XCT-001 OQ-1). The most important one is HO-ACC-002 §4.3: this slice introduces the **one** sanctioned use of IndexedDB (ADR-FE-141) |

---

## 2. Objective

Four browser-side jobs that no area owns:

1. **The front desk keeps working when the internet drops** (US-XCT-01, AC-19). A staff-only service
   worker and a small IndexedDB snapshot let a reception PC or a caretaker tablet show *today's
   appointments and today's boarding tasks* after connectivity is lost — including after a reload.
   The offline state is announced, write actions are switched off rather than allowed to fail, and
   the moment the server comes back the views refresh themselves.
2. **The storefront is fast on a phone, and every flow fits 360 px** (US-XCT-02). This slice adds the
   measurement and the gates, not the pages: real-user reporting of Core Web Vitals, a lab LCP budget
   on a throttled 4G profile, a first-load JavaScript budget per storefront route, and a sweep that
   walks **every route in the app** at 360 px looking for horizontal scroll.
3. **Compliance can see and act on retention** (US-XCT-06). Screens for the retention schedule, the
   nightly runs, the review-eligible report, the erasure ledger, and a staff-taken erasure request on
   the customer page — with a confirmation that says exactly what will and will not be deleted.
4. **The owner can see that backups work** (US-XCT-05). A backup and restore page: last backup, WAL
   freshness, the last restore test with its measured RPO and RTO, a visible overdue state, and the
   form that records a quarterly drill from the script's output.

Three things make this harder than it looks:

1. **Offline caching means personal data at rest on a device.** Every other frontend handover says
   nothing sensitive persists in the browser. This slice carves out exactly one exception, keeps it as
   small as the requirement allows, and wipes it aggressively (ADR-FE-141).
2. **"Offline" is not `navigator.onLine`.** A clinic PC with a working LAN and a dead uplink is online
   by that flag and useless in fact. Connectivity is decided by the server's own ping and by real
   request failures (ADR-FE-142).
3. **A service worker can break the app it is meant to protect.** It is registered only in production
   builds, never in mock mode (where MSW owns a service worker), never outside `/staff/`, and it never
   caches an API response (ADR-FE-139, ADR-FE-140).

---

## 3. In scope

### 3.1 Inherited — do not revisit

The HO-CAT-002 stack (Next.js 15 App Router, React 19, TypeScript strict, Tailwind v4, shadcn/ui,
TanStack Query v5, react-hook-form + zod, Vitest, Playwright, axe, MSW) and its layering
(`app/` → `components/` → `lib/hooks/` → `lib/api/`). HO-ACC-002's session model, `client.ts`, route
groups, guards, idle lock, `PermissionDeniedState`, `UnsavedChangesGuard` and error mapping.
HO-ADM-002's admin shell, active branch and `lib/format`. HO-APT-002's front desk and calendar.

### 3.2 Stories and screens

| Story | Screens / behaviour | Route |
|---|---|---|
| US-XCT-01 | Snapshot sync, connectivity monitor, global offline banner, read-only enforcement, and the **offline board** that survives a reload | `(offline)/staff/offline`, plus the `(staff)` layout |
| US-XCT-01 | Device opt-in: *"Keep an offline copy of today's lists on this device"* | `/staff/offline/settings` (also linked from the banner) |
| US-XCT-02 | Web-vitals reporting from every customer-facing route; the admin performance page | `lib/observability/**`, `/staff/admin/performance` |
| US-XCT-02 | Lab budgets and the 360 px sweep (tests, not screens) | `web/tests/perf/**`, `web/scripts/**` |
| US-XCT-05 | Backup and restore page: status, restore-test history, "record a restore test" form; overdue banner for administrators | `/staff/admin/operations/backup` |
| US-XCT-06 | Retention schedule; run history and detail; review-eligible report; erasure ledger list and detail; "run retention now" | `/staff/admin/retention/**` |
| US-XCT-06 | **Erase personal data** action on the staff customer page, and the reapply-after-restore tool | HO-ACC-002's `/staff/customers/[customerId]`, `/staff/admin/retention/erasures/reapply` |
| — | The nine new settings appear automatically on HO-ADM-002's settings screen, because it renders the registry | `/staff/admin/settings` |

### 3.3 Decisions already taken elsewhere — do not re-litigate

| Decision | Where |
|---|---|
| Sessions are `HttpOnly` cookies over a same-origin proxy; the browser never holds a token; CSRF via `pc_csrf` → `X-CSRF-Token` | HO-ACC-002 ADR-FE-002 |
| Sessions are TanStack Query state; sign-out clears the whole cache | HO-ACC-002 ADR-FE-003 |
| No global store, no second `fetch` call site, no `useQuery` inside a component, no `dangerouslySetInnerHTML` | HO-CAT-002 §8.2, HO-ACC-002 §4.3 |
| No client-side authorization decision; a server `403` renders `PermissionDeniedState` | HO-ACC-002 ADR-FE-009 |
| The idle lock is an in-place overlay, and lifts only for the same `staffAccountId` | HO-ACC-002 ADR-FE-010 |
| Branch time is rendered with `Intl` in the branch's IANA zone; no time-zone library | HO-ADM-002 ADR-FE-014 |
| The active branch is sent as `X-Branch-Id` by `client.ts` | HO-ADM-002 ADR-FE-015 |
| Effective-dated and append-only records are shown, never edited | HO-ADM-002 ADR-FE-018 |
| Actions are rendered from the server's `allowedTransitions` / permissions, never a hardcoded list | HO-APT-002 ADR-FE-028 |

### 3.4 The three standards this slice verifies rather than builds

| Story | What this slice adds |
|---|---|
| US-XCT-03 (UI-6) | **One app-wide axe sweep** over every route in the route inventory, in addition to each area's own E-X1. New serious/critical findings are reported to the owning handover, not fixed here (ADR-FE-148) |
| US-XCT-04 (NFR-USA-4/5) | A check that no `errorCode`, stack trace or GUID appears in rendered error text anywhere in the sweep, and the new screens' own error states |
| US-XCT-07 (NFR-SEC-5) | Nothing. This slice uploads no file. The drill output is pasted text, rendered as text |

---

## 4. Out of scope

| Excluded | Why |
|---|---|
| **Any backend change** | HO-XCT-001 owns `src/**` and `deploy/**`. A missing field or endpoint is a §20.4 stop |
| Offline **writes**, queued mutations, background sync, conflict resolution | NFR-AVL-3 says *read-only*. A queued write replayed minutes later against a changed day is a correctness problem nobody asked for (ADR-FE-143) |
| Offline anything for customers, the storefront or the portal | The requirement is the front desk. The service worker never leaves `/staff/` (ADR-FE-139) |
| Push notifications, install prompts, a full PWA manifest, "add to home screen" | Not in any story; HO-NTF-002 §4 already excludes push |
| Fixing a slow or overflowing storefront page | The measurement and the gate are here; the fix belongs to the owning handover (ADR-FE-148) |
| A Lighthouse / `@lhci/cli` dependency, a workbox build, an `idb` wrapper, a web-vitals package | §10 — all four are replaced by in-repo code or by what Next.js already ships |
| Deleting anything from a retention screen | Nothing in the product hard-deletes a record. The screens report and explain (HO-XCT-001 ADR-301) |

Forbidden shapes are HO-ACC-002 §4.3's, with **one carve-out**: IndexedDB is permitted for the offline
snapshot and the device flag, and for nothing else (ADR-FE-141). No token, password, CAPTCHA answer or
form draft ever goes into storage, in any API.

---

## 5. Prerequisites

| Required | State |
|---|---|
| Node 20 LTS; the HO-CAT-002 scaffold | ❌ at baseline — §20.1 |
| **HO-ACC-002, HO-ADM-002, HO-NTF-002, HO-APT-002 built** | ❌ at baseline — §20.1 |
| HO-BRD-002 built (the boarding task screens this slice mirrors offline) | ❌ not written. The offline board's boarding section is built from HO-XCT-001's snapshot alone and needs no BRD screen, but its E-cases are `BLOCKED` until BRD exists |
| The storefront (HO-SHP-002) and checkout (HO-ORD-002) | ❌ not written. The performance and 360 px sweeps enumerate whatever routes exist at build time (§11.6). Storefront-specific budget rows are `BLOCKED` with a stated reason until then |
| The HO-XCT-001 API | ❌ — build against MSW generated from its §12.4, then re-run every E-case against the real API |

### 5.1 Build against the contract, then prove it against the API

HO-XCT-001 §12.4 is authoritative for routes, shapes, headers (`ETag`, `Cache-Control`), status codes
and error codes. Transcribe `lib/api/types.ts` from it. **A mock may not contain a field it does not
specify.** Every §16 case is run twice — MSW and real API — and the test sheet records both.

### 5.2 Offline cases cannot run under MSW

MSW installs its own service worker at `/` (HO-CAT-002 §10). Two service workers with overlapping
scopes would fight, and the more specific scope (`/staff/`) would win, so MSW would stop intercepting
staff routes. Therefore:

- **The offline service worker is not registered when `NEXT_PUBLIC_USE_MOCKS=true`** (ADR-FE-140).
- Offline cases that need a **reload** (E-A4, E-A5) run only against the real API, in a production
  build (`next build && next start`), with `context.setOffline(true)`.
- Offline cases that do **not** need a reload (the banner, read-only mode, the cached board while the
  tab stays open) run under MSW too, by failing the ping handler.

### 5.3 Amendments this slice needs in other handovers

| Target | Amendment |
|---|---|
| HO-ACC-002 §4.3 and ADR-FE-002 | IndexedDB is permitted for the offline snapshot and the device flag (ADR-FE-141). Its U-X4 / E-X3 storage audits stand unchanged for `localStorage` and `sessionStorage`, and gain "no session token, password, CAPTCHA answer or draft in IndexedDB either" |
| HO-ACC-002 §11 (staff customer page) | The page gains an `ErasePersonalDataPanel` (§11.5) |
| HO-ACC-002 §8.4 | On sign-out, on `session_invalid` and on a person change, the offline store is cleared as well as the query cache (§8.5) |
| HO-APT-002 §11.5, HO-BRD-002 | The front desk (and the boarding task screen) render `OfflineBanner` and take their read-only state from `useConnectivity()` (§11.3) |
| HO-CAT-002 §12.1 | `web/next.config.ts` gains the headers in §12.2, and the root layout gains `<WebVitalsReporter />` |

---

## 6. Governing documents

| Document | What you need |
|---|---|
| [`HO-XCT-001`](HO-XCT-001.md) | **§12.4** routes, the snapshot body, ETag and cache headers; §10.4 codes; §8.2 the record classes the retention screens explain; §8.5 how RPO/RTO and the outcome are computed, so the form never asks for them; §12.3 who may do what |
| [`HO-ACC-002`](HO-ACC-002.md) | §8.4 session model; §11.2 `client.ts`; §11.9 the idle lock; §11.14 error mapping; the staff customer page |
| [`HO-ADM-002`](HO-ADM-002.md) | §8.3 active branch; §8.4 `lib/format`; the admin section shell and its nav |
| [`HO-APT-002`](HO-APT-002.md) | §11.5 the front desk, which the offline board mirrors in reduced form |
| [`HO-CAT-002`](HO-CAT-002.md) | §8.2 layering; §8.5 accessibility; §9 design conventions; §10 dependencies; §12.1 scaffold |
| [`docs/SRS.md`](../SRS.md) §3.1, §6.1, §6.2, §6.4, §9 | UI-1/3/4/6; NFR-PRF-1; NFR-AVL-3/5; NFR-USA-4/5/6; **AC-19** |
| `.claude/agents/frontend-developer.md` | Component size, tokens, flag-don't-introduce libraries |

**The `.ai/` documents describe the .NET backend only** and do not govern this diff — the HO-CAT-002 §6
position, unchanged.

---

## 7. The SRS requirements, as build rules

| Req | Rule | Where enforced |
|---|---|---|
| **NFR-AVL-3 / AC-19** | With the server unreachable, the front desk shows today's appointments **and** today's boarding tasks from local cache, read-only, with a clear offline indicator; it recovers by itself | §11.1–§11.4; E-A1…E-A7 |
| **NFR-PRF-1** | Storefront LCP ≤ 2.5 s at p75 on 4G. Field: real-user reporting. Lab: median of 5 runs on Slow-4G + 4× CPU ≤ 2 500 ms, in CI | §11.6, §12.3; E-B1 |
| **NFR-CMP-2 / UI-1** | Every flow completes at 360 px with no horizontal scrolling | The sweep in §11.6; E-B3 |
| **UI-3** | Destructive actions confirm, stating exactly what is affected. The erasure confirmation names what is **erased** and what is **kept and why** | §11.5 |
| **UI-4** | Lists filter and paginate | Runs, erasure requests, restore tests |
| **UI-6 / NFR-USA-6** | WCAG 2.1 AA. The offline banner is a live region, not colour alone; the offline board is keyboard-operable; the countdown to snapshot staleness is announced at thresholds, not per second | §11, E-X1 |
| **NFR-USA-4** | Offline messages say what is happening and what to do: *"You're offline. This list was last updated at 10:42. Changes are switched off until the connection returns."* | §11.3 |
| **NFR-USA-5** | An offline drop never discards what someone typed. The page stays mounted, forms keep their values, and submission is refused with an explanation | ADR-FE-143; U-A7 |
| **NFR-SEC-4 / C-9** | Nothing weakens CSP. The offline board is bound to the `staffAccountId` that fetched it, and locks on idle timeout | ADR-FE-141, ADR-FE-144 |
| **AC-20** | Retention screens make the audit and retention story inspectable | §11.5 |

---

## 8. Frontend architecture

### 8.1 Route map — additions

```
web/app/
  (offline)/                                    ⭐ NEW group: no session guard, no server data
    staff/offline/page.tsx                      the offline board                       US-XCT-01
    staff/offline/settings/page.tsx             device opt-in + "clear this device"     US-XCT-01
  (staff)/
    layout.tsx                                  ⚠ gains ConnectivityProvider, OfflineBanner,
                                                  OfflineSnapshotSync, ServiceWorkerRegistrar
    staff/
      customers/[customerId]/page.tsx           ⚠ gains ErasePersonalDataPanel (HO-ACC-002)
      admin/
        retention/page.tsx                      schedule + review + "run now"           US-XCT-06
        retention/runs/page.tsx                 run history                             US-XCT-06
        retention/runs/[runId]/page.tsx         run detail (passes)                     US-XCT-06
        retention/erasures/page.tsx             erasure ledger                          US-XCT-06
        retention/erasures/[customerAccountId]/page.tsx   ledger detail                 US-XCT-06
        retention/erasures/reapply/page.tsx     after-restore reconciliation            US-XCT-06
        operations/backup/page.tsx              backup status + restore tests           US-XCT-05
        performance/page.tsx                    web-vitals p75 per route                US-XCT-02
  layout.tsx                                    ⚠ gains <WebVitalsReporter /> (root)
web/public/staff/sw.js                          ⭐ the service worker (scope /staff/)
```

`(offline)/staff/offline` and `(staff)/staff/...` coexist: route groups do not change the URL. The
offline board is deliberately **outside** the staff layout, because that layout's session guard calls
the API, which is exactly what is unavailable (HO-ACC-002 put `/staff/sign-in` outside `(staff)` for
the same reason).

### 8.2 API modules

```
lib/api/offline.ts        getOfflineSnapshot (If-None-Match, 304 handling), ping
lib/api/telemetry.ts      sendWebVitals (keepalive, credentials omitted)
lib/api/retention.ts      schedule, runs, run, review, erasure requests, run now, reapply
lib/api/operations.ts     backup status, restore tests, record restore test
lib/api/admin-erasure.ts  eraseCustomer (on the staff customer page)
```

`lib/api/client.ts` gains three things and nothing else (§13): the offline refusal for unsafe methods
(ADR-FE-143), network-failure reporting into the connectivity monitor (ADR-FE-142), and
`sendTelemetry()` (ADR-FE-146).

### 8.3 New library code

```
lib/offline/db.ts             ~60 lines of hand-written IndexedDB: open, get, put, clear. No package
lib/offline/snapshot.ts       read/write/validate the snapshot record; staleness and validity
lib/offline/device.ts         the device opt-in flag
lib/offline/sw-register.ts    register/unregister rules (ADR-FE-140)
lib/connectivity/monitor.ts   ping loop + failure signals → online | degraded | offline
lib/hooks/useConnectivity.ts  the hook every screen uses
lib/hooks/useOfflineSnapshot.ts
lib/observability/web-vitals.ts  metric → payload mapping, sampling, route template
```

### 8.4 State

| Kind | Where |
|---|---|
| Connectivity | A tiny module-level store in `lib/connectivity/monitor.ts` with `subscribe`, read through `useSyncExternalStore`. **Not** a global app store and **not** server data; it is a device fact (ADR-FE-142) |
| Snapshot (online) | TanStack Query `['offline','snapshot', branchId]`, `refetchInterval` = the payload's `refreshAfterSeconds`, `refetchOnWindowFocus`. Its `onSuccess` writes to IndexedDB |
| Snapshot (offline board) | Read once from IndexedDB into component state. The board never mounts a query |
| Device opt-in | IndexedDB, read once at layout mount |
| Retention, operations, performance screens | TanStack Query per resource; mutations invalidate their resource |
| Forms | react-hook-form + zod |

### 8.5 The offline store — what it holds and when it is wiped

One IndexedDB database, `purrcare-offline`, version 1, two object stores:

| Store | Key | Record |
|---|---|---|
| `snapshot` | `branchId` | The HO-XCT-001 §12.4 payload verbatim, plus `etag`, `fetchedAt` and the `staffAccountId` it came with |
| `device` | fixed key `settings` | `{ offlineEnabled: boolean, enabledAt, enabledByStaffAccountId }` — **no personal data** |

Wiped completely (`snapshot` store cleared) on: explicit sign-out; any `401 session_invalid`; a
session whose `staffAccountId` differs from the stored one; `validUntil` passing; the device opt-in
being turned off; the "Clear this device" button; and a schema-version mismatch. Never written when
the device flag is off, and never written from a portal or storefront route.

### 8.6 Accessibility notes specific to this slice

- The offline banner is `role="status"` with `aria-live="polite"`, is text-first (not colour), and is
  the first element in `<main>`'s flow so a screen-reader user meets it before the data.
- Staleness ("updated 10:42, 3 minutes ago") is announced when it crosses 15 and 60 minutes, never on
  a timer tick.
- The offline board is a plain table / list, fully keyboard-operable, with no drag, no dialog and no
  action button at all.
- Disabled write controls carry a visible reason and `aria-describedby` pointing at it. A control is
  never disabled with no explanation.
- The retention screens use text plus icon for every status, never colour alone.

---

## 9. ADRs and open questions

Record in `web/docs/decisions.md`, continuing HO-APT-002's series. **This handover owns
ADR-FE-139 … ADR-FE-148.**

| ADR | Decision | Why |
|---|---|---|
| **ADR-FE-139** | **Offline support is staff-only.** The service worker lives at `/staff/sw.js`, so its scope is `/staff/`. It never controls the storefront, the portal or `(auth)` routes | The requirement is the front desk. A root-scoped worker would put customer pages and their data under a cache nobody asked for, and would collide with MSW |
| **ADR-FE-140** | **The service worker caches only hashed static assets and the static offline shell.** Navigations are network-first, falling back to the cached `/staff/offline` shell. **No API response and no other HTML is ever cached.** It is registered only in a production build, only when the device opt-in is on, and **never** when `NEXT_PUBLIC_USE_MOCKS=true`; in mock or development mode any previously registered worker is unregistered on load | Caching HTML of staff pages would cache rendered personal data outside the one store we control and wipe. Two service workers in mock mode would silently break MSW (§5.2) |
| **ADR-FE-141** | **IndexedDB `purrcare-offline` is the one sanctioned persistence of personal data in this app** (§8.5): one snapshot per branch, only the fields the board shows, wiped on sign-out, person change, `session_invalid`, expiry or opt-out, and written only when the device is opted in. No token, password, CAPTCHA answer or draft ever goes in it. **Amends HO-ACC-002 §4.3** | NFR-AVL-3 cannot be met without data on the device. Writing that down, bounding it, and wiping it is the honest version; pretending the cache is not personal data is not. Residual risk: a signed-out but unwiped device (power loss) keeps the day's list until `validUntil`, on a physically controlled clinic machine |
| **ADR-FE-142** | **Connectivity is the server's answer, not the browser's flag.** `GET /api/system/ping` every 15 s while a staff route is mounted, plus network failures reported by `client.ts`. Two consecutive failures (or one failure while `navigator.onLine` is false) → `offline`; one success → `online`. A `5xx` is `degraded`: the server is reachable and the offline board is not shown | `navigator.onLine` only means "an interface is up". A clinic with a dead uplink would look online and show a spinner instead of its patients |
| **ADR-FE-143** | **Offline is read-only and nothing is queued.** `client.ts` refuses `POST`/`PUT`/`DELETE` while offline with a typed `OfflineError`, before any fetch; shared submit and confirm controls disable themselves with a visible reason; TanStack mutations use `networkMode: 'always'` so they fail fast instead of pausing invisibly. **The page stays mounted, so typed input survives** and can be submitted when the connection returns | US-XCT-01 AC2: "disabled rather than failing silently". A paused-and-replayed mutation is neither disabled nor honest |
| **ADR-FE-144** | **The offline board is its own route group, with no session guard**, reading only IndexedDB. It refuses to render when the snapshot is missing, expired, or from a different `staffAccountId`, and it **locks itself after `idleTimeoutMinutes` of no interaction**, showing *"Locked. Sign in again when the connection returns."* | The guard would bounce to sign-in exactly when sign-in is unreachable. The idle lock keeps C-9 and US-ACC-08's protection true on an unattended terminal — deliberately at the cost of availability during a long outage (HO-XCT-001 OQ, revisit if the owner disagrees) |
| **ADR-FE-145** | **Recovery is automatic and announced.** On the first successful ping after being offline, TanStack's online manager is flipped, every active query is invalidated, the snapshot is refetched, and the banner says *"Back online — updated just now"* for 5 s. A client on the offline board is routed back to where it came from (`?from=`), or to the front desk | US-XCT-01 AC3 "the view refreshes automatically" |
| **ADR-FE-146** | **Web vitals use what Next.js already ships** (`useReportWebVitals`). Samples are sent through `client.ts`'s `sendTelemetry()` with `keepalive: true` and **`credentials: 'omit'`**, batched on `visibilitychange`, and carry only metric, value, **route template** (the App Router pattern, never the resolved URL or its query), navigation type, effective connection type, viewport class and build id. Sampling: 100 % until the owner asks otherwise | `credentials: 'omit'` means no cookie, so no CSRF check applies and nothing links a measurement to a person. A route template avoids ids in a URL becoming telemetry |
| **ADR-FE-147** | **Budgets are CI gates, not dashboards.** (1) Lab LCP: Playwright + CDP `Network.emulateNetworkConditions` (Slow 4G: 1.6 Mbps down, 150 ms RTT) with 4× CPU throttling, median of 5 runs per route, budget 2 500 ms. (2) First-load JS per storefront route ≤ 170 KB gzipped, read from the Next build output by an in-repo script. (3) A 360 px sweep over the generated route inventory asserting `scrollWidth <= clientWidth`. All three in-repo; **no Lighthouse dependency** | NFR-PRF-1 must be provable before release, on an agreed profile. A dashboard nobody fails on protects nothing |
| **ADR-FE-148** | **This slice measures; the owning area fixes.** A route that breaches a budget, overflows at 360 px, or fails axe is reported as a finding against its handover. XCT-002 may edit only `app/layout.tsx` (fonts, the reporter), `next.config.ts` (images and headers) and `globals.css` | Otherwise the last handover in the order silently rewrites eleven other slices' screens |

### 9.1 Open questions

| # | Question | Owner | Default |
|---|---|---|---|
| OQ-1 | Is the offline copy opt-in per device, or on by default for front-desk roles? | Owner | **Opt-in**, with a one-time prompt for Reception and Caretaker. A personal laptop should not silently keep a patient list |
| OQ-2 | Should the offline board lock on idle, given it cannot be unlocked until the server returns? | Owner / security | Yes (ADR-FE-144). Revisit if an outage makes it unworkable in practice |
| OQ-3 | Ping interval — 15 s per staff tab | Owner / ops | 15 s, backing off to 60 s after 5 minutes offline, immediate on `visibilitychange` |
| OQ-4 | Telemetry sampling rate | Owner | 100 %, reduced only if volume becomes a problem |
| OQ-5 | Which routes are "storefront" for the JS budget? | HO-SHP-002 author | Everything under `(shop)` / `(portal)` plus `/`; the list lives in `web/perf-budgets.json` and fails the script if a named route no longer exists |
| OQ-6 | Should the erasure confirmation show the customer's name? | Owner / legal | Yes, once, in the confirmation only — the operator must be sure who they are erasing. After erasure the UI shows the placeholder |

---

## 10. Dependencies

**No new package.** Specifically, and deliberately:

| Temptation | Instead |
|---|---|
| `workbox-*`, `next-pwa` | ~120 lines of hand-written `public/staff/sw.js` (§12.1) |
| `idb`, `dexie` | ~60 lines in `lib/offline/db.ts` |
| `web-vitals` | `useReportWebVitals` from `next/web-vitals`, already in Next 15 |
| `@lhci/cli`, `lighthouse` | Playwright + CDP throttling (ADR-FE-147) |
| A charting library for the performance page | `recharts` is already permitted (HO-CAT-002 §10) |

Anything else is a §20.6 stop.

---

## 11. Specified changes — components and screens

Components stay under ~150 lines, with distinct loading, empty and error states.

### 11.1 Connectivity — `lib/connectivity/`, `lib/hooks/useConnectivity.ts`

| Piece | Behaviour |
|---|---|
| `monitor.ts` | Starts when a `(staff)` or `(offline)` route mounts. `GET /api/system/ping` every `OQ-3` interval with `cache: 'no-store'` and an `AbortController` timeout of 5 s. Records the result; `client.ts` also reports every network-level failure and every success. State machine per ADR-FE-142 (`online` → `offline` after 2 failures; `degraded` on `5xx`). Exposes `subscribe`, `getSnapshot`, `lastOnlineAt`, `forceCheck()` |
| `useConnectivity()` | `{ status, lastOnlineAt, isOffline, isDegraded, forceCheck }` via `useSyncExternalStore` |
| Recovery | On `online` after `offline`: `onlineManager.setOnline(true)`, `queryClient.invalidateQueries()`, snapshot refetch, banner message (ADR-FE-145) |

### 11.2 Snapshot sync — `components/offline/OfflineSnapshotSync.tsx` (no UI)

Mounted in the `(staff)` layout. Runs only when: the device flag is on, a staff session exists, and the
active branch is known. Uses `useOfflineSnapshot()`, which queries
`GET /api/staff/offline-snapshot` with `If-None-Match` from the stored record, at the payload's
`refreshAfterSeconds`, and on window focus.

- `200` → write the record (payload, etag, `fetchedAt`, `staffAccountId`).
- `304` → update `fetchedAt` only.
- `401 session_invalid` → clear the store (§8.5). `401 session_idle_timeout` → do nothing; the idle
  lock owns it (HO-ACC-002 §8.4).
- `403` → the person may not hold a snapshot: clear the store and turn the device flag off with a
  toast that says so.
- Network failure → leave the record alone. That is the whole point of it.
- Branch change → the record is keyed by branch, so both survive; only the active one is refreshed.

### 11.3 Offline banner and read-only mode

| Component | Behaviour |
|---|---|
| ⭐ `OfflineBanner` (in the `(staff)` layout, above `<main>`'s content) | `offline`: *"You're offline. Showing today's lists saved at {time}. Changes are switched off until the connection returns."* with **Open today's offline list** (→ `/staff/offline?from={path}`) when a valid snapshot exists, and **Try now**. `degraded`: *"The server is having trouble. Some actions may fail."* `online` after recovery: a 5-second *"Back online — updated just now."* `role="status"`, text-first, sticky, never covering focusable content |
| `useWriteEnabled()` | `!isOffline`. Shared submit and confirm controls consume it: disabled, with the visible reason *"Offline — can't save"* and `aria-describedby` |
| `client.ts` guard | Unsafe method while `offline` → throw `OfflineError` **before** fetching. `lib/api/errors.ts` maps it to *"You're offline, so this couldn't be saved. Your entries are still here — try again when the connection returns."* Nothing is discarded, nothing is queued (ADR-FE-143) |
| Front desk and boarding task screens | Consume `useConnectivity()`: while offline they show their **last loaded** data (TanStack cache) with the banner, stop polling, and hide or disable every action (§5.3 amendment) |

### 11.4 The offline board — `(offline)/staff/offline` — US-XCT-01, AC-19

A single page with no data fetching at all.

| Component | Behaviour |
|---|---|
| ⭐ `OfflineBoard` | Reads the snapshot for the last active branch (stored alongside the record). **Refuses to render data** when: no record (*"No offline copy on this device."*), `validUntil` passed (*"The saved copy is from {date} and is out of date."*), or the current known `staffAccountId` differs — each with an explanation and a **Try to reconnect** button. Header: branch name, the local date, *"Saved at 10:42 · {n} minutes ago"*, and an unmistakable offline chip |
| `OfflineAppointmentsList` | Time, cat, customer (+ `tel:` link, OQ-10 in HO-XCT-001), service, vet, room, status as text, and a **high-risk** badge with its handling note. Sorted by start. Read-only: no buttons |
| `OfflineBoardingTasksList` | Due time, task type and label, unit, cat, instructions, status, assignee. Same read-only treatment. Absent section → *"Boarding tasks weren't included in this copy."* with the reason (`NotPermitted` / `NotAvailable`) |
| `OfflineIdleLock` | After `idleTimeoutMinutes` of no keyboard, pointer or touch input, replaces the data with *"Locked. Sign in again when the connection returns."* (ADR-FE-144) |
| Recovery | The page polls connectivity; on `online` it routes to `?from=` or `/staff/front-desk` and announces the return |
| `/staff/offline/settings` | The device opt-in toggle with plain wording about what is stored and for how long, the last-saved time, **Clear this device now**, and a note that clearing happens automatically at sign-out |

### 11.5 Retention, erasure and operations — admin screens

| Screen | Components and behaviour |
|---|---|
| `/staff/admin/retention` | `RetentionScheduleTable`: one row per record class — class, what it covers in plain words, retention period (from the effective setting, with a link to the settings screen), what happens at the end (**Review only** / Purge / Redact / Anonymise on request), table count, and an expandable list of classified tables. Above it, `ReviewEligiblePanel` (counts per class with the sentence *"Eligible for review. Nothing is deleted automatically."*) and `RunRetentionNow` (Administrator only; confirm dialog naming what the run will and will not do; `409` → *"A retention run is already in progress."*) |
| `/staff/admin/retention/runs` + `/[runId]` | `RetentionRunsTable` (paged, filter by status): requested, trigger, status, duration. Detail: per-pass rows with kind, cutoff, examined / affected / held / failed, duration, and the error codes if any, each with a plain-language gloss |
| `/staff/admin/retention/erasures` + `/[customerAccountId]` | `ErasureRequestsTable` (filter by status): customer id (**no name — it no longer exists**), requested on, source, status, scopes summary ("2 of 3 complete, 1 held until 12 Mar 2032"). Detail: one row per scope with status, held-until, basis (*"NFR-LEG-5 acceptance evidence"*), attempts and last error code. An explanatory paragraph about held data, written for a compliance officer |
| `/staff/admin/retention/erasures/reapply` | `ReapplyAfterRestoreForm`: a paste area for customer ids (one per line, ≤ 500, validated client-side for count and shape), a mandatory restore reference, and a confirmation that states this re-anonymises accounts that were restored. Result panel: processed / already erased / not found / merged |
| ⭐ `ErasePersonalDataPanel` on HO-ACC-002's customer page | Shown to `Customers.Erase` holders. Explains what is erased (name, phone, email, addresses, marketing consent, message contents) and what is **kept and why** (clinical records for {clinicalYears} years, financial records for {financialYears} years, the audit trail). Requires: channel (in person / phone / email / letter), how identity was checked (free text, required), reason. Confirmation dialog **names the customer once** (OQ-6) and says *"This cannot be undone."* On success the page reloads to the anonymised record. Already anonymised → the panel is replaced by *"Personal data was erased on {date}."* with a link to the ledger. `400 CustomerRecordMerged` → the message about erasing the surviving record, with a link |
| `/staff/admin/operations/backup` | `BackupStatusCard`: last full, last differential, last archive check, WAL archived until, each with age and a **Stale** state as text plus icon; the objectives (RPO 15 min, RTO 4 h) shown beside the last test's achieved values. `RestoreTestDueCard`: next due, and an **Overdue** state that is impossible to miss. `RestoreTestsTable` (paged): date, environment, outcome, RPO, RTO, performer. Detail: the checks list and the drill output in a `<pre>` (text, never markup). ⭐ `RecordRestoreTestForm`: a **Paste the drill JSON** area that parses `restore-drill.sh`'s output and fills the fields, which stay editable, plus notes and an optional "supersedes" selector. **It never asks for RPO, RTO or the outcome** — the server computes them (HO-XCT-001 ADR-313) — and the result panel shows what the server decided |
| `/staff/admin/performance` | `WebVitalsSummary`: date range (default 28 days), metric tabs (LCP, INP, CLS), viewport-class filter; a table of route template, sample count, p75, budget, verdict (**Pass / Fail / Not enough data**, text plus icon); a `recharts` bar chart of p75 by route against the budget line; an explanatory note that these are real users at the 75th percentile, and that fewer than 50 samples gives no verdict |
| Admin nav | Entries for **Retention**, **Backup & restore** and **Performance** under HO-ADM-002's admin section, each hidden when the caller lacks the action (presentation only — the server still decides, ADR-FE-009) |
| Overdue banner | When a signed-in Administrator loads any admin route and the restore test is overdue, a dismissible-per-session banner links to the backup page (OQ-11 in HO-XCT-001 says no notification; this is the visible substitute) |

### 11.6 Performance instrumentation and budgets

| Piece | Behaviour |
|---|---|
| `components/observability/WebVitalsReporter.tsx` (root layout, client) | `useReportWebVitals` → `lib/observability/web-vitals.ts`: map the metric, resolve the **route template** from `useSelectedLayoutSegments()` / the App Router pattern (never `location.href`), classify the viewport, read `navigator.connection?.effectiveType` when present, buffer, and flush on `visibilitychange` (hidden) and on route change through `sendTelemetry()`. Never on the `(staff)` or `(offline)` routes — the requirement is the storefront, and staff pages are not measured |
| `web/perf-budgets.json` | `{ "lcpMs": 2500, "firstLoadJsKb": 170, "routes": ["/", "/shop", "/shop/[slug]", "/cart", "/checkout", "/portal"] }` (OQ-5). A named route that no longer exists fails the script, so the budget cannot rot |
| `web/scripts/check-js-budget.mjs` | Reads the Next build output (`.next/app-build-manifest.json` plus the file sizes), gzips each first-load bundle, and fails with a per-route table when any budget route exceeds `firstLoadJsKb` |
| `web/scripts/route-inventory.mjs` | Walks `web/app/**/page.tsx`, producing every route with its group and a sample parameter set (from the MSW fixtures), for the sweeps in §16 |
| `web/tests/perf/lcp.spec.ts` | Per budget route: a fresh context, CDP `Network.emulateNetworkConditions` (Slow 4G) + `Emulation.setCPUThrottlingRate(4)`, `PerformanceObserver` for `largest-contentful-paint`, 5 runs, **median** compared to `lcpMs`. Chromium only (`test.skip(browserName !== 'chromium')`) |
| `web/tests/perf/viewport-sweep.spec.ts` | Every route in the inventory at 360 px: `scrollWidth <= clientWidth + 1`, no element wider than the viewport, and no control clipped. Then 768 / 1024 / 1440 px |
| `web/tests/a11y/app-sweep.spec.ts` | axe over every route in the inventory; zero serious or critical. Failures are reported per owning handover (ADR-FE-148) |

### 11.7 Error mapping — `lib/api/errors.ts` (append)

`RecordIsRetained` → *"This record is kept for legal reasons and can't be deleted."*
`CustomerRecordMerged` → *"This record was merged into another customer. Erase the surviving record instead."*
`RetentionRunAlreadyInProgress` → *"A retention run is already running. Try again when it finishes."*
`ReapplyListTooLarge` → *"That's more than 500 customers. Split the list."*
`InvalidRestoreTestTimes` / `RestoreTestChecksRequired` / `RestoreTestAlreadyRecorded` → their field-level messages.
`ActiveBranchRequired` → *"Choose a branch first."*
`OfflineError` → §11.3's wording. The server's message is preferred when present; these are fallbacks.

---

## 12. Specified changes — scaffold, config, mocks

### 12.1 The service worker — `web/public/staff/sw.js`

Hand-written, ~120 lines, no build step, no dependency.

| Concern | Rule |
|---|---|
| Scope | Served from `/staff/sw.js`, so its scope is `/staff/`. **No `Service-Worker-Allowed` header** and no attempt to widen it |
| `install` | Pre-cache `/staff/offline` (the static shell) and its referenced `/_next/static/**` assets, extracted from the fetched HTML, into `purrcare-shell-v{N}` |
| `activate` | Delete every cache whose name is not the current version; `clients.claim()` |
| `fetch` — navigations | Network-first with a 4 s timeout; on failure serve the cached `/staff/offline` shell (so a reload while offline lands on the board) |
| `fetch` — `/_next/static/**`, `/staff/icons/**` | Cache-first (hashed, immutable), stored in `purrcare-assets-v{N}` |
| `fetch` — **anything under `/api/`** | **Never touched**: `return` without calling `respondWith` |
| `fetch` — any other origin or any other path | Untouched |
| `message` | `{type:'warm-shell'}` → refetch and re-cache the offline shell and its assets. `{type:'purge'}` → delete every cache. `{type:'version'}` → reply with the version |
| Personal data | The worker stores none. The snapshot lives in IndexedDB, written by the app |

`lib/offline/sw-register.ts`: register on a `(staff)` route **only** when
`process.env.NODE_ENV === 'production'`, `NEXT_PUBLIC_USE_MOCKS !== 'true'` and the device flag is on;
post `warm-shell` after each successful snapshot write; `unregister()` plus `purge` when the flag goes
off, on sign-out, and whenever mocks are enabled (so a developer who ran a production build once is not
left with a stale worker).

### 12.2 `next.config.ts` (scoped edit) and the offline shell

- Headers: `/staff/sw.js` → `Cache-Control: no-cache` and `Content-Type: application/javascript`;
  `/_next/static/*` keeps Next's immutable defaults. If a CSP is added by then, it must allow
  `worker-src 'self'` — **adding a CSP is not this slice's job** (§20.5 reports its absence instead).
- `app/(offline)/staff/offline/page.tsx` exports `export const dynamic = 'force-static'` and contains
  **no server data**, so the shell is cacheable and safe.
- `.env.local.example` gains nothing. There is no new environment variable, and telemetry must not be
  gated by a `NEXT_PUBLIC_*` switch that would ship in the bundle.

### 12.3 Mocks — `web/mocks/handlers/operations/*.ts`

Generated from HO-XCT-001 §12.4. Fixtures must include what the screens branch on:

- A snapshot with both sections; one with `appointments` omitted `NotPermitted`; one with
  `boardingTasks` omitted `NotAvailable`; one with a high-risk cat and a handling note; one already
  expired (`validUntil` in the past); one belonging to a different `staffAccountId`.
- `304` on a matching `If-None-Match`, and a changed ETag after a status change.
- A ping handler that can be switched to fail, so offline behaviour is testable without a service
  worker (§5.2).
- Retention: a schedule covering every class; runs succeeded and `CompletedWithErrors`; a review report
  with and without rows; erasure requests `Open` with a held scope, `Completed`, and one `Failing`.
- Erasure: success, `isReapplication: true`, `CustomerRecordMerged`, `403`.
- Operations: fresh and stale backups; no restore test ever (overdue); a passed and a failed test; a
  drill JSON blob that the paste parser must accept, and a malformed one it must reject.
- Web vitals: routes passing, failing and with `InsufficientData`.
- Every `403` for a caller lacking each action.

---

## 13. Allowed scope

```
web/app/(offline)/**
web/app/(staff)/layout.tsx                                   ← ConnectivityProvider, OfflineBanner, OfflineSnapshotSync, ServiceWorkerRegistrar, admin nav entries only
web/app/(staff)/staff/admin/retention/**
web/app/(staff)/staff/admin/operations/**
web/app/(staff)/staff/admin/performance/**
web/app/(staff)/staff/customers/[customerId]/page.tsx        ← ErasePersonalDataPanel only (HO-ACC-002's page)
web/app/layout.tsx                                           ← <WebVitalsReporter /> and next/font only (ADR-FE-148)
web/components/{offline,retention,operations,observability}/**
web/lib/offline/** · web/lib/connectivity/** · web/lib/observability/**
web/lib/api/{offline,telemetry,retention,operations,admin-erasure}.ts
web/lib/api/client.ts                                        ← offline guard, failure reporting, sendTelemetry only
web/lib/api/types.ts · web/lib/api/errors.ts                 ← append
web/lib/hooks/**                                             ← new hooks only
web/components/common/**                                     ← submit/confirm controls consuming useWriteEnabled() only
web/public/staff/sw.js · web/public/staff/icons/**
web/next.config.ts                                           ← §12.2 headers only
web/perf-budgets.json · web/scripts/{check-js-budget,route-inventory}.mjs
web/package.json                                             ← scripts only: perf:lcp, perf:js, sweep:viewport, sweep:a11y. NO dependency change
web/mocks/handlers/operations/** · web/mocks/handlers/index.ts
web/tests/components/{offline,retention,operations}/**
web/tests/{perf,a11y}/**
web/tests/e2e/xct-*.spec.ts
web/docs/decisions.md                                        ← ADR-FE-139…148
docs/test-cases/TC-HO-XCT-002.md · docs/gates/HO-XCT-002-*.md
```

## 14. Forbidden scope

| Path | Why |
|---|---|
| `src/**`, `tests/**`, `deploy/**` | The backend and its operations, HO-XCT-001's |
| `.ai/**`, `.claude/**` | Backend architecture and agents |
| `docs/SRS.md`, `docs/USER_STORIES.md`, `docs/handover/**` | Inputs. §5.3 amendments are recorded by the README owner |
| Every other HO-CAT-002 / HO-ACC-002 / HO-ADM-002 / HO-NTF-002 / HO-APT-002 file beyond the lines in §13 | Their slices. A performance, accessibility or overflow failure in one of their routes is a **finding**, not an edit (ADR-FE-148) |
| `web/package.json` dependencies, `tsconfig.json`, `eslint.config.mjs` | §10. Scripts only |
| Storefront, portal and checkout route files | HO-SHP-002 / HO-ORD-002 / HO-POR-002 |

---

## 15. Component test cases

| ID | Case | Expected |
|---|---|---|
| **A — offline** | | |
| U-A1 | Monitor: 1 ping failure; 2 consecutive; then a success | `online` → `offline` after the second → `online` again |
| U-A2 | Monitor: a `500` from ping | `degraded`, not `offline`; the offline board is not offered |
| U-A3 | Monitor: `navigator.onLine === false` plus one failure | `offline` immediately |
| U-A4 | `OfflineSnapshotSync` with the device flag off | No request, nothing written |
| U-A5 | `200` then `304` | Record written once; `fetchedAt` updated on the `304`; `etag` sent the second time |
| U-A6 | `401 session_invalid` during sync | Store cleared; `401 session_idle_timeout` → store untouched |
| U-A7 | A form with typed values while the monitor flips to `offline`; submit | Values intact; submit disabled with a visible reason; **no request is sent**; on recovery the same values submit successfully |
| U-A8 | `client.ts` `POST` while offline | Throws `OfflineError` before `fetch`; mapped message rendered; nothing queued |
| U-A9 | `OfflineBanner` in each state | Offline / degraded / recovered wording; `role="status"`; text not colour; link to the board only when a valid snapshot exists |
| U-A10 | `OfflineBoard` with a valid snapshot | Both lists, sorted, with the saved-at time; **no action button anywhere in the DOM** |
| U-A11 | Expired snapshot; different `staffAccountId`; no snapshot | Three distinct explanations, no data rendered |
| U-A12 | `boardingTasks` omitted `NotPermitted` / `NotAvailable` | Each reason stated in its own words |
| U-A13 | High-risk cat in the board | Badge plus handling note, text and icon |
| U-A14 | `OfflineIdleLock` after `idleTimeoutMinutes` of no input | Data replaced by the locked message; pointer or key input before that resets the timer |
| U-A15 | Recovery while on the board | Routed to `?from=`, queries invalidated, announcement made |
| U-A16 | Sign-out; person change | Store cleared in both cases (spy on the store) |
| U-A17 | `sw-register` in mock mode, in development, and in production with the flag off | Never registers; unregisters and purges an existing worker |
| **B — retention and operations** | | |
| U-B1 | `RetentionScheduleTable` | Every class with its period from the settings and its end-of-period action; "Review only" for clinical, financial, regulatory, audit |
| U-B2 | `RunRetentionNow` confirm; `409` | States what it will not do; the in-progress message |
| U-B3 | Run detail with a `CompletedWithErrors` run | Error codes glossed in plain language |
| U-B4 | Erasure ledger detail with a held scope | "Held until 12 Mar 2032 — NFR-LEG-5 acceptance evidence"; **no name, phone or email anywhere in the DOM** |
| U-B5 | `ErasePersonalDataPanel` confirmation | Lists what is erased and what is kept with its period; names the customer once; requires channel, identity check and reason |
| U-B6 | Panel for an already anonymised customer; `CustomerRecordMerged` | The "erased on {date}" state; the merged message with a link |
| U-B7 | `ReapplyAfterRestoreForm` with 501 ids; with malformed ids | Blocked before submit, with counts |
| U-B8 | `RecordRestoreTestForm`: paste a valid drill JSON; paste rubbish | Fields filled and editable; a clear parse error that does not lose what was pasted |
| U-B9 | The same form | **No RPO, RTO or outcome input exists**; the result panel shows the server's computed values |
| U-B10 | `BackupStatusCard` with a 27-hour-old backup, and with a fresh one | Stale state as text plus icon / normal |
| U-B11 | `RestoreTestDueCard` with no test ever; overdue; due in 40 days | Overdue, overdue, fine |
| U-B12 | `WebVitalsSummary` rows | Pass / Fail / Not enough data as text; budget shown beside p75 |
| U-B13 | Every admin screen with a `403` | `PermissionDeniedState`, no partial data |
| **X** | | |
| U-X1 | Every new form: a server `400` | Mapped to its field; `errorCode` not in the DOM |
| U-X2 | Storage audit after the whole component suite | `localStorage` and `sessionStorage` empty; IndexedDB holds only `snapshot` and `device`; no token, password or draft in either |
| U-X3 | `web-vitals.ts` payload for `/shop/abc-123` | `routeTemplate: "/shop/[slug]"`; no URL, query, user or session field |
| U-X4 | `sendTelemetry` call options | `credentials: 'omit'`, `keepalive: true`, no `X-CSRF-Token` |
| U-X5 | The reporter on a `(staff)` route | Sends nothing |

## 16. End-to-end test cases

MSW first, then the real API, both runs recorded. Offline reload cases are **real-API, production
build only** (§5.2).

| ID | Case | Expected |
|---|---|---|
| E-A1 | **AC-19**: sign in as reception, open the front desk with appointments and boarding tasks, opt the device in, wait for one snapshot, then `context.setOffline(true)` | Within 30 s the banner appears; the front desk keeps showing today's list; every action is disabled with a reason |
| E-A2 | While offline, open the offline board from the banner | Both lists, read-only, with the saved-at time |
| E-A3 | While offline, try to check a cat in | Blocked before any request; the explanation shown; nothing is queued (no request fires on recovery) |
| E-A4 | While offline, **reload the page** | The service worker serves the offline shell; the board renders from IndexedDB — **the story's hardest case** |
| E-A5 | While offline, navigate directly to `/staff/front-desk` | Redirected to the offline board with the explanation, not to a dead session guard |
| E-A6 | `context.setOffline(false)` | Banner turns to "Back online", the queries refetch, the front desk shows live data again — **US-XCT-01 AC3** |
| E-A7 | Offline, idle past the timeout (fake timers) | The board locks; input does not unlock it; after reconnecting and signing in it works again |
| E-A8 | Sign out, then go offline and open `/staff/offline` | "No offline copy on this device" — the store was wiped |
| E-A9 | Staff A opts in and syncs; staff B signs in on the same device | A's snapshot is cleared before B's is written; the board never shows A's data to B |
| E-A10 | Device opt-in off | No snapshot request, no service worker, and the banner offers no board |
| E-B1 | **NFR-PRF-1** lab budget over the storefront routes | Median LCP ≤ 2 500 ms on Slow 4G + 4× CPU. `BLOCKED` with a stated reason until HO-SHP-002 exists |
| E-B2 | First-load JS budget | Every budget route ≤ 170 KB gzipped; the script's table is attached to the test sheet |
| E-B3 | **NFR-CMP-2** 360 px sweep over every route in the inventory | No horizontal scroll, no clipped control, at 360 / 768 / 1024 / 1440 px |
| E-B4 | Browse the storefront and complete a flow with the reporter on, then open `/staff/admin/performance` | Samples arrive; p75 rows appear; the verdict says "Not enough data" below 50 samples |
| E-B5 | Telemetry request inspection during E-B4 | No cookie header; no `X-CSRF-Token`; body carries no URL, id or user field |
| E-C1 | Administrator erases a customer from the customer page | Confirmation names them; success; the record shows the placeholder; the ledger lists the request; the audit log (HO-ADM-002) shows the staff action — **US-XCT-06 AC1** |
| E-C2 | Open the ledger detail for that customer | Scopes with statuses; a held scope with its date and basis; no personal data anywhere |
| E-C3 | Run retention now, then open the run detail | Passes with counts; nothing clinical deleted (cross-checked against a cat created by the test) |
| E-C4 | Open the retention schedule, change `retention.clinicalYears` on the settings screen, return | The new period shown; "Review only" unchanged |
| E-C5 | Reapply-after-restore with three ids | The result panel's four counts match the API |
| E-D1 | Backup page with a stale backup and no restore test | Both problems visible without scrolling; the overdue banner appears on every admin route |
| E-D2 | Record a restore test by pasting the drill JSON | `201`; the row appears with the server-computed RPO, RTO and outcome; a failing drill shows **Failed** — **US-XCT-05 AC2** |
| E-X1 | axe over every route in the inventory, including the offline board, the locked board and the banner | Zero serious/critical. New findings in other slices' routes are reported, not fixed |
| E-X2 | Keyboard-only pass over the offline board and every new admin screen | Fully operable; visible focus |
| E-X3 | Storage audit after the whole suite | Only the two sanctioned IndexedDB stores; `localStorage` / `sessionStorage` empty; `pc_session` unreadable from `document.cookie` |

---

## 17. Implementation order

1. Confirm the prerequisites (§5) — otherwise stop.
2. Types and API modules from HO-XCT-001 §12.4; MSW handlers; `lib/api/errors.ts` entries.
3. Connectivity monitor, `client.ts` guard and failure reporting, `OfflineBanner`, `useWriteEnabled`
   (U-A1…U-A9 green before any caching exists).
4. `lib/offline/db.ts`, snapshot record, `OfflineSnapshotSync`, wipe rules (U-A4…U-A6, U-A16).
5. The offline board, its settings page, the idle lock, recovery (U-A10…U-A15).
6. The service worker and its registration rules; production build; E-A4 and E-A5.
7. Retention screens, the erasure panel, the reapply tool.
8. Backup and restore page; the drill-JSON paste parser.
9. Web-vitals reporter, the performance page, `perf-budgets.json`, the two scripts, the perf and sweep
   specs.
10. U-series, E-series on MSW, then the E-series against the real API per HO-XCT-001 phase.

```bash
cd web
npm run lint && npm run typecheck && npm run test
npm run build && npm run perf:js                 # first-load JS budget
npm run start & npx playwright test tests/perf tests/a11y tests/e2e   # LCP, sweeps, E-series
```

---

## 18. Acceptance criteria

| # | Story | Criterion | Evidence |
|---|---|---|---|
| A1 | XCT-01 AC1 | With connectivity lost, today's appointment list **and** boarding task list stay visible from local cache, read-only | U-A10, U-A12, E-A1, E-A2 |
| A2 | XCT-01 AC1 | They survive a reload while offline | **E-A4**, E-A5 |
| A3 | XCT-01 AC2 | The offline state is announced clearly, and write actions are disabled rather than failing silently — with nothing typed lost and nothing queued | U-A7, U-A8, U-A9, E-A3 |
| A4 | XCT-01 AC3 | When connectivity returns, the view refreshes by itself | U-A15, E-A6 |
| A5 | XCT-01 | The cached copy is bounded, person-bound and wiped: expiry, sign-out, person change, opt-out | U-A11, U-A14, U-A16, E-A7, E-A8, E-A9, E-A10 |
| A6 | XCT-01 | Offline support never reaches customers, and never breaks mock or development mode | U-A17, ADR-FE-139 |
| A7 | XCT-02 AC1 | LCP is measured in the field at p75 per route and judged against 2.5 s | U-X3, E-B4 |
| A8 | XCT-02 AC1 | A lab budget fails the build when a storefront route regresses | E-B1 *(blocked until HO-SHP-002)*, E-B2 |
| A9 | XCT-02 AC2 | Every flow completes at 360 px with no horizontal scrolling | **E-B3** |
| A10 | XCT-02 | Telemetry identifies nobody | U-X4, U-X5, E-B5 |
| A11 | XCT-06 AC1 | A staff-taken deletion request anonymises the customer, states what is kept and why, and is visible in the ledger | U-B5, U-B6, E-C1, E-C2 |
| A12 | XCT-06 AC2 | The screens make it clear that nothing clinical is ever deleted: schedule, review-only, run results | U-B1, U-B3, E-C3, E-C4 |
| A13 | XCT-05 AC1 | Backup health, WAL freshness and the measured RPO/RTO of the last test are visible | U-B10, E-D1 |
| A14 | XCT-05 AC2 | A quarterly restore test can be recorded from the drill output, and an overdue test is unmissable | U-B8, U-B9, U-B11, E-D2 |
| A15 | XCT-03 / UI-6 | Zero serious or critical axe findings across every route, including the offline board | E-X1, E-X2 |
| A16 | XCT-04 | Errors, including offline ones, say what happened and what to do, with no code or id | U-X1, U-A8 |
| A17 | NFR-SEC-4 / ADR-FE-141 | The only browser persistence is the sanctioned snapshot and device flag | U-X2, E-X3 |

**Deferred and named**: E-B1's storefront rows (HO-SHP-002), the boarding half of E-A1/E-A2
(HO-BRD-002 and HO-XCT-001's C-BRD-1 — the section is rendered from the snapshot and reads
`NotAvailable` until then), and fixes for any budget, sweep or axe failure in another slice's route
(ADR-FE-148).

---

## 19. Required gates

| # | Gate | Agent | Focus |
|---|---|---|---|
| 1 | ARCHITECTURE | `architecture-reviewer` | §8 layering; **no new dependency** (§10); the service worker's scope and its never-cache-API rule (ADR-FE-139/140); the connectivity store as a device fact rather than a global app store; scoped edits to HO-ACC-002, HO-APT-002, HO-CAT-002 and the layouts match §13 exactly; ADR-FE-139…148 recorded |
| 2 | SECURITY | `security-reviewer` | **ADR-FE-141 end to end**: what is stored, how little, and every wipe path (U-A16, E-A8, E-A9, E-X3); the offline board's person binding and idle lock; no HTML or API response cached; telemetry anonymity (U-X4, E-B5); the erasure confirmation showing the name only once and never after; the ledger screen leaking no personal data; no `dangerouslySetInnerHTML` for drill output; nothing weakening CSP |
| 3 | CODE_REVIEW | `code-reviewer` | One `fetch` call site; hooks in `lib/hooks`; component size; time only through `lib/format`; the service worker's cache-version handling; no dead MSW/SW interaction |
| 4 | TEST | `test-engineer` | `TC-HO-XCT-002.md` from §15/§16 **first**; mock and real runs recorded separately; E-A4 genuinely reloads offline on a production build; E-B1/E-B2 attach their measured numbers; blocked rows carry a stated reason |
| 5 | ACCEPTANCE | `acceptance-reviewer` | Every §18 row; the deferrals named above recorded as deferrals; the §5.3 amendments confirmed present in the target handovers |

---

## 20. BLOCKED conditions — stop and report

**20.1 A prerequisite is missing** (Node, the HO-CAT-002 scaffold, HO-ACC-002, HO-ADM-002, HO-NTF-002,
HO-APT-002). Do not build a parallel shell, session model or admin nav.

**20.2 The §5.3 amendment rows are not recorded**, in particular HO-ACC-002's storage carve-out. Stop
before writing anything to IndexedDB. The security gate reads that carve-out as the licence for this
whole feature.

**20.3 An offline requirement seems to need writes** — a queued check-in, a "sync later" button, a local
edit. It contradicts NFR-AVL-3 and ADR-FE-143. Report it; do not build an offline write path.

**20.4 The real API disagrees with HO-XCT-001 §12.4** — a missing field, a different ETag behaviour, a
snapshot that is not `no-store`. Report it; do not shim, and do not cache an API response in the
service worker to compensate.

**20.5 The app has no CSP, or the CSP blocks the worker.** Adding or changing a CSP is not in this
slice's scope. Report it as a finding (HO-ACC-002 §7 assumes one exists).

**20.6 A new dependency seems necessary** (§10) — a PWA plugin, an IndexedDB wrapper, Lighthouse, a
web-vitals package. Raise it. Each has an in-repo answer in this document.

**20.7 A storefront route cannot meet the LCP or JS budget.** Report it against HO-SHP-002 (or the
owning handover) with the measurement. **Do not edit that route** (ADR-FE-148), and do not relax
`perf-budgets.json` to make the build green.

**20.8 A route fails the 360 px sweep or axe.** Same rule: a finding against its owner, never a silent
edit and never an exclusion added to the sweep.

**20.9 Mock mode and the service worker collide** (MSW stops intercepting). That means the registration
rules in §12.1 were not followed. Fix the rules; never disable MSW to make an offline test pass.

**20.10 A requirement conflict is a stop, not an edit.** The SRS, the backlog and every handover are
inputs.
