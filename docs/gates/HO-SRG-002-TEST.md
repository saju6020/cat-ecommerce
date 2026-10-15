GATE: TEST
HANDOVER: HO-SRG-002
COMMITS: 5306904c (range 5a187b6b..5306904c, branch ho/SRG-002) plus the TEST commit that adds this file, the case sheet, backlog rows 83-90, one real-API spec and one strengthening test
DATE: 2026-10-14
STATUS: PASS

Case sheet: docs/test-cases/TC-HO-SRG-002.md ("TEST gate sheet"), written with every row NOT RUN before the first run. 49 rows: 45 PASS, 3 FAIL, 1 BLOCKED, 0 NOT RUN. No BLOCKER and no HIGH finding. The 3 FAIL rows are a spec limitation (M4) and two mutants that survived the build's tests (MU4, MU5), both now killed by tests the gate added; none is a production defect.

The main gap is closed: the surgery E-series ran against the REAL API. All 13 real-API specs passed in ONE invocation on a freshly created and migrated database `catecommerce_srg002e2e` (API 5112, web 3112, `--workers=1`; never `catecommerce_dev` or `catecommerce_test`). The database, the seeder, the seed file, the node_modules junction, `.env.local`, `.next`, `test-results` and the bin/obj outputs were removed afterwards.

CHECKED, ALL OBSERVED
- `tsc --noEmit` clean; `eslint . --max-warnings 0` clean; `next build` passes.
- `vitest run --maxWorkers=2` (full): 235 files, 1643 tests, all pass (586 s). The known inventory flakes did not fail, so no rerun was needed. The two tests the gate added pass alone and under lint/tsc.
- Playwright on MSW: chromium booking + flow + owner 11 passed; chromium a11y/responsive 44 passed; mobile-360 a11y/responsive 43 passed + 1 cold-start sign-in race that passed on rerun alone; mobile-360 flow specs 10 passed + 1 failed (E-X1, see below).
- Real API (chromium): E-A1 with the calendar block (surgeon, assistant and theatre columns, Surgery label, link, no Move); E-A2 (explainer names the theatre, surgeon and assistant, the busy start is absent, another start books); E-A3 with two real browser contexts (one wins; the loser gets a real `400 SurgeonBusy` from the exclusion constraint, shown as the race message, and the start leaves the list); E-B1..E-B4 (real 403 on milestone, vitals, drug, end-procedure, report, override, and a nurse's drug and override); E-C1/E-C2 (controlled ketamine with a second real account typed as witness, then corrected with a new witness); E-D1 (cat is `Neutered`, report read-only, a direct PUT after finalising is `409 ReportNotEditable`, addendum); E-E1; E-E2 (post-op sheet, owner pages, and the owner's inbox holds "Ready to go home" with the follow-ups); E-F1; E-X1 (the whole spay in one UI run, 25 s).
- Report autosave on the real endpoints: save then reload (N1); tab switch inside 5 s and a return BEFORE the flush answered, with the PUT held 4 s: "Loading the report..." shown, the typed text and the new version appear, the next save is not a conflict (N2); another vet saves first and the next autosave is a real `409 StaleVersion` with the local text kept (N3).
- Drug entry with a witness: one entry, one stock deduction (drawn quantity), two register rows (administered + wastage); a wrong witness password is refused in the dialog with nothing written; a LOST RESPONSE (the server saved, the answer was dropped) is retried with the same `entryId` and leaves one entry, one deduction and one register set; the same `entryId` with a changed dose is `409 AnaesthesiaEntryIdMismatch`; an identical replay is `201` and writes nothing.
- A correction across midnight: an entry at 23:55 on a past day corrected to 00:05 is stored 10 minutes after the original (the database stays within the API; one SQL write backdated the admission, see the sheet).
- Mutations, each restored by `git checkout` (status clean afterwards): unmount flush removed (3 tests fail), `setQueryData` patch removed (1), in-flight wait removed (2), `encodeURIComponent` removed (1), price regex loosened (3). Two SURVIVED: fingerprint widened (MU4) and the correction's reference rule (MU5).

FINDINGS (none BLOCKER or HIGH; all are in docs/backlog/SRG.md)
- [MEDIUM] SRG 83 tests: the idempotency-key resend test never edited the route (a one-route medicine and an `if (route.options.length > 2)` guard) and `instantFromTime`'s unit test passes its own reference, so two mutants survived. RESOLVED here: `web/tests/components/anaesthesia/gate-strengthening.test.tsx` (2 tests) kills both; the real API (RA-N6, RA-N8) agrees.
- [LOW] SRG 84 DrugEntryDialog: after a lost response the message is only "Something went wrong. Please try again."; retrying is safe but the dialog does not say the entry may already be saved.
- [LOW] SRG 85 config/backend: an http `PublicSite:WebBaseUrl` makes the renderer refuse every Surgery message (including the post-op instructions), suppressed as RenderFailed with only an error log line. Found when the first E-E2 inbox poll came back empty; an https base fixed the run.
- [LOW] SRG 86 tests: E-X1 cannot run on mobile-360 (tabs are a select there); the whole flow at 360 px has never been driven.
- [LOW] SRG 87 ACC: two sign-ins of one staff account at the same instant: one answers `409`. Not a Surgery defect.
- [LOW] SRG 88 tests: cold-start flakes (one MSW sign-in race on mobile-360, one twice-unhydrated sign-in page under the real API once).
- [LOW] SRG 89 integrator: the real-API spec cannot be re-run from the repo until a `SurgeryE2ESeed` fact exists in `tests/**`.
- [LOW] SRG 90 tests: the 30 s Recovery-board refresh is not verified end to end (RA-E1b BLOCKED).

DEVIATIONS FROM THE HANDOVER'S §16, recorded
- E-A2 used another surgery as the busy resource, not a vaccination.
- E-B1's weight and consent were recorded through their real endpoints, not through the cat page and the consent page (other areas' screens).
- The real-API spec is chromium only.
- Seeder, backdate and drop were three throwaway facts in `tests/CatEcommerce.IntegrationTests` (outside the slice's scope), written, run and deleted by the gate. The run's API settings were: sign-in rate limit raised, https `PublicSite:WebBaseUrl`, CORS for 3112.

NOT RUN
- The 30 s refresh of the Recovery board against the real API (RA-E1b, BLOCKED).
- The courier/SMS/email providers (none configured); the pre-op reminder and follow-up-due sweeps.
- The mobile-360 and responsive checks against the real API.

SCOPE CHECK
- files changed: docs/test-cases/TC-HO-SRG-002.md, docs/gates/HO-SRG-002-TEST.md, docs/backlog/SRG.md, web/tests/e2e/surgery-real-api.spec.ts (new, skipped unless `E2E_REAL_SURGERY=1`), web/tests/components/anaesthesia/gate-strengthening.test.tsx (new)
- production code touched: none (every mutation was restored; `git status` clean before the gate's files were added)
- not committed: the three throwaway facts, the seed JSON, the slot/state files
