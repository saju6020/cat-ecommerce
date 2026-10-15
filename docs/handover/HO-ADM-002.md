# HO-ADM-002 — Administration and Configuration (frontend)

## 1. Identity

| Field | Value |
|---|---|
| Handover ID | **HO-ADM-002** |
| Title | Administration and Configuration — frontend slice |
| Date | 2026-09-22 |
| Author | Claude Opus 5 |
| Backlog | [`docs/USER_STORIES.md`](../USER_STORIES.md) v2.4 §14 — **US-ADM-01 … US-ADM-08** |
| SRS | FR-ADM-1 … 8; **UI-1, UI-3, UI-4, UI-6**; NFR-USA-4, NFR-USA-5, NFR-USA-6; NFR-SEC-2; NFR-LEG-5 |
| Pairs with | [`HO-ADM-001`](HO-ADM-001.md) — **§12.4 there is the contract**. On any disagreement about the wire, HO-ADM-001 wins (§20.5) |
| Builds on | [`HO-CAT-002`](HO-CAT-002.md) (scaffold, stack, conventions) and [`HO-ACC-002`](HO-ACC-002.md) (session guards, `client.ts`, `PermissionDeniedState`, error mapping, `(staff)`/`(portal)`/`(auth)` groups) |
| Delivery order | Section 3 of 17 in [`README.md`](README.md#delivery-order) |
| Approved design | None. Build to §11 with the existing shadcn/ui tokens |
| Baseline commit | `4007225` |
| Amended | **2026-09-22 — by [`HO-NTF-002`](HO-NTF-002.md), before implementation.** `adminNavItems` gains a **Message templates** entry. *(append further rows here)* **2026-09-26 — by the integrator (owner decision, [`HO-ADM-001`](HO-ADM-001.md) Amended (28)).** Creating a policy document and publishing a new version need the new action `Policies.Manage`, held by Administrator only; `adminNavItems` gains `Policies.Manage → Administrator`. §11.6 `PolicyList`/`PolicyDetail` stay visible to Shop Managers (the list is still `Content.Manage`), but "New policy" and "Publish new version" are shown only to a caller holding `Policies.Manage`. The server already refuses these calls with `403`, so this is presentation only. **2026-09-28 — self-amendment, this handover's own build.** (1) **ADR numbering collision.** §9's `ADR-FE-013…018` collides with `web/docs/decisions.md`'s existing `ADR-FE-013` (HO-ACC-002's own thirteenth ADR, one beyond its README-allocated `001…012`, already on master at this branch's baseline `08e873e`). This handover's six ADRs are recorded as `ADR-FE-014…019` instead, with a note in `decisions.md` itself; the README table itself is not edited (out of this handover's file scope). (2) **Two out-of-§13 files, each necessary and minimal.** `web/nav/areas/administration.ts` — not named in §13 — gains two entries (`Administration` → `/staff/admin/branches`, `My leave` → `/staff/leave`) so the area is reachable from the staff sidebar at all; without it every `/staff/admin/**` and `/staff/leave` route would be reachable only by typing the URL directly. `web/app/(auth)/policies/[key]/page.tsx` — also not named in §8.1's own route list, which names only `pages/[slug]`/`faqs` under `(auth)` — renders `GET /api/policies/{key}` (already public per §12.4) as a readable page; `PolicyAcceptancePrompt`'s "read the full text" link (§11.6) needs somewhere real to send a customer, and linking directly to the JSON API endpoint would not be "the full text" in any usable sense. (3) **`ActiveBranchPicker` placement.** Mounted in `app/(staff)/layout.tsx` (that file's own scoped edit) as the first element above every staff page's own content, not inside `StaffShell`'s header markup (`components/common/staff-shell.tsx`, outside §13). Flagged for a human reviewer as a visual-placement decision, not a scope violation — see `web/docs/decisions.md` ADR-FE-016's own note. (4) **§11.4's `RosterWeekView` staff-name join.** HO-ADM-001 §12.4's `GET .../rosters` response is not documented as carrying a display name, only `staffAccountId`; rather than invent one (§20.4), the roster page joins names from the already-existing `GET /api/staff-accounts` (HO-ACC-002) list, the same way `LeaveRequest.staffFullName` is treated as server-optional in `lib/api/leave.ts`. (5) **No calendar-visualisation component built.** §11.2's own component list (`BranchTable`, `BranchDetailsForm`, `WeeklyHoursEditor`, `ClosureList`/`ClosureForm`, `RoomList`/`RoomForm`, `ActiveBranchPicker`, `StaffBranchAssignmentPanel`) never names one, so E-B1's "calendar in the detail view skips the holiday" is not independently demonstrable beyond the closure appearing in `ClosureList` — the mock's own `GET .../calendar` endpoint is implemented for contract completeness with no FE consumer. Flagged, not invented. (6) **Active branch state reworked to §8.3 shape after ARCHITECTURE gate HIGH (owner decision 2026-09-28).** The gate's first pass (`docs/gates/HO-ADM-002-ARCHITECTURE.md`) found `lib/branch.ts` built as a module-level variable plus a subscriber list read through `useSyncExternalStore` — outside §13, not recorded here, and a global store forbidden by HO-ACC-002 §4.3, replacing this handover's own approved §8.3 shape. The owner chose to rework rather than accept the deviation. `lib/branch.ts`'s `getActiveBranchId()` (the one function the baseline `08e873e` comment allows this handover to replace the body of) now reads `['staff', 'active-branch']` straight out of the app's `QueryClient` cache; `lib/hooks/useActiveBranch.ts` (§13, new hooks only) seeds and reads that same key with `setQueryData`/`useQuery` instead of the removed external store; `components/branches/ActiveBranchPicker.tsx` (§13) is unchanged, since its own hook calls kept the same names and signatures. One additional file outside §13 was touched, minimally: `web/app/providers.tsx` now calls a new `lib/branch.ts` export, `registerQueryClient()`, from a `useEffect`, passing it the single `QueryClient` instance it renders `QueryClientProvider` with — the registration deliberately flows `app/` → `lib/branch.ts` (correct per HO-CAT-002 §8.2's one-direction layering), and `registerQueryClient()` holds a reference to the one real store, not a second copy of the active-branch value. That call has to sit in an effect, not in the `useState` lazy initialiser that creates the `QueryClient`: verified against a real `next dev` build rather than assumed, React Strict Mode there calls that initialiser twice on mount and keeps only one call's result as the real state, so a registration made inside it can permanently register the discarded instance — `ActiveBranchPicker`'s own context-based read of the cache kept working throughout, while `apiFetch`'s registration-based read silently saw an unregistered client and sent no `X-Branch-Id` header at all (caught by this handover's own E-B2 e2e case, which failed against the initialiser-based version and passes against this effect-based one). `web/docs/decisions.md` ADR-FE-016 is corrected to describe this shape and to record the deviation-and-rework as its own Status note; `web/tests/components/branches/active-branch-picker.test.tsx` (§13) now asserts against the test's own `QueryClient` cache instead of the removed `getActiveBranchId`/`resetActiveBranchIdForTests` exports. **2026-09-28 — self-amendment, TEST gate HIGH (second pass) fix.** `AuditLogSearch`'s text inputs and the content list's `kind`/`status` selects read `filters[field] ?? ''`/`kind ?? ''` straight from `useSearchParams()`; every edit is an async `router.push`/`replace`, so a re-render between an edit and that navigation's commit reset the field to the stale URL value, dropping keystrokes at realistic typing speed and silently losing whichever of two quick, back-to-back edits (e.g. two `<select>`s) landed first. Fixed with one new hook, `web/lib/hooks/useUrlFilterState.ts` (§13, new hooks only), giving each filter field local `useState` as its display source of truth, synced from the URL only when the URL's query string differs from the query this hook itself most recently asked the router for (`expectedQuery`) — never merely because its own write is still catching up. `web/lib/hooks/useFilterUrlUpdate.ts` (this branch's own hook, already named editable) gained the matching `pendingParams`/`expectedQuery` pair so a second quick edit always merges onto the first rather than a stale `searchParams` snapshot. A `popstate` listener was tried first as the "was this a real Back/Forward" signal and abandoned after it demonstrably read as "not external yet" on the one render that mattered — confirmed directly against this app that the App Router can already have updated `useSearchParams()`, with this effect already re-run, before a native `popstate` event reaches a `window` listener (Chromium's client prefers the Navigation API internally, whose `navigate` event fires ahead of the legacy `popstate` per spec); the query-string comparison has no such ordering dependency. `web/docs/decisions.md` gains `ADR-FE-020` for this shape. Both `app/(staff)/staff/admin/audit-log/page.tsx` and `.../content/page.tsx` (§13) now source their filter values from `useUrlFilterState` instead of `useSearchParams()` directly. **2026-09-28 — self-amendment, addition to the item immediately above (TEST gate HIGH, third pass — narrowed but not closed by the second-pass fix).** The `expectedQuery` single-"last write"-string comparison above could still mistake an **intermediate, already-superseded** write of its own for an external navigation: `useSearchParams()` does not necessarily skip straight from one of the hook's writes to the next, and the real History API commit can lag `router.push`/`replace` by several hundred ms — reproduced non-deterministically (roughly one dropped character in a handful of runs, a different character each time) at ordinary 150ms/key typing speed, both chromium and mobile-360. `web/lib/hooks/useFilterUrlUpdate.ts` and `web/lib/hooks/useUrlFilterState.ts` were reworked (`expectedQuery` removed from both) to track **every** write a hook has made that the URL hasn't yet been observed to reflect — `pendingWrites`, a list, oldest first — so the URL matching *any* entry, not only the newest, is recognised as the hook's own write landing (dropping it and everything before it, never resyncing); only a URL matching nothing in the list is treated as external. `web/lib/hooks/useUrlFilterState.ts` additionally gained a `debouncedFields` parameter: `AuditLogSearch`'s five free-text inputs (not its two date fields, not the content list's two selects) now write to the URL ~300ms after the last keystroke rather than on every one (`FILTER_WRITE_DEBOUNCE_MS`), flushed immediately by a new `flush(name)` the hook returns, wired to each field's own blur and Enter key via a new, optional `onFilterCommit` prop on `web/components/audit/AuditLogSearch.tsx` (§13) — the field's own displayed value is never debounced, only the URL write. `web/docs/decisions.md` ADR-FE-020 is corrected in place (Status note) to describe this shape. **2026-09-29 — self-amendment, ACCEPTANCE gate F-H1 (§18 A3) fix.** A server `403` now shows `PermissionDeniedState` on every page that could spin or mis-render it: tax-class detail and service detail (each rendered `LoadingState` while `!data`, so any error spun forever — now 403 → `PermissionDeniedState`, 404 → not-found, other error → `ErrorState` with retry), the branch roster page (rosters/availability/exceptions 403), and, from the same sweep, `admin/system` and `admin/settings` (non-403 error spun), the closures and rooms tabs, and `/staff/leave` (list 403). Create/edit forms (`ServiceForm`, `StaffAccountForm`, `TaxClassForm`, `BranchDetailsForm`, `ClosureForm`, `RoomForm`, `ContentEditor`, `PolicyList`, `LeaveRequestForm`, `RosterExceptionForm`) and `describeApiError`/`submitWithServerErrors` show a permission message on a `403` (`lib/api/errors.ts`: `describeUnexpectedError`, `PERMISSION_DENIED_MESSAGE`; the two §11.9 branch-assignment codes keep their own wording) instead of "Something went wrong. Please try again." Tests: `tests/components/admin/detail-pages-403.test.tsx`, `form-403-message.test.tsx`. **2026-09-29 — self-amendment, ACCEPTANCE gate F-H3.** `web/mocks/handlers/admin/content.ts` seeded `terms` (`requiresAcceptance`, version 2) as pending for the one shared seeded customer every area's e2e signs in as, so the non-dismissable `PolicyAcceptancePrompt` opened on every portal visit and broke HO-ACC-002's `account-area.spec.ts`. The seeded customer now starts with `terms:2` already accepted (`seedAcceptances()`); `/pending` is unchanged in behaviour and still lists anything newly required — a spec publishing its own required policy or version (E-F2, the E-X1 dialog-open case) makes it pending for that customer, and U-F3 overrides the handler. The mock state is `sessionStorage`-backed, so it is per browser context and does not leak between specs. **2026-10-04 — owner decision on §16 E-B1 (ACCEPTANCE F-M1).** §11.2 specifies no calendar view, so E-B1's second half ("calendar in the detail view skips the holiday") was not built or evidenced. The owner decided to **schedule the branch calendar view for later** rather than strike the clause: it is owed by a later handover (to be named when scheduled), whose ACCEPTANCE evidences E-B1's calendar half. Until then E-B1 is evidenced by the closure appearing in `ClosureList` and the mock's `GET .../calendar` contract; A1 does not depend on the calendar. Tracked as backlog ADM row 23. **2026-10-09 — E-B1's calendar half built by [`HO-ADM-002-FU1`](HO-ADM-002-FU1.md)** (MSW evidence in `docs/test-cases/TC-HO-ADM-002-FU1.md`; combined review `docs/gates/HO-ADM-002-FU1-REVIEW.md` still to be written; real-API E-CAL8 not yet run). |

---

## 2. Objective

Build the **back-office administration area**: branches, opening hours, holidays and rooms; staff
branch assignment and an active-branch picker; rosters and leave; the service catalogue with price
history and tax classes; business settings; the audit-log search and export; content, FAQs, banners
and policy versions; and the maintenance switch. And, outside the admin area, two small public
pieces: a renderer for published content pages, and the portal prompt that asks a signed-in customer
to accept an updated policy.

Three things make this harder than a set of CRUD screens:

1. **Time zones.** Opening hours, shifts and closures are wall-clock times *in the branch's time
   zone*, which may not be the browser's. Every time shown or entered here is in the branch's zone,
   labelled as such (ADR-FE-014).
2. **Effective dates.** Prices and tax rates are never edited — a new one is scheduled. The screens
   must make "current", "scheduled" and "past" unmistakable, and must not offer an edit that the
   server will refuse (ADR-FE-018).
3. **Authorization is the server's.** US-ADM-02 is met by the server; the UI hides navigation a role
   cannot use **as presentation only** and renders the server's `403` everywhere else (HO-ACC-002
   ADR-FE-009).

---

## 3. In scope

### 3.1 Inherited — do not revisit

The HO-CAT-002 stack and layering, and HO-ACC-002's session model, `client.ts`, route groups, error
mapping and `PermissionDeniedState`.

### 3.2 Stories and screens

| Story | Screens | Route |
|---|---|---|
| US-ADM-01 | Branch list; branch detail with tabs **Details · Opening hours · Closures · Rooms**; staff branch assignment on the staff account page; active-branch picker in the staff header | `/staff/admin/branches/**`; staff account detail (HO-ACC-002) gains a **Branches** panel |
| US-ADM-02 | No screen of its own. Every admin page handles `403`; admin navigation is filtered by role for presentation | — |
| US-ADM-03 | Roster week view per branch; pattern editor per staff member (current / scheduled next); roster exceptions; **my leave** (request, list, cancel) for every staff member; **leave approvals** for administrators | `/staff/admin/branches/[branchId]/roster`, `/staff/leave`, `/staff/admin/leave` |
| US-ADM-04 | Service catalogue list and editor; price history with "schedule a new price"; tax classes and rate history | `/staff/admin/services/**`, `/staff/admin/tax-classes/**` |
| US-ADM-05 | Settings screen grouped by area, global and per-branch override | `/staff/admin/settings` |
| US-ADM-06 | Audit-log search with filters, paging and CSV export | `/staff/admin/audit-log` |
| US-ADM-07 | Content list and editor (pages, FAQs, banners) with preview and publish; policy documents with versions; public page renderer; portal policy-acceptance prompt | `/staff/admin/content/**`, `/staff/admin/policies/**`, `/pages/[slug]`, `/faqs`, `(portal)` layout |
| US-ADM-08 | Maintenance switch with message | `/staff/admin/system` |

---

## 4. Out of scope

| Excluded | Why |
|---|---|
| Any backend change | HO-ADM-001 owns `src/**` |
| Appointment, boarding or order screens that use rosters, hours or prices | Later areas |
| The storefront's banners and maintenance notice | HO-SHP-002 renders them from the public endpoints |
| Editing permissions | Not built (HO-ADM-001 ADR-044) |
| Rich-text/WYSIWYG editing, HTML input | Content is `LimitedMarkup` (HO-ADM-001 ADR-052) |
| A calendar, grid, scheduler, date-picker or time-zone library | §10 |
| Editing a price, tax rate, policy version or acceptance | Append-only (ADR-FE-018) |

Forbidden shapes are HO-ACC-002 §4.3's, unchanged: no global store, no token in JS, no `fetch` outside
`client.ts`, no hooks inside components, no `dangerouslySetInnerHTML`, no client-side authorization
decision.

---

## 5. Prerequisites

| Required | State |
|---|---|
| Node 20 LTS, HO-CAT-002 scaffold | ❌ at baseline — §20.1 |
| **HO-ACC-002 built** — staff guard, `client.ts` session/CSRF changes, `PermissionDeniedState`, staff account detail page | ❌ at baseline — §20.1 |
| HO-ADM-001 API | ❌ — build against MSW generated from §12.4, re-run every E-case against the real API (HO-ACC-002 §5.1's rules) |

---

## 6. Governing documents

| Document | What you need |
|---|---|
| [`HO-ADM-001`](HO-ADM-001.md) | §12.4 endpoints and shapes; §10.2 the settings registry; §10.4 codes to map; §8.5 availability semantics (so the roster view explains what it shows); §12.3 which roles see what |
| [`HO-ACC-002`](HO-ACC-002.md) | §8 session model; §8.6 accessibility additions; §11.2 client; §11.14 error mapping |
| [`HO-CAT-002`](HO-CAT-002.md) | §8–§10 layering, state, conventions, dependencies |
| [`docs/SRS.md`](../SRS.md) §3.1, §6.4 | UI-1/3/4/6; NFR-USA-4/5/6 |

---

## 7. Build rules

| Req | Rule | Where |
|---|---|---|
| **UI-1** | 360 px floor. The roster week view **switches to a day-by-day list below 768 px** — never a horizontally scrolling grid | §11.4, E-X2 |
| **UI-3** | Confirm, stating what is affected: deactivating a branch or room, cancelling a closure, approving/rejecting/cancelling leave, scheduling a price, changing a setting, publishing content or a policy, turning maintenance on | Each screen |
| **UI-4** | Lists paginate and filter; the audit log exports CSV | §11.7 |
| **UI-6** | WCAG 2.1 AA; the roster grid is a real `<table>` with row/column headers on desktop and a list on mobile; time inputs are labelled with the time zone | HO-ACC-002 §8.6 applies |
| **NFR-USA-4** | Server messages in plain language; `errorCode` never rendered | HO-ACC-002 §11.14 |
| **NFR-USA-5** | Every editor keeps its values across a validation error; `UnsavedChangesGuard` (HO-ACC-002 §11.7) on every editor | All editors |

---

## 8. Frontend architecture

### 8.1 Route map — additions

```
web/app/
  (staff)/staff/
    admin/
      layout.tsx                    ⭐ admin section nav (filtered by role for presentation)
      branches/page.tsx             list                                  US-ADM-01
      branches/new/page.tsx
      branches/[branchId]/layout.tsx  tabs
      branches/[branchId]/page.tsx    details
      branches/[branchId]/hours/page.tsx
      branches/[branchId]/closures/page.tsx
      branches/[branchId]/rooms/page.tsx
      branches/[branchId]/roster/page.tsx      week view + patterns + exceptions   US-ADM-03
      leave/page.tsx                approvals                                        US-ADM-03
      services/page.tsx · services/new/page.tsx · services/[serviceId]/page.tsx       US-ADM-04
      tax-classes/page.tsx · tax-classes/[taxClassId]/page.tsx                       US-ADM-04
      settings/page.tsx                                                                US-ADM-05
      audit-log/page.tsx                                                               US-ADM-06
      content/page.tsx · content/new/page.tsx · content/[contentId]/page.tsx          US-ADM-07
      policies/page.tsx · policies/[key]/page.tsx                                     US-ADM-07
      system/page.tsx               maintenance                                       US-ADM-08
    leave/page.tsx                  my leave (every staff member)                    US-ADM-03
  (auth)/                           public shell reused for public content
    pages/[slug]/page.tsx           published page renderer                          US-ADM-07
    faqs/page.tsx                                                                     US-ADM-07
  (portal)/layout.tsx               ⚠ gains PolicyAcceptancePrompt                   US-ADM-07
  (staff)/layout.tsx                ⚠ gains ActiveBranchPicker in the header         US-ADM-01
```

HO-ACC-002's staff-account detail page gains a **Branches** panel (`StaffBranchAssignmentPanel`).

### 8.2 API modules

`lib/api/admin-branches.ts`, `admin-rosters.ts`, `leave.ts`, `admin-catalogue.ts` (services, prices,
tax), `admin-settings.ts`, `admin-audit.ts`, `admin-content.ts`, `public-content.ts`, `system.ts`.

### 8.3 State

| Kind | Where |
|---|---|
| Server data (branches, rosters, leave, services, prices, settings, audit pages, content) | TanStack Query, one key family per resource; mutations invalidate their family |
| **Active branch** | `['staff', 'active-branch']` — seeded from `GET /api/staff/branches/mine` (primary); the picker updates the value in memory; `client.ts` sends it as `X-Branch-Id` on every request (ADR-FE-015). Not persisted — a reload returns to the primary branch |
| Audit-log filters, content list filters | **URL search params** (shareable, back-button safe) |
| Editor values | react-hook-form + zod |

### 8.4 Time and money helpers — `lib/format/`

- `formatInZone(instant, timeZoneId, pattern)` and `zonedLocalToLabel(date, time, timeZoneId)` built on
  **`Intl.DateTimeFormat` with `timeZone`** (ADR-FE-014). `date-fns` (already allowed) for date
  arithmetic on `yyyy-MM-dd` strings only — never on local `Date` objects for branch times.
- `formatMoney({ amount, currencyCode })` with `Intl.NumberFormat`; **amounts stay strings** end to
  end — parsed only for display, never through `Number` for arithmetic.
- Time inputs are `<input type="time">`; every time field's label ends with the zone, e.g.
  *"Opens (Asia/Dhaka)"*.

### 8.5 `LimitedMarkup` renderer — `components/content/LimitedMarkup.tsx` (ADR-FE-017)

A small in-repo parser for exactly HO-ADM-001 §8.8's markup — paragraphs, `##`/`###` headings, `- `
bullets, `**bold**`, `[text](url)` — producing React elements. Links: https or site-relative only;
external links get `rel="noopener noreferrer"`. Anything else renders as literal text. **No
`dangerouslySetInnerHTML`, no markdown package.** The editor's preview and the public page use the
**same** component, so what the editor sees is what the public gets.

---

## 9. ADRs and open questions

Record in `web/docs/decisions.md`, continuing HO-ACC-002's series.

| ADR | Decision | Why |
|---|---|---|
| **ADR-FE-013** | The admin area is a route-based section under `(staff)/staff/admin/` with its own nav layout; each admin resource is a route, not a modal stack | Deep-linkable; each page loads only its data |
| **ADR-FE-014** | **Branch time is rendered with `Intl.DateTimeFormat` in the branch's IANA zone; no time-zone library**; every time label names the zone | The browser's zone is irrelevant to when the clinic opens |
| **ADR-FE-015** | The active branch is TanStack state seeded from the server and sent as `X-Branch-Id` by `client.ts`; the picker is hidden when the person has one branch | One place attaches the header; nothing branch-related persists |
| **ADR-FE-016** | The roster view is a CSS-grid/`<table>` built in-repo, collapsing to a list below 768 px; no scheduler library | UI-1 at 360 px; no dependency |
| **ADR-FE-017** | Content is rendered from `LimitedMarkup` by one in-repo component shared by preview and public pages | No HTML path exists to exploit |
| **ADR-FE-018** | Effective-dated records (prices, rates, policy versions) are shown as **Current / Scheduled / Past** and are never editable; the only action is "Schedule a new …" with a date that cannot be in the past | Mirrors HO-ADM-001 ADR-049; prevents offering an edit the server refuses |

| # | Question | Default |
|---|---|---|
| OQ-1 | Should the roster view default to this week or next? | This week |
| OQ-2 | Wording of the policy-update prompt | *"We've updated our {title}. Please review and accept to continue using your account."* |
| OQ-3 | Can a customer dismiss the policy prompt? | No. Until they accept, only sign-out and the policy text itself are reachable; the rest of the portal stays behind the dialog |

---

## 10. Dependencies

**No new package.** A scheduler/calendar, date-picker, time-zone, rich-text, markdown or CSV library is
a §20 stop. CSV is downloaded from the server; nothing is generated client-side.

---

## 11. Specified changes — components and screens

Each component under ~150 lines, with distinct loading / empty / error states.

### 11.1 Admin shell and access

`AdminNav` — links filtered by the signed-in staff member's `roleIds` (from `['session','staff']`)
against a **presentation map** that mirrors HO-ADM-001 §12.3's action table *by name only*
(`adminNavItems`, one file). It is a convenience; every page still renders `PermissionDeniedState` on
a server `403`. U-A1 proves a hidden page is still refused by the server, not by the UI.

### 11.2 Branches — US-ADM-01

| Component | Behaviour |
|---|---|
| `BranchTable` | Name, code, zone, active; create button (admin) |
| `BranchDetailsForm` | Name, code (read-only after create), address, phone, email, **time zone** (a `<select>` of `Intl.supportedValuesOf('timeZone')`, default `Asia/Dhaka`); deactivate with confirm; the server's `LastActiveBranchCannotBeDeactivated` shown in plain language |
| ⭐ `WeeklyHoursEditor` | Seven day rows; each row 0–3 intervals (add / remove); "Closed" shown in text for a day with none; **"Copy Monday to weekdays"** helper; the whole week saved at once; client-side overlap/order checks mirror the server so errors appear on the offending row |
| `ClosureList` + `ClosureForm` | Upcoming and past closures (past read-only); form: kind, date range, optional half-day times (only when one date), reason; cancel future closures with confirm naming the dates |
| `RoomList` + `RoomForm` | Name, kind, active |
| `ActiveBranchPicker` | Header `<select>`; hidden with one branch; changing it resets branch-scoped queries |
| `StaffBranchAssignmentPanel` | Checkbox list of active branches + primary radio; on the staff account page (HO-ACC-002) |

### 11.3 Services, prices, tax — US-ADM-04

| Component | Behaviour |
|---|---|
| `ServiceTable` | Code, name, category, duration, **current price** (or "Not priced" badge — such a service cannot be booked), online-bookable, active |
| `ServiceForm` | Name, code, category, description, duration (5-minute steps), **qualified roles** (checkboxes from `GET /api/staff-roles`, ≥ 1), required room kind, tax class, online-bookable |
| ⭐ `EffectiveDatedHistory` | Shared by prices and tax rates: rows grouped **Current · Scheduled · Past**, each with its effective-from instant shown in the **primary branch's** time zone (prices and tax rates are not branch-specific); **no edit or delete** controls |
| `ScheduleChangeDialog` | "Schedule a new price" / "… rate": amount (string input, 2 dp), effective from (**now** or a future date/time — past dates disabled), confirm stating *"Bookings and invoices made before {date} keep their price."* |
| `TaxClassTable` / `TaxClassForm` | Code, name, active; its `EffectiveDatedHistory` of rates |

### 11.4 Rosters and leave — US-ADM-03

| Component | Behaviour |
|---|---|
| ⭐ `RosterWeekView` | `<table>`: rows = staff at the branch, columns = the 7 days of the selected week (prev / next / this week); each cell lists shifts in branch time, marks leave ("On leave"), exceptions ("Extra" / "Cancelled") and **"Outside opening hours"** with text, not colour alone. Below 768 px: a list grouped by day. Data from `GET …/availability` (unrestricted) and `…/rosters` |
| `RosterPatternEditor` | Per staff member: current pattern and scheduled next pattern with its effective date; slot rows per day (0–3), default room; **"Schedule this as next pattern from {date}"** or **"Apply now"**; cancel the scheduled pattern |
| `RosterExceptionForm` | Date, kind, times, reason |
| `MyLeavePage` | Every staff member: request (kind, dates, note), list with status, cancel while requested |
| `LeaveApprovalsTable` | Admin: filter by status/person/range; approve / reject (with note) / cancel approved — each confirm names the person and dates; the sole-administrator self-approval case shows the server's note |

### 11.5 Settings — US-ADM-05

`SettingsForm` — rendered **entirely from the `GET /api/admin/settings` payload** (never a hardcoded
list): one section per group; per setting a typed input (number with unit, toggle, time range,
currency code), the default, and — when a branch is selected in the scope switcher — the global value
beside the override with **"Use global value"** to clear it. Non-overridable settings are read-only in
branch scope with a note. Explicit **Save changes** sends only changed keys; the confirm lists each
change as *"{name}: {old} → {new}"*. The server's all-or-nothing rejection maps to the offending field.

### 11.6 Content, policies, acceptance — US-ADM-07

| Component | Behaviour |
|---|---|
| `ContentTable` | Kind, title, slug, status, last published; filter by kind/status (URL params) |
| ⭐ `ContentEditor` | Title, slug (auto-suggested, editable before first publish), body `<textarea>` with a **markup help panel** listing exactly what is supported, **live preview** via `LimitedMarkup`; banner fields (placement, link, visibility window); FAQ sort order. **Save draft** and **Publish** (confirm: *"This replaces what the public sees at /pages/{slug}."*); Unpublish |
| `PolicyList` / `PolicyDetail` | Documents with current version; version list read-only; **"Publish new version"** (body, change summary, requires acceptance) — confirm states that signed-in customers will be asked to accept it |
| `PublicContentPage` | `(auth)`-shell page rendering a published page with `LimitedMarkup`; `404` page otherwise |
| `FaqList` | Published FAQs as a disclosure list (`<details>`/`<summary>`) |
| ⭐ `PolicyAcceptancePrompt` | In `(portal)/layout.tsx`: reads `GET …/policy-acceptances/pending`; when non-empty, a modal dialog (focus-trapped, background `inert`) shows each policy's title, a link to its full text in a new tab, and **Accept**; sign-out stays available. On accept → invalidate pending → dialog closes. Never pre-ticked |

### 11.7 Audit log — US-ADM-06

`AuditLogSearch` — filters: person (staff/customer search by name → id), entity type, entity id,
action, category, date range (**required**, default last 7 days, max 366 — the server's rule shown
before submit); results table: time (primary branch zone, with UTC in a tooltip-free secondary line),
actor name, action, category, entity, IP, reason; row expansion shows before/after as a
**key-by-key diff table**, JSON values rendered as text (never parsed into markup). Paging per UI-4.
**Export CSV** → navigates the browser to the server's CSV URL with the same filters (a download, no
client-side generation); a `413` shows *"Too many entries to export — narrow the date range."*

### 11.8 System — US-ADM-08

`MaintenanceSwitch` — current state, message field, toggle with confirm: *"The online shop will show
this message. The back office and sign-in are not affected."*

### 11.9 Error mapping — `lib/api/errors.ts`

Append `propertyName → field` entries for every HO-ADM-001 command to the one mapping table. Map
`BranchNotAssigned` / `BranchAssignmentRequired` to a banner above the page with a link to the branch
picker or *"Ask an administrator to assign you to a branch."*

---

## 12. Mocks — `web/mocks/handlers/admin/*.ts`

Generated from HO-ADM-001 §12.4. Fixtures: two branches in **different zones** (`Asia/Dhaka`,
`Europe/London`) so zone rendering is exercised; a week with a holiday, a half-day closure and a
lunch break; rosters with a scheduled next pattern, leave and exceptions; services priced, unpriced
and with a scheduled price; the full settings registry with one branch override; an audit page with a
before/after diff and a formula-looking reason; content in each status; a policy with two versions;
pending acceptances; each `403`; `413` on export; every `400` code in HO-ADM-001 §10.4 used by a form.

---

## 13. Allowed scope

```
web/app/(staff)/staff/admin/**
web/app/(staff)/staff/leave/**
web/app/(staff)/layout.tsx                      ← ActiveBranchPicker only
web/app/(portal)/layout.tsx                     ← PolicyAcceptancePrompt only
web/app/(auth)/pages/**
web/app/(auth)/faqs/**
web/app/(staff)/staff/admin/staff-accounts/[staffAccountId]/page.tsx  ← StaffBranchAssignmentPanel only (HO-ACC-002's page)
web/components/{admin,content,roster,leave,settings,audit,branches}/**
web/lib/api/{admin-branches,admin-rosters,leave,admin-catalogue,admin-settings,admin-audit,admin-content,public-content,system}.ts
web/lib/api/types.ts · web/lib/api/errors.ts    ← append
web/lib/api/client.ts                           ← send X-Branch-Id only
web/lib/hooks/**                                ← new hooks only
web/lib/format/**
web/lib/validation/**
web/mocks/handlers/admin/** · web/mocks/handlers/index.ts
web/tests/components/{admin,content,roster,leave,settings,audit,branches}/**
web/tests/e2e/admin-*.spec.ts
web/docs/decisions.md                           ← ADR-FE-013…018
docs/test-cases/TC-HO-ADM-002.md · docs/gates/HO-ADM-002-*.md
```

## 14. Forbidden scope

`src/**`, `tests/**`, `.ai/**`, `.claude/**`, `docs/**` (except the two named paths), HO-CAT-002's
cat and vaccine pages, `web/package.json`, `tsconfig.json`, `next.config.ts`, `eslint.config.mjs`.

---

## 15. Component test cases

| ID | Case | Expected |
|---|---|---|
| U-A1 | `AdminNav` for a nurse | Admin-only items hidden; visiting `/staff/admin/settings` directly with a `403` mock renders `PermissionDeniedState` |
| U-B1 | `WeeklyHoursEditor`: overlapping intervals; 4 intervals; reversed times | Row-level errors before submit; the whole week sent on save |
| U-B2 | "Copy Monday to weekdays" | Tue–Fri equal Monday; Sat/Sun untouched |
| U-B3 | Closure form: half-day times with a multi-day range | Times disabled/cleared; message explains |
| U-B4 | Hours of the `Europe/London` branch viewed from any browser zone | Labels and times in Europe/London |
| U-B5 | `ActiveBranchPicker` with one branch / two | Hidden / shown; change sends `X-Branch-Id` on the next request |
| U-C1 | `EffectiveDatedHistory` | Current / Scheduled / Past groups; no edit or delete control anywhere |
| U-C2 | `ScheduleChangeDialog` | Past date not selectable; amount kept as a string; confirm text names the date |
| U-C3 | `ServiceTable` unpriced service | "Not priced" badge with text |
| U-D1 | `RosterWeekView` at 1024 px / 360 px | `<table>` with headers / day list; no horizontal scroll |
| U-D2 | Cell with leave, extra shift and outside-hours shift | Each shown as text |
| U-D3 | `RosterPatternEditor` schedule next | Effective date must be after today in branch time |
| U-D4 | `LeaveApprovalsTable` approve | Confirm names person and dates; list refreshes |
| U-E1 | `SettingsForm` from a payload with a new unknown group | Rendered — nothing hardcoded |
| U-E2 | Branch scope: override, then "Use global value" | Override cleared via `DELETE`; non-overridable read-only |
| U-E3 | Save | Only changed keys sent; confirm lists old → new |
| U-F1 | `LimitedMarkup` with `<script>alert(1)</script>`, `[x](javascript:alert(1))`, headings, bullets, bold, https link | Script shown as literal text; the javascript link not a link; the rest rendered as elements |
| U-F2 | Editor preview vs public page for the same body | Identical DOM |
| U-F3 | `PolicyAcceptancePrompt` with two pending | Dialog with both; focus trapped; nothing pre-ticked; accepting both closes it; sign-out reachable |
| U-G1 | `AuditLogSearch` without a date range | Default last 7 days applied; > 366 days blocked with the server's rule |
| U-G2 | Diff row with a value `=HYPERLINK(…)` | Rendered as plain text |
| U-G3 | Export with `413` | The narrow-the-range message |
| U-G4 | Filters | Round-trip through the URL; back button restores them |
| U-X1 | Every editor: server `400` | Mapped to its field; `errorCode` not in the DOM |
| U-X2 | Every editor: dirty + navigate | Unsaved-changes confirm |

## 16. End-to-end test cases

MSW first, then the real API; both runs recorded.

| ID | Case | Expected |
|---|---|---|
| E-B1 | Create a branch in `Europe/London`, set hours, add a holiday | Saved; calendar in the detail view skips the holiday (calendar half owed by a later handover — owner decision 2026-10-04, §1 Amended) |
| E-B2 | Assign a staff member to two branches; sign in as them; switch branch | Picker shown; requests carry the new branch |
| E-C1 | Schedule a price for next month | Appears under Scheduled; current unchanged |
| E-D1 | Nurse requests leave; admin approves; roster view | Leave days marked "On leave" |
| E-E1 | Change the vaccination due-soon window; open a cat's vaccination status (HO-CAT-002 page) | Status reflects the new window |
| E-F1 | Publish a page; open `/pages/{slug}` signed out | Rendered; unpublish → not-found page |
| E-F2 | Publish a policy version requiring acceptance; sign in as a customer | Prompt shown; accept → portal usable |
| E-G1 | Audit search for the E-C1 price change | Row with before/after; CSV downloads |
| E-H1 | Maintenance on; staff sign-in and admin pages | Unaffected |
| E-X1 | axe on every route in §8.1, clean and with errors, with the policy dialog open | Zero serious/critical |
| E-X2 | Every route at 360 / 768 / 1024 / 1440 | No horizontal scroll, no clipped control |

---

## 17. Implementation order

1. Confirm prerequisites (§5) — else stop.
2. Types and API modules from HO-ADM-001 §12.4; MSW handlers; `lib/format/**`; `LimitedMarkup`.
3. Admin shell and nav → branches (details, hours, closures, rooms) → branch picker and assignment panel.
4. Services, prices, tax classes.
5. Rosters and leave.
6. Settings.
7. Audit log.
8. Content, policies, public renderer, acceptance prompt, maintenance.
9. U-series, E-series on MSW, then E-series against the real API per HO-ADM-001 phase.

```bash
cd web && npm run lint && npm run typecheck && npm run test && npm run test:e2e && npm run build
```

---

## 18. Acceptance criteria

| # | Story | Criterion | Evidence |
|---|---|---|---|
| A1 | ADM-01 | Branch hours, holidays and rooms managed, shown in the branch's zone | U-B1…U-B4, E-B1 |
| A2 | ADM-01 | A second branch and branch assignment work with no code change | U-B5, E-B2 |
| A3 | ADM-02 | A role that cannot use a page sees `PermissionDeniedState` from the server's `403`, not a UI-only block | U-A1 |
| A4 | ADM-03 | Rosters, exceptions and leave managed; approved leave visible on the roster | U-D1…U-D4, E-D1 |
| A5 | ADM-04 | Price and tax changes are scheduled, never edited; history is clear | U-C1, U-C2, E-C1 |
| A6 | ADM-05 | Every setting editable from the server's registry; changes confirmed old → new | U-E1…U-E3, E-E1 |
| A7 | ADM-06 | Audit entries filterable and exportable | U-G1…U-G4, E-G1 |
| A8 | ADM-07 | Pages, FAQs, banners and policies edited, previewed and published; customers accept new policy versions | U-F1…U-F3, E-F1, E-F2 |
| A9 | ADM-08 | Maintenance switch works and never blocks the back office | E-H1 |
| A10 | UI-6 / UI-1 | Zero serious/critical axe violations; 360–1440 px | E-X1, E-X2 |
| A11 | — | No HTML rendering path exists | U-F1, U-F2 |

---

## 19. Required gates

| # | Gate | Focus |
|---|---|---|
| 1 | ARCHITECTURE | §8 layering; no new dependency; the scoped edits to HO-ACC-002 files; the nav map is presentation only |
| 2 | SECURITY | `LimitedMarkup` and diff rendering (no HTML path, no `dangerouslySetInnerHTML`); CSV via server only; `X-Branch-Id` only from the picker; policy prompt not bypassable by UI state |
| 3 | CODE_REVIEW | Time only through `lib/format`; money kept as strings; component size |
| 4 | TEST | `TC-HO-ADM-002.md` first; mock and real runs separate |
| 5 | ACCEPTANCE | Every §18 row |

---

## 20. BLOCKED conditions — stop and report

**20.1 A prerequisite is missing** (Node, HO-CAT-002 scaffold, HO-ACC-002). Do not build a parallel scaffold or stub the session.

**20.2 A time looks wrong in another zone.** Fix it in `lib/format`; never add a time-zone package.

**20.3 Content seems to need HTML or rich text.** It is a new decision; do not add it.

**20.4 A screen needs an endpoint HO-ADM-001 does not specify.** Report it; do not invent a mock field.

**20.5 The real API disagrees with HO-ADM-001 §12.4.** Report it; do not shim.

**20.6 A new dependency seems necessary** (§10). Raise it.
