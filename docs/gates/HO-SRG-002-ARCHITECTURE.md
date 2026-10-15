GATE: ARCHITECTURE
HANDOVER: HO-SRG-002
COMMITS: b95263fd 14098a39 a49cb937 9bbf3a17 1626a126 e8de5bf1 6ea95dc8 cfcd13ac df419408 (range 5a187b6b..df419408, branch ho/SRG-002)
DATE: 2026-10-14
STATUS: FAIL

Frontend-only slice: 123 changed files, no `src/**`, `tests/**`, `.ai/**`, `.claude/**`, `package.json`, `tsconfig.json`, `next.config.ts` or `eslint.config.mjs` change. The backend DDD/CQRS checklist does not apply. This gate judged against HO-SRG-002 §8, §13, §14 and §20, and the §19 ARCHITECTURE focus. The builder's deviations were read from §1 "Amended (build)" 2026-10-14.

The one failing finding is mechanical. Six other-area hook files were reformatted with Prettier, outside §13 and with no record. Reverting them closes it. Every structural deviation the caller asked about is below HIGH.

## FINDINGS

- [HIGH] H1 web/lib/hooks/billing/useActiveBranchZone.ts, web/lib/hooks/billing/useBillingMutations.ts, web/lib/hooks/billing/useBillingQueries.ts, web/lib/hooks/useOrderingAdmin.ts, web/lib/hooks/usePos.ts, web/lib/hooks/useStaffReturns.ts (commit a49cb937). The same kind of change also appears in the non-append hunks of web/lib/api/errors.ts (Pharmacy and Ordering entries).
  - **Rule.** §13 allows `web/lib/hooks/**` for **new hooks only**, and `errors.ts` for **append** only. These files belong to HO-BIL-002 and HO-ORD-002, and nothing in §13, §1 Amended or another handover authorises the edit. The gate rule is that any file outside allowed scope is an automatic FAIL.
  - **What the code does.** Prettier line-wrapping only. There is no behaviour change, which was confirmed by reading every hunk. On master the six files fail `prettier --check`, and that is presumably why they were touched. `npm run lint` (eslint) does not run Prettier, so the edit was not needed for §17.
  - **Precedent.** HO-ORD-002 ARCHITECTURE B5 ruled a formatting-only edit to another handover's file a finding (a BLOCKER there, because that file was §14-named). These six files are not named in §14, so this is HIGH, not BLOCKER.
  - **To close.** `git checkout 5a187b6b -- <the six files>`, and revert the reformatted Pharmacy/Ordering hunks in `errors.ts` while keeping the Surgery append. If the owner wants master Prettier-clean, the integrator can do it as a separate commit on master. A combined re-check (or a test-only re-run) is enough afterwards.

- [MEDIUM] M1 web/components/anaesthesia/DrugBatchChooser.tsx. **Ruling on §20.2: not a BLOCKER.**
  - **Why.** §20.2 and §14 stop the builder from forking or copying a component another handover owns. HO-IMP-002 never specified a `BatchPicker` (its only "batch picker" is a row inside `TransferForm`, HO-IMP-002 §11), so there is nothing to copy. The chooser imports IMP's public `useBatches` hook rather than duplicating IMP's API code. It lives in this slice's own `components/anaesthesia/`. It takes the server's `status`/`isExpired` flags as given and does no usability judgement of its own. It collects no credential, which was the safety half of §20.2.
  - **What it is.** A deliberate departure from a written stop condition. It is recorded in §1 Amended (3) "for the owner to confirm". ACCEPTANCE should see that confirmation, or the owner's merge approval naming it.
  - **Should it be recorded against HO-IMP-002?** Yes, as an IMP backlog row (proposed below), so that the assumed `BatchPicker` is either built there or formally dropped. When one exists, this chooser is replaced.

- [MEDIUM] M2 web/components/anaesthesia/DrugBatchChooser.tsx:19
  - **Assumed contract.** §8.5 assumed `locationId="clinical"`.
  - **What exists.** `BatchListParams` has no location filter, and `BatchSummary.onHand` is the batch's total across locations. The list can therefore offer a batch that is held only in a warehouse, and "No usable batch is held **at the clinic**" overstates what the data says.
  - **Effect.** The server still refuses an unusable draw (`BatchNotUsable` / shortfall recording), so this is not a correctness breach of the architecture. Needs a location-scoped batch query from Inventory.

- [MEDIUM] M3 web/components/surgery/RescheduleDialog.tsx:60. The service is found by `procedure.name`, a contract gap recorded in Amended (4a).
  - **Ruling: not HIGH.** `RescheduleSurgeryRequest` carries no `serviceId`, so the server re-checks every clash against the surgery's own service. The name lookup only selects which availability is displayed.
  - **Why it is fragile.** `ClinicService` names are not unique (only `Code` is, `ClinicServiceConfiguration.cs:18`), and the detail carries a name snapshot. A rename makes the lookup miss, the availability query is disabled, and the dialog offers no start. It fails closed and is silent. A duplicate name shows another service's availability, which the server then corrects. Fix on the backend: put `serviceId` on `GET /api/staff/surgeries/{id}`.

- [MEDIUM] M4 web/lib/validation/surgery.ts:130 (`parseMissingReportFields`), used at web/components/surgical-report/SurgicalReportEditor.tsx:138-142. `ReportIncomplete` parts are parsed out of the English message from `SurgicalReportAggregate.cs:85`. This is recorded in Amended (4h).
  - **Ruling: not HIGH.** It degrades gracefully. Unknown words are ignored, and if nothing matches, the server's own message is still shown (`SurgicalReportEditor.tsx:206`).
  - **Why it is fragile.** It couples the UI to message wording, and it is a pattern that should not be copied. Fix on the backend: one violation per missing field with its `propertyName`, or a `missingFields` array.

- [MEDIUM] M5 web/components/surgery/StartPanel.tsx:45-51, SurgeryHeader.tsx:36-38, PreOpChecklist.tsx:65-68. `allowedTransitions` is status-only, so the controls also read hard-coded role lists that mirror HO-SRG-001 §12.3. This is recorded in Amended (4f).
  - **Effect.** The roles are used for presentation only, and the server remains the gate (ADR-FE-072 holds). The role lists will drift if §12.3 changes. Fix on the backend: make `allowedTransitions`/`allowedActions` caller-aware, as on other areas (ADR-FE-093 parity).

- [MEDIUM] M6 (no file; process). The real-API E-series was not run.
  - **Why.** There is no surgery seeder, and `tests/**` is §14-forbidden to this slice.
  - **Ruling.** This is not an architecture finding. It is for TEST and ACCEPTANCE to rule on §16's "both runs recorded". A seeder belongs to the backend/integrator, not here.

- [LOW] L1 web/lib/hooks/useSurgeryBookingData.ts:39. Theatres are the branch's active rooms of kind `Theatre` from `GET /api/admin/branches/{id}/rooms`.
  - **Ruling: not fragile.** This is exactly the server's own definition of a theatre (`SurgerySlotFinder.cs:49`: `IsActive && Kind == RoomKind.Theatre`). The server re-validates (`TheatreNotAvailable`), and HO-APT-002 used the same endpoint for the front desk. A Surgery theatres endpoint would only be a nicety.

- [LOW] L2 Handover text, §8.5 / §11.3 / §11.4 / §13. These deviations are accepted, with authority or precedent, and recorded in §1 Amended (3) and (5). §13/§14 should be amended to match:
  - **Consent.** It links to HO-EMR-002's `/staff/consents/new?catId&appointmentId&kind` page. The page exists, reads exactly those params, and is consumed rather than copied. A link to the owning area's page is consumption, not a substitute, so §20.2 is not engaged.
  - **Witness.** HO-PHM-002's `WitnessDialog` is reused unmodified (no `components/pharmacy/**` edit). The credential is passed as an argument straight into the request body (`DrugEntryDialog.tsx:111-143`, `CorrectEntryDialog.tsx:97-122`) and is never put into this slice's state. `useLogDrug`/`useCorrectEntry` are plain callbacks, not TanStack mutations, so no `variables` cache is kept (`useAnaesthesiaLog.ts:22-70`). This matches HO-SRG-001 §1 Amended (16), which wins over §8.5 under §20.4. SECURITY should confirm U-C4 inside `WitnessDialog`.
  - **Nav.** It lives in `web/nav/areas/surgery.ts` (this area's own registry stub). Neither layout is edited, matching the HO-PHM-002, HO-IMP-002, HO-BIL-002 and HO-ORD-002 precedents.
  - **Front desk.** The Recovery tab is in `web/app/(staff)/staff/front-desk/FrontDeskView.tsx`, where HO-APT-002 put the tabs. The edit is the tab only.
  - **`web/lib/api/staff-appointments.ts`.** This is an HO-APT-002 file beyond §13, so it is literally §14. It is accepted because §11.10 (which §13 authorises in `CalendarGrid.tsx`) cannot be typed without these fields. The two fields are optional and additive, and `surgeryId`/`assistantId` are returned by the owning area's contract (HO-APT-001 §1 Amended, `StaffAppointmentQueryHandlers.cs:64-75`).
  - **`web/components/calendar/CalendarGrid.tsx`.** The edit is §11.10 only: the surgery label, the assistant column, the link to the surgery page, and no drag or *Move…* for surgeries. `CalendarAgenda` is untouched.

- [LOW] L3 web/tests/unit/surgery-format-and-validation.test.ts, web/tests/e2e/surgery-helpers.ts, web/tests/e2e/surgery-routes.ts. These are new surgery-only files outside §13's test globs.
  - The two e2e helpers are recorded in Amended (5), accepted by precedent (Pharmacy backlog 48).
  - The unit test is not named in Amended. It is LOW per the HO-ORD-002 `tests/unit/lib/ordering-*` ruling. Amend §13.

- [LOW] L4 web/mocks/handlers/surgeries/support.ts:137-160. The consent mock writes into the EMR mock's database (`getEmrDb().consents.push`).
  - **Why it is only LOW.** It is recorded in Amended (6), and the other cross-area look-ups fall through (`undefined`) for ids this mock does not own. This is unlike HO-ORD-002 B4, which rewrote `GET /api/cats` for every area.
  - **Risk.** It couples this mock to the EMR mock's store shape. It is mock-only.

### Checked, no finding
- **ADR-FE-069..078.** These are the handover's own reserved range (§9). No other branch or master uses them as headings: `git grep` over master and every `ho/*` branch found them only on `ho/SRG-002`, and no other branch references them at all. RPT-002 (FE-129..138) and XCT-002 (FE-139..148) do not overlap. 077 and 078 are inside the owned range, although §13's comment says "069…076". That is a handover wording nit, not a collision.
- **Server authority (§19 focus; §20.3).**
  - Day-list checklist text is built from the server's `outstandingItems` (`lib/format/surgery.ts:140-147`).
  - Start, override, reschedule and cancel render from `allowedTransitions`/`startBlockers`.
  - Clashes and busy intervals are the server's.
  - No client-side clash, checklist or consent logic was found.
- **Append-only log.** No edit or delete control exists. Corrections are linked entries (ADR-FE-070).
- **Layering.** No `@/components` import under `web/lib/**`. No `@/mocks` import from `app/`, `components/` or `lib/`. Stand-ins (consent, witness fixtures) live only in `mocks/handlers/surgeries/**` and tests (§12).
- **Dependencies.** No new package. Every added import is an existing dependency.
- **§8.5 sources.** No file under `components/pharmacy/**`, `components/inventory/**` or the EMR consent page was changed.
- **`docs/handover/HO-SRG-002.md`.** The only change is the §1 "Amended (build)" row, the standard build record. Accepted by universal precedent despite §14's `docs/**` line.
- **`web/docs/decisions.md`.** Append-only (+86/-0). **`web/mocks/handlers/index.ts`.** Append-only (+6/-0).
- **Pointer for CODE_REVIEW.** Several components exceed the ~150-line rule: `CorrectEntryDialog.tsx` 462, `DrugEntryDialog.tsx` 379, `SurgicalReportEditor.tsx` 302, `SurgeryCampaignForm.tsx` 276, and others. This is §11's size rule, not architecture.

## Proposed backlog rows

These are proposals only; this gate did not edit any backlog file.

**`docs/backlog/SRG.md`** (highest existing row is 49; continue at 50):

| # | Sev | Where | Item | Source |
|---|---|---|---|---|
| 50 | MEDIUM | web/components/anaesthesia/DrugBatchChooser.tsx | Local batch chooser in place of the §8.5 `BatchPicker`, which HO-IMP-002 never specified (a §20.2 deviation, Amended (3)). The owner must confirm it. Replace it when Inventory ships a picker (IMP row 63). | ARCHITECTURE M1 |
| 51 | MEDIUM | DrugBatchChooser.tsx:19 / HO-IMP-001 batch list | The batch list cannot be scoped to the clinic location (no `locationId` on `BatchListParams`; `onHand` is chain-wide). The "held at the clinic" copy overstates this. Needs a location-scoped batch query. | ARCHITECTURE M2 |
| 52 | MEDIUM | HO-SRG-001 `GET /api/staff/surgeries/{id}` | Add `serviceId` to the detail. Reschedule currently matches the service by non-unique name (`RescheduleDialog.tsx:60`) and offers no start after a rename. | ARCHITECTURE M3 |
| 53 | MEDIUM | HO-SRG-001 `SurgicalReportAggregate.Finalise` | Emit `ReportIncomplete` per missing field with `propertyName`, or a `missingFields` array. The FE parses the English message (`lib/validation/surgery.ts:130`). | ARCHITECTURE M4 |
| 54 | MEDIUM | HO-SRG-001 surgery detail | Make `allowedTransitions`/`allowedActions` caller-aware. The FE mirrors the §12.3 roles in `StartPanel`, `SurgeryHeader` and `PreOpChecklist` and will drift. | ARCHITECTURE M5 |
| 55 | MEDIUM | Real-API E-series (§16) | Not run because no surgery seeder exists, and `tests/**` is outside this slice. Needs a backend/integrator seeder, then the E-series re-run against the real API. | ARCHITECTURE M6 |
| 56 | LOW | HO-SRG-002 §8.5 / §11.3 / §13 / §14 | Amend the text to match the build: the EMR consent page link, PHM `WitnessDialog` with in-request credentials, `FormularyPicker`, the nav registry instead of the layouts, `FrontDeskView.tsx`, `staff-appointments.ts` (two optional fields), `tests/unit/surgery-format-and-validation.test.ts`, the e2e helpers, and ADR 069…078 in §13. | ARCHITECTURE L2, L3 |
| 57 | LOW | useSurgeryBookingData.ts:39 | Theatres come from the admin rooms endpoint (kind `Theatre`, active), which mirrors `SurgerySlotFinder`. An optional Surgery theatres endpoint would remove the admin-endpoint dependency. | ARCHITECTURE L1 |
| 58 | LOW | mocks/handlers/surgeries/support.ts:137 | The consent mock writes into the EMR mock's store, which couples it to that shape. | ARCHITECTURE L4 |

**`docs/backlog/IMP.md`** (highest existing row is 62; continue at 63):

| # | Sev | Where | Item | Source |
|---|---|---|---|---|
| 63 | MEDIUM | HO-IMP-002 | HO-SRG-002 §8.5 assumed a reusable `BatchPicker` (`skuId`, `locationId`, usable batches only, FEFO). None was specified or built, so HO-SRG-002 wrote `DrugBatchChooser` over `useBatches`. Either build a shared, location-scoped picker or record that none will exist. | HO-SRG-002 ARCHITECTURE M1/M2 |

## SCOPE CHECK
- files in allowed scope: 117/123. That is 110 literally within §13, plus 7 accepted with recorded authority or precedent: `nav/areas/surgery.ts`, `FrontDeskView.tsx`, `lib/api/staff-appointments.ts`, `tests/unit/surgery-format-and-validation.test.ts`, `tests/e2e/surgery-helpers.ts`, `tests/e2e/surgery-routes.ts`, and `docs/handover/HO-SRG-002.md`.
- files outside allowed scope: web/lib/hooks/billing/useActiveBranchZone.ts, web/lib/hooks/billing/useBillingMutations.ts, web/lib/hooks/billing/useBillingQueries.ts, web/lib/hooks/useOrderingAdmin.ts, web/lib/hooks/usePos.ts, web/lib/hooks/useStaffReturns.ts (H1; plus the non-append Prettier hunks inside the in-scope `web/lib/api/errors.ts`)

---

## Combined re-check — 2026-10-14 (architecture, security and code-review findings of the gate fix round)

GATE: ARCHITECTURE (combined re-check: ARCHITECTURE H1, CODE_REVIEW H1 + M1–M5 + L1–L6, SECURITY L1–L2)
HANDOVER: HO-SRG-002
COMMITS: c2e70472 (fix round), 19e9f304 (TC counts); gate records f1293e56, 0832958f, 0e07b9fb. Range checked: 5a187b6b..19e9f304
DATE: 2026-10-14
STATUS: FAIL

Two HIGH findings. (1) The autosave flush itself is correct and its test is real. But a remounted editor reads a stale cached report: on returning to the tab the text is not shown, and the next save gets a 409 from the real API. The user-visible defect behind CODE_REVIEW H1 is still there. (2) The fix round corrupted §1 of the handover: a dollar-backtick sequence in a string replacement duplicated the file's preamble. Everything else the caller asked about checks out, apart from the MEDIUM and LOW items below.

FINDINGS
- [HIGH] R1 web/lib/hooks/useSurgicalReport.ts:31-33, web/lib/hooks/useReportAutosave.ts:38-48, web/components/surgical-report/ReportTab.tsx:51-56. **CODE_REVIEW H1 is only half closed.**
  - **What was fixed.** The unmount flush works (`useReportAutosave.ts:89-97`: clear the timer, then chain `run`). `run` depends only on `surgeryId`, so the cleanup fires on unmount and not on every render. StrictMode's fake unmount finds `timer.current === null` and does nothing.
  - **What is still wrong.** The report query has `staleTime: Infinity`, and no save writes to the cache (`saveReportDraftRequest` is a plain call; there is no `setQueryData` or invalidation). The production QueryClient keeps the default `gcTime` of 5 min (`app/providers.tsx:44-51`). So on a return to the Report tab within 5 min, `ReportTab` mounts the editor from the cached pre-save report (`key={data?.version ?? 'new'}`, `defaultValues` from it, `initialVersion` from it).
    - The vet sees the old text: empty on a first draft.
    - The next keystroke saves with a missing or stale `expectedVersion`. The real handler refuses that with `StaleVersion` 409 (`SurgeryProcedureCommandHandlers.cs:104-105`; `SurgeryControllerBase.cs:24`). The editor shows a conflict and the autosave stops.
    - *Reload* then replaces whatever was typed after the return.
  - This also applies to text the 5 s timer had already saved before the switch.
  - **Probe.** A throwaway component probe, deleted afterwards, toggled `ReportTab` off and on inside one QueryClient with no pause. The findings field came back `""` after a timer save. Two things mask this in the suite:
    - `renderWithClient` uses `gcTime: 0`, so a pause lets the cache empty and the refetch shows the text.
    - The MSW mock accepts a save with no `expectedVersion` against an existing draft (`mocks/handlers/surgeries/reports.ts:86`, `body.expectedVersion !== undefined && …`), which the real handler refuses.
  - **To close.** After each successful save, write the saved version (and fields) into the `['surgeries', id, 'report', branchId]` cache, or invalidate it on editor unmount. Then add a test that remounts the editor in the same QueryClient with a retained cache, against a mock that enforces `expectedVersion` like the handler.
  - **Mutation check (caller item 2).** Reverting the cleanup to `clearTimeout` alone leaves `puts` at 0, so `gatefix.test.tsx:68` fails. Dropping the cleanup entirely leaves a fake timer that never fires after `useRealTimers()`, so it also fails. The second case (`:73-84`) guards against a flush with nothing pending. The test is not vacuous. It just does not cover the remount.
- [HIGH] R2 docs/handover/HO-SRG-002.md:1-38. **The fix round corrupted §1.**
  - **What happened.** The (8) note contains the campaign price regex, which ends in a dollar sign followed by the closing code backtick. Written through a JS `String.replace`, that dollar-backtick pair became "insert everything before the match". As a result:
    - Line 19's "Amended (build)" row ends at `…{1,2})?` and is followed by a second copy of the title, the `## 1. Identity` heading and the whole identity table (lines 21-36).
    - The end of note (8) (`); every path id … catecommerce_test).`) is attached to the second copy's row (line 37).
    - The file now has two `## 1. Identity` headings, and the fix-round record is split across two rows and missing its regex.
  - **Why HIGH.** The handover is the governing document every later gate and ACCEPTANCE reads, and caller item 5 needs §1 Amended to be readable. The precedent for handover edits covers the Amended row only, not a duplicated preamble.
  - **Severity of the fix.** Mechanical. Restore lines 1-18 once, with one complete "Amended (build)" row that includes (8) and the regex. Edit with a literal write, not a dollar-interpreting replace.
- [MEDIUM] R3 web/components/anaesthesia/DrugEntryDialog.tsx:122-138 and web/lib/validation/anaesthesia.ts:24-40. The widened `entryId` fingerprint is wider than the server's replay check.
  - **What the server does.** It treats a reused id as a replay when medicine, batch, quantity drawn and dose match, and returns the first result without comparing route, time, vet, wastage or FEFO reason (`AnaesthesiaCommandHandlers.cs:186-199`). The old four-field fingerprint matched it exactly. So the premise of CODE_REVIEW M2(a), repeated in SRG backlog row 62 ("a retry with another route … got AnaesthesiaEntryIdMismatch and was stuck"), does not hold.
  - **Effect of the widening.** Take a drug entry that committed but whose response was lost. If the vet then edits the time, route, vet, wastage or reason and resends, the dialog now mints a NEW id. That is a second entry, a second stock deduction and a second controlled-drug register line, where before it replayed.
  - **Why not HIGH.** It needs a lost response after a commit, and the old code had the same hole for the four core fields.
  - **Suggested fix.** Keep the fingerprint aligned with the server's replay fields, or have the backend compare the full body. Correct the row 62 text either way.
  - **No issue.** Resetting the key on `AnaesthesiaEntryIdMismatch` is harmless. M2(b), clearing drawn and wasted when the batch's stock unit changes (`:311-318`), is correct.
- [MEDIUM] R4 docs/backlog/SRG.md rows 60 and 69. **Backlog honesty (caller item 6).**
  - Row 60 marks CODE_REVIEW H1 "Resolved", but see R1.
  - Row 69 marks "`stopped` never cleared" "Resolved … `reset(version)` clears it". `reset` (`useReportAutosave.ts:100-105`) has no caller anywhere: the editor and `ReportTab` remount on reload through `key`. The flag is therefore cleared by remounting, as before, and `reset` is dead code.
  - Reword both rows: row 60 open until R1 is closed; row 69 as "cleared by remount; `reset` unused", or wire `reset` up or remove it.
  - The other rows, 50-59, 61, 63-68 and 70-72, match the code. Every ARCH, CODE_REVIEW and SECURITY item has a row.
- [LOW] R5 web/components/surgery/RescheduleDialog.tsx:61. `serviceMissing = !services.isPending && !serviceId` is also true when the services query FAILED. A network error is then reported as "This procedure can no longer be found … renamed or switched off". Gate it on `services.isSuccess`, or show the error state.
- [LOW] R6 web/components/anaesthesia/CorrectEntryDialog.tsx:98-101. A correction pins the time to the original entry's branch-local day and skips the 12 h rule. Correcting an entry logged at 23:50 to its real time of 00:10 lands about 24 h early (00:10 on the earlier day). The server still decides. Note it in ADR-FE-077 or apply the nearest-instant rule around the original entry's time.
- [LOW] R7 docs/backlog/SRG.md row 52 says "row 64" for the reschedule fix. That fix is row 63; row 64 is the campaign prices.
- [LOW] R8 Test fidelity. The `reports.ts:86` mock and the `gcTime: 0` test client are the two reasons R1 passed the suite (see R1). Making the mock enforce `expectedVersion` like the handler is part of closing R1.

VERIFIED (no finding)
1. **Out-of-scope reformatting (ARCH H1) is closed.**
   - `git diff 5a187b6b 19e9f304` is empty for all six hook files (`billing/useActiveBranchZone.ts`, `billing/useBillingMutations.ts`, `billing/useBillingQueries.ts`, `useOrderingAdmin.ts`, `usePos.ts`, `useStaffReturns.ts`).
   - `web/lib/api/errors.ts` differs from master only by one hunk appended after line 1210 (the Surgery block, +138/-0).
   - No other area's file is touched beyond the seven accepted in the first pass (`FrontDeskView.tsx`, `CalendarGrid.tsx`, `staff-appointments.ts`, `nav/areas/surgery.ts`, `mocks/handlers/index.ts` append, `web/docs/decisions.md` append, `IMP.md` +1 row).
2. **The autosave flush on unmount.** Correct as written; see R1 for the remount gap.
3. **The MEDIUM fixes.**
   - Booking: choosing the assistant as surgeon clears `assistantId` in the same `set`. The effect drops a deep-linked `assistantId == surgeonId`, and a `theatreId` not among the active Theatre rooms, but only once the theatres have loaded. All three are tested.
   - Drug entry: see R3. The unit reset is correct.
   - Reschedule with no service: an explicit alert, *Review* disabled, no picker. Tested. See R5.
   - Price regex (digits, optionally a dot and one or two decimals, anchored): it refuses `1e3`, `800.505`, `-5`, `8,00` and `" "`, and accepts `800` and `800.50`. The wire stays `amount: number`, per the contract type.
4. **The SECURITY LOWs and the cheap LOWs.**
   - Encoding: every path segment in the six surgery API modules is `encodeURIComponent`-ed, either directly or through the `base(surgeryId)` helpers. That covers `kind`, `entryId`, `followUpId`, campaign ids and protocol `serviceId`, and it is tested.
   - Toast: it follows the controller. 204 means the override was recorded; 200 `{ consentWasOverridden: false, overrideIgnored: true }` means it was ignored (`StaffSurgeriesController.cs:333`).
   - "Last vitals" skips struck-through entries.
   - The day list is keyed by theatre id.
   - `instantFromTime`, Asia/Dhaka (UTC+6, no DST): "today" is the branch's `en-CA` date, and "yesterday" is calendar arithmetic at UTC noon, so no zone or DST slip is possible. A time more than 12 h ahead goes to the previous day, which picks the nearest instant.
     - 23:55 at 00:05 on 13 Oct gives 2026-10-12T17:55Z.
     - 00:03 gives 18:03Z.
     - 11:00 at 00:05 stays today (future; the server decides).
     - 12:06 at 00:05 becomes yesterday 12:06 (nearest).
     - The test vectors were checked by hand. The correction path is R6.
5. **The owner decision on DrugBatchChooser ("Accept it", 2026-10-14) is recorded** in §1 Amended (3) (present in both corrupted copies, see R2), ADR-FE-078 Status (`web/docs/decisions.md:1611`), SRG row 50 and IMP row 63. IMP 63 keeps the shared, location-scoped picker as a later Inventory follow-up and says that none exists yet. ARCH M1 is therefore closed as owner-accepted. M2 (location scope) stays open as SRG row 51.
6. **Tests run.** A targeted vitest run (node_modules junctioned temporarily, then removed) passed: `gatefix`, `report`, `drug`, `booking`, `reschedule`, `campaigns` and `surgery-format-and-validation`, 7 files and 63 of 63 tests. No full suite. No real-API run, because no surgery seeder exists (row 55).
7. **No new dependency** and no `src/**`, `tests/**` or `.ai/**` change. The worktree was clean after the probe was deleted.

SCOPE CHECK
- files in allowed scope: 123/123 (range 5a187b6b..19e9f304). The six reverted hook files now show no diff. The 123 include `gatefix.test.tsx`, the three gate records, `docs/backlog/SRG.md` and `IMP.md` (backlog rows, per the gate-blocking rule), `docs/handover/HO-SRG-002.md` (§1 Amended only; its content defect is R2) and `docs/test-cases/TC-HO-SRG-002.md`. The 7 accepted with recorded authority in the first pass still apply.
- files outside allowed scope: none

**To pass:** close R1 (cache in step with saves, plus a remount test against a mock that enforces `expectedVersion`) and R2 (repair §1 of the handover). A test-only re-run plus a read of §1 is enough afterwards. R3–R8 go to the SRG backlog.

---

## Re-check 2 — 2026-10-14 (fixes for combined re-check R1–R8)

GATE: ARCHITECTURE (re-check 2 of the combined re-check)
HANDOVER: HO-SRG-002
COMMITS: da303517 (range 921521a6..da303517)
DATE: 2026-10-14
STATUS: PASS

R1–R8 are closed in code. No BLOCKER or HIGH remains. Two MEDIUM items go to the SRG backlog: one R1 path that no test covers, and a narrow race in R1.

FINDINGS
- [MEDIUM] N1 web/tests/components/surgical-report/remount.test.tsx. The remount test proves only R1's first-save path.
  - **What test 1 covers.** It remounts after the FIRST save. That save finds the cache `null` and takes the `invalidateQueries({ refetchType: 'none' })` branch (`useReportAutosave.ts:68-72`), and the remount then waits for the refetch (`ReportTab.tsx:42`).
  - **The mutations it catches.** Deleting the invalidate, or deleting the `isFetching && !shown` wait, makes `:54` fail.
  - **The common path is untested.** Take a draft that already exists: the vet types, it saves, they switch tab and come back. That save patches the cache (`setQueryData`, `useReportAutosave.ts:53-67`), and no test remounts after a patched save. Deleting the `if (cached)` branch passes both tests.
  - **Test 2 proves less than it claims.** It says "the same element: never remounted". Its only save also goes through the empty-cache branch, which leaves `data` null, so reverting the key to `data?.version ?? 'new'` also passes.
  - **By reading, the code is correct.** The patch sets every `SurgicalReport` field the editor reads, plus `version`. The cache key matches `useSurgicalReport` (`['surgeries', id, 'report', branchId]`, both from `useBranchScope`). The editor key is now stable across saves.
  - **To close.** Add a remount after the second save, or start from an existing draft, so that both the patch and the key are pinned.
- [MEDIUM] N2 web/components/surgical-report/ReportTab.tsx:40-42 with useReportAutosave.ts:116-123. A return to the tab BEFORE the flushed PUT has answered still mounts the stale editor.
  - **What happens.**
    - The flush runs after unmount, and the cache is patched only once the PUT resolves.
    - A return within that round trip mounts the editor from the not-yet-patched cache. On a first draft the cache is still `null` and not stale.
    - The later patch or invalidate does not remount it, because the key is stable by design.
    - The editor therefore shows the old text with the old version, and the next keystroke is a 409. *Reload* recovers, and the server has the text.
  - **Why only MEDIUM.** The window is one request's latency.
  - **To close.** On mount, wait for an outstanding flush for that `surgeryId`, or mark the cache stale before the flush is sent, not after.

VERIFIED (no finding)
- **R1 (stale cache).**
  - Every successful save now patches the cached report, or marks it stale when there was none.
  - The editor key is the version first seen plus a reload counter, so a save does not remount the editor under the typist.
  - *Reload* refetches, then bumps the counter.
  - The MSW mock now refuses a save with no or a stale `expectedVersion` against an existing draft (`reports.ts:86-87`), matching `SurgeryProcedureCommandHandlers.cs:104-105`.
  - `remount.test.tsx` uses `gcTime: 300_000`. Its assertions are listed under N1.
- **R2 (handover §1).** `docs/handover/HO-SRG-002.md` has one `## 1. Identity` and one title. `git diff 5a187b6b da303517` on it is one line: the `*(none — append rows here)*` placeholder replaced by the Amended (build) row. That row is whole: note (8) carries the regex literally and ends `catecommerce_test).`. The pre-existing 2026-10-11 Amended row is intact.
- **R3 (fingerprint).** `drugFingerprint` is back to medicine, batch, quantity drawn and dose, which is exactly the server's replay comparison (`AnaesthesiaCommandHandlers.cs:194-196`).
  - **Mutation check.** Restoring the widened `rest` makes the new lost-response test fail at `expect(ids_[1]).toBe(ids_[0])`. The time is changed unconditionally (`03:15`), so the test does not depend on how many route options exist.
  - **Fingerprint test.** The four-field test pins each field.
- **R4 / R7 (backlog).**
  - Row 52 notes that the reschedule fix is row 63.
  - Row 60 records the R1 follow-up.
  - Row 62 records the wrong premise.
  - Row 69 withdraws the `reset()` claim, and `reset()` is removed.
  - Rows 73–80 log R1–R8. The corrections are honest; N1 and N2 are new.
- **R5 (reschedule error state).** `serviceMissing` requires `services.isSuccess`. A failed fetch shows its own alert with *Try again* and disables *Review*. Tested with a 500.
- **R6 (corrections across midnight).** `instantFromTime(time, zone, referenceMs)` picks the nearest of the day before, the same day and the day after the reference, all in branch-local days. New entries use now as the reference; corrections use the original entry (`CorrectEntryDialog.tsx:100`).
  - The test vectors were checked by hand in Asia/Dhaka:
    - 23:50 on 11 Oct corrected to 00:10 gives 2026-10-11T18:10Z (00:10 on the 12th).
    - 23:30 gives 17:30Z.
    - 23:55 logged at 00:05 gives the previous evening.
  - Behaviour is unchanged where it was right: 11:00 at 00:05 stays today, and 12:06 at 00:05 is the previous day.
  - **Note, not a finding.** A new entry typed as 00:10 at 23:50 now becomes 20 min ahead. The server decides, as documented.
- **R8 (test fidelity).** The mock is fixed and a retaining client is in use. See N1 for the remaining test gap.
- **Tests run.** A targeted vitest run of `tests/components/{surgery,surgical-report,anaesthesia,recovery,surgery-campaigns}` and `tests/unit/surgery-format-and-validation.test.ts` passed: 15 files, 123 of 123. node_modules was junctioned temporarily and then removed. No full suite. No real-API run, because no surgery seeder exists (row 55). The worktree is clean.
- **Scope.** The 12 files in the range are all surgery-owned, or the in-scope handover, backlog and TC docs. No other area's file, no `src/**`, no new dependency.

SCOPE CHECK
- files in allowed scope: 12/12 (range 921521a6..da303517); 124/124 for 5a187b6b..da303517
- files outside allowed scope: none
