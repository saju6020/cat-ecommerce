# HO-ADM-002-FU1 — Administration: branch calendar view (frontend follow-up)

## 1. Identity

| Field | Value |
|---|---|
| Handover ID | **HO-ADM-002-FU1** (follow-up slice to [`HO-ADM-002`](HO-ADM-002.md)) |
| Title | Branch calendar view: a month calendar in the staff branch detail view |
| Date | 2026-10-09 |
| Author | Claude Opus 5.5 (handover-writer) |
| Backlog | [`docs/backlog/ADM.md`](../backlog/ADM.md) row **23** ("Scheduled — owed work"). Story: [`docs/USER_STORIES.md`](../USER_STORIES.md) §14 **US-ADM-01** only |
| Origin | [`HO-ADM-002`](HO-ADM-002.md) §16 **E-B1**. Owner decision 2026-10-04 in HO-ADM-002 §1 Amended. ACCEPTANCE **F-M1** in [`docs/gates/HO-ADM-002-ACCEPTANCE.md`](../gates/HO-ADM-002-ACCEPTANCE.md). On 2026-10-09 the owner asked for the frontends whose backends are complete to be built, and this is one of them |
| SRS | FR-ADM-1; **UI-1, UI-6**; NFR-USA-4 |
| Pairs with | [`HO-ADM-001`](HO-ADM-001.md). Its §12.4 is the contract, and **no backend change is made in this slice** |
| Builds on | [`HO-ADM-002`](HO-ADM-002.md) (branch detail layout and tabs, `lib/api/admin-branches.ts`, `lib/hooks/useAdminBranches.ts`, `lib/format/time.ts`, the admin MSW handlers, ADR-FE-014…017) |
| Approved design | **None.** `docs/design/administration/` has no calendar artboard, and its README says *"No calendar/date-picker widget is shown as a real component"*. Build to §11 using the existing tokens and the `Table`/`Tabs` vocabulary of `BranchDetails.dc.html` and `RosterWeekView.dc.html` |
| Baseline commit | `4a4acb88` (master) |
| Gate level | **One combined review** (architecture + security + code), plus the existing suites and this slice's new cases. This follows the owner's gate-scope rule for follow-up slices (2026-10-07). See §19 |
| Amended | *(append rows here; never edit above)* **2026-10-09 — self-amendment, this handover's own build.** (1) `BranchMonthCalendar.onMonthChange` takes a second argument, the focused day number: `(delta: -1 | 1, focusDay: number)`. The grid unmounts while the next month loads (`useBranchCalendar` has no `keepPreviousData` and `useAdminBranches.ts` may only gain the two invalidations), so the page keeps the day number in `useState` and hands it back as `restoreFocusDay`/`onFocusRestored`; no store, no shared file touched (§20.6 not triggered). (2) E-CAL8 is a test in `web/tests/e2e/admin-branch-calendar.spec.ts` (inside §13), skipped unless `E2E_REAL_CAL8=1`, driving the UI and deactivating its own `E2ECAL…` branch. **It was not run**: both sign-ins as `e2e.admin@purrcare.test` against the API on :5011 were refused ("don't match an account"); stopped after two per the integrator's lockout rule. Per §20.8 E-CAL8 is recorded *not run* and A2/A10 are not marked met on MSW alone. (3) The mobile-360 project runs `admin-branch-calendar.spec.ts` too, so E-CAL2/3/7 read the day list item below 768 px and the grid cell above (`expectDay`). (4) The combined review record `docs/gates/HO-ADM-002-FU1-REVIEW.md` is not written by the implementer; ADM row 23 and the HO-ADM-002 Amended row point at it as pending. |

---

## 2. Objective

HO-ADM-002 §16 E-B1 expects *"the calendar in the detail view skips the holiday"*, but HO-ADM-002
§11.2 never specified a calendar. The owner chose to schedule the calendar rather than strike the
clause. This slice delivers it and so closes the owed half of E-B1.

It adds a **Calendar** tab to the staff branch detail view. The tab shows one month and states each
day as one of the following:

- **open**, with its opening times in the branch's time zone
- **closed for a public holiday** or **closed for a closure**, each with its reason
- **closed by the regular weekly pattern**
- **open with reduced hours**, where a part-day closure removed part of the day

The month is chosen with previous/next controls, and the choice is kept in the URL.

There is one constraint that matters most. **The server decides when the branch is open.**
`BranchCalendar` (HO-ADM-001 §8.5, ADR-045) is the only place that turns wall-clock hours and
closures into open time, and HO-ADM-001 §20.7 forbids a second conversion. The calendar therefore
takes open/closed state and times **only** from `GET /api/admin/branches/{branchId}/calendar`. The
closure list and the weekly hours are used **only to explain why** a day is closed or reduced. They
never decide that a day is open or closed (ADR-FE-170).

---

## 3. In scope

### 3.1 Stories and screens

| Story | Screen | Route |
|---|---|---|
| US-ADM-01 (AC1: *"Given opening hours and holidays, then appointment and boarding availability respect them"*, made visible to the administrator) | Branch detail → **Calendar** tab: a month grid at 768 px and wider, a day list below 768 px, a legend, and month navigation | `/staff/admin/branches/[branchId]/calendar?month=yyyy-MM` |

### 3.2 Decisions already taken — do not revisit

| Decision | Source |
|---|---|
| Branch times are rendered with `Intl.DateTimeFormat` in the branch's IANA zone, every time is labelled with that zone, and no time-zone library is used | ADR-FE-014/015 (`web/docs/decisions.md`), HO-ADM-002 §8.4 |
| Branch detail is route-based tabs under `branches/[branchId]/layout.tsx` | HO-ADM-002 §8.1, ADR-FE-014 |
| No calendar, scheduler, date-picker or time-zone package. `date-fns` (already installed) is used only for arithmetic on `yyyy-MM-dd` strings | HO-ADM-002 §10 |
| `useQuery`/`useMutation` live in `lib/hooks/` only. No global store, no `fetch` outside `client.ts`, no `dangerouslySetInnerHTML` | HO-ACC-002 §4.3 |
| A server `403` is the only "not permitted" the UI recognises | HO-ACC-002 ADR-FE-009 |
| The grid collapses to a list below 768 px and never scrolls sideways | HO-ADM-002 §7 UI-1, ADR-FE-017 (the roster precedent) |

### 3.3 Work items

1. Pure month model `buildBranchCalendarMonth` (§11.1).
2. Components `BranchCalendarMonthNav`, `BranchMonthCalendar` (grid) and `BranchCalendarDayList` (list) (§11.2).
3. Calendar tab page and one `TABS` entry in the branch layout (§11.3).
4. Two cache invalidations added to the existing closure mutations (§12.1).
5. Make the MSW calendar and closure handlers match the real backend (§12.2).
6. Unit tests, component tests and e2e tests (§15, §16). Append the calendar half to E-B1. Add the calendar route to E-X1/E-X2.
7. ADR-FE-170, ADR-FE-171. The TC sheet, the review record, the backlog row and the HO-ADM-002 Amended row (§12.3).

---

## 4. Out of scope

### 4.1 Deferred or excluded

| Excluded | Why / where |
|---|---|
| **Any backend change** (`src/**`, `tests/**`, migrations) | The existing endpoints supply everything (§7.1). The optional backend addition in §9.3 OQ-1 needs an owner decision and its own handover |
| **"Special hours" that *extend* opening** (for example an extra Sunday opening) | No such concept exists in the domain. HO-ADM-001 §8.2 has only weekly hours and closures, and a part-day closure can only **reduce** a day. In this slice "special hours" means **reduced hours from a part-day closure**. Extending hours is a new domain concept and an owner decision (§9.3 OQ-2) |
| Creating, editing or cancelling a closure, or editing hours, from the calendar | The Closures and Opening hours tabs already do this. The calendar is read-only, with one link per empty or closed state that points to those tabs |
| A roster, availability or appointment overlay | HO-ADM-002 §11.4 roster view, HO-APT-002 calendar |
| A customer-facing or public calendar | Not requested. `GET /api/branches` is the public read |
| A week view, a year view, printing or export | Not requested |
| Showing historical opening hours for past dates | The server keeps no history of weekly hours. It computes past dates with the **current** pattern, and the UI says so (§11.2) |
| ADM backlog rows 24–29 | Separate backlog work |

### 4.2 Explicitly forbidden shapes

- **Never derive open/closed state or opening times on the client** from `weeklyHours` plus
  closures, for example as a fallback when the calendar request fails. This would be a second
  implementation of `BranchCalendar`, which HO-ADM-001 §20.7 and ADR-FE-170 forbid.
- No new package. No reuse or modification of `web/components/calendar/**`, which is HO-APT-002's
  appointment time grid and is a different thing.
- No `useQuery`/`useMutation` in a component. No global store. No `fetch` outside `client.ts`. No
  `dangerouslySetInnerHTML`. Closure reasons are rendered as **text**.
- No colour-only status. Every state is written out in words.
- No horizontally scrolling grid at any width.

---

## 5. Prerequisites

| Required | State |
|---|---|
| HO-ADM-002 merged (branch detail layout, `useBranchCalendar`, `useBranchClosures`, `lib/format/time.ts`, admin MSW handlers, E-B1 spec) | ✅ on master `4a4acb88` |
| `GET /api/admin/branches/{branchId}/calendar?from&to` on the real backend | ✅ `src/Api/CatEcommerce.Api/Controllers/AdminBranchesController.cs:194-203` |
| `GET /api/admin/branches/{branchId}/closures?from&to` with an overlap filter | ✅ same controller, lines 102-110. `GetBranchClosuresQueryHandler` |
| `GET /api/admin/branches/{branchId}` with `timeZoneId` and `weeklyHours` | ✅ `GetAdminBranchQueryHandler` |
| `web/.env.local` with `NEXT_PUBLIC_USE_MOCKS=true` in the build checkout | ❌/✅ per checkout. Create it from `.env.local.example` and clear `web/.next` (local test-running rule) |
| Real-API run: API running, and dev-only e2e staff seeded (`e2e.admin@purrcare.test`, owner convention 2026-10-08) | ❌/✅ at build time. Needed only for E-CAL8 |

---

## 6. Governing documents

| Document | What you need from it |
|---|---|
| [`HO-ADM-001`](HO-ADM-001.md) | §8.2 (hours: ≤ 3 intervals per day, never across midnight, a day with no interval is closed; closures: inclusive dates, part-day only when single-day, cancelled closures ignored). §8.5 (`BranchCalendar` rules). §12.4 (the three read endpoints). §20.7 (no second local-time conversion). ADR-045 (time model) |
| [`HO-ADM-002`](HO-ADM-002.md) | §7 build rules, §8.1 route map, §8.3 state (one key family per resource), §8.4 time helpers, §10 dependencies, §11.2 branch components, §16 E-B1/E-X1/E-X2, §1 Amended (2026-10-04 owner decision, and items (5)/(6)) |
| [`HO-ACC-002`](HO-ACC-002.md) | §4.3 forbidden shapes; §8.6 accessibility additions; §11.14 error mapping |
| [`HO-CAT-002`](HO-CAT-002.md) | §8–§10 layering (`app/` → `components/` → `lib/`), conventions |
| [`web/docs/decisions.md`](../../web/docs/decisions.md) | ADR-FE-014 (admin section shape), **ADR-FE-015** (branch time via `Intl`), ADR-FE-016 (active branch, which this slice does not touch), **ADR-FE-017** (in-repo `<table>`/list, no scheduler library) |
| [`docs/SRS.md`](../SRS.md) | FR-ADM-1 *"Administrators shall manage branches, opening hours, holidays and per-branch configuration."* **UI-1** *"Responsive layouts at 360 / 768 / 1024 / 1440 px breakpoints."* **UI-6** *"Accessibility target WCAG 2.1 AA: keyboard operable, visible focus, 4.5:1 text contrast, labelled fields, announced errors."* NFR-USA-4 *"Error messages shall state what went wrong and what to do next, in plain language, never exposing stack traces or internal identifiers."* |
| [`.ai/architecture.md`](../../.ai/architecture.md), [`.ai/decisions/adr.md`](../../.ai/decisions/adr.md) | Read-only context: the modular-monolith read side is the source of truth, and the browser presents it. Read these to judge the §9.3 backend proposal, not to change anything |
| [`.ai/read-side-pattern.md`](../../.ai/read-side-pattern.md) | Only for judging §9.3 OQ-1, the optional backend read. Not implemented here |

---

## 7. Verified platform facts and build rules

### 7.1 Verified facts (read from master `4a4acb88`, not assumed)

| # | Fact | Consequence |
|---|---|---|
| F1 | `GET …/calendar?from&to` (`Branches.View`) returns `[{ start, end }]`. Each is an instant (`DateTimeOffset`, ISO-8601 with offset), and only **open** intervals are returned, sorted and merged (`GetBranchCalendarQueryHandler` → `BranchCalendar.OpenIntervals`) | Open/closed state and times come from here and nowhere else (ADR-FE-170) |
| F2 | The range check is `to.DayNumber - from.DayNumber > 62` → `400 { error }`. `to < from` is **not** rejected and returns `[]` | Always request `from` = 1st of the month and `to` = last day of the month (at most a 30-day difference). Never send `to < from` |
| F3 | An unknown `branchId` gives **`200 []`**, not `404` (`BranchCalendarService` returns `[]`) | The page must not treat `[]` as "branch missing". The layout's `useAdminBranch` already shows "Branch not found" on `404` |
| F4 | The calendar endpoint **does not say why** a day is closed or reduced | The reason is joined from `GET …/closures?from&to` and `weeklyHours` (§11.1). This is an annotation only |
| F5 | `GET …/closures?from&to` returns closures that overlap the range (`EndsOn ≥ from`, `StartsOn ≤ to`), **including cancelled ones** (`isCancelled`), capped at **100** with `X-Truncated: true` | Ignore `isCancelled: true`, as the server calendar does. A month cannot realistically hold 100 closures. If it ever did, only the reason text could be missing, and the state would still be right |
| F6 | `kind` on the wire is the enumeration **name**: `"PublicHoliday"` or `"Closure"` (`ClosureKind` display names equal `nameof`). `partialFrom`/`partialTo` are `"HH:mm"` or `null`, and `startsOn`/`endsOn` are `"yyyy-MM-dd"` | `lib/api/admin-branches.ts` types already match. **No change to that file** |
| F7 | `GET …/{branchId}` returns `timeZoneId` and `weeklyHours: [{ dayOfWeek: "Monday", opens: "HH:mm", closes: "HH:mm" }]`, where a day with no row is closed | Used only to label a closed day as "regular weekly closure" |
| F8 | Opening intervals never cross midnight (`Opens < Closes`, `TimeOnly`). A DST spring-forward gap time is moved forward, and an ambiguous fall-back time takes its earlier occurrence (`BranchCalendar.ToInstant`) | Bucket each interval by the **branch-local date of its `start`** using `localDateOf(start, timeZoneId)` (`lib/format/time.ts`). Render with `timeOnlyInZone` |
| F9 | Weekly hours have **no history**. `SetWeeklyHours` replaces the week, and the calendar computes past dates with today's pattern | Show a caption on months containing past days (§11.2) |
| F10 | `useBranchCalendar(branchId, from, to)` already exists, with key `['admin','branches',branchId,'calendar',from,to]`. `useSetBranchWeeklyHours` invalidates the prefix `['admin','branches',branchId]`, which covers the calendar. **`useCreateBranchClosure`/`useCancelBranchClosure` invalidate only `…,'closures'`, so the calendar would show stale data after a closure change** | §12.1 adds the calendar invalidation |
| F11 | **The MSW calendar handler disagrees with the real backend** (`web/mocks/handlers/admin/branches.ts:398-428`). It emits wall-clock times with a `Z` suffix (wrong instants for any non-UTC branch), ignores part-day closures, returns `404` for an unknown branch, and has no 62-day check. The MSW closures `GET` ignores `from`/`to` | §12.2 makes the mock match the backend. Otherwise the MSW-run tests would show what the real API never would |
| F12 | The seeded mock half-day closure is on **2027-02-14, a Sunday**, when the main branch is closed anyway, so it demonstrates nothing | §12.2 moves it to **2027-02-15 (Monday)**. No test references the old date (checked by grep) |
| F13 | There is no `docs/design/` artboard for a branch calendar | §1 "Approved design: None" |

### 7.2 SRS UI requirements as build rules

| Req | Rule | Where enforced |
|---|---|---|
| **UI-1** | 360 px is the floor. At 768 px and wider: a 7-column month `<table>`. Below 768 px: a day list (`<ol>`), with no grid and no horizontal scroll at 360/768/1024/1440 | §11.2, U-CAL13, E-CAL6 |
| **UI-6** | The grid is a real `<table role="grid" aria-readonly="true">`, labelled by the month heading. Column headers are `<th scope="col" abbr="Monday">Mon</th>`. There is **one Tab stop** into the grid (roving `tabindex`), with arrow, Home/End and PageUp/PageDown keys (§11.2). Focus is visible. Every cell's text states the full date and status. Today has `aria-current="date"` and visible text "Today". axe shows zero serious/critical issues | §11.2, U-CAL9, U-CAL10, E-CAL4, E-CAL5 |
| **UI-6** (not colour alone) | Status is written as words (*Open*, *Closed — Public holiday*, *Reduced hours*, …). Any icon or tint is supplementary. The legend lists the same words | U-CAL9 |
| **NFR-USA-4** | Load errors read *"We couldn't load the calendar just now."* with **Retry**. No error code, status number or id is rendered | U-CAL11 |
| ADR-FE-015 | Every time shown is in the branch zone, and the zone is stated once in the caption: *"Times in Europe/London"* | U-CAL7, E-CAL3 |

---

## 8. Frontend architecture

### 8.1 Route map — addition

```
web/app/(staff)/staff/admin/branches/[branchId]/
  layout.tsx            ⚠ TABS gains { segment: 'calendar', label: 'Calendar' } after 'Closures'
  calendar/page.tsx     ⭐ new — Calendar tab                                    US-ADM-01
```

### 8.2 Layering

`calendar/page.tsx` (app) calls hooks from `lib/hooks/useAdminBranches.ts` and passes the data to
`components/branches/*` (presentational). The month model is a pure function in
`lib/format/branch-calendar.ts`, with no React and no I/O. Components never call `useQuery`.

### 8.3 State

| Kind | Where |
|---|---|
| Branch (zone, weekly hours) | `useAdminBranch(branchId)` (already cached by the layout) |
| Open intervals for the month | `useBranchCalendar(branchId, from, to)` (existing hook) |
| Closures for the month | `useBranchClosures(branchId, { from, to })` (existing hook) |
| Visible month | **URL search param `month=yyyy-MM`**. If it is missing or invalid, use the current month **in the branch's zone** (`todayInZone(timeZoneId).slice(0, 7)`). Previous/next are `<Link>`s to `?month=…`, and PageUp/PageDown call `router.push` from the page. No local copy of the month |
| Focused day (roving tabindex) | Component `useState` inside `BranchMonthCalendar`. This is UI state only, and allowed because §4.3 forbids only data hooks |

### 8.4 Date handling

- Month bounds and grid layout use `date-fns` on **`yyyy-MM-dd` strings**: `startOfMonth`,
  `endOfMonth`, `getISODay` and `addDays`. Parse with `parseISO(\`${date}T12:00:00\`)`, so that a
  browser zone with a DST change at local midnight cannot shift the day, then format straight back
  to `yyyy-MM-dd`. No `Date` for a branch wall-clock time.
- Instants from the server use only `localDateOf` and `timeOnlyInZone` (`lib/format/time.ts`,
  consumed unchanged).
- The weekday name of a date string comes from `getISODay`, mapped to `DAYS_OF_WEEK`
  (`lib/api/admin-branches.ts`). The week starts on **Monday**, the same as `getWeekDates` and the
  weekly-hours editor (OQ-3).

---

## 9. ADRs to record and open questions

### 9.1 ADR numbering for this slice

`web/docs/decisions.md` on master uses **ADR-FE-001…020, 024…058, 079…086, 149…163**.
`docs/handover/README.md` allocates **001…163** to the 18 areas and points the *next new area* at
**ADR-FE-164**. HO-ADM-002's own range (013…018) is used up: it actually used 014…020, see its §1
Amended (1).

This slice takes **ADR-FE-170 … ADR-FE-172** (170–171 used, 172 held for amendments). It does not
take 164 because 164 is the README's default pointer. Several follow-up frontends are being written
from that same pointer on the same day, and starting at 170 keeps this slice clear of them. Numbers
164–169 stay with the README pointer. The implementer appends one row to the README ADR table
(§13). If the integrator has already given 170–172 to another slice by commit time, renumber in the
integrator's merge and append a §1 Amended row here.

### 9.2 ADRs (record in `web/docs/decisions.md`, as a block headed "ADR-FE-170 … ADR-FE-172 — Branch calendar view (HO-ADM-002-FU1)")

| ADR | Decision | Why |
|---|---|---|
| **ADR-FE-170** | **The branch calendar's open/closed state and times come only from `GET …/calendar`.** The closure list and `weeklyHours` only label the reason. Where they disagree with the server (for example two reads taken at different moments), **the server's intervals win**, and a closed day with no matching reason reads plain *"Closed"*. If the calendar request fails, nothing is drawn from the other two reads | `BranchCalendar` is the single conversion point (HO-ADM-001 ADR-045, §20.7). A client recomputation would drift on DST, part-day subtraction and cancelled closures |
| **ADR-FE-171** | **The month view is an in-repo `<table role="grid">` with a roving tabindex** (WAI-ARIA APG grid keys: arrows, Home/End, PageUp/PageDown). It becomes an `<ol>` day list below 768 px. The month is the `?month=yyyy-MM` URL param, defaulting to the current month in branch time | Extends ADR-FE-017 (no calendar library, no sideways scroll). Keeps month links shareable and safe with the Back button. One Tab stop keeps a 31-cell grid usable from the keyboard |

### 9.3 Open questions (none blocks this slice; each has a default)

| # | Question | Default in this slice | Needs |
|---|---|---|---|
| **OQ-1** | **Optional backend addition (owner decision required; not built here).** The client joins three reads by date only to label reasons (F4). The smallest addition that would remove even that join is one read-side query, `GET /api/admin/branches/{branchId}/calendar/days?from&to` (`Branches.View`, ≤ 62 days). It would return per branch-local date `{ date, intervals: [{start,end}], fullDayClosure: {id, kind, reason} \| null, partialClosures: [{id, kind, reason, from, to}], weeklyClosed: bool }`, computed next to `BranchCalendar` in `GetBranchCalendarQueryHandler`'s module. There would be no write-side change and no migration | Not built. The three-read join of §11.1 is used, and it is annotation-only (ADR-FE-170) | Owner decision. If approved, a backend follow-up `HO-ADM-001-FU1`, then a one-file swap in `calendar/page.tsx` |
| **OQ-2** | **"Special hours" that extend opening** (opening on a normally closed day, or longer hours on one date) do not exist in the domain (§4.1) | "Special hours" in this slice = reduced hours from a part-day closure | Owner decision. A new domain concept in HO-ADM-001 (aggregate, `BranchCalendar` rule, migration), so a full backend + frontend handover |
| OQ-3 | First day of the week. Bangladesh's weekend is Friday–Saturday | Monday, consistent with `getWeekDates`, the weekly-hours editor and the roster view | Owner may ask for a setting later. It would be a new `BusinessSettings` key, so a backend change |
| OQ-4 | The owner's 2026-10-04 decision says the later handover's **ACCEPTANCE** evidences E-B1's calendar half. The 2026-10-07 gate-scope rule gives follow-ups **one combined review** and no separate ACCEPTANCE gate | The combined review also checks every §18 row against its evidence and records it (§19). That record is cited from ADM backlog row 23 and the HO-ADM-002 Amended row | Owner may still ask for a standalone `acceptance-reviewer` pass |

---

## 10. Dependencies

**No new package.** `date-fns` (installed) is used on date strings only. `@axe-core/playwright` and
Playwright are already devDependencies. Needing a calendar, grid, date-picker, focus-management or
time-zone library is a §20 stop.

---

## 11. Specified changes — components and screens

Each component stays under about 150 lines and has distinct loading, empty and error handling (in
the page).

### 11.1 Month model — `web/lib/format/branch-calendar.ts` (new, pure) — US-ADM-01

```ts
export type BranchDayStatus =
  | 'open' | 'reduced' | 'holiday' | 'closure' | 'weeklyClosed' | 'closed';

export interface BranchCalendarDay {
  date: string;                       // yyyy-MM-dd, branch-local
  dayOfWeek: DayOfWeekName;
  status: BranchDayStatus;
  intervals: { start: string; end: string; label: string }[]; // label "09:00–13:00" in branch zone
  closure: { kind: ClosureKind; reason: string; partialFrom: string | null; partialTo: string | null } | null;
  isToday: boolean;
  isPast: boolean;
  text: string;                       // the full status sentence (see table)
}

export function monthBounds(month: string): { from: string; to: string };          // '2027-02' → 01..28
export function parseMonthParam(raw: string | null, timeZoneId: string): string;   // invalid → current month in zone
export function shiftMonth(month: string, delta: number): string;
export function formatMonthLabel(month: string): string;                           // 'February 2027'
export function leadingBlankCount(month: string): number;                          // Monday-first offset
export function buildBranchCalendarMonth(input: {
  month: string; timeZoneId: string; weeklyHours: OpeningInterval[];
  intervals: BranchCalendarInterval[]; closures: BranchClosure[] | undefined; today: string;
}): BranchCalendarDay[];
```

**Classification per date. Apply the rows in order, and the first match wins.** `intervals` are
the server intervals whose `localDateOf(start, zone) === date`. "Closure" means a closure with
`isCancelled === false` whose `startsOn ≤ date ≤ endsOn`.

| # | Condition | `status` | `text` (exact wording) |
|---|---|---|---|
| 1 | intervals non-empty **and** a part-day closure (`partialFrom` set) on the date | `reduced` | *"Reduced hours: {labels joined ", "}. Closed {partialFrom}–{partialTo}: {reason}"* |
| 2 | intervals non-empty | `open` | *"Open {labels joined ", "}"* |
| 3 | no intervals **and** a full-day closure of kind `PublicHoliday` | `holiday` | *"Closed — Public holiday: {reason}"* |
| 4 | no intervals **and** a full-day closure of kind `Closure` | `closure` | *"Closed — {reason}"* |
| 5 | no intervals **and** a part-day closure on the date | `closure` | *"Closed — {reason} (part-day closure {partialFrom}–{partialTo} covers all opening hours)"* |
| 6 | no intervals **and** `weeklyHours` has no row for this weekday | `weeklyClosed` | *"Closed (regular weekly closure)"* |
| 7 | no intervals, and none of the above | `closed` | *"Closed"* |

Rules:

- A full-day closure is **never** allowed to hide server intervals (row 2 comes before row 3). See
  ADR-FE-170.
- `weeklyHours` is **never** used to produce times.
- When `closures` is `undefined` (still loading or failed), rows 1/3/4/5 cannot match, so days
  fall to rows 2/6/7. The page shows the warning in §11.3.
- `reason` is a plain string, rendered as text.

### 11.2 Components — `web/components/branches/`

| Component | Behaviour |
|---|---|
| ⭐ `BranchMonthCalendar` (new) | Props: `days`, `month`, `timeZoneId`, `onMonthChange(delta)`. Shown at `md` (≥ 768 px) and wider, hidden below. `<table role="grid" aria-readonly="true" aria-labelledby={monthHeadingId}>`. `<thead>`: seven `<th scope="col" abbr="Monday">Mon</th>` … starting Monday. Body: weeks as `<tr>`. Leading and trailing cells outside the month are empty `<td role="gridcell" aria-disabled="true">`, are not focusable and are skipped by the keys. Each day cell is `<td role="gridcell" tabIndex={focused ? 0 : -1}>` containing: the day number, "Today" text when `isToday` (plus `aria-current="date"`), each interval label on its own line (or the closed wording, shortened visually but with the **full `text`** in an `sr-only` span so the accessible name is complete, for example *"Monday 25 December 2028. Closed — Public holiday: Christmas Day"*), and a status word badge. **Keys:** ←/→ move ±1 day; ↑/↓ move ±7 days; Home/End go to the first/last in-month day of the current week row; PageUp/PageDown call `onMonthChange(-1/+1)`, and after the URL change focus lands on the same day number, clamped to the new month's length. Movement stops at the month's first and last day and does not wrap. The initial focus target is today if it is in this month, otherwise the 1st. Visible focus ring uses the existing `focus-visible` token |
| `BranchCalendarDayList` (new) | Same `days`. Shown below `md`. An `<ol aria-labelledby={monthHeadingId}>` with one `<li>` per in-month day: a date line (*"Mon 25 Dec"*, "Today" when applicable) and the `text`. No grid semantics and no sideways scroll; long reasons wrap (`break-words`) |
| `BranchCalendarMonthNav` (new) | `<h3 id={monthHeadingId}>` *"December 2028"*. `<Link>`s **"Previous month"** / **"Next month"** (to `?month=` ± 1) and **"This month"** (hidden when already on it). Caption under the heading: *"Times in {timeZoneId}."* On months with any past day, the caption adds: *"Past days are shown with the current opening hours; earlier hours are not kept."* (F9). Legend `<dl>` listing the five words: Open, Reduced hours, Closed — Public holiday, Closed — closure, Closed (regular weekly closure) |

### 11.3 Page — `web/app/(staff)/staff/admin/branches/[branchId]/calendar/page.tsx` (new)

- `'use client'`. Read `branchId` from params and `month` via `useSearchParams` →
  `parseMonthParam(raw, branch.timeZoneId)`. Compute `{from,to} = monthBounds(month)` and call the
  three hooks of §8.3.
- `<h2>Calendar</h2>`, then `BranchCalendarMonthNav`, then both views (CSS switches between them).
- **States, in order:**

| State | Condition | Rendering |
|---|---|---|
| Forbidden | any of the three queries fails with `ApiForbiddenError` | `AccessRefusedState` (same as the Closures tab) |
| Loading | branch or calendar loading | `LoadingState` (*"Loading calendar…"*) |
| Error | calendar request failed (not 403) | `ErrorState` *"We couldn't load the calendar just now."* + Retry (`refetch`). **No grid** (ADR-FE-170) |
| Partial | calendar loaded, closures failed | Grid shown. Above it, a warning `role="status"` reads *"Reasons for closed days couldn't be loaded."* + Retry |
| Empty | `branch.weeklyHours` is empty | Grid shown (every day reads per rows 3–7). Above it a notice: *"This branch has no opening hours yet, so every day shows as closed."* + link **"Set opening hours"** → `…/hours` |
| Ready | otherwise | Grid/list |

- The `month` param is reflected as-is. An invalid value is replaced by the default without a
  redirect.

### 11.4 Layout tab — `web/app/(staff)/staff/admin/branches/[branchId]/layout.tsx`

One edit only. Insert `{ segment: 'calendar', label: 'Calendar' }` into `TABS` after `closures`.
Change nothing else. The tab's `aria-current` logic compares `pathname === href`, which still
works with a `?month=` query because `usePathname` excludes the query.

---

## 12. Specified changes — hooks, mocks, docs

### 12.1 Hooks — `web/lib/hooks/useAdminBranches.ts` (scoped edit)

| Change | Story | Reason |
|---|---|---|
| `useCreateBranchClosure` and `useCancelBranchClosure` `onSuccess`: **also** `invalidateQueries({ queryKey: ['admin','branches',branchId,'calendar'] })` | US-ADM-01 | F10. A new or cancelled closure must change the calendar. No other change in this file; `useBranchCalendar`/`useBranchClosures` are reused as they are |

### 12.2 Mocks — `web/mocks/handlers/admin/branches.ts` (scoped edit)

| Change | Reason |
|---|---|
| `GET …/calendar`: (a) unknown branch → `200 []`; (b) `to − from > 62` days → `400 { error: "The range cannot exceed 62 days." }`; (c) for each local date from..to, skip a day with a non-cancelled full-day closure; otherwise take that weekday's intervals, **subtract every non-cancelled part-day closure on that exact date** (the same `Subtract` semantics as `BranchCalendar`), and convert each remaining `(date, HH:mm)` to an instant with the existing `zonedTimeToUtcIso(date, time, branch.timeZoneId)` from `./fixtures`; (d) sort, and merge touching or overlapping intervals; (e) iterate dates as strings (UTC-noon arithmetic), never via the browser zone | F11. The mock must return what the real API returns, or MSW runs prove nothing |
| `GET …/closures`: honour `from`/`to` (keep a closure when `endsOn ≥ from` and `startsOn ≤ to`); still return cancelled ones | F5/F11, matches `GetBranchClosuresQueryHandler` |
| Seed: move `closureHalfDayId` from `2027-02-14` to **`2027-02-15`** (a Monday; main branch is open 09:00–13:00 and 14:00–18:00, so the 13:00–18:00 closure leaves 09:00–13:00) | F12 |

`web/mocks/handlers/admin/fixtures.ts` is **consumed, not edited**.

### 12.3 Docs

| File | Change |
|---|---|
| `web/docs/decisions.md` | Append the block "ADR-FE-170 … ADR-FE-172 — Branch calendar view (HO-ADM-002-FU1)" with ADR-FE-170 and ADR-FE-171 (§9.2) and a one-line note that 172 is held |
| `docs/handover/README.md` | Append **one row** to the ADR numbering table: `\| ADR-FE-170 … ADR-FE-172 \| HO-ADM-002-FU1 (170–171 used) \|`. Change nothing else |
| `docs/test-cases/TC-HO-ADM-002-FU1.md` | New TC sheet, written **before** the tests (test-engineer format), listing every §15/§16 ID, its MSW and real-API result, and the run date |
| `docs/gates/HO-ADM-002-FU1-REVIEW.md` | The combined review record (§19) |
| `docs/backlog/ADM.md` | Row 23 only: change "Scheduled — owed work" to resolved and point to this handover and the review file. Medium/low review findings are appended as new ADM rows (gate blocking rule) |
| `docs/handover/HO-ADM-002.md` | **Append-only** §1 Amended row: *"2026-10-xx — E-B1's calendar half delivered by HO-ADM-002-FU1 (review: docs/gates/HO-ADM-002-FU1-REVIEW.md)."* No other edit |
| this file | Append a §1 Amended row for any deviation found during the build |

---

## 13. Allowed scope

```
web/app/(staff)/staff/admin/branches/[branchId]/calendar/**
web/app/(staff)/staff/admin/branches/[branchId]/layout.tsx        ← one TABS entry only (§11.4)
web/components/branches/BranchMonthCalendar.tsx                   ← new
web/components/branches/BranchCalendarDayList.tsx                 ← new
web/components/branches/BranchCalendarMonthNav.tsx                ← new
web/lib/format/branch-calendar.ts                                 ← new
web/lib/hooks/useAdminBranches.ts                                 ← two onSuccess invalidations only (§12.1)
web/mocks/handlers/admin/branches.ts                              ← calendar + closures GET + one seed date only (§12.2)
web/tests/components/branches/branch-calendar*.test.ts
web/tests/components/branches/branch-calendar*.test.tsx
web/tests/e2e/admin-branch-calendar.spec.ts                       ← new
web/tests/e2e/admin-functional.spec.ts                            ← E-B1: append the calendar half only
web/tests/e2e/admin-a11y-responsive.spec.ts                       ← ROUTES: add the calendar route only
web/docs/decisions.md                                             ← append ADR-FE-170…172 block
docs/handover/README.md                                           ← one ADR-table row
docs/handover/HO-ADM-002.md                                       ← one appended §1 Amended row
docs/handover/HO-ADM-002-FU1.md                                   ← append-only §1 Amended rows
docs/test-cases/TC-HO-ADM-002-FU1.md
docs/gates/HO-ADM-002-FU1-REVIEW.md
docs/backlog/ADM.md                                               ← row 23, plus new rows for review findings
```

## 14. Forbidden scope

| Path | Why |
|---|---|
| `src/**`, `tests/**` (backend), any EF migration | Frontend-only slice. The backend addition is OQ-1 and needs an owner decision |
| `web/lib/api/admin-branches.ts`, `web/lib/api/client.ts`, `web/lib/api/types.ts`, `web/lib/api/errors.ts` | The wire types already match the backend (F6). Any change to them suggests a contract disagreement (§20.3) |
| `web/lib/format/time.ts`, `web/lib/format/week.ts`, `web/lib/dateOnly.ts` | Shared helpers used by roster, audit, appointments and cat pages. Consume, don't edit; new helpers go in `branch-calendar.ts` |
| `web/components/calendar/**`, `web/app/(staff)/staff/appointments/**` | HO-APT-002's appointment calendar, which is unrelated |
| `web/components/branches/*` other than the three new files | `ClosureList`, `ClosureForm`, `WeeklyHoursEditor`, … are not changed by this slice |
| Other branch tab pages (`page.tsx`, `hours/`, `closures/`, `rooms/`, `roster/`) | Not part of the calendar |
| `web/mocks/handlers/admin/fixtures.ts`, `rosters.ts`, any other mock area | Consume only |
| `web/package.json`, lockfile, `tsconfig.json`, `next.config.ts`, `eslint.config.mjs`, `playwright.config.*`, `vitest.config.*`, `web/public/mockServiceWorker.js` | No dependency or config change |
| `.ai/**`, `.claude/**`, `docs/SRS.md`, `docs/USER_STORIES.md`, `docs/design/**` | Governing documents. Not edited by a build |

---

## 15. Unit and component test cases

Vitest + Testing Library + MSW (`web/tests/components/branches/`). Model tests call
`buildBranchCalendarMonth` directly with literal fixtures.

| ID | Case | Expected |
|---|---|---|
| U-CAL1 | Model: Asia/Dhaka branch, a Monday with server intervals `03:00Z–07:00Z` and `08:00Z–12:00Z` | `status: 'open'`, labels `09:00–13:00`, `14:00–18:00`, text *"Open 09:00–13:00, 14:00–18:00"* |
| U-CAL2 | Model: no intervals on 2027-01-01, a non-cancelled `PublicHoliday` closure "New Year's Day" | `holiday`, *"Closed — Public holiday: New Year's Day"* |
| U-CAL3 | Model: a `Closure` spanning 3 dates, reason "Refurbishment" | All three dates `closure`, *"Closed — Refurbishment"*; the day after is unaffected |
| U-CAL4 | Model: Sunday, no intervals, no closure, `weeklyHours` has no Sunday row | `weeklyClosed`, *"Closed (regular weekly closure)"* |
| U-CAL5 | Model: 2027-02-15 with server interval `03:00Z–07:00Z` only and a part-day closure 13:00–18:00 "Staff training (half day)" | `reduced`, *"Reduced hours: 09:00–13:00. Closed 13:00–18:00: Staff training (half day)"* |
| U-CAL6 | Model, server wins: (a) `weeklyHours` has Tuesday 09–18 but the server returns no Tuesday interval and there is no closure; (b) a full-day closure on a date where the server **does** return intervals; (c) a **cancelled** holiday on a date with no intervals and no weekly row | (a) `closed`, *"Closed"*, and **no** 09:00 anywhere; (b) `open` with the server times and no closure text; (c) `weeklyClosed` (cancelled closure ignored) |
| U-CAL7 | Model, zone and DST: Europe/London, intervals `2027-10-29T07:00:00+00:00–16:00Z` (BST) and `2027-11-01T08:00:00Z–17:00Z` (GMT); and a Dhaka interval starting `2027-03-01T23:30:00Z` | Both London days read *"Open 08:00–17:00"*. The Dhaka interval is placed on **2027-03-02**. The result is the same whatever the test process zone is |
| U-CAL8 | `monthBounds('2027-02')`, `monthBounds('2028-02')`, `shiftMonth('2027-12', 1)`, `leadingBlankCount('2027-01')`, `parseMonthParam('2027-13', 'Asia/Dhaka')` with system time set to `2027-03-31T19:00:00Z` | `01..28`; `01..29`; `2028-01`; `4` (1 Jan 2027 is a Friday); `2027-04` (already 1 April in Dhaka) |
| U-CAL9 | `BranchMonthCalendar` for 2027-01 | `role="grid"` with `aria-readonly`, 7 `columnheader`s Mon…Sun with `abbr`, 4 leading disabled cells, 31 day cells. Today has `aria-current="date"` and "Today" text. Every status is present as words, and each cell's accessible name contains the full date and `text` |
| U-CAL10 | Keyboard on `BranchMonthCalendar` | Exactly one cell has `tabIndex=0`. → ← ↑ ↓ move focus by 1/7 days and stop at month bounds without wrapping. Home/End go to the row's first/last in-month day. PageUp/PageDown call `onMonthChange(-1/+1)`. After re-render with the new month, focus is on the clamped same day number (31 Jan → PageDown → 28 Feb) |
| U-CAL11 | Calendar page states (MSW overrides): calendar 403; calendar pending; calendar 500; closures 500 with calendar OK; branch with empty `weeklyHours` | `AccessRefusedState`; `LoadingState`; `ErrorState` with the wording and Retry, **no** gridcell in the DOM; grid plus the "Reasons for closed days couldn't be loaded." status and Retry; grid plus the no-opening-hours notice linking to `…/hours`. No status code or `errorCode` text anywhere |
| U-CAL12 | `useCreateBranchClosure` / `useCancelBranchClosure` success (spy on `QueryClient.invalidateQueries`) | Both invalidate `['admin','branches',id,'closures']` **and** `['admin','branches',id,'calendar']` |
| U-CAL13 | `BranchCalendarDayList` with the U-CAL9 days | An `<ol>` with 31 `<li>`, each containing the same `text` as the grid. No `role="grid"` |
| U-CAL14 | Mock parity (MSW handler called through `getBranchCalendar`/`getBranchClosures`): main branch 2027-02-15; main branch 2027-01-01; London 2027-10-29; range of 63 days; unknown branch id; closures `from=2027-02-01&to=2027-02-28` | Monday → `[{start:'2027-02-15T03:00:00.000Z' (or equivalent instant), end:…07:00Z}]` only; 1 Jan → `[]`; London → `07:00Z–16:00Z`; `400`; `200 []`; only the half-day closure (not New Year's Day) |
| U-CAL15 | Closure `reason` `<img src=x onerror=alert(1)>` | Rendered as literal text in grid and list, with no element created |

## 16. End-to-end test cases

Playwright on MSW first, then E-B1 and E-CAL2 against the real API (E-CAL8). Run with
`--workers=1`, one dev server at a time on its own `PORT`, and stop it afterwards (local
test-running rule).

| ID | Case | Expected |
|---|---|---|
| **E-B1** (calendar half; HO-ADM-002 §16) | Append to the existing E-B1 in `admin-functional.spec.ts`: after the holiday is added, click **Calendar** and go to `?month=2028-12` | 25 Dec 2028 (Monday) reads *"Closed — Public holiday: Christmas Day"*. 18 Dec (Monday) reads *"Open 08:00–17:00"*. 26 Dec (Tuesday) reads *"Closed (regular weekly closure)"*. Caption says *"Times in Europe/London"*. **This is the owed evidence for E-B1** |
| E-CAL2 | Seeded main branch, `?month=2027-01`, then **Next month** | 1 Jan *"Closed — Public holiday: New Year's Day"*; Sundays *"Closed (regular weekly closure)"*; 2 Jan (Saturday) *"Open 10:00–14:00"*. In February, 15 Feb reads *"Reduced hours: 09:00–13:00. Closed 13:00–18:00: Staff training (half day)"*. URL becomes `?month=2027-02` |
| E-CAL3 | London branch, `?month=2027-10` then `2027-11`, run with Playwright `timezoneId: 'America/Los_Angeles'` | Fri 29 Oct and Mon 1 Nov both read *"Open 08:00–17:00"*, independent of browser zone and DST |
| E-CAL4 | Keyboard at 1024 px: Tab into the grid, then → ↓ End PageDown | Focus enters on today or the 1st, moves as specified, and is visible. PageDown changes the URL to the next month and focus lands on the clamped same day. The Tab after the grid leaves it in one press |
| E-CAL5 (E-X1 extension) | Add `{ path: \`/staff/admin/branches/${mainBranchId}/calendar?month=2027-02\`, heading: /calendar/i }` to `ROUTES` in `admin-a11y-responsive.spec.ts` | axe: zero serious/critical issues |
| E-CAL6 (E-X2 extension) | Same route at 360 / 768 / 1024 / 1440 | No horizontal scroll and no clipped control. At 360 the `<ol>` list is visible and the grid hidden. At 768 and wider the grid is visible |
| E-CAL7 | London branch: create a future closure for a unique far date (e.g. a Wednesday in 2029), open the Calendar month, then cancel it on Closures and return to Calendar | The day reads *"Closed — {reason}"*, then after cancel *"Open 08:00–17:00"* without a reload (invalidation, U-CAL12) |
| E-CAL8 | **Real API**: E-B1 (unique `E2E…` code; deactivate the created branch at the end) and E-CAL2's 1 Jan/Sunday/Saturday checks against a branch the run creates (the seeded `MAIN` has no hours on a fresh DB), signed in as `e2e.admin@purrcare.test` | Same results as on MSW. Recorded separately in the TC sheet with the commit and date |

---

## 17. Implementation order and verification

1. Confirm §5. Create `web/.env.local` if it is missing, then clear `web/.next`.
2. Write `docs/test-cases/TC-HO-ADM-002-FU1.md` listing §15/§16.
3. Mock parity (§12.2) together with U-CAL14. **First** run the existing admin suites, to prove that
   the seed date move and the handler rewrite break nothing.
4. `lib/format/branch-calendar.ts` together with U-CAL1…U-CAL8.
5. Hook invalidations (§12.1) together with U-CAL12.
6. `BranchCalendarMonthNav`, `BranchMonthCalendar`, `BranchCalendarDayList` together with U-CAL9,
   U-CAL10, U-CAL13, U-CAL15.
7. `calendar/page.tsx` and the `TABS` entry together with U-CAL11.
8. E-B1 calendar half, E-CAL2…E-CAL7 on MSW, then E-CAL8 on the real API.
9. ADR-FE-170/171, the README row, the TC results, then hand over for the combined review.

```bash
cd web
npm run lint && npm run typecheck && npm run format:check && npm run test && npm run build
# one dev server, own port, serial; stop it after each run
PORT=3112 npx playwright test tests/e2e/admin-branch-calendar.spec.ts tests/e2e/admin-functional.spec.ts --project=chromium --workers=1
PORT=3112 npx playwright test tests/e2e/admin-branch-calendar.spec.ts --project=mobile-360 --workers=1
PORT=3112 npx playwright test tests/e2e/admin-a11y-responsive.spec.ts --project=chromium --workers=1   # fresh server
PORT=3112 npx playwright test tests/e2e/admin-a11y-responsive.spec.ts --project=mobile-360 --workers=1 # fresh server
```

Then run the full `npm run test:e2e` once on a fresh server, to confirm no regression in other
areas from the mock change.

---

## 18. Acceptance criteria

| # | Story | Criterion | Evidence |
|---|---|---|---|
| A1 | US-ADM-01 | The Calendar tab shows every day of the chosen month as open (with times) or closed, taken **only** from `GET …/calendar` | U-CAL1, U-CAL6, E-CAL2 |
| A2 | US-ADM-01 | A public holiday or closure shows as closed, with its kind and reason. **E-B1's "calendar in the detail view skips the holiday" is met** | U-CAL2, U-CAL3, **E-B1**, E-CAL8 |
| A3 | US-ADM-01 | A day the weekly pattern closes reads "Closed (regular weekly closure)" | U-CAL4, E-B1, E-CAL2 |
| A4 | US-ADM-01 | A part-day closure ("special hours") shows the remaining hours and the closed window with its reason | U-CAL5, E-CAL2 |
| A5 | US-ADM-01 / ADR-FE-015 | Times are in the branch's zone, correct across DST, and do not depend on the browser zone | U-CAL7, U-CAL8, E-CAL3 |
| A6 | US-ADM-01 / ADR-FE-170 | The server's state wins over weekly hours and closures. A calendar failure draws no days. Adding or cancelling a closure updates the calendar | U-CAL6, U-CAL11, U-CAL12, E-CAL7 |
| A7 | UI-6 | Keyboard-operable grid with one Tab stop and visible focus; status not shown by colour alone; zero serious/critical axe issues | U-CAL9, U-CAL10, E-CAL4, E-CAL5 |
| A8 | UI-1 | No horizontal overflow at 360/768/1024/1440. A list below 768 px | U-CAL13, E-CAL6 |
| A9 | NFR-USA-4 | Loading, empty, error, partial and forbidden states each render as specified, with no internal codes | U-CAL11 |
| A10 | — | MSW matches the real API for the calendar and closures reads | U-CAL14, E-CAL8 |
| A11 | — | Closure reasons cannot inject markup | U-CAL15 |
| A12 | — | No backend change, no new dependency, every changed file inside §13 | The combined review's SCOPE CHECK (`docs/gates/HO-ADM-002-FU1-REVIEW.md`) |

---

## 19. Required gates

The gate level is set by the owner's gate-scope rule (2026-10-07): **a follow-up slice gets one
combined review plus the existing suites**, not five separate gates. The five standard rows are
kept so that each concern has a named owner:

| # | Gate | Agent | How it runs for this slice | Focus |
|---|---|---|---|---|
| 1 | ARCHITECTURE | `architecture-reviewer` | **Folded into the combined review** (one `code-reviewer` pass) | §8 layering; ADR-FE-170 (no client recomputation of open time); no hooks in components; §13/§14 scope; no new dependency |
| 2 | SECURITY | `security-reviewer` | **Folded into the combined review** | Closure reason rendered as text (U-CAL15); no `dangerouslySetInnerHTML`; `month` param parsed defensively; the 403 path relies on the server |
| 3 | CODE_REVIEW | `code-reviewer` | **The combined review**, written to `docs/gates/HO-ADM-002-FU1-REVIEW.md` in the `docs/gates/README.md` format, headed `GATE: ARCHITECTURE + SECURITY + CODE_REVIEW (combined follow-up review, gate-scope rule)` | Time only through `lib/format`; component size; mock parity correctness (§12.2) |
| 4 | TEST | `test-engineer` | **No separate gate.** The implementer runs §17 and records results in `TC-HO-ADM-002-FU1.md`. The reviewer confirms the sheet and runs `npm run test` | Every §15/§16 ID present with MSW and real results |
| 5 | ACCEPTANCE | `acceptance-reviewer` | **No separate gate (OQ-4).** The combined review checks every §18 row against its evidence ID and states the E-B1 result explicitly | §18 A1–A12; nothing from §4 built |

Only BLOCKER/HIGH findings block. Medium and low findings go to `docs/backlog/ADM.md` as new rows.

---

## 20. BLOCKED conditions — stop and report

**20.1 The real calendar response disagrees with §7.1** (field names, instant format, the
62-day rule, `200 []` for an unknown branch). Report it against HO-ADM-001 §12.4. **Never** shim it
in the client or change the mock to hide the difference.

**20.2 The calendar seems to need data the three reads don't give** (for example extended
"special hours", or a reason the closures list lacks). Stop. This is OQ-1/OQ-2, an owner decision.
**Never** add a mock field, a client-side opening-hours computation, or a `src/**` change.

**20.3 A wire type in `lib/api/admin-branches.ts` looks wrong.** It is forbidden scope (§14).
Report the disagreement. Do not edit the type to make a test pass.

**20.4 The mock rewrite (§12.2) breaks an existing HO-ADM-002 test.** Find out whether the old
test depended on the wrong behaviour (the `Z`-suffixed times, or ignored part-day closures). If it
did, record it in the TC sheet and fix the test's expectation, citing F11. If the cause is anything
else, stop and report. Never revert the mock to the wrong behaviour.

**20.5 A calendar, date-picker, focus or time-zone package seems necessary.** Raise it. Do not
install it (§10).

**20.6 Keyboard focus cannot be restored after PageUp/PageDown** without a global store or
editing shared files. Report the constraint and fall back to focusing the month heading. Never add
a store or edit `lib/format/time.ts`.

**20.7 ADR-FE-170…172 have already been taken by another slice at commit time.** Do not reuse
them. Ask the integrator for the renumbering, and record it in a §1 Amended row.

**20.8 The real-API run (E-CAL8) cannot be done** (API down, no seeded e2e staff). Record E-CAL8 as
*not run* in the TC sheet with the reason, and flag it in the review. Do not mark A2/A10 met on MSW
alone, and do not seed anything except the owner-approved `e2e.*@purrcare.test` convention.
