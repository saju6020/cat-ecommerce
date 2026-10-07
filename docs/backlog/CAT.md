# Backlog — Cat Profiles web (HO-CAT-002)

Medium/low gate findings not blocking the merge (owner rule 2026-09-27: only BLOCKER/HIGH block a gate).
Fix in a later batch. Source: `docs/gates/HO-CAT-002-QR-*.md`, `docs/test-cases/TC-HO-CAT-002-QR.md`.

| # | Severity | Item | Source |
|---|---|---|---|
| 1 | ~~LOW~~ Resolved | **Resolved 2026-10-07 in ho/BUGS-SHP-WEB.** Was — `/cats/{id}`: `isOwner = Boolean(ownershipQuery.data)` can be satisfied by cached `['cat', id]` data (global 30 s staleTime) instead of a fresh fetch. Unreachable from a real QR scan (full page load, empty cache); the destination page and API still enforce ownership. Fix: count ownership only from `isSuccess && isFetchedAfterMount && !isFetching`, or `staleTime: 0` on this check. | SECURITY / CODE_REVIEW delta, TEST R3 |
| 2 | LOW | `window.__forceCatQueryNotOwned` is described as "one-shot / the NEXT document", but `page.addInitScript` re-arms it on every later navigation; a future test that goes on to a cat page would get an unexpected 404. Fix the wording (or clear the init script). | ARCHITECTURE delta, TEST R4 |
| 3 | LOW | `mocks/handlers/areas/cat-profiles.ts:570` comment "Vitest's jsdom — never forced" sits on the `typeof window === 'undefined'` branch; jsdom defines `window`. | ARCHITECTURE delta |
| 4 | LOW | `/cats/{id}`: an invalid id still sends both session requests (results ignored); optional short-circuit. `encodeURIComponent(id)` is a no-op after GUID validation. | CODE_REVIEW |
| 5 | Owner note | A signed-out owner scanning their own cat's card lands on staff sign-in with no direct link to customer sign-in (owner decision 2026-09-27). If owners get confused, add a "Cat owner? Sign in here" link. | ARCHITECTURE delta |
| 6 | Follow-up | Web cat photo page still uses the pre-FIL `storageKey` shape; convert to upload-then-attach-by-fileId with HO-FIL-002's `FileUploader` (8 steps listed in HO-FIL-002's developer report / gate files). | FIL-002 |
| 7 | LOW | `web/lib/validation/catId.ts:7-12` header comment is stale after the owner's change (session queries do fire; the page now calls the cat API for signed-in customers). | ACCEPTANCE |
| 8 | LOW | TC-HO-CAT-002-QR.md line 155 cites e2e I1/I5/I7 as "no cat call" evidence, but none of them asserts it (the unit tests do); header line 6 says tested commits end at b543474 though U19/U20 came in 806f760. | ACCEPTANCE |
| C-APT1 | ~~MEDIUM~~ Resolved | **Resolved 2026-10-07 by HO-CAT-002-ADDCAT (all five gates PASS): `/portal/cats/new`, "Add a cat" on My cats and in the booking cat step, return to `/book` with the new cat chosen.** Was — No customer "add a cat" screen exists in the owner portal (master has only staff `(staff)/staff/cats/new`), so a genuinely new customer cannot complete HO-APT-002 AC-1 online; its booking flow's cat step says so when empty. Needs an owner/handover assignment. | HO-APT-002 ACCEPTANCE |
| C-APT2 | LOW | Pre-existing e2e failure: `cat-vaccinations` E9 ("Due soon" not visible for Biscuit) fails on master cffaa79 as well as on ho/NTF-002 and ho/APT-002 — the fixture derives dates from today. | HO-NTF-002 / HO-APT-002 TEST |

## HO-CAT-002-ADDCAT (customer add-a-cat screen — closes C-APT1)

All five gates PASS (`docs/gates/HO-CAT-002-ADDCAT-*.md`; SECURITY's open-redirect HIGH fixed in 3796911). Medium/low findings:

| # | Severity | Item | Source |
|---|---|---|---|
| AC1 | ~~MEDIUM~~ Resolved | **Resolved 2026-10-07: `CustomerRegisterCatOwnerTests` (Guid.Empty → 201 owned by the caller; naming another owner still registers to the caller).** Was — No backend test pins "customer `POST /api/cats` with `ownerId = Guid.Empty` → 201, owned by the caller" (F19); the e2e runs on MSW only. Add the integration test, or run E-AC1/E-AC3 against the real API. | ACCEPTANCE |
| AC2 | MEDIUM | `shop-portal` E-D3 failed twice in a row on this branch, not on master, then passed three times — load/order-sensitive; `cat-add-customer` E-AC3 failed once on mobile-360 in the 312-test run. | TEST |
| AC3 | LOW | Tests: the My cats empty-state link has none; U-AC9 passes `addCatHref` in (BookingFlow's construction is covered only by U-RP2 on the helper); U-RP1 lacks a positive `/book/<sub>` case and a query value with `//`/`%2F`; no test contrasts staff mode (Owner field) with customer mode. | CODE_REVIEW / TEST / ACCEPTANCE |
| AC4 | ~~LOW~~ Resolved | **Resolved 2026-10-07 in ho/BUGS-SHP-WEB.** Was — `safeBookingPath` runs twice on one value (page and `resolveAddCatReturn`); the Cancel link has no focus-visible style of its own. | CODE_REVIEW |
| AC5 | LOW | Mock: any empty owner (staff too) maps to the fixture customer, where the real API returns 400 for staff (`cat-profiles.ts:829-830`); `CUSTOMER_CALLER_ID` copies `accountsFixtureIds.customerId` (`:777`). | ARCHITECTURE |
| AC6 | LOW | `/portal/cats` subtitle ("shows every registered cat") predates the slice and reads oddly next to the new button; the slice document uses §4/§8 numbering, not the README's §13/§18. | ACCEPTANCE |
