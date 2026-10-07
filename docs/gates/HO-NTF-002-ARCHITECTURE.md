GATE: ARCHITECTURE
HANDOVER: HO-NTF-002
COMMITS: 83a4f10 275cf07 d7ce065 86c0098 778fae8 260e31b 66e8535 (reviewed as `git diff master...HEAD`, master `cffaa79`, branch ho/NTF-002, HEAD `66e8535`)
DATE: 2026-10-04
STATUS: PASS

First pass. This gate re-read all of HO-NTF-002, including the three §1 Amended rows dated 2026-10-04,
and every changed file. Conventions came from HO-CAT-002 §8.2 (layering), `web/docs/decisions.md`,
`web/README.md` ("Adding a functional area"), the `docs/handover/README.md` ADR table and
`.ai/decisions/adr.md`. The diff touches no `src/**` or root `tests/**`, so none of the backend
checklist applies.

This gate built and ran nothing. Every runtime claim is for TEST, including:
- whether the `srcDoc` lint rule actually fails `npm run lint` (U-C6) and whether its `ignores`
  path resolves from `web/`;
- whether the `sandbox=""` frame really runs no script (U-C5);
- the unread-count polling pause while the tab is hidden (U-A2);
- the preview debounce and abort (U-C2).

Owner rule (2026-09-27): only a BLOCKER or HIGH finding fails a gate. The MEDIUM and LOW findings
below belong in `docs/backlog/NTF.md`.

FINDINGS
- [MEDIUM] web/components/templates/useLiveTemplatePreview.ts:4,33 (called from
  web/components/templates/TemplateEditor.tsx:66) and web/components/inbox/InboxBadge.tsx:5,25: two
  components fetch their own data through a `lib/hooks` query hook. HO-CAT-002 §8.2 rule 4 says
  "components take props and callbacks; the page wires them together".
  - Rule 3 still holds: the queries are declared only in `lib/hooks/useNotificationTemplates.ts` and
    `lib/hooks/useInbox.ts`.
  - Master already has approved precedent for this shape: `FileUploader`, `PolicyAcceptancePrompt`,
    `ActiveBranchPicker` and the header session reads. The lint ban covers only
    `components/{account,admin,auth,customer}/**`.
  - Both cases have a reason. The preview is bound to react-hook-form state that lives inside the
    editor. The badge is a self-contained header widget that the layout passes into
    `PortalHeader`.
  - Not HIGH, because it follows an approved precedent. Backlog: either extend the lint folder list
    and lift the preview query to the page, or record the "self-contained widget" exception in
    `web/docs/decisions.md` so the rule matches the code.
- [LOW] web/lib/hooks/useAccountMutations.ts:71 (not in diff): `useChangeNotificationPreferences` has
  no callers now that the preferences page uses the new `useSaveNotificationPreferences`, which does
  the same thing plus `preferredChannel`. The doc comment at
  web/components/account/NotificationPreferenceMatrix.tsx:26 still names the old hook.
  - Leaving HO-ACC-002's hook alone was correct under §13 ("new hooks only").
  - Remove the dead hook and fix the comment in a later HO-ACC housekeeping pass.
- [LOW] web/lib/hooks/useCustomerLookup.ts:12: this is a second hook over the same `findCustomers`
  endpoint that `useFindCustomers` (`lib/hooks/useStaffCustomers.ts:29`) already wraps. It is a
  mutation because it runs on demand. That is a reasonable choice, but there are now two hooks for
  one endpoint, with different cache behaviour.
- [LOW] docs/handover/README.md:71 and :106 (not in diff, outside §13): two places still need
  updating.
  - Line 71: the table still allocates `ADR-FE-019 … 023` to HO-NTF-002.
  - Line 106: the "next new area starts at ADR-FE-159" pointer is now taken. It needs to become
    ADR-FE-164.
  - The integrator must update both at merge, before any new area allocates. The row for HO-ADM-002
    (013–018 vs the 014–020 actually used) is a pre-existing drift, not this handover's.
- [LOW] web/README.md:134 (not in diff): the README marks `mocks/handlers/index.ts` as
  "pre-wired, do not edit".
  - HO-NTF-002 §12/§13 explicitly authorises the alias import, and HO-ADM-002 set the same
    precedent, so the change is in scope.
  - The README now disagrees with two handovers. It should describe the alias pattern so a third
    area does not treat it as forbidden.

CHECKS (clean)
- **ADR numbering.** `web/docs/decisions.md` is purely additive (+84/−0). It has a numbering note and
  `ADR-FE-159…163` in §9 order (019→159 … 023→163).
  - On master `cffaa79` the used numbers are 001–020 and 149–158. Nothing at or above 159 exists on
    master or on any `ho/*` branch. APT-002 and SHP-002 have recorded nothing new, and their
    allocated ranges (024–028, 029–038) do not overlap.
  - This is the same remedy HO-ADM-002 used, and it is recorded in §1 Amended.
- **Admin nav "Message templates".** The developer's claim is verified.
  - `notification-templates` / "Message templates" appears nowhere in `web/` on master, on
    `ho/ADM-002` (already merged into master), or on any other `ho/*` branch.
  - HO-ADM-002 §1 Amended (2026-09-22) records that HO-NTF-002 would add it.
  - The diff appends exactly one entry (+6/−0) at the end of `adminNavItems` (Administrator,
    ShopManager = `NotificationTemplates.View`), so the merge produces no duplicate.
  - `git merge-tree master HEAD` is clean.
- **Staff Messages entry.** It is in `web/nav/areas/notifications.ts` (item `audience: 'staff'`), the
  registry route described in `web/README.md` "Adding a functional area". `(staff)/layout.tsx` is
  untouched, and no other area registers `/staff/notifications`. This is recorded in §1 Amended (4).
- **Mocks.** The handlers are under `web/mocks/handlers/notifications/` (6 files) and are aliased in
  `mocks/handlers/index.ts` as `notificationsAreaHandlers`. That adds 4 lines (+4/−0), the same
  pattern as HO-ADM-002. `areas/notifications.ts` stays the empty stub. `areas/accounts.ts`
  changes are additive and are recorded in §1 Amended (1).
- **Layering.** HO-CAT-002 §8.2 rules 2 and 3 hold:
  - No `fetch(`, `XMLHttpRequest` or axios appears in any added `app/`, `components/` or `lib/`
    code. Every new `lib/api/*` module goes through `apiFetch`.
  - No new `useQuery`/`useMutation` appears outside `lib/hooks/`.
  - `components/templates/useTemplateWrites.ts` holds local UI state only and takes callbacks.
  - Nothing outside `mocks/`, `tests/` and the existing dev harness or providers imports `@/mocks`.
- **Shared pieces are reused rather than copied:**
  - `PermissionDeniedState` (log page, customer page);
  - `useUrlFilterState` with `debouncedFields`/`flush` (log filters);
  - `ConfirmDialog`;
  - `UnsavedChangesGuard`, through `NotificationPreferenceMatrix`;
  - `useMyBranches`/`useAdminBranches` (`useBranchTimeZone`);
  - `findCustomers` / `useStaffCustomer`;
  - `LoadingState`.

  `SimplePager` is new, and its doc comment justifies it: `DataTable`'s pager is URL-wired
  internally, and the log switches to a card list below 768 px.
- **HTML rendering (ADR-FE-160/162).** `srcDoc` appears only in
  `components/notifications/SandboxedEmailPreview.tsx`, which sets `sandbox=""`,
  `title="Email preview"` and `referrerPolicy="no-referrer"`. `dangerouslySetInnerHTML` does not
  appear anywhere.
  - The lint rule (`eslint.config.mjs` +27/−0) catches JSX attributes, object-literal keys and
    `.srcdoc =` assignment, with the one file ignored.
  - No earlier config block sets `no-restricted-syntax`, so the flat-config "later replaces earlier"
    behaviour drops no other rule.
  - Inbox and log bodies are React text in `whitespace-pre-wrap`.
  - `InboxItem` links only when `isSafeInternalPath`.
- **No client-side template rendering (ADR-FE-163).** `lib/validation/templateContent.ts` only builds
  `{{name}}` / `[Link text]({{name}})` tokens, splices text at the caret and builds the zod schema
  from `limits`. It does no substitution and has no `LimitedMarkup` use in `components/templates/`.
  The preview is a server query keyed on the request, with `keepPreviousData` and an `AbortSignal`.
- **Unsubscribe (ADR-FE-161).** The page is a client component under `(auth)`. It reads the token
  from `location.hash`, then calls `history.replaceState`, and the call is guarded for Strict Mode.
  `useUnsubscribe` is a mutation fired only on click.
- **Scoped edits to HO-ACC-002 and HO-ADM-002 files match §13 or §1 Amended:**
  - `(portal)/layout.tsx`: InboxBadge only.
  - The preferences page: PreferredChannelField only, in the same form and the same Save.
  - `customers/[customerId]/page.tsx`: CustomerMessagesLink only.
  - `lib/api/account.ts`: three optional fields, plus an optional `preferredChannel` on the PUT.
  - `lib/api/errors.ts`: appended only.
  - `NotificationPreferenceMatrix.tsx`: three optional props, a no-op when omitted.
  - `portal-header.tsx`: an optional `sessionActions` slot.
- **No new dependency.** `web/package.json`, `package-lock.json`, `tsconfig.json` and
  `next.config.ts` are unchanged.
- **`docs/handover/HO-NTF-002.md`.** Only the §1 Amended cell changed (+1/−1). No section body was
  edited.
- **Deferred to CODE_REVIEW** (its §19 focus):
  - `TemplateEditor.tsx` (215 lines), `NotificationLogFilters.tsx` (204) and
    `NotificationLogTable.tsx` (187) exceed the ~150-line guide in §11.
  - `mocks/handlers/notifications/templates.ts` is 652 lines, but it is a mock.
- **Template defect.** `.ai/templates/*.cs` is not in scope for a web-only diff, so it was not
  re-reported here.

SCOPE CHECK
- files in allowed scope: 90/90. That counts:
  - Literal §13 (84):
    - `app/**` routes (9);
    - `components/{inbox,notifications,templates}/**` (30);
    - `components/account/PreferredChannelField.tsx`;
    - `components/admin/adminNavItems.ts` (one entry);
    - `lib/api/{inbox,unsubscribe,admin-notification-templates,notifications-log}.ts`;
    - `lib/api/account.ts` and `lib/api/errors.ts`;
    - `lib/hooks/**` (8, all new files);
    - `lib/routing/isSafeInternalPath.ts`;
    - `lib/validation/templateContent.ts`;
    - `mocks/handlers/notifications/**` (6) and `mocks/handlers/index.ts`;
    - `eslint.config.mjs`;
    - `tests/components/{inbox,notifications,templates,account}/**` (12);
    - `tests/e2e/notifications-*.spec.ts` (5);
    - `web/docs/decisions.md`;
    - `docs/test-cases/TC-HO-NTF-002.md`.
  - Recorded in §1 Amended, 2026-10-04, each with a reason (5):
    - `web/mocks/handlers/areas/accounts.ts`;
    - `web/components/account/NotificationPreferenceMatrix.tsx`;
    - `web/components/common/portal-header.tsx`;
    - `web/nav/areas/notifications.ts`;
    - `web/tests/unit/is-safe-internal-path.test.ts`.
  - `docs/handover/HO-NTF-002.md`, §1 Amended cell only (1). The precedent is HO-FIL-002 and
    HO-ADM-002.
- files outside allowed scope: none
- §14 forbidden scope: untouched.
  - No `src/**`, root `tests/**`, `.ai/**` or `.claude/**`.
  - No `docs/**` beyond the named paths and the handover's own §1.
  - No `web/package.json`, `tsconfig.json` or `next.config.ts`.
  - The uncommitted `web/public/mockServiceWorker.js` change in the worktree is not part of the
    reviewed commits.

VERDICT: PASS. There are 0 BLOCKER, 0 HIGH, 1 MEDIUM and 4 LOW findings. The MEDIUM and LOW findings
go to `docs/backlog/NTF.md`. The two README items (`docs/handover/README.md` ADR table and pointer,
`web/README.md` mocks note) are for the integrator at merge.
