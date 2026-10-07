# Reporting — UX

The clickable design for backlog §16 *Reporting*, built against
[`HO-RPT-002`](../../handover/HO-RPT-002.md), the frontend handover. Fifth area, after
[Cat Profiles](../cat-profile/README.md), [Accounts & Identity](../accounts-identity/README.md),
[Administration](../administration/README.md) and [Notifications](../notifications/README.md).

## Where to review it

Review locally: http://127.0.0.1:8080/reporting/index.html (served from docs/design)

Nineteen artboards in three rows, matching HO-RPT-002 §3.2's screen table. There is no hosted canvas
for this area yet.

## Where the source is

The `.dc.html` files in this folder are the **source of truth**. Each one loads `./support.js`, the
local reimplementation of the canvas runtime copied unchanged from `../_shared/`. The runtime adds
the shared staff sidebar and header, so a file shows the full app shell only when served, not when
read as source.

## The screens

Three rows, nineteen artboards. HO-RPT-002 has only three routes, but the viewer serves all 27
reports and has many acceptance-critical states: filter errors, empty results, exports in flight or
refused, restricted columns, the 360px card list. Each of those states gets its own artboard or
panel.

| Row | File | Screen | Shows |
|---|---|---|---|
| Home dashboard — US-RPT-05 | `DashboardReception.dc.html` | Home (reception) | ⭐ today at the active branch, tiles in the server's order, "Updated 09:42 (Asia/Dhaka)", and no link on a tile the server sent without a drill-down |
| | `DashboardAdmin.dc.html` | Home (administrator) | Money tiles; payments by the four v1 methods; a value-at-risk figure written as "— Not recorded", never ৳0 |
| | `DashboardNurse.dc.html` | Home (nurse) | Clinic tiles only. No money, stock or sales tile and nothing marking where one would be (ADR-FE-137) |
| | `DashboardStates.dc.html` | Dashboard states | A null figure; Short / Over / Breached as word + icon; refresh with a dialog left open; tab hidden; loading; empty payload |
| | `DashboardMobile.dc.html` | Home at 360px | One column of tiles (UI-1) |
| Catalogue & viewer — US-RPT-01…04 | `ReportCatalogue.dc.html` | Report catalogue | 27 reports in seven plain-language groups, "Covers up to N days", search |
| | `ReportViewer.dc.html` | Report viewer | ⭐ the one generic viewer (daily schedule): filter bar, server notes, `<table>` with caption, sort buttons with `aria-sort`, `<tfoot>` Total, paging, CSV / PDF |
| | `ReportRevenueChart.dc.html` | Revenue by business line | ⭐ the optional chart over the same server numbers, the hidden data table, the margin coverage notes, an 11-column table with a sticky first column |
| | `ReportRestrictedColumns.dc.html` | Two roles, one report | Shop manager (4 columns) and accountant (8): restricted columns simply absent |
| | `ReportFilterBar.dc.html` | Filters & parameters | Defaults seeded by the server; one control per parameter type |
| | `ReportFilterErrors.dc.html` | Filter errors | Range too long, dates missing, end before start, branch not allowed, the timeout banner; exports disabled with the reason |
| | `ReportEmptyLoading.dc.html` | Empty & loading | "No appointments on Tue 22 Sep 2026 at Banani" with two next steps; skeleton under real headers |
| | `ReportExportStates.dc.html` | Export states | Ready, Preparing… (busy, announced), downloaded, PDF too many rows, timed out, CSV too many rows |
| | `ReportMobile.dc.html` | Report at 360px | An 11-column report as labelled `<dl>` cards; no sideways scroll |
| | `ReportPermissionDenied.dc.html` | No access / not found | `PermissionDeniedState` for a nurse and for a shop manager on schedules; unknown report |
| Report schedules — US-RPT-06 | `ScheduleList.dc.html` | Schedule list | Active / Paused / Cancelled as icon + word, zone-labelled times, next run from the server |
| | `ScheduleForm.dc.html` | New schedule | ⭐ prefilled from a report; recipients with one unselectable (no email); the server preview with next three runs, covered period, quiet-hours hold; a refused recipient |
| | `ScheduleDetail.dc.html` | Schedule & run history | Generated / Failed / Purged in words, "Too large to attach — link sent", per-recipient delivery, link to the notification log |
| | `ScheduleConfirmDialogs.dc.html` | Run now / Pause / Cancel | The three UI-3 confirmations and the "Generated — 412 rows" result |

## The design system

Built on the PurrCare design system with no new component. This area uses the existing pieces:
`Table` for the viewer, schedule list and run history; `Field` and `<select>` for every filter and
form control; `Badge` for schedule and run status; `Banner` for page-level errors and export
refusals; `Dialog` for the three schedule confirmations. There are two local patterns:

- **Tile.** A card with a title, one to four labelled figures, an optional note and an optional
  **View report →**. It is the Notifications summary tile, extended to allow more than one figure.
- **Chart.** Inline SVG with a `viewBox` and `width="100%"`, plus a visually hidden `<table>` with
  the same points. This is the same pattern as Cat Profiles' weight chart. Series are told apart
  by a letter under each bar and by fill pattern (solid, hatched, dotted), not only by colour.

As with the earlier areas, token values are typed into each artboard by hand. The design system is
not installed live.

## Rules the design encodes

1. **The page computes no figure.** Every total, percentage, margin, coverage, default date range
   and next-run time in these artboards is drawn as if the server sent it. The filter-error board
   says outright that the page doesn't count days itself (ADR-FE-131, ADR-FE-136).
2. **Permission is shown by absence.** `DashboardNurse` and `ReportRestrictedColumns` show nothing
   where withheld data would be: no lock, no greyed tile, no blank column (ADR-FE-137).
   `ReportPermissionDenied` shows the real gate.
3. **Missing is written out, never zero.** "— Not recorded" for a null value, "Not available" for
   a margin with no cost, and "No appointments on … at …" for an empty result.
4. **Status is never colour alone.** Short / Over / Breached, Active / Paused / Cancelled and
   Generated / Failed / Purged each carry a word and an icon. Chart series carry a letter and a
   pattern.
5. **Every date and time names its zone.** Filter labels, table captions, dashboard headers,
   schedule times and run history all say Asia/Dhaka.
6. **Charts are companions.** The chart is off by default, repeats the table's numbers, and has a
   screen-reader table (ADR-FE-134).
7. **A failed export stays on the page.** Refusals appear as banners beside the buttons, in the
   handover's words (ADR-FE-132).
8. **Confirmations state the consequence.** Run now, Pause and Cancel each say what will happen and
   to how many people (UI-3).
9. **No payment gateway.** Payment figures cover cash on delivery, cash in store, card in store and
   bank transfer only.

## Known gaps, deliberately visible

- **No live interaction.** Every state is its own artboard or side-by-side panel.
- **No hosted canvas.** Review locally at the URL above.
- **The Reports nav entry is not hidden for the nurse** in these mockups. The shared sidebar shows
  every entry; `DashboardNurse` says so in a note.
- **Most catalogue cards link to the daily-schedule viewer**, because the viewer is one generic
  screen. Only four reports have their own artboards.
- **Figures, names and report limits are illustrative.** HO-RPT-001 defines only some maximum ranges
  (31, 365, 731 days). The other "Covers up to" values are placeholders.
- **Both branches are in Asia/Dhaka.** The handover's two-time-zone fixture is not drawn.
- **No date-picker widget** (HO-RPT-002 §10 forbids one). Dates are plain `yyyy-MM-dd` fields.
