GATE: ARCHITECTURE
HANDOVER: HO-ADM-001
COMMITS: 6dec5d0 0c2a617 8495e32 (delta a311677..8495e32, 21 files; integrator doc commit 6d9f8ab accepted)
DATE: 2026-09-26
STATUS: PASS

HISTORY (condensed)
- Pass 1 (08e1dd2, range master 7abc9e4...7f5c284): FAIL. 0 BLOCKER, 4 HIGH, 9 MEDIUM, 8 LOW, 2 files
  outside scope.
- Pass 2 (delta 08e1dd2..04195cc): FAIL. 1 BLOCKER (`Domain.csproj` `InternalsVisibleTo`), 1 HIGH
  (`ADR-055` heading collision), 3 MEDIUM, 7 LOW.
- Pass 3 (delta 72d92f0..4463196): FAIL. 1 BLOCKER (unauthorised README "Operational notes"), 1 MEDIUM,
  6 LOW.
- Pass 4 (delta 4463196..a020bcb): PASS. 0 BLOCKER, 0 HIGH, 1 MEDIUM (§12.3/§12.4 missing
  `Policies.Manage`), 2 LOW.
- Pass 5 (this file, delta a311677..8495e32): PASS. 0 BLOCKER, 0 HIGH, 2 MEDIUM, 3 LOW.

WHAT I REVIEWED
- `git diff a311677..8495e32`: every source file (18) read in full, the two test files checked for
  test names and scope, and the handover diff (Amended (32)–(36)).
- 6d9f8ab (integrator): adds the `Policies.Manage` row to §12.3, splits the §12.4 policy row
  (GET → `Content.Manage`, POST → `Policies.Manage`), and adds an HO-ADM-002 Amended row. Accepted.
- ADR-341, ADR-342 (with all three amendments), ADR-051 addendum, ADR-053, HO-ADM-001 §12.4, §13,
  §14, `.ai/enumeration-pattern.md`, `.ai/read-side-pattern.md`.
- The other global filters for precedent: `ConcurrencyExceptionFilter` (Program.cs, global),
  `UniqueConstraintExceptionFilter` (AccountsModule, global), `MaintenanceModeFilter`
  (AdministrationModule, global but opt-in by marker), `RequireActionFilter`
  (`IAsyncAuthorizationFilter`), `CaptchaValidationFilter` (action-scoped `IActionFilter`).

COMMANDS RUN
- `git archive 8495e32` into the scratchpad, with no git-ignored appsettings (only `appsettings.json`
  and the `.example` were present). `dotnet build`: 0 errors. The 20 warnings were all `NU1900`
  (vulnerability feed unreachable, environmental). There were no code warnings.
- `dotnet dotnet-ef migrations has-pending-model-changes` (Infrastructure, startup Api): "No changes
  have been made to the model since the last migration."
- The scratch folder was deleted. I did not run the tests; runtime claims are marked for TEST.

PASS-4 FINDINGS: VERIFICATION
- MEDIUM (§12.3/§12.4 missing `Policies.Manage`): **closed** by integrator commit 6d9f8ab.
- LOW 1 (dead `ContainsDisallowedControlCharacter`): **closed** by Amended (36); the method is deleted.
- LOW 2 (`ISessionPolicy` override order, `MAIN` `InsertData` seed): **carried** (LOW 3 below).

THE QUESTIONS ASKED
1. **The global `NulByteRejectionFilter` registered from `AdministrationModule`: acceptable for this
   handover, but it should move to host composition.** See MEDIUM 1.
   - No `.ai/` rule forbids it outright. ADR-341 point 4 makes a module the home of *that area's*
     registrations. ADR-342 (second amendment) puts cross-area pipeline behaviour, meaning the one
     exception boundary, "exactly once, in Program.cs, as the single shared pipeline prefix every
     area runs behind", and says a module hook adds "ONLY that area's own" behaviour. A global
     MVC filter that acts on every area's controllers is the MVC-layer counterpart of that prefix.
   - The precedent it cites does not match. `MaintenanceModeFilter` is global but does nothing unless
     an action carries `[StorefrontEndpoint]`, so it changes no other area's behaviour. The real
     precedents are `UniqueConstraintExceptionFilter`, a global filter registered from
     AccountsModule and accepted there, and `ConcurrencyExceptionFilter` in Program.cs. So this is
     not a new anti-pattern. It is a second instance of one the integrator should consolidate.
   - The author could not do it correctly within scope. §13 limits `Program.cs` to §12.7, so
     registering it from the module was the only in-scope option.
   - **Recommendation:** at merge, the integrator moves the registration into
     `Program.cs` `AddControllers(options => ...)`, beside `ConcurrencyExceptionFilter`. The filter
     class stays in `Api/Filters`. Move `ContainsControlCharacter` from
     `BusinessViolationCodes.Administration.cs` to the base `BusinessViolationCodes.cs`. Track it
     alongside the existing "UseRateLimiter lives in AccountsModule" item. Do it at the same time
     for `UniqueConstraintExceptionFilter`.
   - **It does change other areas' error contracts,** by static reading. TEST/SECURITY should confirm:
     - **Accounts sign-in** (`AuthController`/`StaffAuthController`): a body containing a NUL now gets
       `400 ContainsControlCharacter` from the filter, not the generic `401 invalid_credentials`. The
       filter runs ahead of the handler, so the failed-attempt counter does not advance. That gives no
       brute-force gain, because such a request can never succeed. The 400 depends only on the input,
       never on the account, so it is not an enumeration oracle. However, any account whose stored
       password already contains a NUL (hashed before this filter existed) can no longer sign in or
       change its password. For SECURITY.
     - **Customer registration** (`[ServiceFilter(CaptchaValidationFilter)]`): a global filter
       with the same `Order` runs before an action-scoped one. A NUL body now gets 400 before the CAPTCHA
       is checked or consumed, where it used to get `CaptchaRequired`/`CaptchaInvalid` first. No
       account is created, so this is not a bypass. It is an ordering change HO-ACC-001 §12.6 did not
       specify.
     - **Authorization is unaffected.** `RequireActionFilter` and `RequireCustomerSessionFilter` are
       authorization filters, so 401/403 (with the audited denial) still happens before this filter.
       The rate limiter is middleware and still counts the request.
     - **Cats/Vaccines:** a NUL now gets 400 instead of a 500. That is an improvement, and it uses the
       same `ValidationResponse` body shape as the other 400s (ADR-014).
     - No existing test in any area sends a NUL (tree-wide grep), so none breaks. Amended (32) itself
       says HO-ACC-001's carried P13 needs confirming by that handover. That is correct, and it belongs
       to the integrator.
2. **`EnumerationParsing`, `MustBeValidEnumeration` and `ParseEnum`: the layering is fine.**
   - `EnumerationParsing` sits in `Shared.Common.Enums`, next to `Enumeration` itself and all seven
     enumerations it parses. It is a pure function with no dependencies, and Shared references only
     the platform packages.
   - `MustBeValidEnumeration` is a rule extension used inside the already-registered
     `IValidator<TCommand>` classes, and no registration changed. `AdministrationMapper`
     (Application) → Shared is the permitted direction.
   - `EnumQueryParameter` (Api) now delegates, so there is one lookup instead of two copies.
   - `ParseEnum`'s `InvalidOperationException` guards against a programming error ("validator rule
     missing"), not against an expected business outcome, so the HIGH rule on handlers throwing does
     not apply to it as designed. One path does reach it at runtime; see MEDIUM 2.
   - Enumeration values are still persisted on `Value` (no configuration changed), and case-insensitive
     lookup by `DisplayName` is only the way input is parsed. `.ai/enumeration-pattern.md` shows
     `FromDisplayName` only as an example, not as a rule.
3. **`PagingRules.MaxPageSize` rejecting with 400, while audit clamps: not governed by ADR-051 or §12,
   and acceptable.**
   - The ADR-051 addendum covers **unpaginated** lists capped at 100 with `X-Truncated`. The
     leave-request list is paged, so the addendum does not apply to it.
   - §12.4 says only "paged". The audit search's clamp to 200 (`AuditExportRules`) is left alone.
   - Rejecting at 100 is consistent with the same controller's own `pageNumber` rejection from
     pass 4.
   - It is inconsistent with both clamp precedents (audit here, and Cats in `ho/CAT-001-gates`),
     and it uses a different 400 body. See LOW 1.
4. **Scope: compliant.** See SCOPE CHECK.
5. **Absent, as required.** None of these appears in the delta: `IDispatcher`, `IEventPublisher`,
   `IBusMessageDispatcher`, a broker, Mongo/Bson, `202`, a rejection event, `CommandServices`,
   `QueuePublishOnly`, a bus-service hand-registration, a package reference, `Program.cs`,
   `*.csproj`, `*.sln` or appsettings. The filter is added with `Filters.Add<T>()`, which is
   type-activated and has no dependencies, so `ValidateOnBuild` is unaffected.

FINDINGS
- [MEDIUM 1] src/Api/CatEcommerce.Api/Modules/AdministrationModule.cs:68 and
  src/Api/CatEcommerce.Api/Filters/NulByteRejectionFilter.cs:33.
  - **The problem:** host-wide MVC behaviour is registered from one area's module (ADR-341 point 4
    and the ADR-342 second amendment put shared cross-area behaviour in host composition). The
    violation code it returns, `ContainsControlCharacter`, is declared in
    `BusinessViolationCodes.Administration.cs:53` but is not Administration-specific.
  - **What it changes for other areas:** Accounts sign-in goes from 401 to 400 on a NUL, and
    registration now rejects a NUL before the CAPTCHA is checked. Neither HO-ACC-001 nor HO-CAT-001
    specified this (Question 1).
  - **Why this is not HIGH:** the author had no in-scope alternative (§13 restricts `Program.cs`), and
    `UniqueConstraintExceptionFilter` in AccountsModule is an accepted precedent.
  - **Fix (integrator, at merge):** move the registration into `Program.cs` and the code into the base
    `BusinessViolationCodes.cs`. Get HO-ACC-001's acknowledgement of the two contract changes.
- [MEDIUM 2] src/Application/CatEcommerce.Application/DataMappers/AdministrationMapper.cs:118 and
  src/Application/CatEcommerce.Application/CommandHandlers/Validators/ContentCommandValidators.cs:22,:36.
  - **A blank `placement` still produces a 500.**
    - Step 1: `MustBeValidEnumeration` treats blank or whitespace as valid, and `Placement` has no
      `NotEmpty`.
    - Step 2: `ParseNullableEnum` short-circuits only on `null`.
    - Step 3: `ParseEnum("")` then gets `TryParse` → `true` with `result == null`, and throws
      `InvalidOperationException`.
  - So `"placement": ""` or `" "` on create or edit content ends in an unhandled 500. A form select
    with an empty "no placement" option is a plausible way to send it.
  - This existed before the delta (`FromDisplayName("")` also threw), but it is the exact class of
    bug that Amended (33) claims to close.
  - Fix: `ParseNullableEnum` should short-circuit on `IsNullOrWhiteSpace`, or the controller should
    normalise blank to null.
  - For TEST: confirm with `placement = ""`, since no current test sends it.
- [LOW 1] src/Api/CatEcommerce.Api/Controllers/AdminLeaveRequestsController.cs:30. There are now three
  paging behaviours for an oversized page:
  - leave-requests rejects above 100 with an ad-hoc `{ error }` body;
  - audit clamps to 200;
  - Cats clamps to 100 and uses `ValidationResponse` for its 400s.

  No `.ai/` rule picks one, and the handover is silent. A shared `PagingRules` helper that clamps
  (or rejects) the same way everywhere would help the frontend. The `PagingRules` doc comment's
  claim of consistency holds only within this one controller.

  Out of this handover's scope, for the integrator: `StaffAccountsController.cs:92` and
  `StaffCustomersController.cs:62` (HO-ACC-001) still apply `pageSize` with no cap.
- [LOW 2] docs/handover/HO-ADM-001.md Amended (32) and ValidationRuleExtensions.cs:13-15 say the
  per-field `NoControlCharacters` rules are kept because they "give a field-specific error".
  - For a **NUL** that can no longer happen: the global filter always intercepts first. The test
    rename in `AdminContentPolicyEndpointTests` proves it.
  - The per-field rules are still reachable for the other C0 characters, so keeping them is right.
    Only the stated reason is wrong.
- [LOW 3] (carried from pass 1, LOWs 4 and 7) two items are unchanged:
  - `ISessionPolicy` is overridden by registration order with no I-D4 test, and the registry
    defaults are hard-coded.
  - The `MAIN` branch seed is a hand-written `InsertData`.

INTEGRATOR NOTES
- Move the `NulByteRejectionFilter` registration (and `UniqueConstraintExceptionFilter`'s) into
  `Program.cs`. Track it with the `UseRateLimiter`-in-AccountsModule item (MEDIUM 1).
- Ask HO-ACC-001 to confirm its carried P13 (`PUT /api/customers/me` `fullName` NUL 500) is now
  closed by the global filter. Record the sign-in 401→400 on a NUL and the NUL-before-CAPTCHA
  ordering as accepted contract changes.
- Merge risk against `ho/CAT-001-gates` is unchanged from pass 4. The delta touches no Cat file.
  After the merge, Cat endpoints get the NUL filter; no Cat test sends a NUL.

SCOPE CHECK
- files in allowed scope: 21/21.
  - §13 covers:
    - `AdminLeaveRequestsController`;
    - the seven `Validators/*` files;
    - `AdministrationMapper`;
    - `tests/**` (2 files).
  - `BusinessViolationCodes.Administration.cs` is the ADR-341 partial of §13's append-only
    `BusinessViolationCodes`.
  - `AdministrationModule.cs` is this area's ADR-341 module, accepted in passes 1–4.
  - Amended rows cover the rest:
    - (21) `ValidationRuleExtensions`;
    - (29) `SlugFormat` and `CodeFormat`;
    - (30)/(35) `PagingRules`;
    - (32) `NulByteRejectionFilter`;
    - (33) `EnumerationParsing` and `EnumQueryParameter`.
  - `HO-ADM-001.md` covers its own Amended rows, per the passes 1–4 precedent.
  - 6d9f8ab (`HO-ADM-001.md`, `HO-ADM-002.md`) is an integrator-authored commit.
- files outside allowed scope: none.
- Result: PASS. 0 BLOCKER, 0 HIGH, 2 MEDIUM, 3 LOW.
