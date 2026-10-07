GATE: CODE_REVIEW
HANDOVER: HO-CAT-002
COMMITS: cb2ddd2 (web/** only)
DATE: 2026-09-26
STATUS: PASS

SCOPE OF THIS PASS
Delta review of the web/** portion of commit cb2ddd2, which adapts the frontend to HO-CAT-001's
gate-fix pass: `GET /api/cats`, `GET /api/cats/{catId}/weights` and `GET /api/vaccines` now return
`PagedResult<T>` (`{ items, pageNumber, pageSize, totalCount }`) with `pageSize` capped/clamped at
100 server-side (`pageSize <= 0` is a 400), and `RecordCatWeightRequest` drops `recordedByStaffId`
(server sets it from the caller). Combines the architecture, security and code-review angles per
the calling agent's instructions. Files reviewed: `app/(portal)/portal/cats/page.tsx`,
`app/(staff)/staff/cats/page.tsx`, `app/(staff)/staff/cats/[catId]/weights/page.tsx`,
`app/(staff)/staff/vaccines/page.tsx`, `lib/api/cats.ts`, `lib/api/vaccines.ts`,
`lib/hooks/useVaccines.ts`, `mocks/handlers/areas/cat-profiles.ts`, and the three updated unit
tests (`cat-list-page.test.tsx`, `weights-page.test.tsx`, `use-vaccines.test.tsx`).

VERIFICATION PERFORMED
- Confirmed the backend's actual `PagedResult<T>` shape (`src/Read/CatEcommerce.Read/ViewModels/PagedResult.cs`:
  `record PagedResult<T>(IReadOnlyList<T> Items, int PageNumber, int PageSize, int TotalCount)`)
  matches the web `PagedResult<T>` interface (`items`, `pageNumber`, `pageSize`, `totalCount`)
  field-for-field.
- Confirmed `PagingRules.MaxPageSize = 100` and the `pageSize <= 0` → `400 InvalidPageSize`
  behaviour in `CatsController.ApplyPaging` / `VaccinesController` match the web's
  `MAX_PAGE_SIZE = 100` constant and the mock's `resolvePaging`'s 400 branch, including the same
  `ValidationResponse` envelope shape (`isValid`, `errors[{propertyName, errorCode, errorMessage,
  resourceName}]`).
- Confirmed `CatWeightPointViewModel` is `(DateOnly RecordedOn, decimal Kilograms)` — no
  `recordedByStaffId` — matching the web `CatWeightPoint` type and the mock's response mapping,
  and grepped the whole `web/` tree: no remaining reference to `recordedByStaffId` in any request
  body, and no remaining `PageOf` type usage except one stale doc comment (see LOW finding below).
- Checked every call site that sets `pageSize` (`getCats` default 10, `getCatWeights` default 10,
  `getVaccines` default 20, `getCatWeightSeries`/`useAllVaccines` batch at `MAX_PAGE_SIZE = 100`,
  staff list pages at 10/`PAGE_SIZE`, portal list at 12) — none ever requests `pageSize > 100`.
- Traced `getCatWeightSeries`'s new batching loop: pages in batches of 100 using
  `page.totalCount` to know when to stop, capped at `WEIGHT_SERIES_MAX_POINTS = 1000` — same
  effective ceiling as the old single `pageSize=1000` request, now correct under the new
  server-side cap instead of silently returning only 100 rows.
- Traced `useAllVaccines`: computes `totalPages` from the first page's `totalCount` and throws a
  named error if it would exceed `MAX_VACCINE_CATALOGUE_PAGES = 100` (10,000 vaccines), rather than
  silently truncating. Confirmed via `grep` that `DataTable`'s `hasNextPage` pagination mode has no
  remaining callers anywhere in `app/` — all three list pages (staff cats, staff vaccines, staff
  weights) now pass `{ pageSize, totalCount }`, which `DataTable` already supported (built for the
  Accounts side per the commit message; `DataTable.tsx` itself is untouched by cb2ddd2).
- Confirmed layering (`lib/api` → `lib/hooks` → `app`, components untouched) — no component in
  this diff fetches directly; `useAllVaccines` is the only consumer of `useVaccines.ts` and reads
  through `lib/api/vaccines.ts`.
- Ran the required checks in `web/`:
  - `npx tsc --noEmit` — clean, zero errors.
  - `npx eslint .` — clean, zero warnings.
  - `npx prettier --check .` — one warning on `next-env.d.ts`, a Next.js-generated file untouched
    by cb2ddd2 and unrelated to this diff; not a finding against this commit.
  - `npx vitest run` — 37 files / 104 tests passed, including the three updated tests. Read all
    three: they render real components against realistic MSW/mocked `PagedResult<T>` response
    bodies and assert on rendered DOM (empty states, row counts, the ceiling error message) rather
    than asserting on mock call arguments — genuine behavioural tests, not tautologies.

FINDINGS
- [LOW] `components/common/DataTable.tsx:54-64` (not part of cb2ddd2's diff, but the diff makes
  this comment inaccurate) — the doc comment on `DataTablePagination` still names `PageOf<T>` (a
  type this commit renamed to `PagedResult<T>`, so the identifier no longer exists anywhere) and
  still describes "HO-CAT-002's cat/vaccine/weight lists" as the reason the `hasNextPage` mode
  exists. After cb2ddd2, none of those three lists use `hasNextPage` mode any more (confirmed by
  grep — no remaining `pagination={{ ... hasNextPage ... }}` call site in `app/`), so both the type
  name and the stated rationale are stale. `DataTable.tsx` is correctly out of cb2ddd2's diff (the
  commit message states it is unchanged), so this is not a defect introduced by this commit, but a
  follow-up should update the comment (and consider whether `hasNextPage` mode is now dead code
  worth removing) so the next reader isn't sent looking for a type that no longer exists.

SCOPE CHECK
- files in allowed scope (HO-CAT-002 §13: `web/**`, `docs/test-cases/TC-HO-CAT-002.md`,
  `docs/gates/HO-CAT-002-*.md`): 10/10 changed files in cb2ddd2's web/** portion.
- files outside allowed scope: none. (The backend half of cb2ddd2, outside `web/`, is HO-CAT-001's
  scope and out of this review per the calling agent's instructions.)
