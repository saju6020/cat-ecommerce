# Cross-cutting & Technical — UX

The design for backlog §17 *Cross-cutting and Technical*, built against
[`HO-XCT-002`](../../handover/HO-XCT-002.md), the frontend handover. It follows
[Cat Profiles](../cat-profile/README.md), [Accounts & Identity](../accounts-identity/README.md),
[Administration](../administration/README.md) and [Notifications](../notifications/README.md).

## Where to review it

Review locally: http://127.0.0.1:8080/cross-cutting/index.html (served from docs/design)

There is no hosted canvas for this area. `index.html` lists all twenty-four artboards in four
rows, matching HO-XCT-002 §3.2's screen table.

## Where the source is

The `.dc.html` files in this folder are the **source of truth**. Each loads `./support.js`, the
local runtime copied from `../_shared/`. It gives staff screens the shared sidebar and header,
and lets the fixed-size artboards resize to the window.

## The screens

Four rows, twenty-four artboards. That is more than any earlier area, because this handover covers
four stories and ten routes. Three of its seven stories (US-XCT-03, 04 and 07) are standards
the slice only *verifies*, and the lab budgets and the 360 px sweep are tests, not screens. None
of those has an artboard.

| Row | File | Screen | Shows |
|---|---|---|---|
| Offline front desk — US-XCT-01 | `FrontDeskOffline.dc.html` | Front desk, offline | ⭐ the `OfflineBanner` over the last-loaded list, every action disabled with "Offline — can't save", and a check-in form that keeps what was typed and shows the `OfflineError` message |
| | `OfflineBannerStates.dc.html` | Banner states | Offline with and without a saved copy, stale over an hour, degraded ("server is having trouble", not offline), back online, and the return from the offline list |
| | `OfflineBoard.dc.html` | Offline list | ⭐ both lists from the device copy, read-only, **no button in the page**; high-risk badge plus handling note; `tel:` links |
| | `OfflineBoardRefusals.dc.html` | When it won't show | No copy, expired copy, another person's copy — each explained, no data rendered — plus the `NotPermitted` / `NotAvailable` section wording |
| | `OfflineBoardLocked.dc.html` | Locked | The idle lock (ADR-FE-144) and why it can't be lifted offline |
| | `OfflineBoardMobile360.dc.html` | Offline list at 360 px | The list as cards, with boarding "not available yet" (true until HO-BRD-001) |
| | `OfflineDeviceSettings.dc.html` | Device opt-in | The toggle, what is stored and for how long, last saved, "Clear this device now", the automatic clears |
| Retention & erasure — US-XCT-06 | `RetentionSchedule.dc.html` | Schedule | ⭐ `ReviewEligiblePanel`, `RunRetentionNow`, and all eleven record classes with period and end action; Audit expanded to its tables |
| | `RetentionRunNow.dc.html` | Run now | The confirmation listing what the run will and will not do, next to the `409` state |
| | `RetentionRuns.dc.html` | Runs | Paged, filtered by status |
| | `RetentionRunDetail.dc.html` | Run detail | Per-pass counts; the error code under a plain-language gloss |
| | `ErasureLedger.dc.html` | Erasure requests | Account id only, scope summaries such as "2 of 3 complete, 1 held until 12 Mar 2032" |
| | `ErasureLedgerDetail.dc.html` | Ledger detail | Scopes with held-until and basis; the paragraph on held data for a compliance officer |
| | `ReapplyAfterRestore.dc.html` | Re-apply | The form under its confirmation ("re-anonymises accounts that came back with the restore") |
| | `ReapplyStates.dc.html` | Re-apply states | Over 500 and malformed ids blocked before sending, beside the four-count result |
| | `ErasePersonalData.dc.html` | Erase panel | ⭐ on HO-ACC-002's staff customer page: erased vs kept-and-why, channel, identity check, reason |
| | `EraseConfirm.dc.html` | Erase confirm | Names the customer **once** (OQ-6); "This cannot be undone." |
| | `EraseStates.dc.html` | Erased / merged | "Personal data was erased on …" with a ledger link; the `CustomerRecordMerged` message |
| Backup & restore — US-XCT-05 | `BackupStatus.dc.html` | Backup & restore | ⭐ an overdue restore test and a stale backup, both above the fold (E-D1); objectives beside achieved RPO/RTO; history |
| | `RecordRestoreTest.dc.html` | Record a test | Paste the drill JSON, fields filled and editable; **no RPO, RTO or outcome input** |
| | `RecordRestoreTestParseError.dc.html` | Unreadable paste | The parse error that keeps the pasted text |
| | `RestoreTestDetail.dc.html` | Test detail | The server's computed verdict, the checks, the drill output in a `<pre>` |
| | `OverdueBannerOnSettings.dc.html` | Overdue banner | The per-session admin banner, shown on HO-ADM-002's Settings screen with the nine new keys |
| Performance — US-XCT-02 | `Performance.dc.html` | Storefront performance | p75 per route against the budget, chart plus table, Pass / Fail / Not enough data |

## The design system

Built on the [PurrCare design system](https://claude.ai/artifact/Y2WzXo2hnfc7a9ac7GHNvN), which
already had what this area needed: `Table`, `Tabs`, `Dialog`, `Toggle`, `Banner`, `Badge` and
`Field`. It adds two things:

- **An "offline" ink** (`--offline-ink`, a near-black) used only by the offline banner and the
  "Offline · read-only" chip. Offline is not a warning or an error: the staff member did nothing
  wrong. A neutral dark bar keeps it from reading as either one. The words still say everything.
- **A single-series bar chart** on the performance page. It uses one hue, a dashed budget line
  labelled in text, and a faded bar for "not enough data". The verdict is carried by the table,
  not by colour.

As in the earlier areas, token values are hand-transcribed into each artboard rather than
installed live.

## Rules the design encodes

1. **Offline is read-only, and says so before anything fails.** Every write control is disabled
   with the visible reason "Offline — can't save". Nothing is queued. Typed input stays on the
   page (ADR-FE-143, NFR-USA-5).
2. **The offline list has no controls.** `OfflineBoard.dc.html` has no action button at all.
   The only buttons in the offline group re-check the connection or clear the device.
3. **A saved copy that isn't trustworthy is not shown.** An expired copy, another person's copy,
   or no copy at all each gets its own explanation instead of a partial list (U-A11).
4. **Nothing clinical is ever deleted, and the screens say so.** The schedule, the review panel,
   the run-now confirmation and the run detail each state that clinical, financial, regulatory
   and audit records are only counted (US-XCT-06 AC2).
5. **Erasure names what goes and what stays.** It says what is kept, with its period and the
   reason. The confirmation names the customer once. Afterwards the ledger shows no name anywhere
   (UI-3, OQ-6, U-B4).
6. **The server decides the restore-test outcome.** The form has no RPO, RTO or pass/fail field.
   The detail page labels the verdict "Worked out by PurrCare" (ADR-313).
7. **Status is never colour alone.** Every pill, verdict, stale state and banner has an icon and
   a word, and every time names Asia/Dhaka.

## Known gaps, deliberately visible

- **No live interaction.** As in the earlier areas, each state is its own artboard.
- **The one-time opt-in prompt for Reception and Caretaker** (OQ-1's default) is not drawn. The
  opt-in lives on `OfflineDeviceSettings.dc.html`, and the prompt's wording would match it.
- **`PermissionDeniedState` for the new admin screens (U-B13)** is not redrawn. It is HO-ACC-002's
  component, unchanged.
- **No Caretaker identity exists yet.** The 360 px offline list shows the boarding section as
  "not available", which is what it will show until HO-BRD-001 exists.
- **Performance data is illustrative.** The storefront (HO-SHP-002) isn't built, so no route has
  real samples. The chart's hover tooltip is described, not drawn.
- **All data is illustrative.** None of it comes from a real API response.
