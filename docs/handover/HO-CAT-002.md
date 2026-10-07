# HO-CAT-002 — Cat Profiles (frontend)

## 1. Identity

| Field | Value |
|---|---|
| Handover ID | **HO-CAT-002** |
| Title | Cat Profiles — frontend slice |
| Date | 2026-09-20 |
| Author | Claude Opus 5 |
| Backlog | [`docs/USER_STORIES.md`](../USER_STORIES.md) §2 — **US-CAT-01 … US-CAT-11** |
| SRS | **UI-1 … UI-8**, NFR-USA-1 … NFR-USA-6, NFR-PRF-1, FR-CAT-1 … FR-CAT-11, FR-POR-2, FR-POR-3 |
| Pairs with | [`HO-CAT-001`](HO-CAT-001.md) — the backend slice. **This handover consumes its API** |
| Baseline commit | `b5b6744` |
| **Approved design** | [`docs/design/cat-profile/`](../design/cat-profile/README.md) — clickable canvas at <https://claude.ai/artifact/5K9wzKPZ2d7xLvhAZ5w1HJ>. **Build to it**; §9 conventions and §11 components describe the same thing in words |
| Amended | **2026-09-22 — by [`HO-APT-002`](HO-APT-002.md), before implementation.** The staff cat page gains a **Book appointment** link to `/staff/appointments/new?customerId=…&catId=…`. Nothing else here changes. |
| Amended | **2026-09-25 — owner decision, resolving HO-CAT-002-ACCEPTANCE's BLOCKER 1. Confirmed by the owner on 2026-09-25** (commit `3fe983f`, per HO-CAT-002-ARCHITECTURE's fifth-pass BLOCKER). A11 ("a parasite treatment records and shows its next-due date") could not be evidenced under HO-CAT-001's original C10 contract, which returned no body. HO-CAT-001 is amended (its own §1 Amended row, same date) so C10's `201` response carries `treatmentId` and a server-computed, **required** `nextDueOn` (the owner confirmed on 2026-09-25 that `nextDueOn` is required, not nullable — `IntervalDays` is always validated, so the aggregate always computes one). §11.7 and §18 A11 below are updated to match: the parasite-control screen now shows the returned `nextDueOn` as visible, accessible text (not only a toast, and never recomputed client-side — the ARCHITECTURE gate's `1a4cac4` finding stands), replacing the earlier "see the medical-history PDF" workaround |
| Amended | **2026-09-25 — owner decision, resolving HO-CAT-002-ACCEPTANCE's UNDECLARED DEVIATIONS.** §16 E6's "Expected" column was written as "Appears in history with its next-due date"; `docs/test-cases/TC-HO-CAT-002.md`'s E6 row instead reads "Appears in status with next-due", with no note explaining the change. The owner accepts the TC sheet's wording as correct and this row as the missing note: HO-CAT-001 has no vaccination-history read endpoint (only Q4, `GET .../vaccination-status`), so a vaccination can only be shown in the status panel, not in a history list. A9 already reflects this ("showing next-due" is satisfied by the status panel); §16 E6's own wording is left as originally written per this handover's owner instruction that TEST, not this slice, owns `docs/test-cases/` and `docs/gates/` |
| Amended | **2026-09-25 — owner decision, accepting HO-CAT-002-ACCEPTANCE's OWNER DECISION item. Confirmed by the owner on 2026-09-25.** The shared-component edits already on this branch are accepted as-is: `components/common/staff-shell.tsx`'s full rewrite of the below-`sm` behaviour (a `w-16` rail plus a Radix nav sheet, `DialogContent variant="sheet-left"`), and `components/ui/dialog.tsx`'s new `variant: 'center' | 'sheet-left'` prop. Both were declared openly (commits `dbba453`…`c813696`, `92a33e5`) and reviewed by ARCHITECTURE (4th pass) and CODE_REVIEW (2nd pass), both `PASS`. `web/README.md` is updated to record this as the one exception to "don't edit `components/ui/*`" and to document the new prop and behaviour, per ARCHITECTURE's LOW finding on the README gap. **The owner additionally confirmed on 2026-09-25 that `components/common/*` stays on the README's "don't edit unless your handover says so" list** (HO-CAT-002-ARCHITECTURE fifth-pass LOW) — this is a new cross-area rule the README now states explicitly, not only implied by the `staff-shell.tsx`/`dialog.tsx` exception above |
| Amended | **2026-09-25 — by the HO-ACC-001 ARCHITECTURE gate fourth pass, integrator-authorised edit on the HO-ACC-001 branch.** §12.3's CORS claim (`UseCorsToAllowAll()` in Development) was stale; corrected in place to describe the explicit `Cors:AllowedOrigins` allow-list HO-ACC-001 actually applies in every environment |
| Amended | **2026-09-27 — by the HO-ACC-002 ARCHITECTURE gate fourth pass, integrator-authorised edit on the HO-ACC-002 branch.** `app/_components/cat/CatProfileDataShell.tsx` (the shared `[catId]/layout.tsx` wrapper around every cat form — edit, weights, vaccinations, and the rest) used to swap `{children}` out for `ErrorState` on `catQuery.isError || !catQuery.data`, discarding whatever draft the nested form held even when TanStack Query still had perfectly good, if stale, cached `data` alongside the error (its own documented refetch-failure behaviour). The realistic trigger: a staff member's idle lock takes over on a focus event (HO-ACC-002 US-ACC-08/ADR-FE-010), and this SAME shell's own `useCat(catId)` query independently 401s on that same event, before the lock's own pause (see the matching Amended row in [`HO-ACC-002`](HO-ACC-002.md) §1) has fully taken hold. Narrowed to `catQuery.isError && !catQuery.data`, matching the same guard already applied to HO-ACC-002's own `isError`-branching pages (the staff-account detail page, the staff-customer detail page, `MergeReview`) — the loading and 404 branches above it are unchanged. `mocks/handlers/areas/cat-profiles.ts` also gains a small, dedicated test-only hook, `forceNextCatQueryToReportSessionInvalid`/`window.__forceCatQuerySessionInvalid` — this module's own endpoints never checked staff-session state at all (HO-CAT-001 predates HO-ACC-001's session system), so `accounts.ts`'s own `__forceStaffIdleExpired` has no effect on `GET /api/cats/{catId}`; this one-shot flag is the mock-side equivalent for that one endpoint, consumed by the very next call, used by HO-ACC-002's own new E-G2 real-browser e2e test (`tests/e2e/staff-auth.spec.ts`) to prove the fix above end to end on this shared layout. |
| Amended | **2026-09-26 — owner decision on HO-CAT-001's gate-fix pass (`ho/CAT-001-gates`), propagated here in the same pass.** `GET /api/cats`, `GET /api/cats/{catId}/weights` and `GET /api/vaccines` now return `PagedResult<T>` (`{ items, pageNumber, pageSize, totalCount }`) instead of a bare array; `pageSize` is capped at 100 server-side (clamped, not rejected) and `pageSize <= 0` is a `400`. `lib/api/cats.ts`'s `PageOf<T>`/`pageSize + 1`/`hasNextPage` workaround (this handover's own §11.1/§20.2-documented finding against HO-CAT-001) is removed and replaced with the real `PagedResult<T>` type; `lib/api/vaccines.ts` follows the same change. `useAllVaccines` now computes the page count from `totalCount` instead of looping on `hasNextPage`. `getCatWeightSeries` (the weight chart's series fetch) pages through in batches of 100 instead of one `pageSize=1000` request, since the server now enforces the cap. `RecordCatWeightRequest` also dropped `recordedByStaffId` (HO-CAT-001 SECURITY fix: the server always uses the caller's own identity) — the weights page's mutation call is updated to match. `DataTable`'s existing `pagination.totalCount` mode (already built for the Accounts side) is reused for the cat/vaccine/weight lists in place of its `hasNextPage` mode; `DataTable` itself is unchanged. See `docs/gates/HO-CAT-001-CODE_REVIEW.md` (2026-09-26, MEDIUM) for the finding this closes |
| Amended | **2026-09-27 — owner decision on HO-CAT-001's ACCEPTANCE gate first pass (A22), propagated here.** The cage-card QR now opens `{PublicSite:WebBaseUrl}/cats/{id}` (HO-CAT-001 §1 Amended, same date) — an absolute URL, where it was previously a bare relative path that could not resolve on a scanning phone. `/cats/{id}` is meant to be a **"smart redirect" page on this web site**: staff → the staff cat profile (`/staff/cats/{id}`), the owner → the portal cat profile, anyone else → sign-in. **Pending, not built in this round**: the page needs HO-ACC-002's session hooks (to tell staff/owner/anyone-else apart), so it ships after HO-ACC-002 merges. Also: Q2 (`GET /api/cats`) now carries `handlingNote` alongside `isHighRisk` (HO-CAT-001 §1 Amended, same date, correcting HO-CAT-001 §12.3 to match this handover's own §3/§11.4) — `CatListItem` (`lib/api/cats.ts`) and the mock handler updated; the staff cat list (`app/(staff)/staff/cats/page.tsx`) now passes the real value into `HighRiskBadge` instead of a hardcoded `null`, though §11.4's compact variant still deliberately does not render the note text in a list row (unchanged design). |

| Amended | **2026-09-27 — owner-authorised change, supersedes §11.4's compact-only design for the staff cat list (superseding the previous row's closing sentence, same date).** The owner decided staff must see handling warnings without opening each cat — carrying `handlingNote` on the list model while the list's badge hid the text (as shipped in the previous row) does not meet that. `HighRiskBadge` (`components/cat/HighRiskBadge.tsx`) gains a third variant, `variant="list"`, used only by the **staff** cat list (`app/(staff)/staff/cats/page.tsx`): the badge plus a truncated line of the handling note underneath, full text on hover/focus via the native `title` attribute (the DOM text itself is never truncated, so a screen reader always reads the whole note regardless of the CSS `truncate` — no `aria-describedby` needed on top of that). `variant="compact"` (the **portal** cat list, customer-facing, `app/(portal)/portal/cats/page.tsx`) is unchanged and still shows the badge only. Checked at 360px (the UI-1 floor) via the existing `cat-responsive.spec.ts`/`cat-a11y.spec.ts` Playwright specs, both projects (`chromium`, `mobile-360`) — no horizontal overflow and no new axe violations; `Table`'s own `overflow-x-auto` wrapper (`components/ui/table.tsx`) contains any row-width growth within the table itself rather than the page. No new dependency. New unit tests in `tests/unit/cat/cat-list-page.test.tsx` assert the note text is shown for a flagged cat and that "High risk" (only) shows when no note is recorded. |

| Amended | **2026-09-27 — by [`HO-FIL-001`](HO-FIL-001.md), integrator-authorised, documentation only (closes the carried `HO-CAT-002-SECURITY` item on the backend; the web page itself is not touched by this row).** HO-CAT-001's photo contract this page consumes has changed (see HO-CAT-001's own matching §1 Amended row, same date, for the full detail): the staff photos page's `POST` body becomes `{ fileId, isPrimary }` — no more `storageKey`/`contentType`/`sizeInBytes`/`fileName` supplied by the browser — and `fileId` must first be obtained from a real upload through `POST /api/staff/files?purpose=CatProfilePhoto` (multipart, within `files.maxUploadMb`) before the attach call, within 30 minutes of that upload. `GET /api/cats/{catId}`'s `photos[]` shape is now `{ id, fileId, url, contentType, sizeInBytes, originalFileName, urlExpiresOn, isPrimary, uploadedOn }` — `url` is a short-lived, directly-fetchable signed link, not a `storageKey` the page resolves itself; the page must re-request the cat profile (or otherwise get a fresh `url`) once `urlExpiresOn` passes rather than caching the link indefinitely. §11.6 and §20.4 below, which describe the pre-HO-FIL-001 upload flow (a client-supplied `storageKey`), are superseded for the photo fields specifically. **The upload widget itself (`useFileUpload()`/`<FileUploader>`) is HO-FIL-002's** — this page should consume it once it exists rather than building a second client upload path (HO-FIL-002 §14 forbids HO-FIL-002 from touching any page itself). Actually updating `web/lib/api/cats.ts`, the photos page and the MSW mocks to this contract is a separate, scheduled piece of work under HO-CAT-002/HO-FIL-002, not done by this row — until that lands, the staff Photos page fails its `POST` with a `400` against the real backend (the request body it currently sends no longer matches `AddCatPhotoRequest`). |
| Amended | **2026-09-27 — the `/cats/{id}` smart-redirect page is now built** (`ho/CAT-002-qr`, branched from master `08e873e`, after HO-ACC-002 merged), closing the pending item this §1 Amended's own 2026-09-27 row (the one starting "The cage-card QR now opens…") and HO-CAT-001 §1 Amended's matching 2026-09-27 row (item (a)) both left open. `web/app/cats/[id]/page.tsx` is a Client Component that calls `useStaffSession()`/`useCustomerSession()` (HO-ACC-002 §8.4/§11.5) — the same two session queries `web/app/page.tsx` already asks — and `router.replace`s (never `push`) to: a signed-in **staff** member → `/staff/cats/{id}`; otherwise a signed-in **customer** → `/portal/cats/{id}`, **whether or not they own this cat**; anyone else → `/sign-in?next=/portal/cats/{id}`, the `next` value built AND re-validated with the existing `sanitizeNextPath()` (`lib/validation/nextPath.ts`, ACC-002's own open-redirect guard) rather than a second, hand-rolled check. **If a browser carries both a staff and a customer session (e.g. a shared clinic device), staff wins** — checked first, mirroring `app/page.tsx`'s own session-priority order rather than inventing a second convention for the same question. <br><br>**This page never calls `GET /api/cats/{id}`, for any audience, including a signed-in one** — the owner's own instruction that started this work is explicit that a customer who scans a card for a cat they don't own must still land on `/portal/cats/{id}` and see THAT page's existing not-found/forbidden rendering (the query-filter enforcement HO-CAT-001 §20.3 already documents), rather than this page adding a second, new "does this caller own this cat" backend call HO-CAT-001 does not specify (§20.2/§20.7) — chosen because it is the only option that both matches the owner's explicit instruction and adds no contract HO-CAT-001 hasn't already agreed to. This is also what keeps the page from ever leaking whether `{id}` resolves to a real cat to an anonymous visitor: an invalid-format id, a well-formed id for a real cat, and a well-formed id for no cat at all are all indistinguishable to this page, on purpose. <br><br>A **network error or a real `5xx`** resolving either session query is treated exactly the same as "not signed in" (fails closed to `/sign-in`) — the same convention `CustomerSessionGuard`/`StaffSessionGuard`/`app/page.tsx` already use, deliberately not a new, separate "couldn't check your session" screen. An **id that is not a GUID** (`lib/validation/catId.ts`, new — checked against HO-CAT-001's own `{catId:guid}` route constraint) is rejected before either session query's result is used and before any redirect fires, showing an inline `ErrorState` instead — this never reaches a session round trip or a sign-in bounce for a link that could only ever 404 at the end of it. <br><br>**Tests**: `tests/unit/lib/cat-id.test.ts` (the GUID check), `tests/unit/cat/cat-smart-redirect-page.test.tsx` (staff, staff idle-timeout, owner, staff-wins-over-customer, anonymous, loading, a real session-check failure, invalid id — mocked at the `lib/api/{auth,staff-auth}.ts` layer, real hooks), `tests/e2e/cat-smart-redirect.spec.ts` (the same branches through the real MSW cookie-sign-in flow, both Playwright projects, asserting the final URL and that the intended screen rendered). `tests/e2e/cat-a11y.spec.ts` and `tests/e2e/cat-responsive.spec.ts` both gained dedicated `/cats/{id}` cases (staff and portal), each asserting the redirect's final URL before running axe / measuring overflow — `/cats/{id}` could not join either file's existing generic per-screen loop, since both pick an audience from the URL's own `/staff/`·`/portal/` prefix, which `/cats/{id}` has neither of. <br><br>**Every file this row touches is under `web/**`, already within §13** — no file outside §13 was touched. Ports: dev server run on `PORT=3108` for this worktree, matching `playwright.config.ts`'s existing `PORT`-driven `baseURL`; no server left running afterwards. |
| Amended | **2026-09-27 — owner decision, changing two of `/cats/{id}`'s rules after this page's own ARCHITECTURE/SECURITY/CODE_REVIEW gates had already passed it** (`071cbaf`, `d6ce94f`, `1660019`). This row supersedes the earlier "anyone else → `/sign-in?next=/portal/cats/{id}`" and "a signed-in customer → `/portal/cats/{id}`, whether or not they own this cat" behaviour from this §1 Amended's previous 2026-09-27 row (same date, still above). **(1) Anyone not signed in now goes to STAFF sign-in, not customer sign-in**: `/staff/sign-in?next=/staff/cats/{id}` — staff scan cage cards far more often than owners do. The `next` value is built with `sanitizeNextPath()` against the `/staff/cats` prefix, which `(auth)/staff/sign-in`'s own broader `/staff/` accepted-prefix check (`app/(auth)/staff/sign-in/page.tsx`) then honours on the way back in, landing the visitor on `/staff/cats/{id}` after a real sign-in. **(2) A signed-in customer who does NOT own the cat now goes to that same `/staff/sign-in?next=/staff/cats/{id}` destination, not `/portal/cats/{id}`.** The owner still goes to `/portal/cats/{id}`. <br><br>Ownership is now checked, for a signed-in customer only, by reusing `useCat(catId)` — the exact same hook and the exact same `GET /api/cats/{catId}` call `app/(portal)/portal/cats/[catId]/page.tsx` already makes; no new endpoint. `useCat` (`lib/hooks/useCat.ts`) gained a second, optional `{ enabled }` parameter (defaults to `true`, so every other existing call site is unaffected) so `/cats/{id}` can gate this call to fire **only when the visitor is a signed-in, non-staff customer** — never for staff (who win outright before ownership is even considered, and trigger no cat call) and never for an anonymous visitor (who must trigger no cat call at all, so `{id}` never leaks to someone with no session). A successful response is the only outcome treated as ownership; a `404`, a `403`, or any other response that can't tell "doesn't exist" apart from "not yours" is treated as non-owner; a `5xx` or a network error from the ownership check itself fails closed to the same `/staff/sign-in?next=/staff/cats/{id}` destination a confirmed non-owner gets — the same "don't trust a failed check" logic `CustomerSessionGuard`/`StaffSessionGuard`/this page's own session-resolution handling already apply. <br><br>**Mock-side, for e2e (`mocks/handlers/areas/cat-profiles.ts`)**: this module has no way to compute real ownership (it never reads what `pc_session` maps to — that's `accounts.ts`'s own private state, and the README's "don't edit another area's `mocks/handlers/areas/*.ts`" rule is why this module doesn't reach into it). A new one-shot test hook models "signed-in customer, not the owner" as a `404` — but unlike the existing `forceNextCatQueryToReportSessionInvalid`/`__forceCatQuerySessionInvalid` hook this mirrors (which a test calls on an already-loaded page before an in-place, no-navigation refetch), `/cats/{id}`'s ownership check fires on first mount, right after a FULL navigation — so a same-page `window.*` function call set beforehand is torn down with the old page before it's ever read. The new hook instead reads a plain `window.__forceCatQueryNotOwned` boolean directly on every `GET /api/cats/{catId}` call (`consumeForceCatQueryNotOwned()`, one-shot, cleared on read), which an e2e test sets via Playwright's `addInitScript()` — evaluated in the NEXT document before any of its own scripts, so the flag is already present when this module's fresh instance re-initialises there. Not `localStorage`/a cookie (no web storage) — `addInitScript` is a Playwright test-harness mechanism scoped to that one browser context, not app state. The pre-existing `catServerError` fixture (always a `500` on `GET`) is reused as-is for the ownership-check-5xx case; no second force-flag needed for that one. <br><br>**Tests updated for every branch — staff; owner; non-owner customer; anonymous; the ownership-check `5xx`; invalid id; both sessions (staff wins)**: `tests/unit/cat/cat-smart-redirect-page.test.tsx` (mocks `@/lib/api/cats`'s `getCat` alongside the two session endpoints, same layer-mocking pattern as before) and `tests/e2e/cat-smart-redirect.spec.ts` (real MSW, both Playwright projects) both gained the two new branches and updated every destination the first two rules above changed; `tests/e2e/cat-a11y.spec.ts`'s `/cats/{id}` cases gained the non-owner and anonymous cases (four total now: staff, owner, non-owner, anonymous) with the same final-URL-then-axe pattern as before. `tests/e2e/cat-responsive.spec.ts` needed **no code change** — its two existing `/cats/{id}` cases (in `STAFF_SCREENS`/`PORTAL_SCREENS`) already covered the "staff" and "owns the cat" destinations, and neither of those two destinations changed. <br><br>**Corrections to this §1 Amended's own previous 2026-09-27 row, per this pass's ARCHITECTURE gate LOWs**: (a) that row's "an id that is not a GUID... is rejected before either session query's result is used" was read as implying the two session requests are never SENT for an invalid id — they still are (`useStaffSession()`/`useCustomerSession()` are unconditional hook calls; React cannot call a hook conditionally); only their results go unused. The page's own doc comment and this handover both now say so explicitly. (b) That row also said `/cats/{id}` "could not join either file's existing generic per-screen loop" for BOTH `cat-a11y.spec.ts` and `cat-responsive.spec.ts` — true only for the former (a `test.describe` block of its own, since axe's loop has no per-item audience). `cat-responsive.spec.ts` in fact DOES reuse its existing `STAFF_SCREENS`/`PORTAL_SCREENS` arrays and its one shared `assertNoHorizontalScroll` helper, just with a small union type addition (`{ path, expectFinalUrl }` alongside a bare path) — it never needed a separate block. (c) That row also claimed "every file this row touches is under `web/**`, already within §13" — false of the row's own file: this row, like the one it corrects, lives in `docs/handover/HO-CAT-002.md`, under `docs/`, not `web/**`. Recording an amendment in this handover's own §1 is the standing convention this whole section exists for, not a §13 scope item — every OTHER file this row (and the one before it) touches is under `web/**`, already within §13. <br><br>**Files touched this pass, all under `web/**` except this row itself**: `app/cats/[id]/page.tsx`, `lib/hooks/useCat.ts` (new optional `enabled` parameter), `mocks/handlers/areas/cat-profiles.ts` (new `consumeForceCatQueryNotOwned` test hook), `tests/unit/cat/cat-smart-redirect-page.test.tsx`, `tests/e2e/cat-smart-redirect.spec.ts`, `tests/e2e/cat-a11y.spec.ts`. Ports: dev server run on `PORT=3108` for this worktree; `--workers=1`, both Playwright projects; no server left running afterwards. |
| Amended | **2026-10-06 — by [`HO-CAT-002-ADDCAT`](HO-CAT-002-ADDCAT.md) (backlog C-APT1, owner request).** The owner portal gains an add-a-cat screen, `/portal/cats/new`, and `CatForm` an additive `audience: 'staff' | 'customer'` prop (customer hides the owner field; an `OwnerId` server error shows as the form-level message) plus an optional `cancelHref`. The "Add a cat" button is added to `/portal/cats`, including its empty state. Staff registration is unchanged. Files, tests and acceptance are in the slice document; no backend change, no new dependency. |
*(append further rows here)*

---

## 2. Objective

Build the **Cat Profile user interface** for the same eleven stories the backend slice covers.
HO-CAT-001 §4.1 deliberately deferred four acceptance criteria that are UI behaviour and satisfied
only the server half of each. **This handover is where those four are actually met**, alongside the
screens for every other Cat Profile story.

This is also the **first frontend code in the repository** — there is no `package.json`, no
component, no build. The scaffold in §12.1 is part of this handover and every later frontend slice
inherits it, so its conventions matter more than this feature does.

The single most important artifact here is the **cat profile header** (§11.3). SRS **UI-7** requires
it on every cat screen, and it is what carries the allergy list (US-CAT-03) and the high-risk
handling warning (US-CAT-04) in front of a clinician before they touch the animal.

---

## 3. In scope

### 3.1 Decisions already taken

Settled with the business owner before this document was written. Do not revisit them mid-build;
if one turns out to be wrong, that is a §20 stop.

| Decision | Choice |
|---|---|
| Framework | **Next.js 15 (App Router) + React 19 + TypeScript**, fixed by `.claude/agents/frontend-developer.md` |
| Styling | **Tailwind CSS v4**, same source |
| Components | **shadcn/ui** — copied into the repo, not imported. Radix primitives underneath, which is what makes UI-6 achievable |
| Server state | **TanStack Query v5** |
| App structure | **One app, role-gated route groups** — `(staff)` and `(portal)` sharing components, types and the API client |
| Identity | **A single hardcoded staff identity.** No login, no role switcher — see §4.2 and §20.3 |

### 3.2 Stories

All eleven, as user interface. The backend capability each depends on is already specified in
HO-CAT-001 §12.3/§12.4.

| Story | Screens delivered |
|---|---|
| US-CAT-01 | Register-cat form with photo field and validation; profile edit; approximate-age display |
| US-CAT-02 | Neuter status form; weight entry; **weight trend chart** |
| US-CAT-03 | Medical profile editor; **allergies in the persistent profile header**, with an explicit "none recorded" |
| US-CAT-04 | High-risk flag set/clear with reason; **warning badge on every cat screen** |
| US-CAT-05 | Vaccination entry against the catalogue; batch fields; vaccination history |
| US-CAT-06 | Parasite-control entry with next-due display |
| US-CAT-07 | **Vaccination status panel** — Current / Due soon / Overdue, with date, booking link and the boarding warning |
| US-CAT-08 | Ownership transfer with confirmation (UI-3) |
| US-CAT-09 | Mark deceased with confirmation; deceased banner; deceased excluded from the default list |
| US-CAT-10 | Medical-history PDF download |
| US-CAT-11 | Printable cage card with QR |

Plus the vaccine catalogue screens (list, register) that US-CAT-05 depends on.

### 3.3 The four criteria HO-CAT-001 deferred here

These are the reason this handover exists. Each is judged in §18.

| From | Deferred UI behaviour | Server half already delivered |
|---|---|---|
| US-CAT-03 | Allergies shown in a persistent clinical header | `allergies[]` on `GET /api/cats/{catId}` |
| US-CAT-03 | Header reads "none recorded" rather than blank | `hasRecordedAllergies: false` |
| US-CAT-04 | Warning badge on clinic / boarding / order screens | `isHighRisk` + `handlingNote` on profile **and** list |
| US-CAT-07 | "Due soon" shows a booking link | `nextDueOn` + `bookingEligible` |

---

## 4. Out of scope

Building anything here is a **FAIL** at the acceptance gate.

### 4.1 Not this slice

| Excluded | Why |
|---|---|
| Any backend change | HO-CAT-001 owns the whole server. A missing field is a §20.2 stop, not a reason to edit the API |
| Login, registration, password reset | §1 Accounts. Identity is hardcoded — §4.2 |
| Booking, ordering, boarding, clinical notes screens | Their own contexts. US-CAT-07's booking link points at a route that does not exist yet (§20.5) |
| Global search (UI-2) | Spans customers, orders, invoices and products — none of which have an API |
| CSV export (UI-4) | Needs an export endpoint HO-CAT-001 did not specify. List filtering, sorting and pagination **are** in scope |
| Auto-refresh (UI-5) | Applies to clinic queue, boarding occupancy and order fulfilment. No cat screen is in that list |
| Mobile apps | SRS §2.4 — Phase 2 |
| Dark mode, theming, i18n | Not required by any Cat Profile story |

### 4.2 Constrained by decisions taken elsewhere

**Identity is one hardcoded staff user.** `lib/identity.ts` exports a single vet identity. There is
no login, no role switcher and no way to become a cat owner in the UI. Consequences, all recorded
in §20.3:

- the `(portal)` route group is **built and reachable by direct URL**, but always renders as that
  staff user rather than as an owner;
- **US-CAT-08 AC2** — "the previous owner can no longer view the cat" — **cannot be demonstrated
  from the UI.** It is an API-level guarantee (and a weak one: HO-CAT-001 §20.3 records that it is
  a query filter, not authorization);
- no role-conditional rendering. Build every screen as the staff view, with the portal views as
  presentation variants rather than permission-gated ones.

**Photo upload does not upload anything.** HO-CAT-001 §4.2 records that object storage is not
provisioned, so `POST /api/cats/{catId}/photos` takes a **storage key plus metadata**, not a file.
See §11.6 for what to build instead. Do not add an upload endpoint, an S3 client, or a local file
write — that is backend scope.

---

## 5. Prerequisites

**Two are missing. Read §20.1 and §20.2 before starting.**

| Required | State |
|---|---|
| Node.js 20 LTS or newer + npm | ❌ **not installed** — `node` and `npm` are not on PATH. Nothing can be built until this is resolved. §20.1 |
| The HO-CAT-001 API | ❌ **not implemented.** The handover is written and approved; no code exists. §20.2 |
| PostgreSQL / Docker | ✅ available, but irrelevant here — the frontend never touches the database |
| A browser | For manual verification of the responsive and accessibility criteria |

### 5.1 You are building against a contract, not a running API

The backend does not exist yet, and waiting for it would serialise two slices that can run in
parallel. **Develop against Mock Service Worker handlers derived from HO-CAT-001 §12.3 and §12.4**
(§12.5 here). That contract is detailed enough to mock faithfully — every endpoint, view model
field and error code is specified.

Two rules that keep this from going wrong:

1. **The mocks are generated from the contract, not from what is convenient.** If a screen needs a
   field HO-CAT-001 does not specify, you may not invent it in a mock. That is a §20.2 stop.
2. **Every §16 case is re-run against the real API once it lands**, and the sheet records both. A
   case that passed only against a mock has verified your mock, not the system.

---

## 6. Governing documents

| Document | What you need from it |
|---|---|
| [`HO-CAT-001`](HO-CAT-001.md) | **§12.3 view models, §12.4 endpoints and status codes, §10 violation codes.** This is the API contract; read it as the specification it is |
| [`docs/USER_STORIES.md`](../USER_STORIES.md) §2 | The eleven stories and their acceptance criteria |
| [`docs/SRS.md`](../SRS.md) §3.1 | **UI-1 … UI-8.** UI-6 and UI-7 are load-bearing here |
| [`docs/SRS.md`](../SRS.md) §5 | NFR-USA-1 … NFR-USA-6, NFR-PRF-1 |
| [`.claude/agents/frontend-developer.md`](../../.claude/agents/frontend-developer.md) | Stack, and the standing rules on component size and design tokens |
| [`docs/USER_STORIES.md`](../USER_STORIES.md) Definition of Done | Error and empty states implemented; works at 360 px; WCAG 2.1 AA on every screen touched |

There is no `.ai/` document for the frontend — `.ai/` describes the .NET backend only. **Do not
apply its CQRS, aggregate or dispatch rules to React code**; they do not transfer, and a component
named `CatAggregate` would be a category error. §8 is this slice's architecture.

---

## 7. The SRS UI requirements, as build rules

Every one applies to screens in this slice. They are not aspirational.

| Req | Rule | Where it is enforced |
|---|---|---|
| **UI-1** | Layouts work at **360 / 768 / 1024 / 1440 px** | Every screen. 360 px is the hard floor — no horizontal scroll, no clipped controls |
| **UI-3** | Destructive actions confirm, **stating exactly what will be affected** | Mark deceased, transfer ownership, clear high-risk flag, remove a medication |
| **UI-4** | Every list filters, sorts and paginates | Cat list, vaccine list, weight history, vaccination history |
| **UI-6** | **WCAG 2.1 AA** — keyboard operable, visible focus, 4.5:1 contrast, labelled fields, **announced errors** | Every screen. §8.5 |
| **UI-7** | The cat profile header **always** shows name, photo, age, sex/neuter, latest weight, allergies, chronic conditions and active alerts | `CatProfileHeader`, rendered from the `[catId]` layout so no child route can omit it |
| **UI-8** | Weight and dose fields show **units, always visible**, never bare numbers | `FieldWithUnit`; weight entry, weight chart axes, medication dosage |
| **NFR-USA-4** | Errors say what went wrong and what to do next, **never exposing stack traces or internal identifiers** | §11.8 error mapping. A raw GUID or an error code in the UI is a defect |
| **NFR-USA-5** | Forms longer than one screen preserve data across validation errors and accidental navigation | Register-cat and medical-profile forms |

---

## 8. Frontend architecture

### 8.1 Where it lives

```
web/                      ← repo root, sibling to src/. Not inside src/, which is the .NET solution
```

### 8.2 Layering

Four layers, one direction of dependency. A component never calls `fetch`; a query hook never
renders.

```
app/          route segments, layouts, page composition — thin
components/   presentational + form components, no data fetching
lib/hooks/    TanStack Query hooks — the only place queries and mutations are declared
lib/api/      typed fetch client + endpoint functions + contract types
```

**Rules:**
1. **`lib/api/types.ts` mirrors HO-CAT-001 §12.3 exactly** — same field names, same nullability.
   No renaming to taste. When the real API lands, a mismatch must be a compile error, not a
   silently undefined field.
2. **Only `lib/api/client.ts` calls `fetch`.** It owns the base URL, identity headers, and the
   `400 → ValidationResponse` parsing.
3. **Only `lib/hooks/` declares `useQuery` / `useMutation`.** A `useQuery` inside a component is
   how cache keys drift and refetch storms start.
4. **Components take props and callbacks.** A component that imports a hook to fetch its own data
   cannot be tested or reused. The page wires them together.
5. **Server Components for static shell, Client Components for anything interactive.** Mark
   `"use client"` at the smallest boundary that needs it, not on a layout.

### 8.3 Route map

```
web/app/
  layout.tsx                           root: fonts, Tailwind, QueryProvider, Toaster
  page.tsx                             → redirect('/staff/cats')

  (staff)/
    layout.tsx                         back-office shell: sidebar, header, <main> landmark
    staff/
      cats/
        page.tsx                       cat list — filter, sort, paginate        US-CAT-09, UI-4
        new/page.tsx                   register a cat                           US-CAT-01
        [catId]/
          layout.tsx                   ⭐ CatProfileHeader + tab nav             UI-7
          page.tsx                     overview
          edit/page.tsx                edit identity fields                     US-CAT-01
          identification/page.tsx      neuter status + date                     US-CAT-02
          medical/page.tsx             allergies, conditions, medications       US-CAT-03
          weights/page.tsx             weight history + trend chart             US-CAT-02
          vaccinations/page.tsx        vaccination history + record             US-CAT-05
          vaccination-status/page.tsx  Current / Due soon / Overdue             US-CAT-07
          parasite-control/page.tsx    treatments + next due                    US-CAT-06
          photos/page.tsx              photo references                         US-CAT-01
          cage-card/page.tsx           printable QR card                        US-CAT-11
      vaccines/
        page.tsx                       catalogue list
        new/page.tsx                   register a vaccine

  (portal)/
    layout.tsx                         owner-facing shell — simpler chrome
    portal/
      cats/
        page.tsx                       my cats                                  US-CAT-01
        [catId]/
          page.tsx                     profile + vaccination status             US-CAT-07
          medical-history/page.tsx     PDF download                             US-CAT-10
```

> The `[catId]/layout.tsx` placement is deliberate and is **how UI-7 and US-CAT-04 AC1 are
> actually guaranteed**. Putting the header in the layout means every child route renders it, and
> a new tab added next year cannot forget the allergy list or the high-risk badge. Do not move it
> into the individual pages.

### 8.4 State

| Kind of state | Where it lives |
|---|---|
| Server data — cats, vaccines, status | **TanStack Query.** Never copied into `useState` |
| Form state | **react-hook-form + zod**, resolver-validated |
| Ephemeral UI — dialog open, active tab | `useState`, local to the component |
| URL-owned — filters, sort, page | **`useSearchParams`**, so a filtered list is linkable and survives reload (NFR-USA-5) |

**No Redux, Zustand, Context-as-store or any other global store.** There is no state in this slice
that is not server data, form state or a URL parameter. Introducing one is an architecture decision
and a §20 stop.

Query key convention, so invalidation is predictable:

```ts
['cats', filters]                     ['cat', catId]
['cat', catId, 'weights']             ['cat', catId, 'vaccination-status']
['vaccines', filters]
```

A mutation invalidates `['cat', catId]` and every narrower key beneath it. Recording a vaccination
**must** also invalidate `['cat', catId, 'vaccination-status']` — the status is derived server-side
and will otherwise show a stale badge (§16, I9).

### 8.5 Accessibility — UI-6, non-negotiable

Not a review-stage polish item. Build it in.

- **Semantic HTML first.** `<button>` for actions, `<a>` for navigation, real `<form>`, one `<h1>`
  per page, `<main>` / `<nav>` landmarks. ARIA only where semantics run out.
- **Every input has a real `<label>`** tied by `htmlFor`. Placeholder text is not a label.
- **Errors are announced.** Field errors go in `aria-live="polite"` and are linked by
  `aria-describedby`. A red border alone is invisible to a screen reader and fails UI-6's
  "announced errors" clause outright.
- **Keyboard operable end to end.** Every dialog traps focus, closes on `Escape`, and returns focus
  to its trigger. shadcn/ui's Radix primitives give this — which is why the library was chosen.
  Hand-rolled dialogs do not.
- **Visible focus.** Never `outline: none` without a replacement ring.
- **4.5:1 contrast** on all text, including the status and high-risk badges. §11.4 has the palette
  note — a red-on-amber warning badge is the easy way to fail this.
- **Colour is never the only signal.** Vaccination status and the high-risk flag each carry an icon
  and a text label, not just a colour (§11.4, §11.5).

---

## 9. Design conventions

- **Tailwind utilities, with tokens for anything semantic.** No hex literals in components. Status
  colours are defined once in `app/globals.css` as CSS variables and consumed by name.
- **shadcn/ui components are copied in and then owned.** Customise them in place; do not wrap a
  shadcn component in a near-identical component of your own.
- **Component files stay small.** A page that exceeds roughly 150 lines is composing too much
  itself — extract.
- **Loading, empty and error are three distinct states**, every one implemented (Definition of
  Done). `EmptyState` says what to do next, not "No data".
- **Dates** — `date-fns`, formatted `dd MMM yyyy`. `DateOnly` from the API is an ISO `yyyy-MM-dd`
  string; never parse it into a `Date` in a way that applies a timezone shift. A cat's date of
  birth moving a day when the browser is in `UTC-6` is the classic version of this bug.

---

## 10. Dependencies

Permitted by this handover, and only these:

| Package | Purpose |
|---|---|
| `next`, `react`, `react-dom` | Framework |
| `typescript`, `@types/*` | Types |
| `tailwindcss` v4, `@tailwindcss/postcss` | Styling |
| `@tanstack/react-query` v5 | Server state |
| `react-hook-form`, `zod`, `@hookform/resolvers` | Forms and validation |
| `class-variance-authority`, `clsx`, `tailwind-merge` | shadcn/ui dependencies |
| `@radix-ui/*` | Pulled in per shadcn/ui component |
| `lucide-react` | Icons |
| `recharts` | The weight trend chart, US-CAT-02 AC2 |
| `date-fns` | Date formatting |
| `msw` | Mock Service Worker, §5.1 (dev + test only) |
| `vitest`, `@testing-library/react`, `@testing-library/user-event`, `jsdom` | Component tests |
| `@playwright/test` | End-to-end, §16 |
| `eslint`, `eslint-config-next`, `prettier` | Lint and format |
| `@axe-core/playwright` | Automated accessibility checks, §16 |

Anything else needs approval. A date-picker or data-grid library in particular is an architecture
decision — shadcn/ui covers both.

---

## 11. Specified changes — components and screens

### 11.1 API types — `lib/api/types.ts`

Mirror HO-CAT-001 §12.3 exactly. The types this slice needs:

`CatProfile` · `CatListItem` · `CatWeightPoint` · `CatVaccinationStatus` ·
`CatVaccinationStatusItem` · `CatMedicalHistory` · `VaccineListItem` · `CatCageCard` ·
`Allergy` · `ChronicCondition` · `Medication` · `CatPhoto` · `PagedResult<T>` ·
`ValidationResponse` · `ValidationError`

```ts
// The error shape every 400 returns — HO-CAT-001 §12.4
export interface ValidationError {
  propertyName: string;
  errorCode: string;      // HO-CAT-001 §10 — never shown to the user
  errorMessage: string;
  resourceName: string;
}
export interface ValidationResponse { errors: ValidationError[]; isValid: boolean; }
```

### 11.2 API client — `lib/api/client.ts`

One `fetch` wrapper. Responsibilities, and nothing beyond them:

- base URL from `NEXT_PUBLIC_API_BASE_URL`
- attach the hardcoded identity headers from `lib/identity.ts`
- `200`/`201` → parsed JSON
- **`400` → throw a typed `ApiValidationError` carrying `ValidationResponse`** so forms can map it
  per field (§11.8)
- `404` → typed `ApiNotFoundError`, so a page renders "not found" rather than an error boundary
- **`409` → typed `ApiConflictError`.** HO-CAT-001 §12.4 returns this for a concurrent edit
  (`xmin`). The UI must tell the user someone else changed this record and offer to reload —
  silently discarding their edit is the failure mode that loses clinical notes
- `5xx` → generic `ApiError`; the message shown is ours, never the server's (NFR-USA-4)

`lib/identity.ts` — the single hardcoded identity, in **one** module with a comment naming §20.3,
so replacing it when auth lands is a one-file change.

### 11.3 ⭐ `components/cat/CatProfileHeader.tsx`

**The most important component in this slice.** UI-7, US-CAT-03 AC1, US-CAT-04 AC1. Rendered from
`app/(staff)/staff/cats/[catId]/layout.tsx`, so it is present on every cat screen.

Shows, in this order of visual priority:

1. **High-risk warning** — `HighRiskBadge` with the handling note, if `isHighRisk`. Top of the
   header, before the name. A clinician must see it before anything else
2. **Deceased banner** — if `lifeStatus === 'Deceased'`, with the date (US-CAT-09)
3. Photo (primary, or a placeholder), name, species, breed, colour
4. **Age** via `AgeDisplay` — approximate when estimated (US-CAT-01 AC2)
5. Sex and neuter status
6. **Latest weight with its unit** (UI-8) — "4.2 kg", never "4.2"
7. **Allergies** — chips when present; the literal text **"None recorded"** when
   `hasRecordedAllergies` is false. **US-CAT-03 AC2: never render blank space.** An empty region
   reads as "not checked"; "None recorded" reads as "checked, and there are none"
8. **Chronic conditions** — same treatment
9. Vaccination status badge (§11.5)

Responsive: stacks vertically at 360 px with the high-risk warning still first (UI-1).

### 11.4 `components/cat/HighRiskBadge.tsx`

US-CAT-04 AC1. Icon + the text "High risk — handle with care" + the handling note. **Icon and text,
not colour alone** (UI-6). Contrast ≥ 4.5:1 against its background — check the amber/red
combination specifically; it is the usual way this requirement is failed.

Appears in the profile header **and** as a compact variant in the cat list row, because the story
says "on any clinic, boarding or order screen" and a list is one of them.

### 11.5 `components/cat/VaccinationStatusBadge.tsx` and the status panel

US-CAT-07, all three criteria.

| `overallStatus` | Badge | Panel behaviour |
|---|---|---|
| `Current` | ✓ "Current" | Next due date listed per vaccine |
| `DueSoon` | ⚠ "Due soon" | **The due date**, and a **"Book a vaccination" link** — AC2 |
| `Overdue` | ⛔ "Overdue" | A warning that **boarding will be blocked** — AC3. Drive it from `bookingEligible === false`, not from a client-side date comparison |

The panel lists every core vaccine with its own status and `nextDueOn`, including ones never
administered. Never recompute status in the browser — HO-CAT-001 §12.3 Q4 computes it server-side
against the configured window, and a second implementation here would drift.

The booking link's target route does not exist yet — §20.5.

### 11.6 `components/cat/PhotoField.tsx`

US-CAT-01 AC3, under the constraint in §4.2. **This is unusual; read it carefully.**

`POST /api/cats/{catId}/photos` takes a **storage key + content type + size**, not a file. So:

- Present a real file picker, because that is what the criterion describes and what the user
  expects.
- **Validate the chosen file in the browser** against the same limits the API enforces —
  `image/jpeg`, `image/png`, `image/webp`, ≤ 5 MB (HO-CAT-001 §10). Reject with a clear,
  plain-language reason naming the actual limit: *"That image is 8.2 MB. The maximum is 5 MB."*
  **This is what satisfies AC3.**
- Then, because there is nowhere to upload it, submit the **derived metadata plus a storage key**
  in a dev-only field, and show an honest inline note that image upload is not yet available.
- Server-side rejection must still be handled: the same validation runs in the API validator, and
  a `400` maps back onto this field (§11.8). Client validation is a courtesy, never the guarantee.

**Do not** build an upload endpoint, add an S3 client, or write files locally. §20.4.

### 11.7 Remaining components

| Component | Story / Req | Notes |
|---|---|---|
| `AgeDisplay` | US-CAT-01 AC2 | "3 years 2 months", or **"~3 years (estimated)"** when `dateOfBirthPrecision === 'Estimated'`. Renders "Unknown" when there is no DOB |
| `CatForm` | US-CAT-01 | Register and edit. zod schema mirroring the API validator. DOB precision is a radio pair; an exact DOB is required when "Exact" is chosen |
| `NeuterStatusForm` | US-CAT-02 | Date required and shown only when "Neutered" is selected |
| `WeightEntryForm` | US-CAT-02 | Units visible (UI-8); range 0.10–30.00 kg client-side |
| `WeightTrendChart` | US-CAT-02 AC2 | `recharts` line chart. **Y axis labelled "kg"** (UI-8). Needs an accessible fallback: a visually-hidden table of the same points, because a chart alone fails UI-6 |
| `MedicalProfileForm` | US-CAT-03 | Repeatable rows for allergies, conditions, medications. Submits the whole set — the API replaces wholesale |
| `HighRiskFlagDialog` | US-CAT-04 | Set and clear. **Reason is required** — the API rejects without it. Confirm on clear (UI-3) |
| `VaccinationForm` | US-CAT-05 | Vaccine from the catalogue; batch reference, batch expiry, date given, administering vet. Surface `ExpiredVaccineBatch` as a field error on batch expiry |
| `ParasiteTreatmentForm` | US-CAT-06 | Type, product, date, interval. The **page** (not this form) shows the server-returned `nextDueOn` after saving — HO-CAT-001 C10's `201` body carries it (owner decision 2026-09-25); never recomputed client-side |
| `TransferOwnershipDialog` | US-CAT-08 | UI-3: state exactly what transfers — "all medical history moves with the cat, and the current owner will no longer see this record" |
| `MarkDeceasedDialog` | US-CAT-09 | UI-3: state that reminders stop and the cat is hidden from booking and ordering, **and that the record is kept** |
| `DeceasedBanner` | US-CAT-09 | On every screen for a deceased cat. All edit controls disabled with an explanation, not merely hidden |
| `CageCard` | US-CAT-11 | Print layout with the QR from the API, cat name, owner, and the high-risk warning if flagged. `@media print` rules |
| `DataTable` | UI-4 | Filter, sort, paginate. URL-driven (§8.4). One implementation, used by every list |
| `ConfirmDialog` | UI-3 | Takes an explicit consequence description. No generic "Are you sure?" |
| `FieldWithUnit` | UI-8 | Unit rendered adjacent and always visible, inside the label's accessible name |
| `EmptyState` / `ErrorState` / `LoadingState` | DoD | Skeletons for loading, not spinners, on list and profile screens |

### 11.8 Error mapping — `lib/api/errors.ts`

The bridge between the API's `ValidationResponse` and react-hook-form, and where **NFR-USA-4** is
met or failed.

```ts
// Map propertyName -> form field, then setError per field.
// errorMessage is written for a human by the API; errorCode is NOT shown.
applyValidationErrors(error: ApiValidationError, setError: UseFormSetError<T>): void
```

- `propertyName` matches the command property from HO-CAT-001 §11.2. Map it to the form field
  name; where they differ, keep the mapping in **one** table, not scattered across forms.
- An error whose `propertyName` matches no field becomes a **form-level** message. It must never be
  swallowed — a validation failure with no visible cause is worse than a crash.
- **Never render `errorCode`.** `MicrochipAlreadyRegistered`-style identifiers are for logs.
  NFR-USA-4 forbids exposing internal identifiers.
- Field errors are announced (§8.5).

---

## 12. Specified changes — scaffold, config, mocks

### 12.1 Scaffold — `web/`

```
web/
  app/                 routes per §8.3, globals.css, layout.tsx
  components/
    ui/                shadcn/ui, copied in
    cat/               §11.3 – §11.7
    common/            DataTable, ConfirmDialog, Empty/Error/Loading, FieldWithUnit
  lib/
    api/               client.ts, types.ts, cats.ts, vaccines.ts, errors.ts
    hooks/             useCats, useCat, useCatWeights, useCatVaccinationStatus, useVaccines, + mutations
    identity.ts        the single hardcoded identity — §20.3
    utils.ts           cn() and friends
  mocks/               MSW handlers + fixtures, §12.5
  tests/               Vitest setup; Playwright specs
  package.json  tsconfig.json  next.config.ts  eslint.config.mjs  .prettierrc
  .env.local.example
```

`tsconfig.json` — **`strict: true`**, `noUncheckedIndexedAccess: true`, path alias `@/*`.
Loose TypeScript on a slice whose whole job is to match a contract defeats the point.

### 12.2 Environment

`.env.local.example`, committed:

```
NEXT_PUBLIC_API_BASE_URL=https://localhost:7xxx
NEXT_PUBLIC_USE_MOCKS=true
```

`.env.local` itself is git-ignored. It holds no secret today, but it will.

### 12.3 CORS — confirm before integrating

*(Corrected 2026-09-25, integrator-authorised one-line edit on the HO-ACC-001 branch, ARCHITECTURE
gate fourth pass MEDIUM: ADR-009's `UseCorsToAllowAll()` claim was superseded by HO-ACC-001's own
CORS work and was already stale.)* The API host applies an explicit origin allow-list from
`Cors:AllowedOrigins` configuration (`HostPipelineExtensions`, HO-ACC-001 §12.2) in every
environment, including Development — it does **not** allow all origins. `http://localhost:3000` is
listed in `appsettings.Development.json` and its committed `.example`. Confirm `http://localhost:3000`
is accepted **before** debugging a failed request as a frontend bug. If it is not, that is a
**backend** change and a §20.2 stop.

### 12.4 HTTPS

The API runs on HTTPS with a dev certificate. `dotnet dev-certs https --trust` must have been run,
or every request fails at the TLS layer with an error that does not mention certificates.

### 12.5 Mocks — `web/mocks/`

MSW handlers for all 20 endpoints in HO-CAT-001 §12.4. Fixtures must include the cases the screens
branch on, or those branches ship untested:

- a cat with allergies, **and** a cat with none (US-CAT-03 both criteria)
- a high-risk cat with a handling note
- a deceased cat
- a cat with an estimated DOB, and one with an exact DOB
- three cats covering `Current`, `DueSoon` and `Overdue`, including a never-administered core vaccine
- a cat with no photo, and one with several
- enough cats to paginate
- **error fixtures**: a `400` with a field-level `ValidationResponse`, a `400` with an unmatched
  `propertyName`, a `404`, and a `409`

---

## 13. Allowed scope

```
web/**
docs/test-cases/TC-HO-CAT-002.md
docs/gates/HO-CAT-002-*.md
.gitignore          ← to add web/node_modules, web/.next, web/.env.local
```

## 14. Forbidden scope

| Path | Why |
|---|---|
| `src/**` | The entire .NET backend. A missing field or endpoint is a §20.2 stop, never an edit |
| `tests/**` | Backend tests |
| `.ai/**` | Backend architecture |
| `.claude/**` | Agent definitions |
| `docs/SRS.md`, `docs/USER_STORIES.md` | Requirements are an input |
| `docs/handover/HO-CAT-001.md` | Another slice's handover. Disagree with it in a finding |
| `NuGet.Config`, `global.json`, `*.sln`, `*.csproj` | .NET build configuration |

---

## 15. Component test cases

`web/tests/` — Vitest + React Testing Library. Behaviour, not implementation: query by role and
label, never by class name or test id where a role exists.

| ID | Case | Expected |
|---|---|---|
| U1 | `AgeDisplay`, exact DOB | Exact age, no "estimated" |
| U2 | `AgeDisplay`, estimated DOB | Marked approximate — **US-CAT-01 AC2** |
| U3 | `AgeDisplay`, no DOB | "Unknown", no crash |
| U4 | `CatProfileHeader` with allergies | Every allergen rendered |
| U5 | `CatProfileHeader`, `hasRecordedAllergies: false` | Renders **"None recorded"** — **US-CAT-03 AC2** |
| U6 | `CatProfileHeader`, high-risk cat | Badge + handling note present, **before** the name in DOM order — **US-CAT-04 AC1** |
| U7 | `CatProfileHeader`, not high-risk | No badge |
| U8 | `CatProfileHeader`, deceased cat | Deceased banner with the date |
| U9 | `CatProfileHeader` latest weight | Rendered with "kg" — **UI-8** |
| U10 | `VaccinationStatusBadge`, each of the three statuses | Correct label **and** icon, not colour alone |
| U11 | Status panel, `DueSoon` | Due date **and** booking link — **US-CAT-07 AC2** |
| U12 | Status panel, `Overdue` | Boarding-blocked warning — **US-CAT-07 AC3** |
| U13 | Status panel, core vaccine never administered | Listed as overdue, not omitted |
| U14 | `PhotoField`, 8 MB file | Rejected, message names the 5 MB limit — **US-CAT-01 AC3** |
| U15 | `PhotoField`, `image/gif` | Rejected, message names the accepted types — **US-CAT-01 AC3** |
| U16 | `PhotoField`, valid 2 MB JPEG | Accepted |
| U17 | `CatForm`, precision Exact without a DOB | Blocks submit, field error |
| U18 | `CatForm`, precision Estimated without a DOB | Submits |
| U19 | `CatForm`, empty name | Field error, announced |
| U20 | `NeuterStatusForm`, Neutered without a date | Field error |
| U21 | `WeightEntryForm` | Unit visible; 0 and 31 kg both rejected |
| U22 | `WeightTrendChart` | Y axis labelled kg; hidden data table present for screen readers |
| U23 | `applyValidationErrors`, matching propertyName | Error lands on that field |
| U24 | `applyValidationErrors`, unmatched propertyName | Surfaces as a form-level error, **not swallowed** |
| U25 | `applyValidationErrors` | `errorCode` appears **nowhere** in the DOM — **NFR-USA-4** |
| U26 | `MarkDeceasedDialog` | Names the actual consequences, incl. that the record is kept — **UI-3** |
| U27 | `TransferOwnershipDialog` | States history transfers and the current owner loses access — **UI-3** |
| U28 | `ConfirmDialog` | `Escape` closes; focus returns to the trigger |
| U29 | `DataTable` | Filter, sort and page changes are written to the URL |
| U30 | `DeceasedBanner` present | Edit controls disabled **with an explanation**, not hidden |
| U31 | Every form | Every input has an associated `<label>` |
| U32 | Every list | Loading, empty and error states each render |
| U33 | Parasite control page, after a successful record | Shows the server-returned `nextDueOn` as visible text (not only a toast) — **US-CAT-06, A11**. Added 2026-09-25 with HO-CAT-001's C10 amendment |

## 16. End-to-end test cases

`web/tests/e2e/` — Playwright. **Run against MSW first, then re-run against the real API** (§5.1);
the sheet records both, and a case that has only passed against a mock is not `PASS` for the real
system.

| ID | Case | Expected |
|---|---|---|
| E1 | Register a cat, land on its profile | Cat created; header shows the entered data |
| E2 | Register with an empty name | Inline field error; **no navigation**; entered data preserved — NFR-USA-5 |
| E3 | Open a high-risk cat, visit every tab | Badge and handling note on **every** tab — US-CAT-04 AC1, UI-7 |
| E4 | Open a cat with no allergies | "None recorded" on every tab — US-CAT-03 AC2 |
| E5 | Record a weight, open the chart | Point appears; header's latest weight updates — US-CAT-02 AC2 |
| E6 | Record a vaccination | Appears in history with its next-due date — US-CAT-05 AC1 |
| E7 | Record a vaccination with an expired batch | `400` surfaces as a field error on batch expiry — US-CAT-05 AC2 |
| E8 | Open a cat whose core vaccine is overdue | Overdue badge + boarding warning — US-CAT-07 AC3 |
| E9 | Record a vaccination, then reopen the status tab | Status refreshed, **not stale** — the invalidation in §8.4 |
| E10 | Flag high-risk without a reason | Blocked with a field error |
| E11 | Mark a cat deceased | Confirmation names the consequences; banner appears; edit controls disabled — US-CAT-09 |
| E12 | Cat list after E11 | Deceased cat absent by default; visible with the filter on — US-CAT-09 AC1/AC2 |
| E13 | Transfer ownership | Confirmation states what transfers; succeeds — US-CAT-08 AC1 |
| E14 | Download the medical-history PDF | A PDF downloads — US-CAT-10 |
| E15 | Open the cage card | QR renders; print layout applies — US-CAT-11 |
| E16 | Keyboard only, register a cat | Completable with no mouse; focus always visible — UI-6 |
| E17 | `@axe-core/playwright` on every screen in §8.3 | **Zero serious or critical violations** — UI-6 |
| E18 | Every screen at **360 px** | No horizontal scroll, no clipped control — UI-1, DoD |
| E19 | Every screen at 768 / 1024 / 1440 px | Layout intact — UI-1 |
| E20 | Simulated `409` on an edit | User told the record changed and offered a reload; **their input is not discarded** |
| E21 | Simulated `500` | Plain-language message, **no stack trace, no GUID** — NFR-USA-4 |
| E22 | Cat list with 25 cats, page size 10 | Pagination works; state survives reload — UI-4 |

---

## 17. Setup and verification

### 17.1 Prerequisites

**Install Node.js 20 LTS or newer first** — §20.1. Then:

```bash
cd web
npm install
cp .env.local.example .env.local
npm run dev          # http://localhost:3000, MSW on by default
```

### 17.2 Build order

1. Scaffold, Tailwind, shadcn/ui init, QueryProvider (§12.1)
2. `lib/api/types.ts` transcribed from HO-CAT-001 §12.3 — **before any component**
3. `lib/api/client.ts`, `errors.ts`, `identity.ts`
4. MSW handlers + fixtures (§12.5)
5. `lib/hooks/`
6. `components/common/`
7. **`CatProfileHeader` and its parts (§11.3 – §11.5)** — the highest-risk component; build and test
   it before the screens that host it
8. Staff screens, in §8.3 order
9. Portal screens
10. Component tests U1–U32
11. E2E E1–E22 against MSW
12. Re-run E1–E22 against the real API once HO-CAT-001 ships

### 17.3 Commands

```bash
npm run lint            # zero warnings
npm run typecheck       # tsc --noEmit, zero errors
npm run test            # Vitest, U1-U32
npm run test:e2e        # Playwright, E1-E22
npm run build           # production build must succeed
```

---

## 18. Acceptance criteria

| # | Story / Req | Criterion | Evidence |
|---|---|---|---|
| A1 | CAT-01 | A cat can be registered through the UI with all its details | E1 |
| A2 | CAT-01 | An estimated date of birth displays the age as approximate | U2, E1 |
| A3 | CAT-01 | An oversized or disallowed photo is rejected with a clear reason | U14, U15 |
| A4 | CAT-02 | Neuter status requires a date when neutered | U20 |
| A5 | CAT-02 | Weight history is viewable as a trend chart with units | U21, U22, E5 |
| A6 | CAT-03 | Allergies appear in the persistent profile header on every cat screen | U4, **E3** |
| A7 | CAT-03 | With no allergies the header states "None recorded", never blank | U5, **E4** |
| A8 | CAT-04 | A high-risk cat shows a warning badge and handling note on every screen | U6, **E3** |
| A9 | CAT-05 | A vaccination can be recorded with batch, date and vet, showing next-due | E6 |
| A10 | CAT-05 | An expired batch is rejected with a message on the right field | E7 |
| A11 | CAT-06 | A parasite treatment records and shows its next-due date | U33, the parasite-control e2e (`cat-parasite-control.spec.ts`) |
| A12 | CAT-07 | All core vaccines in date shows "Current" | U10 |
| A13 | CAT-07 | "Due soon" shows the date and a booking link | U11 |
| A14 | CAT-07 | "Overdue" warns that boarding will be blocked | U12, E8 |
| A15 | CAT-08 | Ownership transfer confirms, stating exactly what transfers | U27, E13 |
| A16 | CAT-09 | Marking deceased confirms, and the cat leaves the default list | U26, E11, E12 |
| A17 | CAT-09 | A deceased cat's record stays viewable and is not editable | U30, E12 |
| A18 | CAT-10 | The medical history downloads as a PDF | E14 |
| A19 | CAT-11 | The cage card renders a QR and prints | E15 |
| A20 | UI-1 | Every screen works at 360 px, and at 768 / 1024 / 1440 | **E18, E19** |
| A21 | UI-3 | Destructive actions confirm, stating what will be affected | U26, U27 |
| A22 | UI-4 | Lists filter, sort and paginate | U29, E22 |
| A23 | UI-6 | WCAG 2.1 AA — keyboard operable, focus visible, errors announced | U28, U31, **E16, E17** |
| A24 | UI-7 | The profile header always carries the full required set | U4–U9, E3 |
| A25 | UI-8 | Weight and dose fields always show units | U9, U21, U22 |
| A26 | NFR-USA-4 | Errors are plain language; no stack trace, code or internal id | U25, E21 |
| A27 | NFR-USA-5 | Form data survives a validation error | E2 |
| A28 | — | A concurrent-edit conflict is explained and the user's input is preserved | E20 |

---

## 19. Required gates

| # | Gate | Agent | Focus |
|---|---|---|---|
| 1 | ARCHITECTURE | `architecture-reviewer` | §8 layering; no global store; §13/§14 scope. **`.ai/` backend rules do not apply — see §6** |
| 2 | SECURITY | `security-reviewer` | The hardcoded identity (§20.3), no secrets in client bundles, no `dangerouslySetInnerHTML`, no internal ids leaked to the DOM |
| 3 | CODE_REVIEW | `code-reviewer` | Component composition, typing, the §9 conventions |
| 4 | TEST | `test-engineer` | Writes `TC-HO-CAT-002.md` from §15/§16 first, then executes. **Records mock vs real API per §5.1** |
| 5 | ACCEPTANCE | `acceptance-reviewer` | Every §18 row against named evidence |

> **A note for the ARCHITECTURE gate.** `architecture-reviewer.md` is written for the .NET backend —
> aggregates, CQRS, `IInMemoryDispatcher`, EF Core. None of it applies to this diff. Review against
> §8 and §9 of this handover, and return `BLOCKED` rather than inventing a rule if something is not
> covered.

---

## 20. BLOCKED conditions — stop and report, do not work around

**20.1 Node.js is not installed.** `node` and `npm` are not on PATH. **Nothing in this handover can
begin.** Install Node 20 LTS or newer, confirm `node --version` and `npm --version`, and record the
versions in the test sheet's Environment section. Do not attempt a workaround.

**20.2 The HO-CAT-001 API does not exist yet.** Build against MSW per §5.1. Two hard rules:
- **Never invent an endpoint, field or error code that HO-CAT-001 does not specify.** A mock that
  returns a field the API will not return produces a screen that works in development and breaks on
  integration — the most expensive class of bug on this slice.
- **`src/**` is forbidden scope (§14).** If a screen genuinely cannot be built from the specified
  contract, **stop and report it as a finding against HO-CAT-001.** Do not add the endpoint.

**20.3 Identity is hardcoded and this is not authentication.** `lib/identity.ts` returns one fixed
staff user. Consequences to state plainly in the gate reports rather than leave implied:
- there is **no login, no roles and no access control** in this UI;
- **US-CAT-08 AC2 — "the previous owner can no longer view the cat" — cannot be verified here.**
  HO-CAT-001 §20.3 records that even server-side it is a query filter, not authorization;
- the `(portal)` routes render as that staff user, not as an owner.
Do not build login, and do not wire up the FusionAuth instance running in Docker. That is §1
Accounts, and the API has no token validation to accept a token anyway.

**20.4 Photo upload has nowhere to go.** The API takes a storage key, not a file (§11.6). Validate
the file client-side to satisfy US-CAT-01 AC3, show the honest limitation, and **stop there**. Do
not add an upload route, an object-storage client, or a local file write — all three are backend
scope, and NFR-SEC-5 requires virus scanning and signed URLs that do not exist.

**20.5 US-CAT-07's booking link has no destination.** Appointments (§3) are unbuilt. Render the
link to the intended future route and leave it visibly inert, or render it disabled with a tooltip.
**Do not build a booking screen.** Record which you chose — the acceptance gate needs to know A13
was met deliberately rather than by accident.

**20.6 CSV export (UI-4) has no endpoint.** HO-CAT-001 specifies none. Filtering, sorting and
pagination are in scope; export is not (§4.1). Do not generate CSV client-side from a paginated
response — it would silently export only the current page, which is worse than no button.

**20.7 A contract mismatch is a stop, not a patch.** If the real API returns something other than
HO-CAT-001 §12.3 specifies, report it. Do not add a client-side shim that hides the divergence;
the next consumer of that API will hit the same thing with no shim.
