GATE: ARCHITECTURE
HANDOVER: HO-ADM-002
COMMITS: c7f29d7 ce14242 8498378 ec00965 fa80c47 (delta range 2299141..HEAD, product code under web/ only)
DATE: 2026-09-29
STATUS: PASS

# Delta pass (third ARCHITECTURE pass)

This is a **delta pass** over `git diff 2299141 HEAD -- web` (33 files). The ACCEPTANCE first pass
(`docs/gates/HO-ADM-002-ACCEPTANCE.md`, F-H2) found that product commits made after the second
ARCHITECTURE pass (PASS at 62178ab, record 2299141) had never had an ARCHITECTURE review. Gate-record,
test-case and backlog commits in the range are not reviewed as product code.

It was judged against HO-ADM-002 §8.3, §11.7, §11.9, §13, §14 and the §1 Amended rows dated
2026-09-28 (TEST second and third pass) and 2026-09-29 (ACCEPTANCE F-H1). It was also judged against
HO-ACC-002 §4.3 and HO-CAT-002 §8.4. This is a web-only handover, so the `.ai/` backend checklist does
not apply.

`npx tsc --noEmit` is clean. `eslint` on every changed source directory is clean. This gate ran no
component or e2e tests; runtime behaviour belongs to TEST.

## ADR-FE-020 ruling: compatible with §8.3, no owner decision needed

§8.3 decides **where filter state lives**: in the URL search params, so that it is shareable and
back-button safe. ADR-FE-020 does not move it.

- **The URL is still the only durable and shared copy.**
  - Deep links and reload read the URL, through the `useState` initialiser.
  - Back and Forward are driven by URL history: `router.push`, with `replace` for a same-field
    typing burst.
  - Any external navigation resyncs the local copy from the URL.
- **The local `useState` is a per-instance display buffer, not a store.** It lives in one component
  instance and is never shared across components, modules or routes. It holds no server data, and it
  always converges on the URL: at once for selects and dates, and within 300 ms or on blur/Enter for
  debounced text.
- HO-CAT-002 §8.4 forbids a global store and copying *server data* into `useState`. Neither happens
  here. A controlled input's momentary draft is the "ephemeral UI, local useState" row of that table.
- §4.3 "no global store" is not engaged, because there is no Context, no module-level state and no
  external store.

Condition: the design only holds while local state reliably converges on the URL. Backlog row 22 (a
debounce timer that survives Back) is a known convergence defect. Fixing it is what keeps this
ruling true. It stays MEDIUM, as already recorded.

## Hook design: sound

- **Module-level state.** `useFilterUrlUpdate.ts` and `useUrlFilterState.ts` have none. The only
  module-level bindings are the constants `FILTER_TYPING_BURST_MS` and `FILTER_WRITE_DEBOUNCE_MS`.
- **Per-instance mutable state.** It all lives in `useRef`s: `lastEdit`, `pendingParams`,
  `pendingWrites`, `mounted` and `debounceTimers`.
- **Timers.** They are cleared on unmount.
- **The debounced callback.** It captures `writeToUrl` from an earlier render. That is safe, because
  it only reaches refs and the `setUrlFilter` callback, which is stable on `[router, pathname]`.
- **Scope.** Both files are new under `web/lib/hooks/**` (§13, "new hooks only"). They are disclosed
  in §1 Amended and ADR-FE-020.

## fa80c47: error-state handling

The page-level `if (x.error instanceof ApiForbiddenError) return <PermissionDeniedState />` follows
the convention the audit-log, content, system and settings pages already used. Splitting
loading / 404 `ErrorState` / other-error `ErrorState` with `onRetry` matches the shared
`ErrorState` API. There are no new components and no new state shapes.

The `lib/api/errors.ts` change goes beyond §13's "append" annotation (see the MEDIUM below). It is
disclosed in the 2026-09-29 §1 Amended row.

## c7f29d7: leave reject note

The note is local dialog state inside the existing `pending` action, with no new state kind. It is
architecture-neutral.

FINDINGS
- [MEDIUM] web/lib/api/errors.ts:81,97 — §13 permits `errors.ts` "← append" only. fa80c47 also changes the bodies of two baseline (08e873e, HO-CAT-002/HO-ACC-002) shared helpers, `submitWithServerErrors` and `describeApiError`. As a result, a `403` on about 20 forms outside this area (cat forms and dialogs, account forms, the photos page) now shows the ADM permission wording instead of the generic text. The direction is consistent with ADR-FE-009 and the change is disclosed in §1 Amended (2026-09-29). But the Amended row does not say that it reaches other areas' forms. Record the cross-area effect. TEST: re-run the cat and account component suites.
- [MEDIUM] web/app/(staff)/staff/admin/services/[serviceId]/page.tsx:47 (also tax-classes/[taxClassId]/page.tsx:43, branches/[branchId]/roster/page.tsx:86, closures/page.tsx:24, rooms/page.tsx:17, staff/leave/page.tsx:15) — §11.9 maps `BranchNotAssigned`/`BranchAssignmentRequired` to a banner. `errors.ts`'s own doc comment says callers must not fall through to `PermissionDeniedState`. The new page-level checks send every `403`, including those two codes, to `PermissionDeniedState`. The service page's comment even says "a branch refusal is the usual cause here". `components/branches/BranchAssignmentBanner.tsx` has no consumer anywhere in `web/`. The banner gap predates this delta, but fa80c47 extends the pattern that bypasses it. Forms do use the branch wording, through `describeForbidden`. ACCEPTANCE should rule on §11.9 conformance.
- [LOW] web/app/(staff)/staff/admin/audit-log/page.tsx:63 — `pageNumber` still reads `useSearchParams()`, while the filters now come from local state. During the 300 ms debounce window, or before a push commits, the query runs with new filters and the old page, so a narrowed search can briefly fetch page N of a smaller result. The URL write drops `page` on commit.
- [LOW] web/lib/hooks/useUrlFilterState.ts:187 / useFilterUrlUpdate.ts:66 — two parallel `pendingWrites` lists track the same writes: the outer hook pushes the string the inner one has just recorded. They are equivalent today, but the outer hook could read the inner hook's list and not keep a copy.
- [LOW] web/lib/api/errors.ts:119-127 — `describeForbidden` re-implements the `BranchNotAssigned`/`BranchAssignmentRequired` test that `getBranchAssignmentErrorCode` (same file) already does. It should reuse the helper so the two stay in step.
- [LOW] web/docs/decisions.md:581 — ADR-FE-020 is one more ADR beyond the README-allocated range, which Amended (1) already moved to 014…019. Extends backlog row 7: the integrator should update `docs/handover/README.md`'s ADR table to 014…020.

No BLOCKER. No HIGH.

SCOPE CHECK
- files in allowed scope: 33/33
  - `app/(staff)/staff/admin/**` (7) and `app/(staff)/staff/leave/**` (1)
  - `components/{admin,audit,branches,content,leave,roster}/**` (14)
  - `lib/api/errors.ts` (in §13; edit mode exceeds "append"; see MEDIUM)
  - `lib/hooks/**` (2 new)
  - `docs/decisions.md` (ADR-FE-020, disclosed in §1 Amended)
  - `tests/components/{admin,audit,content,leave}/**` (6)
  - `tests/e2e/admin-filters.spec.ts`
- files outside allowed scope: none
  - Outside `web/`, the range also edits `docs/handover/HO-ADM-002.md`, but only the §1 Amended self-amendment rows. That is the established process, accepted by the earlier passes.
  - The other outside-`web/` paths are `docs/gates/**`, `docs/test-cases/TC-HO-ADM-002.md` and `docs/backlog/ADM.md`. These are process records and were excluded from review as instructed.
