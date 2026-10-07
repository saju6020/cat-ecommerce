GATE: ARCHITECTURE
HANDOVER: HO-APT-002
COMMITS: 93fd85f 59d2660 34309a6 7b4db3c cd45fcf eba60c5 b2d0e4e a9df8a8 31d6e40 bb183b8 b3377df (first pass master cffaa79...bb183b8, 107 files; second pass e116c69..b3377df, 1 file; branch ho/APT-002)
DATE: 2026-10-04
STATUS: PASS

# Second pass (delta)

This pass reviewed `git diff e116c69..b3377df`. `e116c69` is the first-pass record and `b3377df` is the owner-decision commit. The delta changes one file, `docs/handover/HO-APT-002.md`: 3 lines added and 2 removed. No source, test or mock file changed, so every first-pass judgement of the code still stands.

## First-pass BLOCKER: closed

The BLOCKER was `CustomerSessionGuard.tsx`, a §14 file edited with only the builder's own authorisation. It is closed.

- **The owner decision is recorded.** §1 Amended now ends with *"2026-10-04 — owner decision, closing the ARCHITECTURE BLOCKER (e116c69)"*.
  - It approves the 4-line edit to HO-ACC-002's `web/components/portal/CustomerSessionGuard.tsx`.
  - It states the reason: §8.1 requires the booking parameters to survive sign-in, and `sanitizeNextPath` still applies.
  - This matches the HO-FIL-002 precedent, where a §14 BLOCKER was resolved by a recorded owner decision. This gate verified the record in the handover. It did not hear from the owner directly; the coordinator relayed that the owner gave the decision.
- **§13 now authorises the file.** It lists `web/components/portal/CustomerSessionGuard.tsx` with the note "`?next=` carries the query string only", citing the decision.
  - The code edit at `CustomerSessionGuard.tsx:43-47` is unchanged since the first pass and does exactly that. Nothing else in the file changed.
- **The handover defect behind the BLOCKER is fixed.** §13's non-existent `web/lib/routing/**` is now `web/app/(auth)/sign-in/page.tsx`, "`/book` on the next-param allow-list only".
  - This branch does not touch that file: `/book` was already on the allow-list from HO-ACC-002 (`95e09bc`). The line only makes §13 correct for the record.
  - This closes the first pass's LOW finding against `HO-APT-002.md:317`.
- **The handover edit is in scope.** It is append-only on the Amended row, plus the two §13 lines that the decision itself calls for. This is the same as the HO-ADM-002 precedent for a handover's own Amended row.
- **SECURITY handover.** SECURITY should still confirm the open-redirect guard with a query string present, for example `next=/book?x=//evil` and `next=/portal?…`, as the first pass noted.

## Second-pass findings

No new findings. The open items carry over unchanged from the first pass:
- MEDIUM: 5;
- LOW: 9. The handover-path LOW is now closed, so 8 remain open.

Under the owner rule they go to the backlog (APT, ADM, integrator, ACC) as listed under "Backlog candidates" below. None of them blocks this gate.

## Scope check (second pass)
- files in allowed scope: 107/107 on the full branch, plus this record.
  - `CustomerSessionGuard.tsx` is now listed in §13 under the owner decision.
  - The other files are as in the first pass: 102 explicitly in §13, the handover's own Amended row, and three files justified in §1 Amended (13).
- files outside allowed scope: none.

---

# First pass (FAIL, superseded by the second pass above)

First pass. Reviewed with `git diff master...HEAD` against:
- HO-APT-002 §1 Amended (1)–(14) of 2026-10-04, §4, §6, §8–§14 and §20;
- `web/docs/decisions.md`, `docs/handover/README.md` (ADR-FE allocation table), HO-ACC-002 §4.3 forbidden shapes;
- the HO-APT-001 backend on master `cffaa79`, for the wire-shape claims in Amended (1)–(3), (10) and (11).

This is a web-only handover with no `src/**` change, so the `.ai/` backend checklist (layers, aggregates, dispatch, persistence, host) has nothing to apply to. No build, lint or test was run by this gate. Runtime items are marked for TEST.

Owner rule (2026-09-27): only BLOCKER and HIGH fail a gate. MEDIUM and LOW go to `docs/backlog/<AREA>.md`, which this gate does not edit.

The gate fails on **one BLOCKER, and it needs a decision, not a code change**: a §14 file edited on the builder's own say-so. Apart from that, the slice complies with the architecture. There are no HIGH findings.

## What was verified

**Layering and forbidden shapes (HO-ACC-002 §4.3, HO-APT-002 §8, §10)**
- No `useQuery`/`useMutation`/`useQueries` inside any new component or page. Every hook lives in `lib/hooks/`.
- No `fetch` outside `lib/api/client.ts`.
- No `createContext`, global store, `localStorage`, `sessionStorage` or `dangerouslySetInnerHTML` in new non-mock code. The hold stays in component state only (ADR-FE-024).
- `web/package.json`, `tsconfig.json`, `next.config.ts` and `eslint.config.mjs` are unchanged, so no dependency was added. `.ics` generation, the calendar grid, drag and drop and the date strip are all in-repo.
- **No client-side availability or conflict logic.**
  - `components/calendar/layout.ts` is geometry only. Its off-shift shading is the complement of the server's own `availability` payload, which §11.6 asks for.
  - `SlotStep` renders server slots as returned.
  - `MoveDialog` takes a typed time and lets the server judge it.
  - The staff "Outside opening hours" marker comes from the server's `outsideOpeningHours`.
- Actions come only from `allowedTransitions`/`canChange` (ADR-FE-028).
- Every staff query key carries the active branch (`useBranchScope`, §8.3), and staff lists poll at 30 s.

**Shared hooks and components are reused, with no parallel shell, search or form (§20.1)**
- HO-ACC-002: `useFindCustomers`, `useStaffCustomer`, `useStaffCustomersByIds`, `WalkInCustomerForm`, `UnsavedChangesGuard`, `PermissionDeniedState`, `ConfirmDialog` and the common Loading, Error and Empty states.
- HO-CAT-002: `CatProfileHeader` (wrapped by `CatHeader.tsx`), `HighRiskBadge` and `useRegisterCat`.
- HO-ADM-002: `useActiveBranch*`, `lib/format/time`, `useFilterUrlUpdate`, `getPublicServices`, `getPublicBranches` and `useBranchRooms`. `Branches.View` is held by `StaffRoles.All`, so receptionists can read rooms.
- `CustomerPicker` is a new presentational control over the existing ACC-002 lookup hook, not a second search API.

**ADR numbering.** ADR-FE-024…028 match the `docs/handover/README.md:88` allocation, and none of them clashes:
- master: 001–020 and 149–158;
- ho/NTF-002: 159–163, plus a prose mention of 039;
- ho/SHP-002: 029–038.

**Point 2: HO-APT-001 code against its §12.4 prose.** All five claims hold on master `cffaa79`:
- The hold `201` returns `performerId`/`roomId` (`CustomerAppointmentsController.cs:40`).
- Confirm and reschedule return `GetDetailAsync(...)`, the detail itself (`:62`, `:98`). Move returns `Ok(detail.Result)` (`StaffAppointmentsController.cs:124`).
- `BranchRefViewModel(Guid Id, string Name, string? PhoneNumber)` (`ClinicViewModels.cs`).
- `ServiceSnapshotViewModel.PriceAmount` is a `decimal`. No JSON number-handling or converter is registered under `src/`, so it serialises as a JSON number.
- A `409` carries a ValidationResponse on these paths:
  - customer `Conflict(validationResult)` when the result contains `SlotNoLongerAvailable` (`:133`);
  - staff Move `Conflict(...)` for `StaleCalendarVersion` (`:129`);
  - staff Book/Move `SlotNoLongerAvailable` only through the 40P01 filter. That is backlog APT row 22, and HO-APT-002 handles it as a staff 409.

**Point 1: `client.ts`, the `ApiConflictError` body.** This is within the architecture and backward-compatible.
- The constructor argument is optional, and the message, `status` and `name` are unchanged. `instanceof` works as before.
- The only pre-existing callers, `errors.ts:74` and `:104` and `CatForm`, are untouched and ignore `response`.
- On `409` the body is now read with `.json().catch(() => undefined)`, and kept only when `errors` is an array. An empty or non-JSON `409`, such as HO-CAT-001's concurrency `409`, still yields the bare error.
- The body was never read before, so consuming it changes nothing for any other caller.
- `client.ts` is named in §13, and the extension is disclosed in Amended (1). This follows the HO-ADM-002 `lib/branch.ts` precedent: an allowance extended additively and disclosed is not a scope violation. See the LOW below.
- The module-level clock-sample array holds no application state, the same as the accepted `sessionErrorHandler` and `queryClientRef` precedents. §8.4 itself places it in `client.ts`.

**Point 7: mock import order.** No other mock module creates the hazard.
- `grep` finds no import of `@/lib/api/client` or `@/lib/branch` anywhere under `web/mocks/`.
- The only value imports from `@/` in mocks are `lib/format/time`, `lib/format/appointment` (which imports only `./time`) and `lib/catPhotoRules` (no imports). All three are pure.
- Every `lib/api/*` import in mocks is `import type` (`appointments/staff.ts:4-5`, `store.ts`, `areas/cat-profiles.ts`, `files/fixtures.ts`), so it is erased at compile time.
- `files/fixtures.ts:15` already uses the same workaround.

FINDINGS

- [BLOCKER] web/components/portal/CustomerSessionGuard.tsx:43-47 — **a §14 forbidden-scope file was changed, authorised only by the builder's own Amended row.**
  - **The file and the rule.** The file belongs to HO-ACC-002 (`95e09bc`, `04f70ee`, `f90d465`). §14 forbids "every other … HO-ACC-002 … file beyond the lines in §13", and §13 does not list this file.
  - **The change itself is sound and minimal.**
    - `?next=` now carries `window.location.search`. `sanitizeNextPath` still decides what is honoured, and its `matchesPrefix` accepts `/book?…`.
    - `/book` is already on the sign-in allow-list (`app/(auth)/sign-in/page.tsx:46-50`).
    - The change is required by §8.1: "sent to sign-in with `?next=` carrying the booking parameters".
  - **Why the handover is contradictory.** §8.1 demands a behaviour that can only be built in a §14 file. §13's provision for it, `web/lib/routing/**`, names a directory that does not exist; the allow-list lives in `app/(auth)/sign-in/page.tsx`.
  - **Why it is still a BLOCKER.** Amended (13) is "recorded by the agent that built it". This repo's gates require more than that for a §14 edit:
    - HO-ACC-002 ARCHITECTURE (package.json, eslint.config.mjs): a BLOCKER, closable only by an integrator-authorised Amended row.
    - HO-FIL-002 ARCHITECTURE: a BLOCKER, resolved by an owner decision.
  - **To close (owner or integrator decision).** Either:
    - ratify the edit in §1 Amended as an owner or integrator decision. No code change is needed, and this gate would pass on a delta check. Or:
    - revert it, which leaves §8.1's query-string carry unbuilt.
  - Either way, also correct §13's nonexistent `web/lib/routing/**` path.
  - SECURITY should still confirm the open-redirect guard with a query string present.

- [MEDIUM] web/lib/api/booking.ts:164-175, app/(portal)/portal/appointments/[appointmentId]/MyAppointmentView.tsx:50-54 — **point 3, the `findServiceIdForSnapshot` interim.**
  - The gap is real: no `serviceId` on the Summary, Detail, Calendar/Day-list or Queue view models (`ClinicViewModels.cs`), while `GET …/slots` requires it.
  - **It is a sound interim and does not need a stop.**
    - It returns an id only on exactly one name + category + duration match. Otherwise it returns null, and the screen shows the clinic contact card.
    - The reschedule `POST` carries only `startsOn`/`performerId`. The server re-validates against the appointment's own service, so a wrong match can at worst show a wrong slot list, and the server refuses the move.
    - It invents no mock field (§20.5) and is disclosed in Amended (2).
  - **Its weakness.** It silently degrades to "call the clinic" when a service is renamed, or when two services share a name, category and duration.
  - **APT backlog:** add `serviceId` to `AppointmentSummaryViewModel`, `AppointmentDetailViewModel`, `CalendarAppointmentViewModel` and `ClinicQueueEntryViewModel`. Then delete the heuristic, and give **Move…** its §11.6 slot list (see the next finding).

- [MEDIUM] web/components/calendar/MoveDialog.tsx:40-50 — §11.6 says Move… takes "time from the staff slots endpoint for that vet". It takes a typed time instead.
  - This follows from the same missing `serviceId`, and is disclosed in Amended (2) and ADR-FE-026.
  - No availability is computed client-side, and the server re-validates.
  - It closes with the APT `serviceId` backlog item above.

- [MEDIUM] web/lib/hooks/useStaffAppointments.ts:90-101, app/(staff)/staff/front-desk/FrontDeskView.tsx:77-83, app/(staff)/staff/appointments/flagged/FlaggedView.tsx:28-32 — **point 5, the N+1 detail fetch.**
  - **What it does.** One `GET /api/staff/appointments/{id}` per open Today row, or per queue entry, each polled every 30 s. The Flagged view does the same and adds a customer read per row, also polled, although §8.3 does not list Flagged as a polling screen.
  - **It is acceptable as an interim.**
    - It is the only way to honour ADR-FE-028 when the list payloads omit `allowedTransitions`; the alternative, a status-to-buttons table, is what ADR-FE-028 forbids.
    - It is scoped to the visible tab and to open statuses.
    - The staff detail endpoint has no rate-limit policy, so it will not trip a `429`.
  - **The cost.** It scales as rows × open screens ÷ 30 s, about 2 requests/s per Front desk screen on a 60-row day.
  - **APT backlog:** add `allowedTransitions` to `CalendarAppointmentViewModel` (day list) and `ClinicQueueEntryViewModel`, and `customerName`/phone to `FlaggedAppointmentViewModel` (Amended (3) and (5)), then drop the per-row reads.
  - Later handovers that cite ADR-FE-028, such as HO-ORD-002 ADR-FE-093, should not copy the N+1 pattern.

- [MEDIUM] web/lib/api/client.ts:278 and ADR-FE-025 — **point 4, the `Date` header across origins.**
  - `HostPipelineExtensions.cs:97-106` calls `AllowAnyHeader()` but never `WithExposedHeaders("Date")`, and `Date` is not a CORS-safelisted response header.
  - The web calls the API at `NEXT_PUBLIC_API_BASE_URL`, cross-origin, so in deployment the offset stays 0 and the countdown runs on the browser clock.
  - **The frontend degrades safely.** The server still expires the hold, and confirm then gets `SlotHoldExpired`, which goes down the expiry path. The frontend is compliant and the gap is disclosed in Amended (10).
  - **But** §18 A2 ("honest countdown" on a phone with a wrong clock, U-A6) only holds under MSW or same-origin.
  - **Integrator backlog:** add `.WithExposedHeaders("Date")` to the `HostAllowedOrigins` policy.
  - ACCEPTANCE should judge A2 against the deployed topology, not against the mock.

- [MEDIUM] app/(portal)/portal/appointments/[appointmentId]/MyAppointmentView.tsx:57 — **the reschedule picker's default date ignores the branch zone.**
  - It is `detail.startsOn.slice(0, 10)`, the date part of the server's ISO instant, which is UTC-offset from `timestamptz`. It is not the branch-local date.
  - §8.4 requires branch-local `yyyy-MM-dd` dates in the flow. An appointment between 00:00 and 06:00 in Asia/Dhaka would open the picker on the previous day.
  - `localDateOf(detail.startsOn, zone)` already exists in `lib/format/time`.
  - Hand to CODE_REVIEW: "time only through `lib/format`".

- [LOW] web/lib/api/booking.ts:137-155 — **point 8, the dual-key read in `getClinicBranches`.**
  - It reads `addressLine ?? address` and `phoneNumber ?? phone` through an `as unknown as Record<string, unknown>` cast.
  - The disagreement is real: `BranchSummaryViewModel` sends `AddressLine`/`PhoneNumber`, while HO-ADM-002's `PublicBranch` type (`lib/api/admin-branches.ts:27-35`) and its `/api/branches` mock (`mocks/handlers/admin/branches.ts:222-229`) say `address`/`phone`.
  - Fixing the ADM files is §14 for this handover, so the local tolerance is the right in-scope choice. It is disclosed in Amended (11).
  - **ADM backlog (MEDIUM there):** correct `PublicBranch` and its mock to `addressLine`/`phoneNumber`, and add `description` to `PublicService` (`booking.ts:120` widens it locally for the same reason). Then remove the fallback here.

- [LOW] web/lib/api/client.ts:55-66, 299-303 — `ApiConflictError` gains an optional body, which goes beyond §13's "Date-header offset capture only".
  - It is additive, backward-compatible, necessary for HO-APT-001's real `409` contract, and disclosed in Amended (1). It is not a scope violation, by the HO-ADM-002 `lib/branch.ts` precedent.
  - The header comment at `client.ts:19` ("409 → ApiConflictError (a concurrent edit)") should mention the optional `response`. That is in scope as a comment fix.

- [LOW] web/nav/areas/appointments.ts:18-23 — §13 allows "one nav entry only" for the portal, but there are two portal items: **Appointments** and **Book a visit**.
  - Moving the entries from the two layouts into the area's own registry file is correct. The scaffold comment says the file is owned by the Appointments area, and the shells read the registry. Amended (13) records it.
  - The extra entry is not recorded. Either record it or drop it.

- [LOW] web/tests/e2e/appointments-helpers.ts — outside §13's `web/tests/e2e/appointments-*.spec.ts` glob, by the letter only.
  - It is a test-only helper under `web/tests`, not the §14 root `tests/**`, and Amended (13) records it.
  - The component-test helpers (`tests/components/booking/{helpers.tsx,nav-mock.ts}`, `tests/components/front-desk/helpers.ts`) are inside §13's glob and need no justification.

- [LOW] web/mocks/handlers/appointments/fixtures.ts:147-149 — `apiUrl()` duplicates `mocks/handlers/files/fixtures.ts:15` `mockApiUrl()`.
  - Two copies of the same import-order workaround exist.
  - There is no guard against the next mock module value-importing `@/lib/api/*`; only `import type` keeps the current ones safe.
  - Suggested backlog item: a shared `mocks/apiUrl.ts`, plus a lint `no-restricted-imports` on `@/lib/api/client` and `@/lib/branch` under `web/mocks/**`. `eslint.config.mjs` is §14 here, so that needs its own handover.

- [LOW] web/components/booking/LeaveHoldGuard.tsx:15-57 — a second copy of `UnsavedChangesGuard`'s capture-phase click interception (ADR-FE-013), with a custom consequence and an `onLeave` callback.
  - Extending the HO-ACC-002 component would have been §14, so a local variant is justified.
  - It replays with `router.push(href)` rather than replaying the prevented click.
  - Suggested ACC backlog item: give `UnsavedChangesGuard` `consequence` and `onConfirm` props, and fold this guard into it.

- [LOW] Component size (§11: "each component under ~150 lines").
  - Eleven files exceed 150 lines. The largest are `WalkInPanel.tsx` (398), `BookingFlow.tsx` (384), `StaffBookingForm.tsx` (373), `CalendarGrid.tsx` (327) and `StaffAppointmentDetail.tsx` (313).
  - This is not architectural. Handed to CODE_REVIEW.

- [LOW] docs/handover/HO-APT-002.md:317 — §13's `web/lib/routing/**` names a directory that does not exist. The `/book` next-param allow-list lives in `app/(auth)/sign-in/page.tsx`, an HO-ACC-002 file.
  - This handover defect is the root of the BLOCKER above, and should be corrected when that decision is recorded.

**Runtime items for TEST** (not verified here):
- U-A6 under a cross-origin real-API run, which is expected to fall back to the browser clock;
- `pagehide` `keepalive` release (U-A9);
- U-D2/U-E3, polling against open dialogs and drags;
- E-X1/E-X2;
- every E-case against the real API, which Amended (14) says was not run.

**Backlog candidates (owner rule 2026-09-27)**
- **APT:** add `serviceId` to the Summary, Detail, Calendar/Day and Queue view models; add `allowedTransitions` to the Day-list and Queue entries; add customer name and phone to `FlaggedAppointmentViewModel`; the MEDIUM `MyAppointmentView.tsx:57` zone fix; the LOW findings above.
- **ADM:** correct the `PublicBranch`/`/api/branches` mock field names, and add `description` to `PublicService`.
- **Integrator:** add `WithExposedHeaders("Date")` in `HostPipelineExtensions`.
- **ACC:** add `consequence`/`onConfirm` props to `UnsavedChangesGuard`.

SCOPE CHECK
- files in allowed scope: 106/107
  - Explicitly in §13: 102.
  - Covered by the §1 row itself: `docs/handover/HO-APT-002.md` (Amended row only, the HO-ADM-002 precedent).
  - Outside §13, not §14, justified in §1 Amended (13) — 3 files:
    - `web/nav/areas/appointments.ts` (area-owned registry; one unrecorded extra entry, LOW);
    - `web/mocks/handlers/areas/appointments.ts` (area-owned re-export, the HO-ADM-002 split);
    - `web/tests/e2e/appointments-helpers.ts` (test helper, LOW).
  - `docs/gates/HO-APT-002-ARCHITECTURE.md` (this file) is in §13.
- files outside allowed scope: `web/components/portal/CustomerSessionGuard.tsx` — HO-ACC-002 file, §14 forbidden; required by §8.1 but authorised only by the builder's own Amended (13). BLOCKER above.
