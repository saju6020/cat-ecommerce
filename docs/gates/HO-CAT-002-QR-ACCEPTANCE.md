GATE: ACCEPTANCE
HANDOVER: HO-CAT-002 (follow-up: `/cats/{id}` smart-redirect page, §1 Amended 2026-09-27 rows + owner decision 2026-09-27; branch `ho/CAT-002-qr`)
COMMITS: 8738d2e 5b7447a 9558066 6027ee8 d6ce94f 071cbaf 1660019 e8a6d25 01309bc 9f94bee b543474 e66fb3d a209b50 8267265 806f760 1a8c4fd 495e172 (reviewed as `git diff 08e873e..495e172`)
DATE: 2026-09-28
STATUS: PASS

PRIOR GATES (verified from artifacts, not commit messages)
- ARCHITECTURE: PASS (docs/gates/HO-CAT-002-QR-ARCHITECTURE.md — first pass 2026-09-27, delta pass 2026-09-28)
- SECURITY: PASS (docs/gates/HO-CAT-002-QR-SECURITY.md — first pass, delta pass)
- CODE_REVIEW: PASS (docs/gates/HO-CAT-002-QR-CODE_REVIEW.md — first pass, delta pass)
- TEST: PASS (docs/gates/HO-CAT-002-QR-TEST.md; docs/test-cases/TC-HO-CAT-002-QR.md — 38 cases, 36 PASS, 0 FAIL, 2 BLOCKED, 0 NOT RUN).
  The 2 BLOCKED rows (R3 staleTime cache reuse, R4 mock-hook comment wording) are regression rows for
  LOW findings, not criteria; they are not counted as passes and neither is needed for any rule below.

COMMANDS RE-RUN BY THIS GATE (web/, head 495e172, worktree clean before and after)
- `npx tsc --noEmit` — exit 0
- `npx eslint .` — exit 0, no output
- `npx vitest run` — exit 0, 62 files, 259/259 passed
- `npx vitest run tests/unit/cat/cat-smart-redirect-page.test.tsx tests/unit/lib/cat-id.test.ts` — 20/20 passed (12 page + 8 GUID)
- Playwright was not re-run by this gate; e2e evidence is the TEST sheet's recorded runs (I1–I14, full suite 390/390).

CRITERIA (owner decision 2026-09-27 + HO-CAT-002 §1 Amended rows)
- R1 Staff -> /staff/cats/{id}: MET — page.tsx:111-113; unit "staff: redirects to the staff cat profile…" asserts `routerReplace('/staff/cats/{id}')`; e2e "staff lands on the staff cat profile" asserts final URL + h1 (TC I1).
- R2 Staff wins over a co-present customer session: MET — page.tsx:111 checked before :116; unit "staff wins when the browser carries both…" and U19 (idle staff + customer) assert staff URL and `not.toHaveBeenCalledWith('/portal/…')`; e2e "staff wins when…" (TC I7).
- R3 Owner -> /portal/cats/{id}: MET — page.tsx:118 (`isOwner` only from successful `data`); unit "owner (customer)…" asserts portal URL; e2e "a signed-in customer who owns the cat…" (TC I2).
- R4 Signed-in non-owner customer -> /staff/sign-in?next=/staff/cats/{id}: MET — page.tsx:118; unit 404 case and U20 (403) assert the exact `STAFF_SIGN_IN_NEXT_URL` and not the portal; e2e non-owner case via `__forceCatQueryNotOwned` (TC I3). Ownership-check 5xx fails closed to the same URL (unit 500 case; e2e `catServerError`, TC I4).
- R5 Not signed in -> /staff/sign-in?next=/staff/cats/{id}: MET — page.tsx:122; unit "anonymous…" asserts exact URL; e2e anonymous case also completes the staff sign-in round trip back to /staff/cats/{id} (TC I5). `next` built with `sanitizeNextPath(…, '/staff/cats', '/staff/cats')` (page.tsx:100-104) and re-checked by `(auth)/staff/sign-in/page.tsx:39` against `/staff/`.
- R6 Ownership check runs only for signed-in customers; anonymous and staff make no cat call: MET — page.tsx:90-92 `shouldCheckOwnership = validId && isSessionResolved && !isStaffSignedIn && isCustomerSignedIn` passed as `useCat(id, { enabled })`; useCat.ts `enabled: options?.enabled ?? true`. Unit tests for staff, staff idle-timeout, staff+customer, U19, anonymous and invalid id each assert `expect(getCatMock).not.toHaveBeenCalled()`; the owner/404/403/500 cases prove the call does fire for a signed-in customer.
- Supporting: invalid id -> inline error, no redirect, no cat call (unit + e2e I6); session-check 5xx fails closed (unit); staff idle-timeout counts as staff (unit); only `router.replace` used (page.tsx has no `push` call).
- Regression: full vitest 259/259 (this gate); full e2e 390/390 recorded in TC I14.

§1 AMENDED ROW ACCURACY
- The second 2026-09-27 row (owner decision) matches the code: rules (1)/(2), the `useCat` `enabled` gate, 404/403/other = non-owner, 5xx/network fail closed, the mock hook, and its "files touched this pass" list equals `git diff --stat 1660019..b543474` exactly (6 web files + the handover). Its "cat-responsive.spec.ts needed no code change" is also true for that pass.
- The row explicitly supersedes the first row's rules and corrects the first row's three inaccuracies (a)-(c). No unrecorded contradiction found.
- HO-CAT-001 §1 Amended item (a) still reads "anyone else -> sign-in" and "pending". Not updated here, correctly: HO-CAT-001.md is §14 forbidden scope for this handover. Not a finding against this implementation.

FINDINGS
- [LOW] web/lib/validation/catId.ts:7-12 — the doc comment is stale after the owner change. It says the check runs "before firing a single session query" (both session queries still fire; page.tsx:67-72 says so correctly) and that an existence check "would need a network call `/cats/{id}` is not allowed to make at all" (the page now makes `GET /api/cats/{id}` for signed-in customers). Wording only. Not yet in docs/backlog/CAT.md.
- [LOW] docs/test-cases/TC-HO-CAT-002-QR.md:155 — the coverage row "Anonymous visitors and staff trigger no cat call" cites e2e I1, I5 and I7. None of those e2e tests asserts the absence of a request. The criterion is still met on the unit evidence (U1, U3, U4, U8, U9, U19 each assert `getCatMock` not called). This is an evidence overclaim in the sheet, not a gap in behaviour.
- [LOW] The TEST sheet header (line 6) lists the commits under test as ending at b543474, but the gate's own additions (U19/U20, 806f760) came after that. Record-keeping only.
- Already backlogged, not re-raised: docs/backlog/CAT.md rows 1-5 (staleTime cache reuse, the addInitScript "one-shot" wording, the jsdom comment, the invalid-id session requests / no-op encode, the owner note on the missing customer sign-in link).

No BLOCKER or HIGH findings. Under the owner's gate-blocking rule (2026-09-27), the LOWs above go to docs/backlog/CAT.md and do not block.

SCOPE CHECK
- Out-of-scope (§4) work found: none. No `src/**` or backend change. No login, registration or reset UI was built; the page only consumes HO-ACC-002's already-merged session hooks. No new endpoint: it reuses `GET /api/cats/{catId}` through the existing `useCat`.
- Files in allowed scope: 12/16 under `web/**` or `docs/gates/HO-CAT-002-*.md` (§13).
- Files outside the literal §13 list: `docs/handover/HO-CAT-002.md` (a §1 Amended row, the standing convention the handover itself records), `docs/test-cases/TC-HO-CAT-002-QR.md` (a follow-up-suffixed TC sheet the TEST gate is required to write), `docs/backlog/CAT.md` (required by the owner's 2026-09-27 gate-blocking rule). All three are process artifacts, not implementation, and are not treated as scope violations. None is in §14 forbidden scope.

UNDECLARED DEVIATIONS
- none. The mock-side ownership stand-in (`__forceCatQueryNotOwned`) and the `useCat` `enabled` parameter are both declared in the §1 Amended row.
