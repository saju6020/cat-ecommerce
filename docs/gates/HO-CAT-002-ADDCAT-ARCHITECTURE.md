GATE: ARCHITECTURE
HANDOVER: HO-CAT-002-ADDCAT (slice of HO-CAT-002; backlog CAT C-APT1)
COMMITS: 84b5c90 308b3ac 283ecb8 e715f06 ceb0786 (reviewed as `git diff 578dc79...HEAD`)
DATE: 2026-10-06
STATUS: PASS

There are no BLOCKER or HIGH findings. Under the owner rule (2026-09-27), MEDIUM and LOW findings
do not fail the gate. They go to `docs/backlog/CAT.md`. Nothing was built, type-checked or run.
Runtime claims are marked for TEST.

SPEC USED
- `docs/handover/HO-CAT-002-ADDCAT.md`: §3 backend facts, §4 scope, §5 behaviour, §8 ACs.
- HO-CAT-002: §8.2 layering rules 1–5, §13 allowed scope and §14 forbidden scope. The parent's
  §13 and §14 govern the slice.
- HO-APT-002 §11 step table row 1 (`docs/handover/HO-APT-002.md:213`): *"Add a cat" links to
  HO-CAT-002's form and returns here*.
- `web/docs/decisions.md` ADR-FE-024 (booking choices live in URL params).
- `lib/validation/nextPath.ts`: the HO-ACC-002 open-redirect guard.
- `CatsController.RegisterCat`, `RegisterCatRequest`, `RegisterCatCommandValidator` and
  `RegisterCatCommandHandler` at master. These were read only to verify the "no backend change"
  claim. The controller does not differ between `578dc79` and master.

QUESTIONS ASKED
1. **Is the `CatForm` `audience` prop a clean extension, with staff behaviour unchanged?**
   Yes, with one caveat (finding M1). The new props are optional:
   - `audience` defaults to `'staff'`.
   - `cancelHref` is optional. Neither staff caller passes it, so no Cancel link renders on any
     staff screen.

   The callers are `(staff)/staff/cats/new/page.tsx:32` and `(staff)/staff/cats/[catId]/edit/page.tsx:43`.
   - Edit mode was already hiding `ownerId`.
   - The only other staff-visible change is that the submit wrapper gained
     `flex flex-wrap items-center gap-3`. With a single child this has no visual effect.
   - The zod schema already had `ownerId` optional, so the customer mode needs no schema fork.
   - No second form was created. This is reuse, as §1 of the slice records.
2. **Does the backend need no change (F19)?** Confirmed on master.
   - `CatsController.RegisterCat` (`src/Api/CatEcommerce.Api/Controllers/CatsController.cs:44-45`)
     replaces `request.OwnerId` with `caller.AccountId` when `caller.Kind == Customer`. It does this
     **before** the command is built.
   - So `RegisterCatCommandValidator`'s `OwnerId.NotEmpty()` sees the real account id, never the
     empty GUID.
   - `RegisterCatRequest.OwnerId` is `required Guid`, so sending
     `00000000-0000-0000-0000-000000000000` is the correct way to satisfy the wire contract.
   - The endpoint returns `201` with `{ catId }`. The page reads `result.catId`, which matches.
3. **Does the `?next=` return to /book work?** It conforms.
   - `sanitizeNextPath(next, '/book', '')` reuses ACC-002's guard rather than a hand-rolled check.
     It accepts `/book`, `/book?…` and `/book/…`, and rejects `//…`, absolute URLs and `/bookX`.
   - On success, `new URL(next, 'http://local')` takes the path and query and calls
     `searchParams.set('catId', result.catId)`. This **replaces** any existing `catId` rather than
     appending a second one, and keeps every other booking parameter.
   - Only `pathname + search` is pushed, so the dummy origin never leaks.
   - `BookingFlow` builds `next` from `searchParams.toString()`, URI-encoded once. This matches
     ADR-FE-024: choices live in the URL, and the hold does not survive.
   - The cat stage only renders when there is no `catId`, so the merged `catId` advances the flow.
   - The case of no `next`, or an off-site `next`, falls back to `/portal/cats/{catId}`.
   - Correct round-tripping, and the cats list refreshing through `useRegisterCat`'s
     `invalidateCatsList`, are runtime behaviour. They are marked for TEST (U-AC6, E-AC3).
4. **Layering (HO-CAT-002 §8.2).** It conforms.
   - The page uses `useRegisterCat` from `lib/hooks/`.
   - `CatForm` and `CatStep` stay presentational and take props only. `CatStep` receives
     `addCatHref` from its container rather than computing it.
   - No `fetch`, `useQuery` or `useMutation` was added outside `lib/hooks/`.
   - No new dependency.
5. **Omitting the photo block.** This is justified. `RegisterCatRequest` has no photo field. The
   slice §4 records the omission as out of scope, and adding a second upload call would be a new
   flow that no handover specifies.
6. **Do the mock changes stay inside CAT's own mock module?** Yes.
   - `MaximumLengthValidator` mirrors `RegisterCatCommandValidator`'s `Name.MaximumLength(100)`.
   - The empty-GUID-to-caller mapping models F19 and is documented in place.
   - There is a minor divergence (finding L2).
7. **Edits to files owned by other handovers.**
   - `web/components/booking/CatStep.tsx` and `web/app/(portal)/book/BookingFlow.tsx` belong to
     HO-APT-002. Both are listed in slice §4. The change also **implements** HO-APT-002's own
     step-table row 1, which already called for an "Add a cat" link that returns to the booking
     flow. The 2026-10-06 owner request covers the booking entry point.
   - `web/app/(portal)/portal/cats/page.tsx` and `web/mocks/handlers/areas/cat-profiles.ts` belong
     to HO-CAT-002, the parent. Both are listed in slice §4.
   - No file in HO-CAT-002 §14 forbidden scope was touched: there are no changes under `src/**`,
     `tests/**` or `.ai/**`, and no csproj or package.json changes.
   - The uncommitted `web/public/mockServiceWorker.js` is not part of any commit under review and
     was left alone.

FINDINGS
- [MEDIUM] web/components/cat/CatForm.tsx:13-14,131 (with web/lib/api/errors.ts:74-75) — Customer
  mode hides the `ownerId` input, but `CAT_FORM_FIELD_MAP` still maps `OwnerId → ownerId` and lists
  it in `CAT_FORM_KNOWN_FIELDS`. A server 400 on `OwnerId` is therefore set on a field that is not
  rendered, and the customer sees nothing.
  - The handler can return such an error to a customer caller: `CustomerNotFound`, or
    `AccountNotActive` if the account is deleted, merged or anonymised mid-session
    (`RegisterCatCommandHandler.cs:48-53`).
  - Slice §5 says that fields the form does not map go to a form-level message, and AC-2 says
    errors are plain language.
  - In customer mode, `OwnerId` should fall through to `root`.
  - The case is rare, and staff behaviour is not affected. Owner: CODE_REVIEW / backlog.
- [MEDIUM] docs/handover/HO-CAT-002-ADDCAT.md — The slice document does not follow the
  `docs/handover/README.md` gate-reading structure.
  - It has no §6 naming the governing docs (HO-CAT-002 §8.2, ADR-FE-024, `nextPath.ts`).
  - It has no §14 forbidden scope. The parent's §14 was applied by inference.
  - HO-CAT-002 §1 has no Amended row pointing to the slice. Earlier HO-CAT-002 follow-ups, such as
    the QR redirect, were recorded as §1 Amended rows.
  - HO-APT-002 does not cross-reference the slice either, even though it is now satisfied by it.
  - This gate read the parent docs anyway. The finding is against the handover, not the code.
- [LOW] web/mocks/handlers/areas/cat-profiles.ts:829-830 — The mock maps an **empty or missing**
  `ownerId` from any caller, including staff, to the fixture customer. The real API behaves
  differently:
  - A staff empty GUID fails `OwnerId.NotEmpty()` with a 400.
  - An empty string fails JSON binding.

  So the mock silently accepts a staff registration that has no owner. It is harmless today
  because the staff form is not under test for this, but the mock is more permissive than the
  contract it stands in for.
- [LOW] web/mocks/handlers/areas/cat-profiles.ts:777 — `CUSTOMER_CALLER_ID` is a hard-coded copy of
  `accountsFixtureIds.customerId`. It should not be imported, because of the README rule against
  reaching into another area's mock. But if the accounts fixture id changes, this copy drifts
  silently. A test should pin the two together, or the comment should name the file.
- [LOW] web/app/(portal)/portal/cats/new/page.tsx:26,41 — `sanitizeNextPath` checks the raw
  string. `new URL()` then normalises dot-segments, so `next=/book/../staff/x` passes the guard and
  lands on `/staff/x`.
  - The redirect stays same-origin, so it is not an open redirect.
  - It does escape the `/book` prefix the guard means to enforce.
  - The guard is pre-existing shared code (HO-ACC-002), not introduced here.
  - Noted for the SECURITY gate. Not an architecture defect of this slice.

SCOPE CHECK
- files in allowed scope: 10/10
  - From slice §4: `portal/cats/new/page.tsx`, `portal/cats/page.tsx`, `BookingFlow.tsx`,
    `CatStep.tsx`, `CatForm.tsx` and `cat-profiles.ts`.
  - Tests from §7: `cat-step.test.tsx`, `cat-add-customer.spec.ts` and `add-cat-customer.test.tsx`.
  - The slice document itself.
  - All of these are also inside parent HO-CAT-002 §13 (`web/**`, `docs/`).
- files outside allowed scope: none
- other handovers' files edited: `CatStep.tsx` and `BookingFlow.tsx` (HO-APT-002). Both are
  authorised by slice §4 and the 2026-10-06 owner request, and are anticipated by HO-APT-002's own
  step-table row 1.
- new dependencies: none
- backend, `src/**`, migrations: none
