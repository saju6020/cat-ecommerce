GATE: CODE_REVIEW
HANDOVER: HO-PHM-002
COMMITS: a8761829..aa78e655 (ho/PHM-002)
DATE: 2026-10-11
STATUS: PASS

Checked: `tsc --noEmit` clean, `eslint --max-warnings=0` clean, 98 component tests in 14 files pass (vitest --maxWorkers=2). Every U-/E- id in TC-HO-PHM-002 is referenced by a test file; no skips other than the documented chromium-only flows.
Verified against src/: the unit, route, form, frequency and dosing-basis tables in `lib/format/units.ts` and `lib/format/pharmacy.ts` match `Shared/Common/Enums/{DoseUnit,DispensingUnit,StrengthUnit,MedicineForm,AdministrationRoute,DoseFrequency,DosingBasis}.cs` name for name (frequency labels match `DoseFrequency` display text). 409 is sent only for `StaleVersion`/`PrescriptionWarningsChanged` (`PharmacyControllerBase`), and the assessment is carried in the 400/409 body, as `assessmentFromError` expects.
Composer: debounced query keyed on the request text, previous result kept while checking, acknowledgements stored against the fingerprint and cleared on change, submit blocked while stale/checking/failed, 409 replaces the panel and keeps values. Dispense: follows `canDispense` (expired-only renders no control), split quantities summed exactly with scaled integers, FEFO reason, witness dialog before any request, credentials kept out of the mutation cache. Portal: client filtering/paging over plain arrays is correct (see backlog for the unclamped page). Formulary editor remounts on `version` so the 409 reload works. Idioms (query keys, invalidation, error mapping through `pharmacyViolations`) match the repo.

FINDINGS
- [MEDIUM] web/components/pharmacy/DispenseScreen.tsx + dispense/[prescriptionId]/page.tsx — a failed plan refetch after a successful dispense swaps the result panel for the error state (backlog PHM row 35)
- [MEDIUM] web/components/controlled-drugs/CorrectionForm.tsx; correct/page.tsx:27-30 — correction cat picker searches only the first 100 cats client-side (row 36)
- [MEDIUM] web/components/my-prescriptions/MyPrescriptionsList.tsx:70; portal/prescriptions/page.tsx — portal `page` not clamped; diet-only cats missing from the filter (row 37)
- [LOW] WitnessDialog.tsx:78-90 duplicate error effects (row 38)
- [LOW] witness-failure handling written three times (row 39)
- [LOW] PrescriptionComposer.tsx:218-222 any 409 hides the server message behind a fixed line (row 40)
- [LOW] e2e axe is serious/critical only and flows are chromium-only on MSW; real-API verification still owed, TC sheet "Real API re-run" is NOT RUN (row 41)

SCOPE CHECK
- files in allowed scope: not checked here (scope is the SECURITY/ARCHITECTURE gates' remit); the diff is 117 files, all under web/, docs/ and tests
- files outside allowed scope: none observed
