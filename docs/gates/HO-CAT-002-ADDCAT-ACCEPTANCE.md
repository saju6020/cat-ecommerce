GATE: ACCEPTANCE
HANDOVER: HO-CAT-002-ADDCAT (slice of HO-CAT-002; backlog CAT C-APT1; owner request 2026-10-06 "allow customer to add cat")
COMMITS: 84b5c90 308b3ac 283ecb8 e715f06 ceb0786 3796911 2a58de5 1cfffe9 82ae258 (code and slice docs; branch ho/CAT-002-ADDCAT, range 578dc79..82ae258); gate records 27bc369 d32b39a c783bf8 08aad43 40f40de a260558
DATE: 2026-10-07
STATUS: PASS

First pass. This gate read:
- the slice document in full (§1-§8, §14);
- the two §1 Amended rows it adds to HO-CAT-002 and HO-APT-002;
- the four prior gate records and `docs/test-cases/TC-HO-CAT-002-ADDCAT.md`;
- the source diff of every changed web file and every slice test.

It re-ran the slice unit tests and the staff `cat-form` test at the head: 4 files, 29/29 passed. It did not run Playwright.

Owner rule (2026-09-27): only BLOCKER and HIGH findings fail a gate. MEDIUM and LOW findings belong in `docs/backlog/CAT.md`, which this gate has not edited.

MSW-only evidence is judged as in the HO-APT-002, HO-NTF-002 and HO-SHP-002 ACCEPTANCE records:
- A criterion is MET on the unit and MSW e2e evidence, plus source read for the backend half.
- The live-API run is carried as a condition, not counted as a pass.

The slice document has no §18, §19 or §20. Its acceptance criteria are §8 (1-7), its allowed scope is §4, and its forbidden scope is §14 (which mirrors the parent's). The five-gate sequence is the standing one. No BLOCKED condition is defined, and none was hit or worked around: the slice needed no backend change (§3).

PRIOR GATES (read from docs/gates/ and docs/test-cases/, not from commit messages)
- ARCHITECTURE: PASS (d32b39a).
  - 0 BLOCKER, 0 HIGH.
  - M1 (OwnerId error routed to a hidden field) was fixed in 2a58de5.
  - M2 (the slice document lacked §6, §14 and the cross-reference rows) was fixed in 1cfffe9.
- SECURITY: PASS on the second pass (c783bf8).
  - The first pass (27bc369) FAILED on 1 HIGH, an open redirect through `?next=` normalisation.
  - It was fixed in 3796911 (`bookingReturnPath.ts`), re-fuzzed by the gate, and is pinned by U-RP1 and mutation M1.
- CODE_REVIEW: PASS (08aad43). 0 BLOCKER, 0 HIGH. Its MEDIUMs were dealt with as follows:
  - Double-submit: fixed in 82ae258 and pinned by U-AC11 and mutation M2.
  - POST body unasserted: U-AC5 now asserts the exact body.
  - BookingFlow `next` construction untested: covered by the U-RP2 helper test.
- TEST: PASS (a260558), run at code head 82ae258.
  - The sheet has 33 rows: 33 PASS, 0 FAIL, 0 BLOCKED, 0 NOT RUN. It was written NOT RUN first (40f40de).
  - Full vitest: 715/715.
  - Wide Playwright run: 11 first-run failures, each classified. `cat-vaccinations` E9 is pre-existing (C-APT2) and the rest were flakes. No regression.
- No product code changed after 82ae258: `git diff --stat 82ae258 a260558` touches only the TC sheet and the TEST record.

The TC sheet's unit "Evidence" column is a generic "26/26 pass; name seen as passing". It shows nothing about what each test asserts, so this gate read every test body itself before citing any of them below.

CRITERIA
- AC-1 MET. A signed-in customer opens `/portal/cats/new` from My cats (button and empty state) and registers without entering an owner.
  - E-AC1 (`web/tests/e2e/cat-add-customer.spec.ts`) signs in as a customer. It clicks the My cats "Add a cat" link, asserts `Owner customer ID` has count 0, submits, and asserts the new cat's page heading.
  - E-AC1 passed on chromium and mobile-360 (TC E-AC1 rows).
  - U-AC1 asserts there is no Owner field in customer mode.
  - U-AC5 asserts the exact POST body, including `ownerId: 00000000-0000-0000-0000-000000000000`.
  - The empty-state link on My cats is evidenced by source only: the `EmptyState action` links to `/portal/cats/new` in `web/app/(portal)/portal/cats/page.tsx`. No test reaches it, because the mock is not owner-scoped (a known gap, slice §7). See the LOW finding below.
  - The route sits under the `(portal)` layout's `CustomerSessionGuard`, as the SECURITY record verifies.
- AC-2 MET. Errors are plain language and no typed value is lost.
  - U-AC2: "Enter a name.", nothing sent, Breed kept.
  - U-AC3: date-of-birth error, nothing sent.
  - U-AC4: a server 400 on Name lands on its field, and Name, Breed and Colour are all kept.
  - U-AC8: a page-level MSW 400 keeps Breed, and there is no navigation.
  - E-AC2: the page stays put, `#name-error` reads "enter a name", and Breed is kept.
  - Unmapped and non-400 errors go to `root` with plain-language text (`web/lib/api/errors.ts:10,29,83`).
- AC-3 MET. On success the customer reaches the new cat's page, or returns to `/book` with its parameters.
  - U-AC5: push to `/portal/cats/cat-1`.
  - U-AC6: push to `/book?category=Vaccination&catId=cat-2`.
  - U-RP2: all five booking parameters round-trip through `buildAddCatHref` and then `resolveAddCatReturn`, with `catId` appended.
  - E-AC1: lands on `/portal/cats/{guid}` with the cat's heading.
  - E-AC3: lands on `/book?category=Vaccination&catId={guid}` with "Vaccination services" visible.
  - `BookingFlow.tsx:95-96` passes `searchParams.toString()` to `buildAddCatHref`, as read in the source.
- AC-4 MET. Staff registration (`/staff/cats/new`) is unchanged.
  - `(staff)/staff/cats/new/page.tsx:32` passes no `audience`, and `CatForm` defaults it to `'staff'`.
  - The Owner field renders on `mode === 'register' && audience === 'staff'`, and staff `knownFields` is unchanged (`CAT_FORM_KNOWN_FIELDS`).
  - No `cancelHref` is passed, so no Cancel link appears.
  - `web/tests/unit/cat/cat-form.test.tsx:25` types into "Owner customer ID" and passed in this gate's re-run.
  - `cat-register.spec.ts`, which fills the Owner field, passed in the TEST gate's wide run (TC R6).
- AC-5 MET. No backend, migration or new dependency.
  - `git diff --name-only 578dc79..a260558` lists no path under `src/`, `tests/` or `.ai/`, no `package.json`, no lockfile and no migration (checked by this gate).
- AC-6 MET. An OwnerId server error shows as the form-level message in customer mode.
  - U-AC10 rejects with `OwnerId`/`AccountNotActive` and asserts "This account is not active." is shown, with Name kept.
  - Mechanism: `CUSTOMER_KNOWN_FIELDS` drops `ownerId` (`CatForm.tsx`), so `submitWithServerErrors` routes the error to `root`, which renders at `CatForm.tsx:129-131`.
- AC-7 MET. An unclean `?next=` falls back to the cat page.
  - U-RP1 (`web/tests/unit/cat/booking-return-path.test.ts`) covers each class the AC names. For every payload it asserts both `safeBookingPath(...) === null` and a fallback to `/portal/cats/{id}`. The payloads are:
    - `/book/..//evil` and `/book/./..//evil` (dot-segments);
    - `/book/%2e%2e//evil` and `/book/.%2e//evil` (encoded dots);
    - a backslash payload;
    - `//evil.example/book`;
    - `https://evil.example/book`;
    - `/book/../portal/account`;
    - `/bookXYZ`.
  - U-AC7 runs the same check through the page.
  - Mutation M1 (the post-parse checks removed) made 5 U-RP1 cases fail, so the tests detect the bug.

C-APT1 AND HO-APT-002 AC-1
- In the web app, C-APT1 is closed. A customer with no cats now sees **Add a cat** on the booking cat step (U-AC9 and `CatStep.tsx`) and on My cats (source). They register with no owner field (AC-1) and return to `/book` with every booking parameter and the new `catId` (U-RP2, U-AC6, E-AC3). The booking flow then advances to "Choose a service" (E-AC1) or to the category's services (E-AC3).
- The backend half is evidenced by reading the source, not by running it:
  - `CatsController.RegisterCat` (`src/Api/CatEcommerce.Api/Controllers/CatsController.cs:45`) replaces `request.OwnerId` with `caller.AccountId` for a customer before the command is built.
  - `GET /api/cats` carries `OwnerScope.CatCollection`, which forces the list filter to the caller (`OwnerScope.cs:10-16`, F19).
  - So on the live API, a new customer's list is empty until they add a cat, and the new cat then appears in the booking step.
- That makes the HO-APT-002 AC-1 gap ("new customer registers, adds a cat, books") closed in the web app on mock evidence. Its live end-to-end run is still owed. It is carried with HO-APT-002's own unrun real-API E-A1 (HO-APT-002 §1 Amended (14)) and is not counted as passed here.

FINDINGS
- [LOW] Missing real-API evidence for the empty-GUID owner path.
  - Every e2e case runs against MSW. The mock maps the empty GUID to the fixture customer, which is a model of F19, not proof of it.
  - Existing backend integration tests show that a customer can register a cat (`tests/CatEcommerce.IntegrationTests/Endpoints/AuditImmutabilityTests.cs:116-124`), but they send the caller's **own** `ownerId`, not the empty GUID.
  - No test pins "a customer POST with `ownerId = Guid.Empty` returns 201, owned by the caller". The only evidence is the controller source at line 45.
  - Backlog: add that integration test, or run E-AC1 and E-AC3 against the real API.
- [LOW] web/app/(portal)/portal/cats/page.tsx — the My cats empty-state "Add a cat" link, which AC-1 names, has no test. The mock is not owner-scoped, so the e2e cannot stage it, and no component test renders the page with an empty list. A component test with an empty `useCats` response would close this.
- [LOW] web/app/(portal)/portal/cats/page.tsx:42-44 — the subtitle "Owner sign-in isn't available yet, so this shows every registered cat rather than only yours" is pre-existing, and the slice did not touch it. It is stale against the live API, where the list is caller-scoped. It now sits directly beside the new Add a cat button. Belongs with the owner-scoped list item (out of scope by slice §4).
- [LOW] web/tests/components/booking/cat-step.test.tsx:7 — U-AC9 passes `addCatHref` in as a prop, so it proves rendering only. The `BookingFlow` call site is covered by source read plus U-RP2 on the helper, not by a component test. This repeats the TEST gate's LOW.
- [LOW] docs/handover/HO-APT-002.md — slice §4 lists only "this document" for docs, and parent HO-CAT-002 §13 does not cover another area's handover. Even so, 1cfffe9 adds a §1 Amended row to HO-APT-002.
  - The row is documentation only and declared in the commit.
  - The ARCHITECTURE gate (M2) asked for it.
  - It follows the established cross-handover Amended-row convention (for example, HO-FIL-001's row in HO-CAT-002).
  - This gate does not count it as out-of-scope work. The slice's §4 should have listed it.
- [LOW] docs/handover/HO-CAT-002-ADDCAT.md — the slice uses §4 and §8 where the handover template has §13 and §18, and it has no §19 (gates) or §20 (BLOCKED conditions). Every criterion could still be evaluated as written, so this is not a BLOCKED defect.
- Carried, not re-raised:
  - ARCHITECTURE and CODE_REVIEW LOWs: the mock treats any blank `ownerId` as the customer, `CUSTOMER_CALLER_ID` is a hard-coded copy, and `safeBookingPath` runs twice.
  - SECURITY LOW: `nextPath.ts` hardening (ACC row A-APT1).
  - TEST MEDIUMs: shop-portal E-D3 and the mobile E-AC3 flake.
  - C-APT2: `cat-vaccinations` E9 is pre-existing.

SCOPE CHECK
- files in allowed scope: 19/19 (`git diff --name-only 578dc79..a260558`).
  - 7 web source files, all listed in slice §4.
  - 4 test files, all listed in §7.
  - The slice document.
  - The TC sheet and 4 gate records. Gate-produced, and `docs/gates/HO-CAT-002-*` is in parent §13.
  - The §1 Amended rows in HO-CAT-002, which is the convention.
  - The §1 Amended row in HO-APT-002. Declared and gate-requested (LOW above).
- files outside allowed scope: none counted (see the HO-APT-002 LOW).
- forbidden scope (§14): untouched. There is no change under `src/**`, `tests/**` or `.ai/**`, nor to `nextPath.ts`, `SRS.md`, `USER_STORIES.md`, `package.json` or the lockfile.
- out-of-scope work (§4: photo upload, owner-scoped cat list, backend, migration): none built. The page posts no photo, and the mock list stays unscoped.
- uncommitted `web/public/mockServiceWorker.js` in the worktree: not part of any commit, left untouched, not committed.

UNDECLARED DEVIATIONS
- none found. The Cancel link (`cancelHref`) and the mock's 100-character Name rule are both listed in slice §4. The HO-APT-002 row is declared in 1cfffe9's subject.
