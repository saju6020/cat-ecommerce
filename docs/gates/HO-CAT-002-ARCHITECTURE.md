GATE: ARCHITECTURE
HANDOVER: HO-CAT-002
COMMITS: 2e663e9 c2d941c 7f5301c 03226e9 b546fb1
DATE: 2026-09-25
STATUS: PASS

Sixth pass, a short delta only: `git diff 0efab8c..b546fb1`, 5 commits and 9 files. It checks
whether the fifth-pass findings are resolved, whether HO-CAT-001's C10 is consistent, whether the
owner confirmations are recorded, and whether the web changes raise anything new under HO-CAT-002
§8, §9, §11 or §20. This gate built and ran nothing. The new unit and e2e tests are TEST's to
execute: the catId banner reset, the `useAllVaccines` ceiling, and the soft-navigation Back spec.

FIFTH-PASS FINDINGS
- [HIGH] invented `catId` in the C10 response: **resolved.**
  - `web/lib/api/cats.ts:451-454`: `RecordCatParasiteTreatmentResponse` is now exactly
    `{ treatmentId: string; nextDueOn: string }`.
  - `web/mocks/handlers/areas/cat-profiles.ts:1114`: the mock returns `{ treatmentId, nextDueOn }`.
  - No other `web/` file references the type or reads `catId` from the response.
- [BLOCKER, cleared on owner confirmation] the scope of 3fe983f: **cleared.**
  - On 2026-09-25 the owner confirmed the 3fe983f amendments, relayed through the integrator.
  - The confirmation is recorded in HO-CAT-001 §1 (line 16) and HO-CAT-002 §1 (both Amended rows).
  - The HO-CAT-001 and HO-CAT-002 edits in this delta (2e663e9, c2d941c) stay inside the
    confirmed C10 amendment and owner decision (iii). They apply this gate's own LOWs to that
    amendment and change no other command, endpoint or rule.
- [LOW] select by `TreatmentId`: **resolved.**
  - HO-CAT-001:503 now says
    `cat.ParasiteTreatments.Single(t => t.Id == command.TreatmentId)`.
  - The wording that relied on list order is gone.
- [LOW] `NextDueOn` nullable: **resolved** by owner decision (iii).
  - `RecordCatParasiteTreatmentResult(Guid TreatmentId, DateOnly NextDueOn)` is at HO-CAT-001:505.
  - The §1 row, the §11.5 note and the §12.4 note (lines 749-751) all say "required".
  - The web type is `nextDueOn: string`. The page's state is `string | undefined`, and the
    unreachable "No next-due date" branch is removed.
  - No `DateOnly?` remains anywhere in C10.
- [LOW] the C10 snippet hid F3: **resolved.**
  - HO-CAT-001:724-738 spells out every C10 field from the §11.2 row (line 406). This includes
    the controller-minted `TreatmentId`, `UserContext`, `CorrelationId` and
    `ValidateCommand = true`.
  - These match the RegisterCat snippet (lines 664-666).
  - The failure path is `BadRequest(ValidationResult)`, never `202`.
- [LOW] FormFieldRow's `required` and styling: **resolved.**
  - `web/components/cat/FormFieldRow.tsx:44` forwards `required`.
  - The component now has FieldWithUnit's `focus-visible` outline and
    `error && 'border-danger-ink'`.
- [LOW] the README's `components/common/*` rule: **resolved** by owner decision (ii), which is
  recorded in HO-CAT-002 §1 (the third Amended row).

C10 CONSISTENCY (HO-CAT-001)
- These all agree on a body of exactly `{ treatmentId, nextDueOn }`, with a required `DateOnly`
  computed by the aggregate:
  - the §1 row;
  - the §8.3 entity (line 280, non-null);
  - the §11.2 command row (line 406, `TreatmentId`);
  - the §11.5 handler note and record;
  - the §12.4 endpoint row (line 694);
  - the §12.4 note ("no `catId`") and the snippet;
  - the §12.4 required note;
  - §15 U41 and §16 I10a.
- The §8.2 rule (`AdministeredOn.AddDays(IntervalDays)`) is unchanged.

NEW FINDINGS
- [LOW] `web/app/(staff)/staff/cats/[catId]/parasite-control/page.tsx:41-43` — the `catId` reset
  happens in a `useEffect`, which runs after render. When cat B's data is already in the cache,
  one commit can still paint cat A's banner before the effect clears it.
  - Keying the page subtree by `catId`, or resetting during render on a changed `catId`, avoids
  the flash.
  - The state stays local and ephemeral, as §8.4 requires, so the layering is correct. This is
    CODE_REVIEW's call, and it is not blocking here.
- [LOW] `web/components/common/staff-shell.tsx` — carried: below `sm` the rail shows first-letter
  links, and `NavItem.icon` is still unused. This is an owner design decision.
- [MEDIUM] Paging and CORS assumptions — carried, deferred to TEST and integration, and not
  assumed to pass.

NOTES
- The web changes introduce no new §8, §9, §11 or §20 issues:
  - The frontend never computes next-due. It only formats the value the server returned, in the
    `role="status"` region.
  - `components/**` still imports no hooks.
  - Nothing is invented in the API types or mocks.
- The new test files are under `web/tests/**`.
- There is no new dependency: `package.json` and the lockfile are untouched.

HISTORY
- 1st–3rd pass: see git history of this file.
- 4th pass (`06121d9..c813696`): PASS. It had 1 MEDIUM and several LOWs.
- 5th pass (`d0ed408..631904f`): FAIL.
  - 1 HIGH: `catId` invented in the C10 response.
  - 1 scope BLOCKER, pending owner confirmation of 3fe983f.
  - 5 LOWs.
- 6th pass (`0efab8c..b546fb1`): PASS.
  - The HIGH is resolved.
  - The BLOCKER is cleared by the owner's confirmation on 2026-09-25.
  - All 5 LOWs are resolved.
  - 1 new LOW: the effect-timed banner reset.

SCOPE CHECK
- files in allowed scope: 9/9.
  - 7 are under `web/**`.
  - `docs/handover/HO-CAT-001.md` and `docs/handover/HO-CAT-002.md` are owner-confirmed handover
    amendments, recorded in each §1 on 2026-09-25.
- files outside allowed scope: none
