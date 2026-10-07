GATE: ARCHITECTURE
HANDOVER: HO-ACC-001
COMMITS: 53fb683 328622e d8b0869 2ff0a6d (delta range c36d1d0..2ff0a6d, on top of the passes below)
DATE: 2026-09-25
STATUS: PASS

Fifth pass, a short delta review. I reviewed `git diff c36d1d0..2ff0a6d` (47 files) in the ACC-001
worktree against HO-ACC-001 §1 Amended (1)–(18), §12, §13 and §14; ADR-342 and its third amendment;
`.ai/persistence-ef-core.md` §9; and the fourth-pass findings. I took the two owner decisions of
2026-09-25 as given: (A) a per-session CSRF token stored in the DB, and (B) the explicit pipeline is
ratified, with the `.ai/` guides left for the integrator after wave A.

I ran two commands, both read-only against the branch:
- `dotnet dotnet-ef migrations has-pending-model-changes`, run on a `git archive` export of 2ff0a6d
  in a scratch folder so it would not collide with SECURITY's parallel build. Result: "No changes
  have been made to the model since the last migration."
- `git merge-tree --write-tree master ho/ACC-001`, a dry-run merge. Results are under MERGE RISK.

I did not run the tests. Every runtime claim below is marked for TEST.

## History
- **First pass** (aa53249): FAIL. 2 HIGH, 7 MEDIUM, 5 LOW.
- **Second pass** (d0b19cf): FAIL. 1 HIGH, 2 MEDIUM, 5 LOW.
- **Third pass** (2165111): PASS. 1 MEDIUM, 3 LOW.
- **Fourth pass** (c36d1d0): FAIL. 1 BLOCKER, 1 HIGH, 6 MEDIUM, 1 LOW, and 4 files outside scope.
- **Fifth pass** (this file, 2ff0a6d): PASS. 0 BLOCKER, 0 HIGH, 3 MEDIUM, 4 LOW, plus integrator
  carry-overs.

## Fourth-pass findings: status now
- **BLOCKER, the HO-ACC-002 edit: RESOLVED.** Amended (18)(i) records the integrator's
  authorisation for HO-ACC-002 §1, `CONTRACTS.md` #22 and `docs/handover/README.md`.
- **HIGH, the CSRF lockout after a restart: RESOLVED in design.**
  - `CsrfTokenBinder` is deleted.
  - `CsrfTokenHash` is a durable column on both session rows, set at `Issue()` from a second
    `ISessionTokenIssuer.Issue()` call.
  - The middleware compares the hash of the header value against the stored hash in constant time.
  - A safe request with a missing or stale `pc_csrf` gets a fresh cookie.
  - ADR-342's third amendment corrects the false claim about restart equivalence and keeps the old
    text under a banner, as the never-delete rule requires.
  - `CsrfRestartTests.cs` exists. TEST must run it: sign in on host A, then `PUT /me`, sign out and
    sign in again on host B.
- **MEDIUM, 4 files out of scope: RESOLVED.** Amended (18)(b) ratifies `CorsOptions.cs` and the
  `Cors` section. Amended (18)(i) covers `CONTRACTS.md` and `README.md`.
- **MEDIUM, stale `.ai` guide docs: CARRIED as an integrator item** (owner decision B). They are no
  longer a finding against this branch.
- **MEDIUM, host CORS owned by Accounts: RESOLVED.**
  - `Api/Hosting/HostPipelineExtensions.cs` owns both forwarded headers and CORS.
  - It is called once from `Program.cs`'s shared block.
  - The policy is renamed to `HostAllowedOrigins`.
  - `AccountsModule` keeps no CORS or forwarded-headers code.
  - It is host code, not area code: it lives under `Api/Hosting/`, beside
    `SafeExceptionHandlerMiddleware`, and not under `Modules/`.
- **MEDIUM, dev CORS: RESOLVED.**
  - The local `appsettings.Development.json` now carries `http://localhost:3000`.
  - HO-CAT-002 §12.3 is corrected, and `.example` already had the entry.
  - See LOW 3: the local file does not travel with the branch.
- **MEDIUM, Q-H2 contract docs: RESOLVED.** The Q-H2 and Q-I1 rows carry `mobileMatchedOn`. HO-ACC-002
  §1 now covers `mobileMatchedOn`, the walk-in returning `201` on an additional-number-only match,
  and the new `400`.
- **MEDIUM, unused `Platform.Infrastructure.Host.WebApi` package: CARRIED as an integrator item.**
  It is still at `CatEcommerce.Api.csproj:18`, and the csproj is not in §13.
- **LOW, stale comments: RESOLVED in `AccountsModule`.** `Filters/ConcurrencyExceptionFilter.cs:12-13`
  still describes `UseHttpPipeline`. That file is §14 for this branch, so it is CARRIED as an
  integrator item.

## The questions asked
1. **Migration `AddSessionCsrfTokenHash`.**
   - It is consistent with the model: `has-pending-model-changes` is clean.
   - It is additive only: two `AddColumn` calls with `Down` drops, `character varying(128)`,
     `NOT NULL DEFAULT ''`, in schema `account`. It has no `DROP` and does not touch `clinic.*`.
   - The configuration maps it in `IEntityTypeConfiguration` as `csrf_token_hash`, 128, required.
   - Rows that exist before the migration get `''`. No header value hashes to `''`, so those
     sessions fail closed on writes until a GET re-issues the token. ADR-342 names this recovery
     path.
   - Scope: see MEDIUM 3.
2. **The aggregate.**
   - `CsrfTokenHash` has a private setter. It is set in `Issue()`, which already calls
     `UpdateVersion()`, and changed only through `RefreshCsrfToken`.
   - Refreshing raises no event and does not bump the version. This matches the
     `StaffSessionAggregate.RecordActivity` precedent that earlier passes accepted: bookkeeping, not a
     business event.
   - Concurrency is still covered by `xmin` (`IsRowVersion`, `CustomerSessionConfiguration.cs:40`,
     `StaffSessionConfiguration.cs:41`). The middleware swallows `DbUpdateConcurrencyException` as
     best-effort.
   - Unlike `RecordActivity`, it has no guard against an ended session (LOW 1).
3. **The two new commands.**
   - Handlers and `IValidator<TCommand>` validators are registered Scoped, explicitly, at
     `AccountsModule.cs:167-171`.
   - Handlers inject `IAggregateRootRepository<T>` and pass `InMemoryPublishOnly`. They return a
     `SessionInvalid` violation rather than throwing. Each mutates one aggregate.
   - **ActionRoles:** no rows are needed. ActionRoles maps `[RequireAction]` endpoint actions, and
     these commands are dispatched only by the middleware, with no endpoint.
   - **RecordClassification:** no entry is needed. It classifies tables, not columns, and both
     session tables are already classified.
4. **`HostPipelineExtensions.cs`** is host-owned and the policy is renamed. See the fourth-pass
   status above.
5. **`[CsrfExempt]` skips session resolution.** The code goes further than the documents say
   (MEDIUM 1).
6. **The rate limit on all of `PUT /me`.** It is documented only in part (MEDIUM 2).

## MERGE RISK (dry run: `git merge-tree --write-tree master ho/ACC-001`)
- The merge base is `1ae949c` and master is at `06644ae`, which already includes ho/CAT-002.
- **One conflict: `docs/handover/HO-CAT-002.md`, the §1 `Amended` row, lines 16-24 of the merged
  file.**
  - Master split the Amended cell into four separate `| Amended |` rows: HO-APT-002 plus three owner
    decisions dated 2026-09-25, followed by `*(append further rows here)*`.
  - This branch instead appended its "integrator-authorised one-line edit" text to the end of the
    original single HO-APT-002 cell.
  - **Resolution:** keep all four of master's rows, then add this branch's text as a fifth
    `| Amended |` row above the `*(append further rows here)*` marker.
- The §12.3 CORS correction in the same file merges **cleanly**. The merged text says the host uses
  an explicit `Cors:AllowedOrigins` allow-list.
- `HO-CAT-001.md` changed only on master. It is not touched here, so there is no conflict.
- Master changed no `src/`, `.ai/` or migration files, so the migrations snapshot does not conflict.
- **Semantic check:** master's `web/lib/api/client.ts` still calls `NEXT_PUBLIC_API_BASE_URL`
  cross-origin with `credentials: 'include'`. It echoes `pc_csrf` as `X-CSRF-Token` on unsafe
  methods, which fits the new design. The integrator's own `appsettings.Development.json` needs the
  `Cors` entry (LOW 3).

FINDINGS
- [MEDIUM] src/Api/CatEcommerce.Api/Sessions/SessionAuthenticationMiddleware.cs:~49-66, compared
  with HO-ACC-001 §1 Amended (18)(c) and ADR-342's third amendment ("Sign-in is now explicitly
  CSRF-exempt") — the code does more than the records describe.
  - Both records say `[CsrfExempt]` skips the **CSRF check**.
  - The code skips **session resolution entirely** for both sign-in endpoints, so they run as
    anonymous whatever cookies are sent. The code comment says so; the handover and the ADR do not.
  - For staff this changes session semantics that ADR-037 and R5 define:
    - A stale staff cookie on sign-in no longer triggers the R5 refusal.
    - An idle-expired session is no longer ended through `EndForIdleTimeout` and audited when it is
      detected at sign-in. The replaced session's row stays not-ended until its absolute expiry.
  - The attribute's name also understates what it does.
  - Fix: record the resolution skip, and its ADR-037 audit consequence, in Amended (18)(c) and
    ADR-342. Renaming the attribute is optional.
  - TEST should confirm: sign in as staff with an idle-expired staff cookie, and check whether an
    idle-end audit row appears.
- [MEDIUM] src/Api/CatEcommerce.Api/Controllers/CustomerProfileController.cs:44, compared with
  docs/handover/HO-ACC-001.md:1037 (§12.4) and HO-ACC-002 §1 — the rate-limit change is only partly
  documented.
  - Amended (18)(f) records rate-limiting the whole action as a judgement call. It does not state
    the effect: name-only and phone-list edits are now capped at `AccountSensitive`'s 5 per 60
    minutes per client IP (`RateLimitingOptions.cs:12`).
  - One named policy's partition is normally shared by every endpoint that uses it. If so, these
    edits share one bucket with `personal-data-export` and `deletion`, and five profile saves would
    block account deletion for an hour. TEST should confirm.
  - The §12.4 row still reads `PUT /api/customers/me | customer | C6 | 204`. The export and deletion
    rows carry `customer · account-sensitive`; this row does not.
  - HO-ACC-002 has no note that `ProfileForm` can now receive `429`. Its global `ApiRateLimitedError`
    handling probably covers it, but the contract is silent.
- [MEDIUM] src/Infrastructure/CatEcommerce.Infrastructure/Persistence/Migrations/20260925132759_AddSessionCsrfTokenHash.cs
  — no Amended row names this migration.
  - §13 limits `Migrations/**` to "the seven in §12.1 only". Amended (18)(c) implies a schema change
    but does not name the migration.
  - The parallel build plan approved on 2026-09-25 says only the integrator runs
    `dotnet ef migrations add`.
  - The migration itself is correct (question 1 above). What is missing is the record and the
    integrator's acknowledgement.
  - Fix: add the migration's name to Amended (18)(c), and have the integrator either accept it or
    regenerate it at merge.
- [LOW] src/Domain/CatEcommerce.Domain/Aggregates/Account/CustomerSessionAggregate.cs:96 and
  StaffSessionAggregate.cs (`RefreshCsrfToken`) — no guard against an ended session.
  - Its `RecordActivity` precedent throws when `EndedOn is not null`. This method does not.
  - It is harmless today: the middleware refreshes only a session it has just resolved, and a
    refreshed hash on an ended session is never read.
  - It is still a check the aggregate could make from its own state.
- [LOW] src/Api/CatEcommerce.Api/Sessions/SessionAuthenticationMiddleware.cs:~81 — the CSRF guard
  now reads `caller.Kind != Anonymous && resolvedSession is not null`, which fails open.
  - Before, a non-anonymous caller with no session hash was **refused**.
  - Today the two values are always assigned together, so there is no live bypass.
  - The shape no longer fails closed if a future caller source sets `caller` alone. For CODE_REVIEW
    and SECURITY.
- [LOW] Amended (18)(e) and the HO-CAT-002 §12.3 correction — the dev CORS fix does not travel with
  the branch.
  - The fix to `appsettings.Development.json` is local to this worktree: the file is git-ignored and
    listed in §14, so it is not in the diff.
  - Master and every other checkout still need the `Cors` entry added by hand. The only committed
    guidance is `.example` and HO-CAT-002 §12.3.
- [LOW] docs/handover/HO-ACC-001.md Amended (18)(i) and docs/handover/HO-CAT-002.md — two small gaps
  in the HO-CAT-002 edit.
  - (18)(i) lists HO-ACC-002, `CONTRACTS.md` and `README.md` but not the HO-CAT-002 edit, which is
    recorded only in HO-CAT-002 itself.
  - The authorisation was for a "one-line" §12.3 fix. The edit also appends to HO-CAT-002's Amended
    cell, and that append causes the merge conflict above.

CARRIED INTEGRATOR ITEMS (not findings against this branch)
- The `.ai/` guides still prescribe `UseHttpPipeline()`: host-composition.md:71,
  new-project-bootstrap.md:171, examples/platform-extension-reference.md:14, architecture.md:109 and
  `.claude/agents/backend-developer.md:33`. Update them after wave A (owner decision B).
- The unused `Platform.Infrastructure.Host.WebApi` reference in `CatEcommerce.Api.csproj:18`. TEST
  should check that it contributes no controllers to `MapControllers()`.
- The stale `UseHttpPipeline` comment at `Filters/ConcurrencyExceptionFilter.cs:12-13`.
- Resolve the `HO-CAT-002.md` Amended-row conflict at merge (MERGE RISK above).

VERIFIED COMPLIANT IN THE DELTA (no finding)
- **Layers.**
  - Domain gains only `string` fields and a DTO parameter.
  - The Read ViewModels carry `CsrfTokenHash` as `[JsonIgnore]`, so it never reaches a response
    body. Their query handlers remain `AsNoTracking` projections and write nothing.
  - The write on a GET is a **command** dispatched by the middleware, which has the staff
    `RecordActivity` precedent. It is not a query handler writing.
- **Handlers.**
  - The email step-up lockout (`RecordFailedLogin`) and the refusal of a squatted additional number
    mutate only `CustomerAccountAggregate`.
  - The squat check is a repository `ExistAsync` in the handler, which ADR-015 allows for
    uniqueness across aggregates.
  - Violations come back as responses, not throws.
- **Controllers.** Each still dispatches one command. Sign-in writes the CSRF cookie from
  `SignInResult.CsrfToken`, and no controller computes anything.
- **Host.**
  - There is still one host.
  - `AddInMemoryBusServices()` services are not hand-registered.
  - `ValidateScopes` and `ValidateOnBuild` are untouched.
  - No new package is added.
  - `HostCompositionTests.cs` covers the move.
- **Absent, as required.** None of the following appears: `IDispatcher`, `IEventPublisher`, a
  broker, Mongo, `202`, a rejection event, `CommandServices` or `QueuePublishOnly`.
- **ADR-342 third amendment.** It is append-only, and it keeps the wrong text under a correction
  banner.

SCOPE CHECK
- files in allowed scope: 47/47.
  - Several files count as in scope only on the basis of an Amended row or authorisation:
    - `HostPipelineExtensions.cs` and `Program.cs`: Amended (18)(d).
    - `CsrfExemptAttribute.cs`: `Api/Authorization/**`.
    - The two new validators: Amended (18)(c) names their commands.
    - `.ai/decisions/adr.md`: ADR-342 is owner-ratified, and the edit is append-only.
    - `HO-ACC-002.md` and `HO-CAT-002.md`: integrator authorisation, 2026-09-25.
    - The migration: `Migrations/**`, with a record gap (MEDIUM 3).
- files outside allowed scope: none.
- Result: PASS. 0 BLOCKER, 0 HIGH, 3 MEDIUM, 4 LOW.
  - All three MEDIUMs are gaps in the record, not structural problems.
  - One merge conflict awaits the integrator, in `docs/handover/HO-CAT-002.md`'s §1 Amended rows.
