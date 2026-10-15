GATE: ARCHITECTURE
HANDOVER: HO-EMR-002
COMMITS: 57666ac4 a00a9ab2 9245fea0 d69a71f7 d65fff82 4b6f505a 53696ad5 (base master d3527166; branch ho/EMR-002)
DATE: 2026-10-08
STATUS: FAIL

Frontend gate. §6 names no `.ai/` document, and `.ai/` governs the backend only (HO-ACC-002 §19).
This review is against HO-EMR-002 §8/§9/§10/§11/§13/§14/§20, the inherited rules (HO-CAT-002 §8.2,
HO-ACC-002 §4.3 and §5.1, the HO-ADM-001/002 active-branch rule) and the frontend gate precedent
(HO-BIL-002 and HO-IMP-002 ARCHITECTURE). The contract was checked against the real API on master
(HO-EMR-001 merged): `src/Api` controllers and `src/Read` view models. Nothing was built or run here.
The real-API E-run is BLOCKED on credentials in TC-HO-EMR-002 and is referred to TEST.

FINDINGS
- [HIGH] web/components/visit/DischargeSummaryView.tsx:55, web/lib/api/visits.ts:359-360 (staff path,
  used by `app/(staff)/staff/visits/[visitId]/summary/page.tsx:51`); web/components/consent/ConsentDetail.tsx:29-32,136-138.
  The staff downloads cannot send `X-Branch-Id`:
  - the staff discharge-summary PDF is a plain `<a href download>`;
  - the stored consent signature is an `<img src>` pointing at `GET /api/staff/consents/{id}/signature.svg`.

  Neither an anchor nor an image can send `X-Branch-Id`. Both endpoints resolve the branch with
  `ClinicalResponses.TryResolveBranch` (StaffVisitsController.cs:240, StaffConsentsController.cs:85).
  Without the header, `HttpContextActiveBranchAccessor` falls back to the caller's primary branch
  (HttpContextActiveBranchAccessor.cs:15,47). Then the query filters by that branch, and a visit or
  consent of another branch returns the cross-branch `404`.

  For staff working at a non-primary branch, this means:
  - **Download PDF** of the discharge summary fails;
  - the signature on every consent captured at that branch shows as a broken image. That is the
    evidence US-CLN-21 exists to keep.

  The JSON reads around both work, because they go through `apiFetch`, which sends the header. Only
  the binary fetches are wrong. This is the same defect, and the same standard, as HO-BIL-002 (HIGH)
  and HO-IMP-002 (HIGH).

  Fix it in the frontend, inside §13 (`lib/api/visits.ts`, `lib/api/consents.ts`, `lib/hooks/**`):
  1. Fetch both as a blob through a `lib/api` helper that sends `X-Branch-Id`. Master has no shared
     helper yet; BIL-002's 6527673b is the pattern.
  2. Save the PDF with `URL.createObjectURL` plus `download`.
  3. Show the signature from an object URL, and revoke it on unmount.

  Not affected:
  - The portal PDF (`my-visits.ts:41`), because customers have no branch.
  - Lab, photo and consent-file attachments. They are signed file URLs (ADR-FE-055); the file-serving
    endpoint checks the signature, not the branch.
- [MEDIUM] web/components/visit/VisitWorkspace.tsx:37-39, web/app/(staff)/staff/diagnostics/[orderId]/page.tsx:36 — declared deviation: a Vet can enter results and attach files after finalising, although the visit's `allowedActions` lists no such action.
  - **Ruling on the question asked:** yes, the client decides to show an action the server does not advertise. On a finalised visit, `VisitAllowedActions.For` (Domain/DomainServices/VisitAllowedActions.cs:43-46) emits only `addAddendum`. The client adds result entry from `roles.includes('Vet')`, and on the standalone order page it does so regardless of visit state.
  - **Why it is not HIGH:**
    - Results belong to `DiagnosticOrderAggregate`, not to the visit. The endpoints (StaffDiagnosticsController.cs:73-110) are guarded by `Visits.WriteNote` = `[Vet]` (ActionRoles.Consultation.cs:13), which is exactly the role the client mirrors.
    - Recording results after finalising is the specified behaviour (§11.1 FinaliseDialog: "results can be added after finalising").
    - `DiagnosticOrderViewModel` (DiagnosticViewModels.cs:20-25) carries no `allowedActions`, so the client has no server signal to obey.
    - The server still refuses anything it does not allow.
  - This is the same contract-gap class HO-BIL-002 rated MEDIUM (*Void* on `ChargeViewModel`). The client re-implements no eligibility rule (§20.2).
  - It was not reported under §20.5, and ADR-FE-051's Status line claims "Built as specified" (see the LOW below).
  - Backlog EMR (backend): add `allowedActions` (`recordResults`, `attachFile`, `cancel`) to `DiagnosticOrderViewModel`. Then gate on it and delete both role checks.
- [MEDIUM] web/components/visit/AppointmentClinicalLinks.tsx:171-172,198 — declared deviation: **Record consent** shows for `Spay`, `Neuter` and `Dental` appointments only.
  - §11.9 says "for theatre appointments". The appointment detail payload has `roomId` and `roomName` but no room kind.
  - The server places no category or room restriction on `RecordConsentCommand` (ConsentHandlers.cs:39-83). So the category set is a business rule assumed on the client, not one mirrored from the server. Per §20.2 and §20.5 it should have been reported, not assumed.
  - **Consequence:** this link is the only UI entry point to consent capture. `/staff/cats/[catId]/consents` has no *Record consent* action, although §11.6 says capture starts "from the appointment detail **or cat page**". As a result, Anaesthesia or Euthanasia consent for an Emergency or Consultation appointment cannot be reached without typing the URL.
  - Rated MEDIUM here because §11.9 itself specifies a client-side visibility rule and the server accepts any appointment.
  - **Referred to ACCEPTANCE** for A11 (US-CLN-21 kinds) and the missing cat-page entry point.
  - Backlog EMR (backend): expose a consent requirement on the appointment detail (`requiresConsent` or the room kind) and gate the link on it.
- [MEDIUM] web/components/visit/ProvidersGate.tsx:5,25 — declared deviation: the queue and appointment additions sit behind `ProvidersGate`.
  - It imports `AppRouterContext` from `next/dist/shared/lib/app-router-context.shared-runtime`. That is a private Next.js module path, and this is the only `next/dist` import in `app/`, `components/` or `lib/`. A Next upgrade can move it.
  - It exists to let HO-APT-002's unedited component tests render `ClinicQueue` and `StaffAppointmentDetail` bare, so production code is shaped by a test harness. If a future host renders these components without the providers, the clinical actions disappear silently instead of failing.
  - The real app always mounts both providers, so behaviour is correct today.
  - Backlog: make the additions injectable (a render prop or slot that HO-APT-002's tests leave empty), or add the providers to the HO-APT-002 test helpers under an owner-approved Amended row. Then delete `ProvidersGate`.
- [MEDIUM] web/components/visit/VisitCatHeader.tsx (whole file) — declared deviation: a new compact cat header.
  - UI-7 and §6 assume HO-CAT-002's `CatProfileHeader` has a compact variant. It has none (`CatProfileHeader({ cat, vaccinationStatus })` only), and adding one would edit a §14 file.
  - So a second header in own scope is the only in-scope option. It reuses `HighRiskBadge`, `VaccinationStatusBadge` and `AgeDisplay`.
  - It is still a second rendering of UI-7's safety facts (high-risk, allergies, chronic conditions) that can drift from the canonical one. §20.1's "do not build a parallel … header" is in that spirit.
  - It is used on 6 screens.
  - Line 59 re-formats `latestWeightKg` with `toFixed(2)` against §8.4's "values are rendered as the server's string". `CatProfile` carries a number here, so this is referred to CODE_REVIEW.
  - Backlog CAT: add a `compact` variant to `CatProfileHeader` and replace `VisitCatHeader` with it.
- [LOW] web/docs/decisions.md:1108-1187 — the ADR Status lines say "Built as specified" for all ten. Three of them are realised differently:
  - **ADR-FE-051:** result entry after finalise is role-inferred (MEDIUM above).
  - **ADR-FE-055:** `FileUploadField` is a thin wrapper over HO-FIL-002's `FileUploader`. The pre-check, progress, cancel and error mapping are FIL-002's, and no `lib/api/files.ts` upload code is added by this handover. That is correct reuse, but the Status should say so.
  - **ADR-FE-057:** the staff PDF is a plain link that fails off the primary branch (HIGH above).

  For **ADR-FE-056** no deviation was found:
  - the drawer mounts `ClinicalHistoryView` only while open;
  - `HistoryOverview` renders from the one summary call with no further hooks;
  - each section fetches on tab open, with "Load more".

  Fix: amend the three Status lines to name the realisation. The Decision and Why texts must stay verbatim.
- [LOW] web/components/visit/VisitWorkspace.tsx:139-144 — ADR-FE-049 says summaries open "in a drawer or dialog over it, never a navigation". The finalised record links to `/staff/visits/[visitId]/summary`, which is a navigation. No note form is mounted in the finalised state, so the ADR's purpose (the draft) is not engaged, and §8.1 makes the summary its own printable route. Backlog: record the exception in ADR-FE-049's Status.
- [LOW] web/nav/areas/consultation.ts:14-18 — declared deviation: §13 names `(staff)/layout.tsx` for the two nav entries, but the build puts them in the area registry.
  - This is correct under web/README.md "Adding a functional area" and follows the HO-NTF-002, HO-SHP-002, HO-BIL-002 and HO-IMP-002 precedent.
  - The edit is +6/−1 and additive, and `(staff)/layout.tsx` is untouched.
  - HO-EMR-002 §1 Amended still says "(none)". Add the row; §13 names the wrong file, which is a handover defect.
- [LOW] web/tests/e2e/emr-helpers.ts — this is outside §13's `web/tests/e2e/emr-*.spec.ts` glob by the letter only. It is a test helper, the HO-APT-002 `appointments-helpers.ts` and HO-SHP-002 `shop-helpers.ts` precedent. Record it in §1 Amended with the nav row.
- [LOW] web/app/(staff)/staff/consents/new/ConsentNewView.tsx:39-42,79 — declared deviation: the witness picker lists today's calendar performers (`view: 'performer'`).
  - The resource ids are staff account ids (StaffAppointmentQueryHandlers.cs:118-127), so `witnessStaffId` is valid data.
  - §11.6 asks for a "staff picker". Receptionists and staff with no performer shift today cannot be witnesses.
  - This is a narrowing caused by the API (a receptionist cannot list staff accounts), not a layering issue.
  - Referred to ACCEPTANCE (A11).
  - Backlog EMR (backend): a witness-eligible staff list readable with `Consents.Record`.
- [LOW] web/lib/format/clinical.ts:117, web/components/visit/DischargeSummaryView.tsx:112-113 — declared deviation: the client shows a constant prescription-label sentence.
  - §11.5 says "the server's sentence", but `DischargeSummaryViewModel` (DischargeSummaryViewModels.cs:33) carries only `bool PrescriptionsAvailable`. The sentence exists only inside the server's PDF.
  - The client constant is identical to `ClinicalMessages.PrescriptionsOnLabel` (Shared/…/ClinicalMessages.cs:19), and the client shows it only on the server's flag. Not inventing a field is correct under §20.5.
  - Two copies can drift. Backlog EMR (backend): add the sentence to the view model, or accept the copy.

DECLARED DEVIATIONS: RULINGS
- `FileUploadField` wraps HO-FIL-002's `FileUploader`: **accepted, no finding.**
  - Reusing the module's uploader is the right shape. Writing a second uploader would duplicate FIL-002 and its `FILE_RULES` mirror.
  - `FilePurpose` already includes `LabResult`, `ClinicalPhoto` and `ConsentForm`.
  - The XHR upload sends `X-Branch-Id` and CSRF (lib/api/files.ts:224-225).
  - The only cost is the ADR Status wording (LOW).
- Constant prescription-label sentence: LOW above (correct under §20.5; a two-copies risk).
- Nav entries in `nav/areas/consultation.ts`: LOW above (accepted; the §1 Amended row is owed).
- Queue and appointment additions behind `ProvidersGate`: MEDIUM above.
  - The additions themselves are within §13. `ClinicQueue.tsx` is +3/−0 and `StaffAppointmentDetail.tsx` is +9/−0.
  - Each adds one component that renders exactly the two specified actions and the two specified links.
- Walk-ins open the Start dialog: **accepted, no finding.**
  - §11.9 says *Open consultation* "starts the appointment through HO-APT-002's existing Start flow if still `CheckedIn`".
  - A walk-in or unassigned appointment needs a vet and a room. `onStart(entry)` is that existing flow (QueueVisitActions.tsx:88-90), so no parallel start path is built.
- Results after finalise without an advertised action: MEDIUM above.
- New `VisitCatHeader`: MEDIUM above.
- Witness list from calendar performers: LOW above.
- Record consent for Spay, Neuter and Dental: MEDIUM above. It is an assumed client rule, and it is referred to ACCEPTANCE.
- ADR Status "Built as specified" for 055, 056 and 057: LOW above.
  - 055 and 057 do deviate. 057's deviation is the HIGH.
  - For 056 no deviation was found.
  - 051 is the one that should also be amended.

CHECKS (clean)
- **§8 layering (`app` → `components` → `lib/hooks` → `lib/api`).**
  - No `fetch(`, `XMLHttpRequest` or `axios` in the slice's `app/`, `components/` or `lib/hooks`. The one `keepalive` request is `saveDraft` in `lib/api/visits.ts:283-290`, called from `useConsultationAutosave`.
  - No `useQuery`, `useMutation`, `useQueryClient` or `useInfiniteQuery` outside `lib/hooks/`.
  - Nothing outside `mocks/` and `tests/` imports `@/mocks`.
  - Components import from `lib/api` only:
    - the error classes;
    - the error mappers;
    - `getApiUrl`;
    - the two PDF URL builders;
    - types.
- **One-route workspace (ADR-FE-049).**
  - `/staff/visits/[visitId]` renders `VisitWorkspace`. The editable, read-only-open and finalised states are branches of one component, not routes.
  - History (`HistoryDrawer`) and the diagnostic order (`DiagnosticsPanel` → `SideSheet`) open over it.
  - Nested visit and order views inside the history are in-place with *Back* (ClinicalHistoryView.tsx).
  - In the open state no `Link` or `router.push` exists in `components/visit/**` or `components/clinical-history/**`.
  - `HistoryDrawer` is rendered at the workspace root, outside the editor, so the note form stays mounted.
  - The only navigation is the finalised-state summary link (LOW above).
- **Actions only from `allowedActions` (ADR-FE-051).** These are each gated on the visit's `allowedActions`:
  - Save and Finalise (VisitHeaderBar.tsx:35-36);
  - the editor (VisitWorkspace.tsx:53, ConsultationEditor.tsx:71);
  - *Record vitals* (VitalsPanel.tsx:19);
  - *Order test* (DiagnosticsPanel.tsx:43);
  - *Add photo* (PhotosPanel.tsx:26);
  - follow-up (FollowUpPanel.tsx:25);
  - the addendum form (VisitWorkspace.tsx:151).

  A finalised visit renders `VisitRecordView` as text, not disabled inputs.

  Role checks remain only where the server sends no action list:
  - result entry (MEDIUM above);
  - the queue-card actions (§11.9 specifies them by role, and no visit exists yet);
  - *Complete appointment* (the visit's appointment ref carries none);
  - the catalogue managers (admin screens);
  - the appointment Visit link (avoids a `403` read).

  All of these are presentation; the server re-checks.
- **Treatments deferred correctly.**
  - No `TreatmentsPanel`, `RecordTreatmentDialog`, `DispensaryItemSearch`, `lib/api/dispensary.ts`, dispensary mock or treatment endpoint exists.
  - `VisitDetail.treatments` is typed `unknown[]` and not rendered.
  - The summary renders the server's `treatments: string[]` list, which is part of the existing contract.
  - The `BatchExpired` and `InsufficientStock` texts in `errors.ts` are the §11.10 list, text only.
  - TC-HO-EMR-002 records U-D1..D4, E-C2 and the E-A1 consumable step as DEFERRED, not passed.
- **MSW mocks against the real API (HO-ACC-002 §5.1).**
  - All 298 object keys in `mocks/handlers/visits/*.ts` were cross-checked against identifiers in `src/**`. The 32 not found are fixture ids (`visitOpenId`, `catTigerId`, …) or store and helper fields (`fileContentType`, `holdsAction`, `fallbackName`, `daysAgo`, `vetStaffName`), none of which is serialised as a key.
  - The same scan of the six response-type modules (`visits`, `diagnostics`, `consents`, `clinical-history`, `clinical-catalogue`, `my-visits`) leaves only `keepalive`, which is a `RequestInit` option.
  - Compared field by field with no TS-only field:
    - `VisitDetail` and its children against `VisitDetailViewModel` (VisitViewModels.cs);
    - `Consent` and `ConsentListItem` against `ConsentViewModel` and `ConsentListItemViewModel`;
    - the diagnostic and summary types against their records.
  - The mocks mirror the real amendments (cross-branch `404`, `treatments: []`, `thumbnailUrl == url`, no dispensary), as commit 57666ac4 states.
  - The clinical upload mock shadows FIL-002's `POST /api/staff/files` but delegates to its `resolveMockUploadOutcome`. It is mock-only and adds a file registry.
- **Overlays on `components/ui/dialog.tsx`.** Built on it:
  - `SideSheet` (history and order drawers);
  - `FinaliseDialog`;
  - `QuickVitalsDialog`;
  - `VitalsConfirmDialog`;
  - `OrderDiagnosticDialog`;
  - `FollowUpBookingDialog`;
  - `CatalogueDialog`;
  - `ConsentDetail` (withdraw).

  Confirms (addendum, revert draft, consent submit, follow-up remove) use `components/common/ConfirmDialog`, which is built on `ui/dialog`. There is no hand-rolled `role="dialog"`, `aria-modal`, `fixed inset-0`, `createPortal`, `<dialog>` or `window.confirm`.
- **ADR-FE-049…058 match §9 word for word.**
  - Every Decision and Why text in `web/docs/decisions.md` was extracted by script and compared with the §9 table: 10/10 identical.
  - Each ADR is recorded once.
  - `.ai/**` and `.claude/**` are untouched (0 lines in the diff).
- **No new dependency (§10).** `web/package.json`, the lockfiles, `tsconfig.json`, `next.config.ts` and `eslint.config.mjs` are unchanged. `SignaturePad` is an in-repo canvas, and the weight trend uses `recharts`.
- **No browser storage.** No `localStorage`, `sessionStorage`, `indexedDB` or `caches.` in the slice's `app/`, `components/` or `lib/`. SECURITY owns the depth review (U-A9, U-X4).
- **Query keys follow §8.3 as written:** `['visit', visitId]`, `['cat', catId, 'clinical-history', …]`, `['diagnostic', orderId]`. The branch-scoped lists carry `branchId`: `['visits', 'open', branchId]` and `['diagnostics', 'pending', branchId]`.
- **Scoped edits to other areas' files, each matching §13:**
  - `cats/[catId]/layout.tsx` +3/−0: two tab entries;
  - `portal/cats/[catId]/page.tsx` +7/−0: one link;
  - `adminNavItems.ts` +4/−0: three entries;
  - `ClinicQueue.tsx` +3/−0;
  - `StaffAppointmentDetail.tsx` +9/−0;
  - `lib/api/errors.ts` +106/−0 (append-only);
  - `mocks/handlers/index.ts` +5/−0 (the alias pattern).

  Untouched: `(staff)/layout.tsx`, `client.ts`, `types.ts`, `components/ui/*`, `components/common/*`, and every existing `lib/hooks` file.
- `git merge-tree` against current master (d97e42c4): clean.

REFERRED
- **To SECURITY:** the signature `<img>` and the PDF anchor once they are fixed (object URLs must be revoked, and nothing logged).
- **To CODE_REVIEW:**
  - `VisitCatHeader.tsx:59` `toFixed(2)`;
  - component size: `ConsentCaptureForm` 380, `LabAnalyteManager` 363, `DiagnosisCodeManager` 325, `ProblemListEditor` 295 and `NoteTemplateManager` 281 lines, against §11's ~150.
- **To TEST:** the real-API E-run, which is BLOCKED (no staff credentials). E-D2 on the real API is not measurable, because the dev database has no 10-year cat.
- **To ACCEPTANCE:**
  - A11: the consent entry point is only the Spay/Neuter/Dental appointment link, and there is none on the cat page;
  - the witness list is narrowed;
  - A5 is deferred.

BACKLOG (MEDIUM/LOW, owner rule 2026-09-27)
- **EMR backend:**
  - `allowedActions` on `DiagnosticOrderViewModel`;
  - a consent requirement (or room kind) on the appointment detail;
  - a witness-eligible staff list;
  - the prescription-label sentence in `DischargeSummaryViewModel`.
- **EMR frontend:**
  - replace `ProvidersGate`, with no `next/dist` import;
  - a cat-page *Record consent* entry;
  - amend the ADR-FE-049, 051, 055 and 057 Status lines.
- **CAT:** a compact `CatProfileHeader` variant, then retire `VisitCatHeader`.
- **HO-EMR-002 §1 Amended:** record the nav-registry substitution and `emr-helpers.ts`.

SCOPE CHECK
- files in allowed scope: 133/133 reviewed. 131 are inside the literal §13 globs. The other two are accepted by precedent, pending the §1 Amended row:
  - `web/nav/areas/consultation.ts` is the convention-mandated equivalent of §13's `(staff)/layout.tsx` line;
  - `web/tests/e2e/emr-helpers.ts` is a non-spec helper beside the `emr-*.spec.ts` glob.
- files outside allowed scope: none (no §14 path touched)
