GATE: ARCHITECTURE
HANDOVER: HO-ADM-002
COMMITS: f93b790 6f9da0a 5598fa5 14ea722 b7f18d9 338834f 994305c f2a8970 b7b244d ceb0019 62178ab (range 08e873e..62178ab)
DATE: 2026-09-28
STATUS: PASS

# Second pass

This pass re-reviews the whole branch diff, `git diff 08e873e HEAD` (130 files). It focuses on the fix commit `62178ab`, made after the first pass FAILed on one HIGH (`web/lib/branch.ts`: a global store outside §13).

The owner decided on 2026-09-28 to rework the code to the §8.3 shape rather than accept the deviation.

It was judged against:
- HO-ADM-002 §8.3, §13 and §14, and §1 Amended rows (1) to (6) of 2026-09-28.
- The baseline `web/lib/branch.ts` contract at `08e873e`.
- HO-ACC-002 §4.3 and HO-CAT-002 §8.2 and §8.4.

This is a web-only handover, so the `.ai/` backend rules do not apply. No build or tests were run by this gate. Runtime items are marked for TEST.

## First-pass HIGH: resolved

**§8.3 shape.** The active branch now lives only in the TanStack Query cache, under `['staff', 'active-branch']` (`ACTIVE_BRANCH_KEY`, `web/lib/hooks/useActiveBranch.ts:26`).
- It is written only through `queryClient.setQueryData`. There are two writers: the seed effect from `GET /api/staff/branches/mine` and `useSetActiveBranch`.
- It is read by `useQuery` with `enabled: false`.
- In `@tanstack/query-core` 5.103.2, `refetchQueries` skips disabled queries (`query.isDisabled()` filter). No invalidation of the `['admin']` family, or of any other family, can run `queryFn: () => null` over the stored value.
- No residue remains anywhere in `web/`: `useSyncExternalStore`, `subscribeActiveBranchId`, `setActiveBranchId` and `resetActiveBranchIdForTests` are all gone.

**Baseline `lib/branch.ts` contract.** `getActiveBranchId()` keeps its name, signature and module, and its body is replaced. The file also gained three things beyond the body:
- a type-only import;
- `let queryClientRef`;
- the export `registerQueryClient()`.

That is strictly more than "this function's body, nothing else". It is the minimum needed, though: `apiFetch` is synchronous and not a hook, so it can only reach the cache through some reference to the `QueryClient`. It is also disclosed in §1 Amended (6), which names `registerQueryClient()` as a new `lib/branch.ts` export. It is not a scope violation (see the LOW below about wording).

**Is the module-level `QueryClient` reference a global store in disguise? No.** It holds no application state:
- There is no branch value in it.
- There is no subscriber list.
- There is no setter for any domain value.
- It has no reactive API.

The only copy of the active branch is the TanStack entry, and every React consumer reads it via `useQueryClient()` or context, not via this reference. `getActiveBranchId()` returns whatever the cache holds, so the reference cannot drift out of step with the cache.

The pattern matches the baseline precedent at `web/lib/api/client.ts:151`: `let sessionErrorHandler` plus `setSessionErrorHandler()`, registered from `useEffect` by `CustomerSessionGuard` and `IdleLock`. That precedent was accepted by the HO-ACC-002 gates under the same §4.3 "no global store" rule.

HO-CAT-002 §8.4 forbids a second store for state that is not server data, form state or a URL parameter. That is not what this is: it is a handle to the one sanctioned store.

**`web/app/providers.tsx`.** The change is outside §13 and is recorded in §1 Amended (6) and in ADR-FE-016's Status. It is minimal:
- one import;
- a three-line `useEffect(() => registerQueryClient(queryClient), [queryClient])`;
- an explanatory comment.

No other behaviour changed. `providers.tsx` is not in §14.

**Registration in a `useEffect`: ordering.** Child passive effects run before parent effects, so observers mounted in the first commit subscribe and start their fetches (for example `['staff','branches','mine']`, and `['admin', …]` on a deep link) before `Providers` registers the client. Those requests go out without `X-Branch-Id`.

This has no effect on behaviour:
1. At that moment the `['staff','active-branch']` entry is empty in any case. Nothing is persisted, and the seed only runs after `branches/mine` resolves. So a registered client would also have returned `null`.
2. Every network response arrives in a later task than the commit's passive-effect flush. The seed, and every later `setQueryData`, therefore always happens after registration.
3. With no header, the API defaults to the primary branch (HO-ADM-001 ADR-046). That equals the seed value in the no-refusal case.

Under React Strict Mode the effect runs mount, unmount and mount again. There is no cleanup and it registers the same instance, so that is idempotent.

Registering in an effect rather than in the `useState` initialiser is the correct choice. **TEST:** confirm E-B2 (header after a switch) against a fresh load.

**Nothing is persisted.** There is no `localStorage`, `sessionStorage` or cookie in `lib/branch.ts`, `useActiveBranch.ts` or `ActiveBranchPicker.tsx`. There is no query persister anywhere in `web/lib`, `web/app` or `web/package.json`, and `web/package.json` is unchanged. `sessionStorage` appears only in the MSW fixtures, as accepted in the first pass.

**`ActiveBranchPicker.tsx`** is unchanged by `62178ab`. The picker test now asserts against its own `QueryClient` cache.

FINDINGS

- [LOW] `docs/handover/HO-ADM-002.md:18` (§1 Amended (6)): the row describes `lib/branch.ts` as "the one function the baseline `08e873e` comment allows this handover to replace the body of", and counts only `providers.tsx` as "one additional file outside §13".
  - In fact the baseline contract was extended, with a module-level `let queryClientRef` and a second export, `registerQueryClient()`. The row does name that export.
  - The wording should say the contract was extended, not just the body replaced.

- [LOW] `web/lib/api/client.ts:12-15`: the header comment still says `lib/branch.ts` is "currently a documented no-op hook point rather than a real branch source". That is stale now that `getActiveBranchId()` reads the cache.
  - §13 permits `client.ts` edits for "send X-Branch-Id only", so a comment-only correction is in scope.

- [LOW] `web/lib/branch.ts:48-54` (TEST): no unit test exercises the real `registerQueryClient()` → `getActiveBranchId()` → `X-Branch-Id` path.
  - `tests/unit/lib/api-client-with-branch.test.ts` mocks `@/lib/branch`, and the picker test asserts the cache only.
  - The Strict-Mode regression described in Amended (6) is caught only by e2e E-B2.
  - TEST should confirm E-B2 is in the gate run.

- [LOW] `web/lib/hooks/useActiveBranch.ts:37-43` (TEST): `queryClient.clear()` removes the active-branch entry. This happens on idle lock (`IdleLock.tsx:159,168,308`), sign-out and sign-in.
  - Afterwards the header falls back to the primary branch until the observers re-render and re-seed.
  - This is consistent with "not persisted". TEST should confirm that after an idle-lock unlock the picker's displayed branch and the `X-Branch-Id` actually sent agree (no stale `b2` shown while primary is sent).

No BLOCKER or HIGH findings. The first pass's MEDIUM/LOW items are already `docs/backlog/ADM.md` rows 3-8 and are not repeated here.

Backlog candidates for `docs/backlog/ADM.md` (owner rule 2026-09-27): the four LOW findings above.

SCOPE CHECK
- files in allowed scope: 130/130
  - Explicitly in §13: 124.
    - This is the first pass's 123 plus `docs/gates/HO-ADM-002-ARCHITECTURE.md`, which is covered by `docs/gates/HO-ADM-002-*.md`.
    - It includes `web/components/branches/ActiveBranchPicker.tsx`, `web/lib/hooks/useActiveBranch.ts`, `web/tests/components/branches/active-branch-picker.test.tsx` and `web/docs/decisions.md`.
  - Recorded in §1 Amended 2026-09-28 (2):
    - `web/nav/areas/administration.ts`
    - `web/app/(auth)/policies/[key]/page.tsx`
  - Recorded in §1 Amended 2026-09-28 (6):
    - `web/lib/branch.ts`, via the baseline hook-point allowance plus the disclosed `registerQueryClient()` export.
    - `web/app/providers.tsx`, a minimal registration effect.
  - Covered by the §1 row itself: `docs/handover/HO-ADM-002.md`, append-only to the Amended row.
  - Covered by the owner rule of 2026-09-27: `docs/backlog/ADM.md`.
- files outside allowed scope: none
