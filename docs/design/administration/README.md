# Administration — UX

The clickable design for backlog §14 *Administration and Configuration*, built against
[`HO-ADM-002`](../../handover/HO-ADM-002.md), the frontend handover. Third area, after
[Cat Profiles](../cat-profile/README.md) and [Accounts & Identity](../accounts-identity/README.md).

## Where to review it

**Clickable canvas:** <https://claude.ai/artifact/7NydGQg2myb7CroX3ykLdX>

That is the version to look at — twenty-two artboards, six rows, matching HO-ADM-002 §3.2's screen
table. It is private to the owner; others need it shared from the page's Share menu.

If the link is ever lost: `/artifacts` in the Claude Code terminal lists artifacts you own (`o`
opens, `c` copies the link), or browse <https://claude.ai/code/artifacts>.

## Where the source is

The `.dc.html` files in this folder are the **source of truth**, committed so the design survives
independently of the hosted link. They do not open standalone in a browser — each one loads
`./support.js` from the canvas runtime, which is not in this repo. Read them as source; view them
on the canvas.

## The screens

Six rows, twenty-two artboards — the largest of the three areas designed so far, because this
handover's admin surface (branches, rosters, pricing, content, audit) is genuinely the biggest.

| Row | File | Screen | Shows |
|---|---|---|---|
| Branches — US-ADM-01 | `BranchList.dc.html` | Branch list | The admin section shell (sidebar nav, active-branch picker) established here and reused everywhere below |
| | `BranchDetails.dc.html` | Branch → Details | The `Tabs` component in its route-based form: Details · Opening hours · Closures · Rooms |
| | `WeeklyHoursEditor.dc.html` | Branch → Opening hours | ⭐ seven day rows, "Closed" written out for a day with none, "Copy Monday to weekdays" |
| | `Closures.dc.html` | Branch → Closures | Upcoming (cancellable) vs past (read-only), the add form beside it |
| | `Rooms.dc.html` | Branch → Rooms | Name, kind, active — deliberately plain, nothing more is specified |
| | `BranchPickerAndAssignment.dc.html` | Two supporting pieces | The header's active-branch picker (hidden with one branch) and the staff-account Branches panel with its primary radio |
| Services, prices & tax — US-ADM-04 | `ServiceList.dc.html` | Service catalogue | The "Not priced" badge — such a service cannot be booked |
| | `ServiceForm.dc.html` | Service editor | Qualified roles (≥1 required), required room kind, tax class |
| | `EffectiveDatedHistory.dc.html` | Price history | ⭐ Scheduled / Current / Past, no edit control anywhere, the schedule-change dialog with its exact confirmation sentence |
| | `TaxClasses.dc.html` | Tax classes | The same `EffectiveDatedHistory` pattern applied to a rate instead of a price |
| Rosters & leave — US-ADM-03 | `RosterWeekView.dc.html` | Roster week view | ⭐ a real `<table>`, shifts/leave/exceptions/"Outside opening hours" all in text, **and** the below-768px day list beside it — never a scrolling grid |
| | `RosterPatternEditor.dc.html` | Roster pattern | Current pattern vs a scheduled next one with its effective date |
| | `MyLeave.dc.html` | My leave | Every staff member's own request/list/cancel |
| | `LeaveApprovals.dc.html` | Leave approvals | Admin-only; the sole-administrator self-approval case shown as a note, not a missing button |
| Settings — US-ADM-05 | `SettingsForm.dc.html` | Settings | Global value beside a branch override, "Use global value" to clear one, a non-overridable setting read-only with a reason |
| Content, policies & acceptance — US-ADM-07 | `ContentList.dc.html` | Content list | Filterable by kind/status |
| | `ContentEditor.dc.html` | Content editor | ⭐ the markup help panel and a live preview using the **same** renderer the public page uses |
| | `PolicyList.dc.html` | Policies | "Publish new version" dialog, its confirmation naming that customers will be asked to accept |
| | `PublicPageAndFaqs.dc.html` | Public page & FAQs | `/pages/[slug]` and `/faqs`, no identity, the same `LimitedMarkup` rendering as the editor's preview |
| | `PolicyAcceptancePrompt.dc.html` | Policy acceptance | ⭐ blocking modal in the portal — no dismiss, sign-out is the only other path, nothing pre-ticked |
| Audit log & system — US-ADM-06, 08 | `AuditLog.dc.html` | Audit log | Filters incl. the required date range, a row expanded into a key-by-key diff, CSV export |
| | `MaintenanceSwitch.dc.html` | System | The one-switch page that still confirms (UI-3), because its consequence is product-wide |

## The design system

Built on the [PurrCare design system](https://claude.ai/artifact/Y2WzXo2hnfc7a9ac7GHNvN), extended
in this session for exactly what this area needed and nothing more:

- **`Table`** — every list screen here uses it: branches, services, tax classes, leave approvals,
  the audit log.
- **`Tabs`** — the branch detail tabs, matching the account tabs Accounts & Identity established.
- **`Toggle`** — online-bookable, settings switches, maintenance mode.
- **`Dialog`** — the schedule-price dialog, publish-new-version, the maintenance confirmation, the
  branch-removal confirmation. Its README also records the system's one deliberate exception to
  "borders, not shadows": a new `shadow-modal` token, used nowhere else.
- **`Badge`** extended with the Scheduled/Current/Past vocabulary for effective-dated records.
- **`Field`** extended with a `<select>` example (time zone pickers, filters).

As with Accounts & Identity, token values are hand-transcribed into each artboard rather than
installed live on the canvas — the same deliberate scope cut, for the same reason.

## Rules the design encodes

1. **Effective-dated records are never edited, only scheduled.** `EffectiveDatedHistory.dc.html`
   and `TaxClasses.dc.html` show no edit or delete control anywhere in the Current/Scheduled/Past
   list — the only action is "Schedule a new …" (ADR-FE-018).
2. **The roster never scrolls sideways at the 360px floor.** `RosterWeekView.dc.html` shows the
   desktop grid and the day-by-day list side by side specifically so the re-layout is visible as a
   deliberate choice, not an afterthought (UI-1).
3. **Time always names its zone.** Every time input and every roster/audit timestamp is labelled
   or captioned with the branch's IANA zone — never left to the reader's own clock (ADR-FE-014).
4. **What the editor sees is what the public gets.** `ContentEditor.dc.html`'s live preview and
   `PublicPageAndFaqs.dc.html`'s rendered page use the same markup rules, so there's no gap between
   "looks right in the editor" and "is right on the site" (ADR-FE-017).
5. **A blocking dialog has one way out, stated plainly.** `PolicyAcceptancePrompt.dc.html` offers
   Accept or sign-out and nothing else — no dismiss, no pre-ticked box (OQ-3).
6. **Confirmations state the consequence, every time** — scheduling a price, publishing a policy,
   turning on maintenance, removing a role. None of them ask "Are you sure?" alone.
7. **A permission is enforced by the server, shown by the UI.** `AdminNav`'s role filtering is
   presentation only; the design still shows `PermissionDeniedState` as the real gate.

## Known gaps, deliberately visible

- **No live interaction.** As with Accounts & Identity, every state is a separate artboard, not a
  wired-up toggle — the screen count already covers the states a click would reveal.
- **The design system isn't installed live on the canvas** — see above.
- **The capability preview, roster data and audit entries are illustrative**, not real API
  responses — no backend exists yet for this area to call.
- **No calendar/date-picker widget is shown as a real component** because HO-ADM-002 §10
  explicitly forbids one; every date field here is a plain text input standing in for whatever
  native or in-repo control the build settles on.
