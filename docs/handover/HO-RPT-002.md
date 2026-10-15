# HO-RPT-002 — Reporting (frontend)

## 1. Identity

| Field | Value |
|---|---|
| Handover ID | **HO-RPT-002** |
| Title | Reporting — frontend slice |
| Date | 2026-09-22 |
| Author | Claude Opus 5 |
| Backlog | [`docs/USER_STORIES.md`](../USER_STORIES.md) v2.4 §16 — **US-RPT-01 … US-RPT-06** |
| SRS | [`docs/SRS.md`](../SRS.md) v2.3 — **§8.1, §8.2, §8.3, FR-RPT-1, FR-RPT-2, FR-RPT-3**; **UI-1, UI-3, UI-4, UI-5, UI-6**; NFR-PRF-3, NFR-USA-4, NFR-USA-5, NFR-USA-6, C-6, C-7 |
| Pairs with | [`HO-RPT-001`](HO-RPT-001.md) — **§12.4 there is the contract**. On any disagreement about the wire, HO-RPT-001 wins (§20.4) |
| Builds on | [`HO-CAT-002`](HO-CAT-002.md) (scaffold, stack, `recharts`), [`HO-ACC-002`](HO-ACC-002.md) (route groups, session guards, `client.ts`, error mapping, `PermissionDeniedState`), [`HO-ADM-002`](HO-ADM-002.md) (active branch and `X-Branch-Id`, `lib/format`, admin nav, the in-repo grid approach, `EffectiveDatedHistory` conventions), [`HO-NTF-002`](HO-NTF-002.md) (notification log, where a scheduled delivery is visible) |
| Delivery order | Section 16 of 17 in [`README.md`](README.md#delivery-order) |
| Approved design | None. Build to §11 with the existing shadcn/ui tokens |
| Baseline commit | `4007225` |
| Amended | **2026-10-14 — build on `ho/RPT-002` (from master 5a187b6b), against the merged HO-RPT-001 (its Amended rows 1–24 and `docs/backlog/RPT.md`).** **Built:** the home dashboard at `/staff` (ADR-FE-130, with the landing change in `app/page.tsx` and the staff sign-in default), the catalogue, the one generic report viewer (filter bar, table, card list, totals, notes, optional chart, CSV and PDF export by blob), report schedules (list, editor with the server's preview, Pause / Resume / Cancel / Run now, run history), the `formatCell` rendering path, the blob-save helper, error mapping, MSW fixtures, component and e2e tests, and ADR-FE-129 … ADR-FE-138. **Absent by the "absent" rule, never stubbed:** R21–R26 (including R23) and the `boarding.*` / subscription tiles are not in the API's catalogue or dashboard, so no screen mentions them. **Differences from the handover, found against the merged backend (the backend wins, ADR-FE-138; nothing is shimmed):** (1) **Date presets.** §11.3 / OQ-2 asks for Today, Yesterday, This week, Last week, This month, Last month, Last 30 days. The API gives only `defaultRange` and `defaultPeriod`; resolving a preset in the browser is date arithmetic (ADR-FE-131). The filter bar therefore offers one preset, "Default: {period}", plus two `yyyy-MM-dd` fields. **Owner question:** extend the catalogue with server-resolved `presets: [{ label, from, to }]`. (2) **Relative-period names.** The server's `ReportRelativePeriod` values are `Today, PreviousDay, PreviousWeek, PreviousMonth, MonthToDate, Last7Days, Last30Days, Next30Days, Next90Days`, not §11.5's Mon–Sun wording; plain-language labels are in `lib/format/report.ts`. The editor offers the periods that suit a report by mirroring `ReportSchedulePeriods.IsCompatible` (backlog 20) from the catalogue's `kind` and `defaultPeriod`; the server still decides. (3) **`branchId=all`** is the server's token for every branch (`ReportRules.AllBranches`); §12.4 does not state it. A schedule uses `branchId: null`. (4) **`groupBy`** has no allowed values in the catalogue; the filter bar offers the server's `TimeBucket` names `Day`, `Week`, `Month` (default Day). A schedule request has no `groupBy`, so "Schedule this report" does not carry it and a scheduled time-bucketed report uses the server default. (5) **Preview carries no zone.** `POST …/preview` returns instants and the covered period but no `timeZoneId`; instants are labelled with the schedule branch's zone (the primary branch's for "All branches"). (6) **Recipient refusals do not name the person** (`ReportRecipientNotPermitted` etc. are generic sentences on `recipientIds`), so §11.7's "{name} can't open this report" cannot be produced; the server's sentence is shown on the recipients field. The schedule list has `recipientCount` only, so names appear on the detail page, not on hover in the list. (7) **No Over / Short / Breached.** Variance figures are signed money with no direction word, and the sign convention differs between R13 (expected − received) and R14 (cash-up). A variance shows "Mismatch" (any non-zero amount, by a string test) or "Matched" with an icon, never a guessed direction. **Owner question:** have the server send a direction word. (8) **CORS exposes only `Date`** (`HostPipelineExtensions`), so the browser cannot read `Content-Disposition` or `Retry-After` cross-origin. Export filenames fall back to `purrcare-{key}-{from}-to-{to}.{ext}` (the server's own pattern); a `429` waits "60 seconds" whatever the server said. **Backend follow-up:** `WithExposedHeaders("Content-Disposition", "Retry-After")`. (9) **Nav.** The shell reads `nav/areas/reporting.ts`, not `(staff)/layout.tsx`, so the two entries (Home, Reports) are there; Report schedules is one line in `adminNavItems`. The shell has no per-role filter and `StaffShell` is outside §13, so the Reports entry is shown to every role (like Messages) and a Nurse gets `PermissionDeniedState`; §11.1 / U-B2 / E-A2 "entry hidden" is adapted to "page refused". **Owner question:** allow an optional `roles` on `NavItem`. (10) **Table breakpoint.** Cards replace the table when the content container is under 640 px (measured), not at a 768 px viewport, because the 240 px sidebar takes the room (ADR-FE-133). (11) **Types** live in `lib/api/{reports,dashboard,report-schedules}.ts`, not `types.ts`, whose header forbids domain types. (12) **Files outside §13's literal list:** `nav/areas/reporting.ts`, `tests/e2e/reports-helpers.ts`, `tests/e2e/reports-real-api.spec.ts`, `lib/hooks/useBranchOptions.ts` (under `lib/hooks`), `.env.local` (git-ignored, not committed). No existing test broke on the landing change. (13) **A bare 500** (the clinical-access log failing closed, HO-RPT-001 Amended (12)) is explained as "nothing was shown"; a `429` reads "Try again in N seconds". (14) **Report files are not purged yet** (backlog 5, 60): a run stays `Generated` after its file is gone; the UI shows what the API sends. (15) **`generatedBy`** is also in the result payload (not in §12.4); unused. (16) Real email goes out only in Development (HO-RPT-001 Amended); the run history shows delivery statuses as the notification rows report them. **Defects found and fixed while building:** an absolutely positioned screen-reader span inside a scrolled table cell escaped the table's scroll wrapper and widened the page at 1024 px (fixed in `CellValue`); a navigation made in the same tick as a save is aborted by the unsaved-changes guard's history pop (the create page waits for it); and, only against the real API, the report filter on the schedules page overflowed at 360 px because real report titles are longer than the mock's (fixed: the selects are `max-w-full`). (17) **Real-API run recorded** (`docs/test-cases/TC-HO-RPT-002.md`): database `catecommerce_rpt002e2e` migrated with `dotnet ef database update --connection`, a throwaway seeder (not committed), API :5114, web :3114; E-A1, E-A2, E-A4, E-B1–B3, E-C1–C4, E-D1–D4 and E-X1/E-X2 pass. The real catalogue titles differ from the mock's ("Daily clinic schedule"), and `Reports.Growth.View` is Administrator-only in `ActionRoles.Reports.cs` (the mock first gave it to the Accountant and Shop Manager too; corrected), so growth reports were opened as the administrator. Not run against the real API: E-A3 (one seeded branch), the 429 limiter and the email's arrival. (18) **Gate fix round (2026-10-14)**: see `docs/backlog/RPT.md` rows 67-88 (blocker: `mockServiceWorker.js` restored to master; `decisions.md` back to master text plus ADR-FE-129..138; the 429 wait worded as an estimate when the header is unreadable; the post-save timer cleared and a saved form not re-submittable; a real branch chosen when a report cannot run for all branches; the export filename sanitised and `scheduleId` encoded; chart ticks through the money formatter). (19) **TEST gate fix (2026-10-14, coordinator scope amendment): `web/tests/e2e/home.spec.ts` (outside section 13) changed by one line, from expecting `/staff/cats` to expecting `/staff`, because ADR-FE-130 moved the staff landing.** This corrects (12) and ADR-FE-130, which said no existing test broke: that was true of the Vitest suite only; the Playwright suite had this one case. The report chart now has an explicit Y-axis width so real money labels are not clipped (backlog 90). The ActiveBranchPicker overflow (backlog 89) belongs to HO-ADM-002 and is unchanged. (20) **Owner decisions, 2026-10-14 (after ACCEPTANCE bb089085).** OQ-1 landing: "Dashboard" (ADR-FE-130 owner-confirmed). Date presets: "One date preset" accepted as built, Amended (1) stands. Reports menu: "Hide the menu entry" — **owner-authorised scope amendment to the shell:** `web/nav/types.ts` gains an optional `roles` on `NavItem`, `web/components/common/staff-shell.tsx` filters items by the session's roles (an item with no `roles` is for everyone; a role-limited item is not shown until the session is known), and `web/nav/areas/reporting.ts` limits Reports to the `Reports.Any` roles copied from ActionRoles.Reports.cs (Reception, Vet, Caretaker, Inventory, ShopManager, Accountant, Administrator); Report schedules stays an Administrator-only `AdminNav` entry. The page-level refusal stays as defence in depth. This supersedes the "entry not hidden" part of Amended (9). Tested in `tests/unit/nav-roles.test.tsx` (a Nurse has Home and no Reports; Vet, Accountant, Administrator and Reception see Reports). Chart axis: "Show ৳ on axis" — Y ticks are compact labels with the taka sign ("৳12.5K", `formatAxisTick`), checked by screenshot at 360 and 1440 px and pinned in `chart-axis.test.ts` (supersedes the tick wording in Amended (19) and backlog 90). Refusal names: not chosen; Amended (6) stands. Variances: "Over / Short" is wanted; the backend part is RPT-001-FU1 (branch `ho/RPT-001-FU1`, a direction field on R13 and R14 variance rows) and the frontend part is deliberately not built until that contract lands; Amended (7) stands until then. ACCEPTANCE rows 96 (U-A5 rewritten to assert dialog, focus and announcement through a real refetch) and 101 (docstring, tests, TC evidence, stale row) are fixed; rows 96-102 are in `docs/backlog/RPT.md`. (21) **Variances built against RPT-001-FU1 (branch `ho/RPT-001-FU1`, commit 23dda084; contract in its `docs/handover/HO-RPT-001-FU1.md`). THIS BRANCH DEPENDS ON IT: the integrator must merge HO-RPT-001-FU1 BEFORE HO-RPT-002.** Without FU1 the API sends no `varianceDirection`, so the R13 and R14 direction cells would read "—". The contract: a Text column `varianceDirection` right after `variance` in `payment-reconciliation` and `daily-cash-up`, values "Over", "Short", "Matched" or `null` (nothing to compare: card in store, a day not counted or closed — never shown as "Matched"); `columns[]` of the run result and the catalogue gain an optional trailing `description` (null except on that column); neither report has totals. Built: `ReportColumn.description` optional; `VarianceBadge` (word + icon: Over, Short, Matched) used by `CellValue` for the table and the card list (the one shared badge replaces the duplicate in `CellValue` and `TileFigure`, backlog 80); `isNonZeroAmount` and `isVarianceKey` and all digit and sign logic removed (the page never reads the sign of `variance`); the cash-up dashboard tile shows the variance amount only, because the tile payload carries no direction (FU1 puts it on the report rows). This supersedes Amended (7) and the "Mismatch / Matched" wording; MSW fixtures send the field with deliberately inconsistent signs; tests cover Over, Short, Matched and `null` in the table and the card list. | |

---

## 2. Objective

Two screens carry this slice, and one of them is the first thing staff see every morning.

- **The home dashboard** (`/staff`) shows each role today's numbers for its own job — reception's day,
  the vet's list, the caretaker's overdue tasks, the inventory officer's expiries, the accountant's money,
  the owner's revenue. It shows **only what the server sent**, which is only what the caller may see.
- **The report viewer** (`/staff/reports/[reportKey]`) is **one screen for all 27 reports**. The server sends
  the report's parameters and typed columns; the screen renders the filter bar, the table, the totals and the
  notes from that payload, and downloads CSV or PDF from the server. A new report next year needs no frontend
  change at all.
- **Report schedules** (`/staff/admin/report-schedules`) let an administrator have a report emailed on a
  schedule, with the next run times previewed by the server and the run history visible.

Four things make this harder than it looks:

1. **The client computes nothing.** Not a total, not a margin, not a percentage, not a next run time, not even
   a default date range — every one of them comes from the API (ADR-FE-131). A number computed twice is a
   number that will eventually disagree with itself, and money is involved.
2. **A wide table must still work at 360 px.** The catalogue's column list can be twelve columns wide. Below
   768 px each row becomes a labelled card, so the page never scrolls sideways (ADR-FE-133, UI-1).
3. **A download must be able to fail visibly.** Exports can answer `413` or `400 ReportTimedOut`. Navigating
   the browser to the URL would render the server's error body as a page, so exports are fetched and saved as
   a blob (ADR-FE-132).
4. **Charts are a companion, never the answer.** A chart alone fails UI-6, so the table is always the primary
   presentation and any chart carries the same numbers (ADR-FE-134).

---

## 3. In scope

### 3.1 Inherited — do not revisit

The HO-CAT-002 stack, layering, accessibility rules and dependency list; HO-ACC-002's route groups, session
guards, `client.ts`, `PermissionDeniedState`, error mapping and `UnsavedChangesGuard`; HO-ADM-002's active
branch and `X-Branch-Id` header, `lib/format` (money as strings, `Intl` in the branch zone), `AdminNav` and its
`adminNavItems` map, and its rule that role-based hiding is presentation only.

### 3.2 Stories and screens

| Story | Screens | Route |
|---|---|---|
| US-RPT-05 | **Home dashboard** — tiles for the caller's roles, today, at the active branch, each with a drill-down link; auto-refresh | `/staff` ⭐ new landing route |
| US-RPT-01 AC2 | **Report catalogue** (grouped by category) and the **generic report viewer**: filter bar (date range presets, branch, report parameters), sortable paginated table, totals, notes, CSV and PDF download | `/staff/reports`, `/staff/reports/[reportKey]` |
| US-RPT-01 AC1 | No extra screens — the daily schedule, surgery register, vaccination due list and queue performance are catalogue entries in the viewer | `/staff/reports/clinic-daily-schedule` … |
| US-RPT-02, US-RPT-03, US-RPT-04 | Likewise: stock, revenue, payment and growth reports are catalogue entries. Time-bucketed reports gain an optional chart | `/staff/reports/[reportKey]` |
| US-RPT-06 | **Report schedules**: list, create / edit (report, branch, period, format, frequency, send time, recipients), server-previewed next runs, pause / resume / cancel, run now, run history with delivery status | `/staff/admin/report-schedules/**` |

### 3.3 Decisions already taken — do not re-litigate

| Decision | Where |
|---|---|
| Server data is TanStack Query; no global store | HO-CAT-002 §8.4 |
| Filters and list state live in URL search params | HO-CAT-002 §8.4, HO-ADM-002 §8.3 |
| Money stays a **string** end to end; `Intl` only for display | HO-ADM-002 §8.4 |
| Branch times are rendered with `Intl.DateTimeFormat` in the branch's IANA zone, and every time label names the zone; no time-zone library | HO-ADM-002 ADR-FE-014 |
| The active branch is TanStack state sent as `X-Branch-Id` by `client.ts` | HO-ADM-002 ADR-FE-015 |
| Role-based nav hiding is presentation; a server `403` renders `PermissionDeniedState` | HO-ACC-002 ADR-FE-009, HO-ADM-002 §11.1 |
| Data the system does not record is stated as such, never shown as zero | HO-ACC-002 ADR-FE-012 |
| No scheduler, calendar, date-picker, data-grid or CSV library | HO-CAT-002 §10, HO-ADM-002 §10 |

---

## 4. Out of scope

| Excluded | Why |
|---|---|
| Any backend change | HO-RPT-001 owns `src/**` |
| **Any client-side aggregation, total, margin, percentage, currency conversion or date-range calculation** | ADR-FE-131; the server sends them (HO-RPT-001 §8.6, §12.4) |
| A report that HO-RPT-001's catalogue does not define, or a hardcoded report layout | ADR-FE-129 |
| Customer-facing reporting | No story. The customer portal is HO-POR-002 |
| The live clinic queue, boarding dashboard, reconciliation workflow and cash-up entry screens | HO-APT-002, HO-BRD-002, HO-BIL-002 own them. Reporting links **to** them |
| Editing a generated run or its file | Runs are records; only "run now" creates one |
| Offline dashboards | HO-XCT-002 |

Forbidden shapes are HO-ACC-002 §4.3's, unchanged.

---

## 5. Prerequisites

| Required | State |
|---|---|
| Node 20 LTS, HO-CAT-002 scaffold | ❌ at baseline — §20.1 |
| **HO-ACC-002, HO-ADM-002, HO-NTF-002 built** | ❌ at baseline — §20.1 |
| The frontends of the areas this slice links to (HO-APT-002, HO-BRD-002, HO-BIL-002, HO-ORD-002, HO-IMP-002) | ❌ — drill-down links to screens that do not exist yet are **omitted**, not rendered as dead links (§11.2, OQ-4) |
| HO-RPT-001 API | ❌ — build against MSW generated from §12.4 there, then re-run every E-case against the real API |

---

## 6. Governing documents

| Document | What you need |
|---|---|
| [`HO-RPT-001`](HO-RPT-001.md) | **§12.4** endpoints and payload shapes (the contract); §8.2 what a report definition carries; §8.3 the 27 reports and their rules; §8.6 what margin and coverage mean, so the screen can explain them; §8.7 date range and branch semantics; §8.8 who sees what, and that restricted columns are simply **absent**; §8.9 schedules; §8.11 tiles; §10.4 violation codes and their messages |
| [`HO-ADM-002`](HO-ADM-002.md) | §8.3 active branch; §8.4 `lib/format`; §11.1 `AdminNav` and `adminNavItems`; ADR-FE-014, ADR-FE-015, ADR-FE-016 |
| [`HO-ACC-002`](HO-ACC-002.md) | §8.3 route groups and the staff sign-in landing; §8.4 session model; §11.14 error mapping; `PermissionDeniedState` |
| [`HO-CAT-002`](HO-CAT-002.md) | §8 layering, state, accessibility; §9 conventions; §10 the dependency list (`recharts` is already permitted); §11 `WeightTrendChart`'s hidden-table pattern — reused here |
| [`HO-NTF-002`](HO-NTF-002.md) | The notification log, where a scheduled report's delivery appears |
| [`docs/SRS.md`](../SRS.md) §3.1, §6.1, §6.4 | UI-1, UI-3, UI-4, UI-5, UI-6; NFR-PRF-3; NFR-USA-4/5/6 |

The `.ai/` documents describe the .NET backend only and do not govern this diff — the same position as
HO-CAT-002 §6 and HO-ACC-002 §6.

---

## 7. The SRS requirements, as build rules

| Req | Rule | Where enforced |
|---|---|---|
| **UI-1** | 360 px floor. The report table becomes a **list of labelled cards below 768 px**; the dashboard is a single column of tiles; the filter bar stacks. **No page-level horizontal scrolling anywhere**, including a 12-column report | §11.3, §11.4, U-C6, E-X2 |
| **UI-3** | Destructive actions confirm and state what they affect: **Cancel schedule** (*"'Daily revenue' will stop being emailed to 3 people. Past runs are kept."*), **Pause**, and **Run now** (*"This generates the report and emails it to 3 people now."*) | §11.5 |
| **UI-4** | Every list filters, sorts, paginates and exports CSV — the report viewer *is* that list, and the schedule list and run history follow it too | §11.3, §11.5 |
| **UI-5** | The dashboard refetches on the payload's `refreshSeconds` (server default 60 s) **without** resetting an open dialog or a filter being typed; the refresh is paused while the tab is hidden, and the last-updated time is shown | ADR-FE-135, U-A5 |
| **UI-6 / NFR-USA-6** | WCAG 2.1 AA. The report table is a real `<table>` with `<th scope>` and a `<caption>`; sorting is a `<button>` in the header with `aria-sort`; every chart carries the same numbers as a visually hidden table; no figure is conveyed by colour alone (variances and SLA breaches carry a word and an icon); the refresh announces politely, never interrupting | §11.3, §11.4, U-X1, E-X1 |
| **NFR-PRF-3** | A report page shows a skeleton immediately and never blocks on the catalogue: the definition and the rows are two queries. The dashboard renders each tile as it is (one request, but a tile that the server omitted never leaves a spinner behind) | §11.2, §11.4 |
| **NFR-USA-4** | Report errors say what to do: `ReportRangeTooLong` → *"This report covers at most {n} days. Choose a shorter range."*; `ReportTimedOut` → *"That took too long. Try a shorter range or a single branch."*; `413` → *"Too many rows for a PDF — export CSV instead."*; `403` → `PermissionDeniedState` | §11.7 |
| **NFR-USA-5** | Filter values survive validation errors and reloads, because they live in the URL; the schedule editor keeps its values across a server error and guards navigation while dirty | §8.3, §11.5 |
| **C-6 / C-7** | Money is never parsed into a number; every date and time is rendered in the branch zone named in the payload | ADR-FE-131, §8.4 |

---

## 8. Frontend architecture

### 8.1 Route map — additions

```
web/app/
  page.tsx                                       ⚠ MODIFIED — redirect('/staff') instead of '/staff/cats'   ADR-FE-130
  (staff)/
    layout.tsx                                   ⚠ nav entries: Home, Reports
    staff/
      page.tsx                                   ⭐ role home dashboard                     US-RPT-05
      reports/
        page.tsx                                 catalogue, grouped by category             US-RPT-01
        [reportKey]/page.tsx                     ⭐ the generic report viewer               US-RPT-01…04
      admin/
        report-schedules/
          page.tsx                               list                                       US-RPT-06
          new/page.tsx                           create                                     US-RPT-06
          [scheduleId]/page.tsx                  edit + run history                         US-RPT-06
```

`/staff` is inside `(staff)`, so HO-ACC-002's `StaffSessionGuard` and idle lock already cover it. **HO-ACC-002's
staff sign-in default destination changes from `/staff/cats` to `/staff`** — one line there, listed in §13
(ADR-FE-130, OQ-1).

### 8.2 API modules

`lib/api/reports.ts` (catalogue, run a report, build an export URL, fetch an export blob),
`lib/api/dashboard.ts`, `lib/api/report-schedules.ts` (list, detail, create, update, pause, resume, cancel,
run now, preview, runs, eligible recipients). Types in `lib/api/types.ts` mirror HO-RPT-001 §12.4 **exactly** —
same names, same nullability, so a mismatch is a compile error (HO-CAT-002 §8.2 rule 1).

### 8.3 State

| Kind | Where |
|---|---|
| Report filters — `from`, `to`, `branchId`, `groupBy`, `sort`, `pageNumber`, `pageSize` and every report parameter | **URL search params** (ADR-FE-129). A filtered report is a shareable link and survives reload |
| Report definition | `['reports','catalogue']` — one fetch, `staleTime` 5 min; the viewer reads its definition from it |
| Report rows | `['report', reportKey, searchParams, activeBranch]` — refetched on any filter change; `keepPreviousData` so the table does not flash while paging |
| Dashboard | `['dashboard', activeBranch]`, `refetchInterval` from the payload's `refreshSeconds`, `refetchIntervalInBackground: false` |
| Schedules, runs, eligible recipients, preview | `['report-schedules', …]`; the preview is a **mutation**, not a query, so it is explicit |
| Editor values | react-hook-form + zod (shape only: required, lengths, 5-minute steps — the server owns the rules) |
| Export in flight | Local `useState` per button (label → *"Preparing…"*, disabled, announced) |

### 8.4 Rendering values — `lib/format` additions

One function per `ReportColumnType`, in a single `formatCell(value, column, timeZoneId)`:

| Column type | Rendering |
|---|---|
| `Money` | `formatMoney` (HO-ADM-002) from the row's `{ amount, currencyCode }` **string** — never `Number(...)` |
| `Percent` | the server's string + `%` |
| `Decimal`, `Integer` | `Intl.NumberFormat` grouping only, no rounding |
| `Date` | `dd MMM yyyy` from the `yyyy-MM-dd` string, with **no** `Date` construction that could shift a day (HO-CAT-002 §9) |
| `Instant` | `formatInZone(value, timeZoneId, 'dd MMM yyyy HH:mm')`, the zone named in the column header or the filter summary |
| `Boolean` | *Yes* / *No* as text |
| `Text` | plain text; never `dangerouslySetInnerHTML` |
| **`null`** | **"—"** with a visually hidden *"Not recorded"* — never `0` (HO-ACC-002 ADR-FE-012) |

---

## 9. ADRs and open questions

Record in `web/docs/decisions.md`, continuing HO-APT-002's series. **This handover owns ADR-FE-129 …
ADR-FE-138** (orchestrator assignment); it uses ADR-FE-129 … ADR-FE-137.

| ADR | Decision | Why |
|---|---|---|
| **ADR-FE-129** | **One generic report viewer**, rendered entirely from the catalogue payload: filter controls from `parameters`, table columns from `columns`, totals from `totals`, explanations from `notes`. There is no per-report page or per-report component, and no hardcoded column list anywhere | HO-RPT-001 defines 27 reports and will define more. A per-report screen guarantees drift between what the server can filter and what the screen offers |
| **ADR-FE-130** | **`/staff` is the staff home and the landing route**: `app/page.tsx` redirects there and staff sign-in defaults there. `/staff/cats` remains reachable from the nav | FR-RPT-3 says the *home* dashboard shows the role's figures. HO-ACC-002 §8.3 flagged the landing route as a product decision; this is it (OQ-1) |
| **ADR-FE-131** | **The client computes no figure.** No totals, margins, percentages, currency maths, next-run times or default date ranges. Money stays a string; `defaultRange` and `nextRuns` come from the server | C-6; two implementations of "net revenue" is how a dashboard and a report end up disagreeing in front of the owner |
| **ADR-FE-132** | **Exports are fetched as a blob** through `client.ts` (so the session, CSRF-free `GET` and error handling all apply) and saved with an object URL, instead of navigating the browser to the URL | A navigation cannot show `413 ReportTooLargeForPdf` or `400 ReportTimedOut` — the user would see a raw JSON page. The object URL is revoked immediately after the download starts |
| **ADR-FE-133** | **The report table collapses to labelled cards below 768 px** (each row a `<dl>` of column label → value), rather than scrolling horizontally. The desktop table's first column is sticky | UI-1 at 360 px for tables the server may make 12 columns wide; matches HO-ADM-002 ADR-FE-016's approach for the roster |
| **ADR-FE-134** | **Charts are optional companions.** Only `timeBucketed` reports offer one (`recharts`, already permitted); the table is always rendered and the chart repeats **the same server numbers**, with a visually hidden data table, as HO-CAT-002's `WeightTrendChart` does. A chart is never the only presentation and never a different calculation | UI-6; a chart that recomputed from raw rows would contradict ADR-FE-131 |
| **ADR-FE-135** | **The dashboard polls on the server's `refreshSeconds`**, pauses while the tab is hidden, never resets an open dialog or a focused control, and announces *"Updated HH:mm"* in a polite live region | UI-5 "without losing unsaved input"; a dashboard on a reception screen runs all day |
| **ADR-FE-136** | **Schedule timing is previewed by the server.** The editor posts the draft to `…/preview` and shows the next three run times and the period the next run will cover, plus any quiet-hours hold. The client never computes a recurrence or a DST-correct instant | HO-RPT-001 ADR-287's calculator is the only implementation; a second one in JavaScript would disagree on the DST night |
| **ADR-FE-137** | **Permission is expressed by absence, not by placeholders.** A report the caller may not open is not in the catalogue; a restricted column is not in the payload and is simply not rendered; a tile the caller may not see is not sent. The UI adds no "hidden" or "locked" affordance, and a direct URL still renders `PermissionDeniedState` from the server's `403` | HO-RPT-001 §8.8. Telling someone exactly which financial columns exist but are withheld is itself information |

| # | Question | Owner | Default |
|---|---|---|---|
| OQ-1 | Landing route change to `/staff` (touches HO-CAT-002's `app/page.tsx` and HO-ACC-002's sign-in default) | Owner | Yes — ADR-FE-130 |
| OQ-2 | Date-range presets offered | Owner | Today, Yesterday, This week, Last week, This month, Last month, Last 30 days, Custom — mapped to the server's `ReportRelativePeriod` values plus a custom pair |
| OQ-3 | Default chart type per report | Owner | Bar for bucketed money and counts; line for rates and percentages; the table is authoritative either way |
| OQ-4 | Drill-down targets whose screens are not built yet | Product | The tile still links to its **report**; a link to another area's screen is omitted until that area's frontend exists |
| OQ-5 | Should the dashboard remember a collapsed tile? | Owner | No — it is short and reordering is the server's |
| OQ-6 | Print stylesheet for the report viewer | Owner | No — PDF export is the print path |

---

## 10. Dependencies

**No new package.** `recharts` and `date-fns` are already permitted by HO-CAT-002 §10 and are the only additions
used here. A data-grid, pivot-table, CSV, PDF, spreadsheet or date-picker library is a **§20 stop** — the table
is in-repo, the exports are the server's.

---

## 11. Specified changes — components and screens

Each component under ~150 lines, with distinct loading / empty / error states.

### 11.1 Navigation

- `(staff)/layout.tsx` gains two nav entries: **Home** (`/staff`) and **Reports** (`/staff/reports`). Reports is
  hidden for a staff member with no report category (presentation only — HO-ADM-002 §11.1): hidden when
  `GET /api/staff/reports` answers `403`, which for a Nurse it does.
- `AdminNav` (`adminNavItems`, HO-ADM-002) gains **Report schedules**.

### 11.2 Home dashboard — US-RPT-05 · `/staff`

| Component | Behaviour |
|---|---|
| ⭐ `DashboardPage` | Header: *"Today — {localDate} · {branchName}"* with the zone, the active-branch picker (HO-ADM-002) and *"Updated HH:mm"*. Renders **only the tiles the server sent**, in the server's order (ADR-FE-137). Empty payload → an empty state explaining that today's figures appear here once the relevant areas are in use |
| `DashboardTile` | Card: title, one to four figures, each `label` + `formatCell(value, type)` with its unit. Layout: 1 column at 360 px, 2 at 768 px, 3 at 1024 px. `note` shown as small text. `drillDown` renders **View report →** linking to `/staff/reports/{reportKey}?…params` (omitted when the server sent `null`, OQ-4) |
| `TileFigure` | `Money`, `Percent`, `Integer`, `Decimal` via `formatCell`; **`null` → "—" with "Not recorded"**; a figure that is a variance or a breach shows the word (*"Over"*, *"Short"*, *"Breached"*) beside the icon — never colour alone (UI-6) |
| Refresh | `refetchInterval` from `refreshSeconds`; paused when `document.hidden`; the polite live region announces *"Dashboard updated"* at most once a minute (ADR-FE-135) |

### 11.3 Report catalogue and viewer — US-RPT-01…04 · `/staff/reports/**`

| Component | Behaviour |
|---|---|
| `ReportCatalogue` | Groups by `category` with a plain-language heading (Clinic operations · Clinical · Boarding · Stock · Sales · Financial · Growth); each card shows title, description and *"Covers up to {maxRangeDays} days"*; a search box filters by title and description (client-side over the small list). `403` → `PermissionDeniedState` |
| ⭐ `ReportFilterBar` | Rendered from the definition: **date range** (`DateRangePresets` + two `yyyy-MM-dd` inputs labelled with the branch zone; the server's `defaultRange` seeds an empty URL), **branch** (`All branches` shown only when `canUseAllBranches`), **group by** for `timeBucketed`, then one control per `parameter` (`Enum` → `<select>` of `allowedValues`, `Boolean` → checkbox, `Integer` → number with min/max, `Guid` → the area's picker where one exists, else a plain field). **Apply** writes the URL; **Reset** returns to the defaults. The range limit is shown before submitting (*"Up to 366 days"*) |
| ⭐ `ReportTable` | A real `<table>`: `<caption>` = title + resolved period + branch + zone; `<th scope="col">` per visible column with the unit in the label; sortable headers are `<button>`s with `aria-sort`, writing `sort` to the URL; `formatCell` per cell; `<tfoot>` for `totals`, labelled *"Total"*. Numeric columns right-aligned with `tabular-nums`. Sticky first column on desktop; **below 768 px, `ReportCardList` renders each row as a `<dl>`** (ADR-FE-133). Paging per UI-4 with `totalCount` |
| `ReportNotes` | Each server `note` as a short paragraph above the table — this is where *"Margin covers 83.0 % of shop revenue…"* (HO-RPT-001 §8.6) and the card-terminal caveat appear. **Never reworded by the client** |
| `ReportChart` | `timeBucketed` reports only: a **Show chart** toggle above the table; `recharts` bar or line over the same rows; axis labelled with the unit and currency; a visually hidden `<table>` repeats the points (HO-CAT-002's pattern). Hidden by default so the page is fast |
| ⭐ `ExportButtons` | **Export CSV** and **Export PDF**: fetch the blob through `client.ts` with the current filters, save with an object URL and the server's filename, revoke it immediately (ADR-FE-132). While in flight the button is busy and announced. `413` → *"Too many rows for a PDF — export CSV instead."* / *"…narrow the date range."*; `400 ReportTimedOut` → the timeout message; `403` → `PermissionDeniedState` |
| `ScheduleThisReport` | Shown when the definition says `canSchedule`: **Schedule this report** → `/staff/admin/report-schedules/new?reportKey=…&branchId=…&{parameters}` prefilled from the current filters |

### 11.4 Empty, loading and error states

- **Loading**: a skeleton table with the definition's column count (the definition is cached, so the shape is
  known before the rows arrive).
- **Empty**: *"No {rows} for {period} at {branch}."* with the two most useful next steps — widen the range, or
  switch branch. Never a bare "No data".
- **Error**: field-level for filter errors (`from`/`to`/parameter), a page-level banner otherwise, using
  HO-ACC-002's mapping; the filters stay in the URL so nothing typed is lost.

### 11.5 Report schedules — US-RPT-06 · `/staff/admin/report-schedules/**`

| Component | Behaviour |
|---|---|
| `ScheduleTable` | Name, report, branch, *"Daily at 07:00 (Asia/Dhaka)"*, format, recipients (count + names on hover/focus as text), status, **next run** (instant in the schedule's zone), last run with its status. Filters by status and report in the URL |
| ⭐ `ScheduleForm` | Report (`<select>` of schedulable catalogue entries), branch (`All branches` only when permitted), period (`ReportRelativePeriod` values the report allows, with a plain-language hint: *"Covers the previous month"*), format, frequency (Daily / Weekly + day / Monthly + day-of-month or last day), send time (`<input type="time">`, 5-minute steps, labelled with the zone), and **recipients** — a multi-select fed by `…/recipients?reportKey&branchId`, showing each staff member's roles and marking anyone without an email as unselectable with the reason. Any parameters the report defines are rendered by the same control set as the filter bar |
| ⭐ `NextRunPreview` | Debounced `POST …/preview` on every change: *"Next runs: Mon 5 Oct 07:00, Tue 6 Oct 07:00, Wed 7 Oct 07:00 (Asia/Dhaka)"*, *"The next one covers 1–30 Sep"*, and, when the server returns `deliveryHeldUntil`, *"Quiet hours mean it will be delivered at 08:00."* (ADR-FE-136) |
| `ScheduleActions` | **Pause** / **Resume**; **Cancel** with a UI-3 confirm naming the schedule and the recipient count and stating that past runs are kept; **Run now** with a confirm stating that it emails the recipients immediately, then a result toast (*"Generated — 412 rows"*) and a refreshed run list |
| `RunHistoryTable` | Due, trigger (Scheduled / Manual), status (**Generated / Failed / Purged** as text), period covered, rows, file size, `attachmentOmitted` shown as *"Too large to attach — link sent"*, failure code in plain language, and per-recipient delivery status. A `Failed` row explains what to change (shorter period, CSV instead of PDF). Links to HO-NTF-002's notification log for the delivery detail |
| Guards | The whole section is administrator-only on the server; a `403` renders `PermissionDeniedState`. `UnsavedChangesGuard` on the form |

### 11.6 Cross-links from other areas

Nothing else is edited. Other areas link **into** the viewer with a query string, and the viewer needs no
knowledge of them.

### 11.7 Error mapping — `lib/api/errors.ts`

Append HO-RPT-001 §10.4's codes with the messages used above: `UnknownReport` (*"That report doesn't exist."* +
a link to the catalogue), `ReportRangeRequired`, `ReportRangeInverted`, `ReportRangeTooLong`,
`InvalidReportParameter`, `UnknownReportParameter`, `CrossBranchReportNotPermitted` (*"You can run this for your
own branch."*), `ReportTooLargeForCsv`, `ReportTooLargeForPdf`, `ReportTimedOut`, `ReportScheduleNotFound`,
`InvalidReportSchedule`, `InvalidReportSchedulePeriod`, `ReportRecipientRequired`,
`ReportRecipientLimitExceeded`, `ReportRecipientNotPermitted` (*"{name} can't open this report, so they can't
receive it."*), `ReportRecipientHasNoEmail`, `ReportRecipientNotAssignedToBranch`, `ReportScheduleCancelled`,
plus `BranchNotAssigned` (already mapped by HO-ADM-002). The server's message is preferred when present; the
mapped text is the fallback. **`errorCode` never reaches the DOM** (HO-ACC-002 §11.14).

---

## 12. Mocks — `web/mocks/handlers/reports/*.ts`

Generated from HO-RPT-001 §12.4. Fixtures:

- A catalogue with **one report per category**, including a 12-column financial report with restricted columns
  present for the "accountant" identity and absent for the "shop manager" identity, a `timeBucketed` report, an
  `AsAt` report, and one with every parameter type.
- Row sets: a normal page, an empty result, a result with `totals`, a result with a coverage `note` and `null`
  margin, a result containing `null` cells, and a 500-row page for the card-list breakpoint.
- Errors: `400 ReportRangeTooLong`, `400 ReportTimedOut`, `413` on PDF, `403` on a report and on the catalogue
  (the Nurse identity), `429`.
- Dashboards per role identity (reception, vet, nurse, caretaker, inventory, shop manager, accountant,
  administrator), including one tile with a `null` figure, one without `drillDown`, and a second poll whose
  numbers have changed.
- Schedules: active, paused, cancelled; a preview response with three next runs and a `deliveryHeldUntil`; run
  history with `Generated`, `Failed` and `Purged` rows and mixed delivery statuses; recipient lists including a
  staff member without an email.
- Two branches in **different time zones**, to prove every rendered instant follows the payload's `timeZoneId`.

---

## 13. Allowed scope

```
web/app/page.tsx                                              ← the redirect target only (HO-CAT-002's file)
web/app/(staff)/layout.tsx                                    ← two nav entries only
web/app/(staff)/staff/page.tsx                                ⭐ new
web/app/(staff)/staff/reports/**
web/app/(staff)/staff/admin/report-schedules/**
web/app/(auth)/staff/sign-in/page.tsx                         ← default post-sign-in destination only (HO-ACC-002's file)
web/components/{dashboard,reports,report-schedules}/**
web/lib/api/{reports,dashboard,report-schedules}.ts
web/lib/api/types.ts · web/lib/api/errors.ts                  ← append
web/lib/format/**                                             ← §8.4 additions (formatCell and the per-type helpers)
web/lib/hooks/**                                              ← new hooks only
web/lib/download/**                                           ← the blob-save helper (ADR-FE-132)
web/lib/validation/**                                         ← schedule form schema only
web/mocks/handlers/reports/** · web/mocks/handlers/index.ts
web/tests/components/{dashboard,reports,report-schedules}/**
web/tests/e2e/reports-*.spec.ts · web/tests/e2e/dashboard-*.spec.ts
web/docs/decisions.md                                         ← ADR-FE-129…137
docs/test-cases/TC-HO-RPT-002.md · docs/gates/HO-RPT-002-*.md
```

`AdminNav`'s `adminNavItems` file (HO-ADM-002) gains **one** entry; it is inside
`web/components/…` or `web/lib/…` per HO-ADM-002 §11.1 and is the only line changed there.

## 14. Forbidden scope

| Path | Why |
|---|---|
| `src/**`, `tests/**` | HO-RPT-001 owns the backend |
| `.ai/**`, `.claude/**`, `docs/**` except the two named paths | Governing docs and other handovers |
| Every other HO-CAT-002, HO-ACC-002, HO-ADM-002, HO-NTF-002 and HO-APT-002 file beyond the lines in §13 | Their areas. In particular `/staff/cats`, `/staff/front-desk` and the admin screens are untouched |
| `web/package.json`, `tsconfig.json`, `next.config.ts`, `eslint.config.mjs` | No new dependency or config (§10) |
| Any client-side aggregation, recurrence or currency maths | ADR-FE-131, ADR-FE-136 |

---

## 15. Component test cases

| ID | Case | Expected |
|---|---|---|
| **A — dashboard** | | |
| U-A1 | Payload with 6 tiles | Exactly those tiles, in the server's order; none invented |
| U-A2 | The nurse payload (no financial tiles) | No financial, stock or sales tile rendered anywhere, and no placeholder for them — ADR-FE-137 |
| U-A3 | A figure with `value: null` | "—" plus a visually hidden *"Not recorded"*; **never** `0` |
| U-A4 | A tile with `drillDown: null` | No link rendered |
| U-A5 | Poll while a dialog is open and a field focused | Values update; the dialog stays open, focus is kept; *"Updated HH:mm"* announced politely |
| U-A6 | `document.hidden` | No refetch until the tab is visible again |
| U-A7 | Money and percent figures with a `Europe/London` branch payload viewed in `Asia/Dhaka` | Money from the string; the date is the payload's `localDate`, not the browser's today |
| **B — catalogue** | | |
| U-B1 | Catalogue grouped by category | Headings in plain language; a report the payload omits is absent |
| U-B2 | `403` from the catalogue (Nurse) | `PermissionDeniedState`; the Reports nav entry hidden |
| U-B3 | `canSchedule: false` | No "Schedule this report" |
| **C — the report viewer** | | |
| U-C1 | Definition with every parameter type | One control per parameter, correctly typed, labelled and bounded; unknown types never crash the page |
| U-C2 | Empty URL | Filters seeded from the server's `defaultRange` and parameter defaults — **no client-side date arithmetic** (spy: no `Date` maths in the filter bar) |
| U-C3 | Apply a preset and a custom range; reload | Filters in the URL; the reload shows the same report |
| U-C4 | Payload with `totals` and two `notes` | `<tfoot>` labelled Total; the notes rendered verbatim above the table |
| U-C5 | Restricted columns absent (shop-manager fixture) vs present (accountant fixture) | Rendered from `columns` alone; no "hidden column" affordance in either |
| U-C6 | 12-column report at 1440 px and at 360 px | Table with sticky first column / labelled cards; **no horizontal page scroll at 360 px** |
| U-C7 | Sort a sortable and a non-sortable header | `aria-sort` and the URL update / no button at all |
| U-C8 | `null` cells, money, percent, date, instant, boolean | Per §8.4; instants in the payload's zone, zone named |
| U-C9 | Chart toggle on a `timeBucketed` report | Chart hidden by default; when shown, the hidden data table matches the visible rows exactly |
| U-C10 | Empty result | The empty state names the period and branch and suggests widening the range |
| **D — exports** | | |
| U-D1 | Export CSV | One `GET` to `…/export.csv` with the current filters; blob saved with the server's filename; the object URL revoked |
| U-D2 | Export PDF returning `413` | The "too many rows — export CSV" message; no download; the button returns to its idle label |
| U-D3 | Export returning `400 ReportTimedOut` | The timeout message with the "shorter range" advice |
| U-D4 | While an export is in flight | The button is busy, disabled and announced; the table is still usable |
| **E — schedules** | | |
| U-E1 | Form with a report whose periods are limited | Only the allowed periods offered, with plain-language hints |
| U-E2 | Recipient without an email | Unselectable, with the reason as text |
| U-E3 | Any change to the form | One debounced `preview` call; the next three runs and the covered period shown; **no client-side recurrence maths** (spy) |
| U-E4 | Preview with `deliveryHeldUntil` | The quiet-hours sentence shown |
| U-E5 | Server `400 ReportRecipientNotPermitted` | Mapped to the recipients field naming the person; the rest of the form keeps its values |
| U-E6 | Cancel, Pause, Run now | Each confirm states exactly what happens and to how many recipients (UI-3) |
| U-E7 | Run history with Generated / Failed / Purged and `attachmentOmitted` | Status as text; the failure explained; the "too large to attach" note shown |
| U-E8 | Dirty form + navigate | Unsaved-changes confirm |
| **X — cross-cutting** | | |
| U-X1 | axe on the viewer, the dashboard and the schedule form | Zero serious/critical; the table has a caption and header scopes; sort buttons carry `aria-sort` |
| U-X2 | Every server `400` | Mapped to its field where the `propertyName` matches, page-level otherwise; `errorCode` never in the DOM |
| U-X3 | Source scan of `components/{dashboard,reports}` | No arithmetic on money, totals or percentages; no `Number(` over an `amount`; no recurrence calculation |

## 16. End-to-end test cases

MSW first, then the real API; **both runs recorded**.

| ID | Case | Expected |
|---|---|---|
| E-A1 | Sign in as reception | Lands on `/staff`; today's reception tiles shown for the active branch — **US-RPT-05** |
| E-A2 | Sign in as accountant, then as nurse | Accountant sees money tiles; the nurse sees none of them and has no Reports nav — **US-RPT-05 "nothing I am not permitted to see"** |
| E-A3 | Switch the active branch | Tiles reload for the other branch; the date label follows that branch's zone |
| E-A4 | Leave the dashboard open past the refresh interval (real API) | Figures update; a dialog left open stays open |
| E-B1 | Open the daily schedule report for a date and branch; then the vaccination due list; then the queue performance report | Rows as seeded by the API run — **US-RPT-01 AC1** |
| E-B2 | Apply a range over the limit; then a valid one | The limit message; then results — **US-RPT-01 AC2** |
| E-B3 | Export CSV and PDF from the same report | Two files downloaded, both non-empty; the CSV opens with the same column headers as the screen — **US-RPT-01 AC2** |
| E-B4 | Copy the URL into a second browser context signed in as the same role | The identical filtered report |
| E-C1 | Stock reports: on hand, near expiry, low stock | Values, valuation and suggestions shown — **US-RPT-02** |
| E-C2 | Revenue by business line, then shop by category and origin, as accountant then as shop manager | Margin and coverage visible to the accountant only — **US-RPT-03 AC1, AC2** |
| E-C3 | Payment reconciliation report | Rows for COD, cash, card and bank transfer; the variance flagged in words — **§8.2** |
| E-C4 | Growth reports | New vs returning, LTV, cohort, subscriptions, cross-line — **US-RPT-04** |
| E-D1 | Create a daily schedule for the revenue report with two recipients; check the preview; save | `201`; next runs shown; the schedule in the list — **US-RPT-06** |
| E-D2 | **Run now**, then open the run history (real API) | A `Generated` run with rows and a file size; the delivery row per recipient; the email visible in the capturing sender or the notification log — **US-RPT-06** |
| E-D3 | Pause, resume, cancel | Status changes; cancel confirms and keeps past runs |
| E-D4 | A shop manager opens `/staff/admin/report-schedules` directly | `PermissionDeniedState` — FR-RPT-2 |
| E-X1 | axe on `/staff`, `/staff/reports`, a report with a chart open, an export in flight, and the schedule form | Zero serious/critical |
| E-X2 | Every route at 360 / 768 / 1024 / 1440 px, including a 12-column report | No horizontal page scroll, no clipped control — UI-1 |

---

## 17. Implementation order and verification

1. Confirm prerequisites (§5) — else stop.
2. Types and API modules from HO-RPT-001 §12.4; MSW handlers; `lib/format` additions (`formatCell`); the blob
   download helper.
3. **Dashboard** (`/staff`) + the landing-route change + nav entries.
4. **Catalogue** → **report viewer** (filter bar → table → card list → totals and notes) → export buttons.
5. Chart companion for `timeBucketed` reports.
6. **Report schedules**: list → form with server preview → actions → run history.
7. U-series, then E-series on MSW, then the E-series against the real API per HO-RPT-001 phase.

```bash
cd web && npm run lint && npm run typecheck && npm run test && npm run test:e2e && npm run build
```

---

## 18. Acceptance criteria

| # | Story | Criterion | Evidence |
|---|---|---|---|
| A1 | RPT-05 | The home dashboard shows the signed-in role's key figures for today at the active branch | U-A1, U-A7, E-A1, E-A3 |
| A2 | RPT-05 | It shows nothing the caller is not permitted to see, with no placeholder for what is withheld | U-A2, U-C5, E-A2 |
| A3 | RPT-05 / UI-5 | It refreshes without losing an open dialog or focus, and pauses when hidden | U-A5, U-A6, E-A4 |
| A4 | RPT-01 AC1 | The daily schedule, surgery register, vaccination due list and queue performance are each viewable for a date and branch | E-B1 |
| A5 | RPT-01 AC2 | Every report is filterable by date range and branch through one screen driven by the catalogue | U-C1, U-C2, U-C3, E-B2, E-B4 |
| A6 | RPT-01 AC2 | Every report exports to CSV and PDF, with failures explained | U-D1, U-D2, U-D3, E-B3 |
| A7 | RPT-02 | Stock, expiry and reorder reports are usable | E-C1 |
| A8 | RPT-03 AC1, AC2 | Revenue by line, category and origin; margin and coverage shown only to those permitted, and never recomputed client-side | U-C4, U-C5, U-X3, E-C2 |
| A9 | RPT-04 | The growth reports are usable | E-C4 |
| A10 | §8.2 | Payment reports present COD, cash, card and bank transfer, with variances in words not colour | E-C3 |
| A11 | RPT-06 | An administrator schedules a report, sees the server-computed next runs, and manages the schedule | U-E1…U-E6, E-D1, E-D3 |
| A12 | RPT-06 | A run's outcome and per-recipient delivery are visible, including failures and unattached files | U-E7, E-D2 |
| A13 | FR-RPT-2 | Non-administrators cannot reach the schedule screens | E-D4 |
| A14 | ADR-FE-131 | No figure is computed in the browser | U-C2, U-E3, U-X3 |
| A15 | UI-6 / UI-1 | Zero serious/critical axe violations; usable from 360 px including a 12-column report; charts carry an equivalent table | U-C6, U-C9, U-X1, E-X1, E-X2 |
| A16 | NFR-USA-4 / 5 | Errors say what to do; filters and form values survive errors and reloads | U-C3, U-D2, U-E5, U-X2 |

---

## 19. Required gates

| # | Gate | Agent | Focus |
|---|---|---|---|
| 1 | ARCHITECTURE | `architecture-reviewer` | §8 layering (components take props; hooks own queries); **one generic viewer, no per-report component** (ADR-FE-129); no new dependency; the scoped edits to HO-CAT-002, HO-ACC-002, HO-ADM-002 files match §13 exactly; no client-side aggregation (ADR-FE-131) |
| 2 | SECURITY | `security-reviewer` | Permission by absence (ADR-FE-137) — no restricted column, report or tile inferable from the DOM; a direct URL still renders the server's `403`; export blobs and object URLs revoked, nothing persisted; report cells rendered as text only; no report data in `localStorage` or the query string beyond filters |
| 3 | CODE_REVIEW | `code-reviewer` | `formatCell` the single rendering path; money never through `Number`; time only through `lib/format` with the payload's zone; component size; polling never resets state |
| 4 | TEST | `test-engineer` | `TC-HO-RPT-002.md` from §15/§16 first; mock and real runs recorded separately; U-C6 and E-X2 actually measure horizontal overflow; U-X3 is a real source scan |
| 5 | ACCEPTANCE | `acceptance-reviewer` | Every §18 row; the landing-route change (ADR-FE-130) recorded as a product decision; drill-down links omitted for areas whose frontends do not exist yet (OQ-4) listed |

---

## 20. BLOCKED conditions — stop and report

**20.1 A prerequisite is missing** (Node, HO-CAT-002 scaffold, HO-ACC-002, HO-ADM-002, HO-NTF-002). Do not build
a parallel shell, branch picker, session guard or error mapping.

**20.2 A screen seems to need a figure the API does not send** — a total, a margin, a percentage, a currency
conversion, a next run time or a default range. **Stop and ask HO-RPT-001 for the field.** Computing it here
contradicts ADR-FE-131 and will eventually disagree with the export and the scheduled email.

**20.3 A report seems to need its own bespoke screen.** Report the shape the catalogue cannot express (a matrix,
a nested group) so HO-RPT-001 can extend the definition. Do not add a per-report component (ADR-FE-129).

**20.4 The real API disagrees with HO-RPT-001 §12.4.** Report it; do not shim, and do not rename a field "to
taste" in `types.ts`.

**20.5 A screen needs an endpoint HO-RPT-001 does not specify.** Report it; do not invent a mock field and build
against it.

**20.6 A new dependency seems necessary** (§10) — a data grid, pivot table, CSV or PDF generator, spreadsheet
exporter or date picker. Raise it; the table is in-repo and the exports are the server's.

**20.7 The landing-route change is refused** (OQ-1). Then `/staff` is still the dashboard, reached from the nav,
and `app/page.tsx` stays as it is. Report the decision; do not leave the redirect half-changed.
