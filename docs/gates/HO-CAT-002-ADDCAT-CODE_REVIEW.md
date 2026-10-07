GATE: CODE_REVIEW
HANDOVER: HO-CAT-002-ADDCAT
COMMITS: 84b5c90 308b3ac 283ecb8 ceb0786 3796911 2a58de5 (range 578dc79...c783bf8)
DATE: 2026-10-06
STATUS: PASS

Checks run in web/: `tsc --noEmit` clean; `eslint app components lib tests` clean;
`vitest run --maxWorkers=3 tests/unit/cat tests/components/booking` 29 files / 111 tests passed.
Playwright not run (per instruction). No BLOCKER or HIGH findings. Success path (toast, then
`router.push`), `['cats']` invalidation (awaited by `mutateAsync`, so My cats and the booking cat
step refetch), typed values kept on every error path, customer-mode OwnerId routing to root, staff
default of `audience`, and the `next`/`catId` round trip all read correct.

FINDINGS
- [MEDIUM] web/app/(portal)/portal/cats/new/page.tsx:39 — the submit button is driven by `registerCat.isPending`, which goes false as soon as the POST resolves, before `router.push` completes. A second click in that window re-submits and registers a duplicate cat. Keep the button disabled after success (a local `submitted` flag, or RHF `isSubmitting`).
- [MEDIUM] web/tests/unit/cat/add-cat-customer.test.tsx:105-134 — U-AC5/U-AC6 assert only the redirect. None asserts the request body (empty-GUID `ownerId`, `species` default "Cat", `dateOfBirth` null for Estimated), the "Cat added" toast, or that the cat list query is invalidated. Capture the POST body via msw and assert it; assert the toast.
- [MEDIUM] web/tests/components/booking/cat-step.test.tsx:7 — U-AC9 passes `addCatHref` in itself, so it proves only that CatStep renders the prop. The construction in BookingFlow.tsx:94-95 (current params into `next`, bare `/book` when empty) and the pre-selection on return with `catId` have no unit test; E-AC3 covers the return, but not a multi-parameter `next`. Add a BookingFlow test with `?category=Vaccination&date=...` and no cats.
- [LOW] web/lib/validation/bookingReturnPath.ts:14-30 — `safeBookingPath` is run twice on the same input (page.tsx:23 and again inside `resolveAddCatReturn`), and it sanitises both the raw and the normalised string. Safe but harder to read than needed; the page could pass the already-safe value, or `resolveAddCatReturn` could take the raw value only.
- [LOW] web/tests/unit/cat/booking-return-path.test.ts — no positive case for a `/book/<sub-path>` or for a query value containing `//` or `%2F` (for example `/book?next=%2F%2Fx`), and no assertion that `catId` appended after a pre-encoded query stays well-formed.
- [LOW] web/tests/unit/cat/add-cat-customer.test.tsx:21 — no test asserts staff `CatForm` still shows the Owner field after the `audience` change. Existing staff tests probably cover it, but U-AC1 is the natural place for a one-line contrast.
- [LOW] web/components/cat/CatForm.tsx:268 — the Cancel link is a plain underlined text link beside a filled button, with no focus-visible style of its own beyond browser default; check it against the other tertiary links in the portal.
- [LOW] web/mocks/handlers/areas/cat-profiles.ts:824 — the mock treats any empty/missing `ownerId` as the fixture customer; a staff request with a blank owner would succeed in the mock where the real API returns 400. Restrict the fallback to the customer session if the mock can see it, or note the divergence.

SCOPE CHECK
- files in allowed scope: 16/16 (13 listed in handover section 4, the two amended rows in HO-APT-002 and HO-CAT-002, and docs/gates records)
- files outside allowed scope: none (uncommitted web/public/mockServiceWorker.js left untouched and not committed)
