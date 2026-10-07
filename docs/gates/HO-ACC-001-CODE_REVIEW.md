GATE: CODE_REVIEW
HANDOVER: HO-ACC-001
COMMITS: full branch `git diff master...ho/ACC-001` (407 files), with the three commits since the
last gates (`64883ec..df6739f`: b830eb5, bfa3ce6, df6739f) reviewed for security as well as
correctness, since no security gate has seen them yet.
DATE: 2026-09-25
STATUS: PASS

This is the first CODE_REVIEW pass for this handover. ARCHITECTURE (fifth pass) and SECURITY (third
pass) both PASSED at `2ff0a6d` with their own findings already on record in
`docs/gates/HO-ACC-001-ARCHITECTURE.md` / `-SECURITY.md`; I did not re-litigate anything they already
covered and closed. I read the full `git diff master...ho/ACC-001`, prioritising `src/`, then did a
line-by-line review of the three post-gate commits, then sampled handler logic, concurrency,
EF queries, session state machines, the merge/walk-in flows, audit writes, time handling, null
handling and disposal across the rest of the branch. I did not run the test suite (Postgres is
shared with TEST, running in parallel on this same worktree/database per the task's instructions;
ARCHITECTURE and SECURITY already ran the committed suites and probes at `2ff0a6d` and reported
348/348 green with no regressions, which the three post-gate commits' own new tests are consistent
with by inspection).

## The three new commits — security review

**b830eb5 (`RateLimitPolicies.ProfileUpdate`, customer-partitioned).**
- **Spoofing.** The partition key is `customer:{caller.AccountId}`, and `caller` is read from
  `HttpContext.Items[ResolvedCaller.HttpContextItemsKey]`
  (`AccountsModule.cs:304`), written only by `SessionAuthenticationMiddleware` at
  `SessionAuthenticationMiddleware.cs:129` after it resolves the caller from a server-side session
  lookup keyed by the hash of the `pc_session` cookie. Nothing in the request (headers, body, query)
  can set this value directly — it cannot be spoofed.
- **Evasion by rotating sessions.** `AccountId` is the customer account id, not the session token or
  its hash — signing out and back in as the same customer yields a new session row but the same
  `AccountId`, hence the same partition key. Confirmed by reading `ResolvedCaller.Customer(Guid
  customerAccountId)` (`ResolvedCaller.cs:33`) and `TryResolveStaffAsync`/the customer branch in
  `SessionAuthenticationMiddleware.cs:73-80`, both of which derive the caller from the account id
  returned by the session query, never from the token itself. A fresh sign-in does **not** buy a
  fresh bucket.
- **Pipeline order.** `UseAccountsModule()` (`AccountsModule.cs:272-276`) calls
  `app.UseMiddleware<SessionAuthenticationMiddleware>()` before `app.UseRateLimiter()` — the `Items`
  key is always written before the rate limiter's partition callback runs. Verified this is also the
  only place the key is written (`grep` for `HttpContextItemsKey` — one writer, three readers).
- **IP fallback.** Reached only when `caller.Kind != Customer` (anonymous or staff), consistent with
  `AddFixedWindowPolicy`'s existing IP-partition pattern used by `AccountSensitive` (already reviewed
  by SECURITY pass 1/2), and downstream of the host's forwarded-headers configuration
  (`Program.cs:168`, `HostPipelineExtensions.cs`), not a new attack surface.
- Test coverage (`ProfileEndpointTests.cs`, new) directly proves: a 6th request in-window 429s; five
  profile saves do not block a genuine deletion attempt (separate bucket); two different customers
  from the same client/IP do not share a bucket. **Gap, not a defect:** no test proves the same
  customer signing out and back in still shares the old bucket (the "rotate to evade" scenario the
  task asked about) — this holds by the code's own construction (see above), but is only verified by
  reading, not by a regression test. Suggested addition, not blocking.

**bfa3ce6 (`ShouldRefuseUnsafeRequest` extraction, fail closed; `RefreshCsrfToken` ended-session
guard).**
- Old code gated the *entire* CSRF branch (both the unsafe-method refusal and the safe-method
  reissue) on `resolvedSession is not null`. That meant a hypothetical non-anonymous caller with no
  resolved session would skip the CSRF check entirely and the unsafe request would proceed
  unrefused — the LOW ARCHITECTURE flagged at `2ff0a6d`. New code moves the null check inside
  `ShouldRefuseUnsafeRequest` and returns `true` (refuse) for that combination
  (`SessionAuthenticationMiddleware.cs:151-160`). Traced both call sites
  (`TryResolveStaffAsync`, and the customer branch in `InvokeAsync`): caller and `resolvedSession`
  are always set together today, so this combination stays unreachable in practice — the fix is
  purely a shape change from fail-open-if-ever-reached to fail-closed-if-ever-reached, with no
  behavioural change for any currently reachable case.
- **Every previously-passing case preserved:** anonymous → never refused (unchanged, first line of
  `ShouldRefuseUnsafeRequest`); missing header → refused (unchanged); mismatched header → refused
  (unchanged, same `FixedTimeEquals` comparison, same operand order); matching header → accepted
  (unchanged). The safe-method reissue branch's behaviour is identical, just moved from an implicit
  `if` condition to an explicit `else if (resolvedSession is not null)`
  (`SessionAuthenticationMiddleware.cs:106`). Confirmed by reading, and by the new unit tests in
  `SessionAuthenticationMiddlewareTests.cs`, which exercise anonymous/matching/mismatched/missing/
  no-session combinations directly against the extracted pure function — good, targeted coverage of
  exactly the decision table that matters, with no HTTP plumbing needed to reach it.
- `RefreshCsrfToken` on both session aggregates now throws `DomainRuleViolationException` when
  `EndedOn is not null`, mirroring `StaffSessionAggregate.RecordActivity`'s existing guard. Both
  command handlers catch it and return a `CommandResponse` violation rather than letting it escape
  as an unhandled exception (ADR-014 shape, consistent with every other handler in this slice). New
  aggregate tests cover both the ended-session throw and the active-session success path for both
  `CustomerSessionAggregate` and `StaffSessionAggregate`.
- **`InternalsVisibleTo` scope.** `CatEcommerce.Api.csproj` adds exactly one
  `<InternalsVisibleTo Include="CatEcommerce.IntegrationTests" />` (no wildcard, no second
  assembly) — scoped to the one test project, as intended. `ResolvedSession` and
  `ShouldRefuseUnsafeRequest` are `internal`, not `public`, so nothing beyond that one assembly can
  see them.
- **`Items` key read timing.** Same pipeline-order fact as above applies here too — `caller` is
  resolved and written before `ShouldRefuseUnsafeRequest` is ever called, since both live inside the
  same `InvokeAsync` after the resolution block.

No new findings from this pair of commits. `df6739f` is a documentation-only commit (HO-ACC-001 §1
Amended (18), ADR-342's third amendment, `.ai/decisions/adr.md`) and introduces no code.

## Main review

**Handlers / violation paths.** Sampled `MergeCustomerCommandHandler`, `AddDeliveryAddressCommandHandler`,
`RefreshCustomerSessionCsrfTokenCommandHandler`/staff equivalent, `RecordAuthorizationDenialCommandHandler`.
All load the aggregate through `IAggregateRootRepository<T>`, mutate it, catch
`DomainRuleViolationException` and translate to a `CommandResponse` violation — no `CommandService`
layer, no aggregate state set directly by a handler (ADR-015). `EventPublishOption.InMemoryPublishOnly`
used consistently for aggregates, `DoNotPublish` for audit writes (F9).

**Concurrency.** `EfAggregateRootRepository<T>.UpdateAsync` (`EfAggregateRootRepository.cs:40-59`)
only calls `.Update()` when the aggregate is detached, with a documented rationale for why calling it
unconditionally (as the `.ai/persistence-ef-core.md` template does) would turn an ordinary first
write into a false `DbUpdateConcurrencyException` — a correct, well-reasoned deviation, called out
explicitly rather than silently diverging from the template. `xmin` is the concurrency token
(`IsRowVersion()`, F15), and `UpdateVersion()` (called at the end of every mutating aggregate method
I sampled) marks the aggregate root row dirty even when only an owned child collection actually
changed, which is what makes optimistic concurrency fire correctly for owned-collection races like
address adds (see the flaky-test analysis below). The CSRF re-issue race
(`TryReissueCsrfAsync`, `SessionAuthenticationMiddleware.cs:163-181`) deliberately swallows
`DbUpdateConcurrencyException` and leaves the client's existing cookie alone — correct, since the
reissue is opportunistic and must never turn a safe GET into a failure.

**EF queries.** `GetDuplicateCustomerGroupsQueryHandler` (Q-I1) is a good example of the
GROUP BY-in-SQL fix ARCHITECTURE's history records: `FindCandidateCustomerIdsAsync` translates
`Concat(...).GroupBy(...).Where(g => g.Count() > 1)` into SQL to find shared identifier *values*
first, and only loads the (typically small) set of *customers* touching one of those values into
memory — not every active customer. The per-page cat lookup is one batched `IN`-style query keyed
by every page member's id, not one query per member (the N+1 ARCHITECTURE flagged and this
handler's own doc comment records as fixed). All read-side queries sampled use `AsNoTracking()` and
project before materialising (`.ai/read-side-pattern.md`).

**Session lifecycle.** `SessionStateRules.Classify` (`SessionStateRules.cs`) is the single place both
aggregates and the read side derive `Active`/`Expired`/`Revoked`/`Ended`/`IdleExpired` from, so they
cannot disagree. Boundaries are consistent and not off-by-one: `asOf >= expiresOn` → `Expired`,
`asOf - lastActivityOn >= idleTimeout` → `IdleExpired` (both inclusive of the boundary instant, which
is the documented, tested convention, not an accidental off-by-one).

**Merge / walk-in flows.** `MergeCustomerCommandHandler` loads both accounts, refuses a non-active
survivor, and lets the aggregate's own `MergeInto` enforce the match rule and idempotent
reapplication; cats move afterward in `ReassignCatsOnCustomerMergedHandler`, one at a time via
`GetByFilterAsync`'s single-match contract, which is also what makes a partial-failure re-send safe
(each reassignment removes the moved cat from the next iteration's match set) — a real and
correctly-explained trade-off, not silent inefficiency. `AuditCustomerMergedHandler` and
`AuditCatOwnerReassignedOnMergeHandler` write the business-event and data-change audit rows off the
same `CustomerMergedEvent`/reassignment event, sharing one `CorrelationId`.

**Audit writes.** `AuditChangeCaptureInterceptor` adds the audit row to the *same* `DbContext` before
`SavingChangesAsync` actually runs, so it commits atomically with whatever it audits (§11.9, ADR-040).
It correctly matches owned entries to their root via the ownership FK rather than assuming one root
per transaction (needed for ADR-033's sign-in exception), and redacts secret-shaped property names
before serialising the before/after snapshot.

## Findings

- **[MEDIUM]** `src/Domain/CatEcommerce.Domain/Aggregates/Account/CustomerAccountAggregate.cs:263`
  (`MergeInto`) — `MergedOn = DateTimeOffset.UtcNow;` reads the wall clock directly inside the
  aggregate, instead of being threaded in from the caller the way every sibling timestamped mutation
  in this same aggregate is: `Anonymise` takes `AnonymisedOn` on `CustomerAnonymisationDto`
  (`CustomerAnonymisationDto.cs:6`) and just assigns it (`CustomerAccountAggregate.cs:297`);
  `RecordSuccessfulLogin`/`RecordFailedLogin` take `asOf` explicitly. `CustomerMergeDto`
  (`CustomerMergeDto.cs`) carries **no** timestamp field at all, and
  `MergeCustomerCommandHandler` doesn't even inject `TimeProvider` — there is nowhere for a
  caller-supplied time to come from today. This is the one place in the sampled aggregate code that
  breaks the project's own "no `DateTime`/`DateTimeOffset.UtcNow` in domain logic, `TimeProvider`
  everywhere" convention this handover is built around (§7 F-facts, and the review brief's own
  explicit ask). Consequence: `MergeInto` cannot be tested with a controlled clock, and indeed no
  test asserts `MergedOn`'s value at all (`CustomerAccountAggregateTests.cs:181-256` — six `MergeInto`
  tests, none reads `customer.MergedOn`). Fix: add `MergedOn` to `CustomerMergeDto`, inject
  `TimeProvider` into `MergeCustomerCommandHandler`, and pass `timeProvider.GetUtcNow()` through the
  same way `ToMergeDto` already threads the survivor's identifiers.

- **[MEDIUM] Test quality — the flaky `TwoConcurrentAddsAtNineAddresses` test is a test bug, not a
  production race.** `ProfileEndpointTests.cs:107-134`
  (`TwoConcurrentAddsAtNineAddresses_OneSucceedsOneConflictsNeverEleven`) hard-asserts that firing
  two concurrent `POST /addresses` requests at nine existing addresses always produces exactly one
  `201` and one `409`. Tracing the production code: `AddDeliveryAddressCommandHandler` loads the
  address book via `GetByIdAsync` on a per-request `DbContext`, and `CustomerAddressBookAggregate
  .AddAddress` (`CustomerAddressBookAggregate.cs:41-66`) throws a **400** business-rule violation
  (`DeliveryAddressLimitExceeded`) when the in-memory count is already at the 10-address cap — it
  only becomes a **409** `DbUpdateConcurrencyException` (via `xmin`) if both requests' reads
  genuinely overlap before either commits. Whether they overlap is a timing accident of two
  independent async HTTP calls fired with `Task.WhenAll` against a shared, possibly busy Postgres
  instance (§5.1 — `catecommerce_dev` is shared with dev and every other integration suite, and
  xunit runs test *classes* in parallel by default here — no `[CollectionBehavior
  (DisableTestParallelization = true)]` or shared `[Collection]` was found). If the two requests
  happen to serialize (the first's read-modify-write fully completes before the second's read even
  starts — more likely exactly when the shared database is under load from a concurrently-running
  suite), the outcome is legitimately `[201, 400]`, not `[201, 409]`, and
  `Assert.Contains(HttpStatusCode.Conflict, statuses)` fails despite the application behaving
  correctly in both orderings (the invariant that actually matters — never more than 10 addresses,
  never a 500 — holds either way, and the test's own final `Assert.Equal(10, ...)` would still pass).
  The team already recognises this exact class of nondeterminism one test below: `I-E5`
  (`TwoConcurrentFirstWrites_NeverBothLostNeverAServerError`, `ProfileEndpointTests.cs:138-164`)
  carries a comment explaining why a black-box HTTP race is not reliably forceable and asserts only
  the safe invariant instead of a specific status-code pairing — `I-E4` should get the same
  treatment. Fix: relax the assertion to `Assert.All(statuses, s => Assert.True(s is
  HttpStatusCode.Created or HttpStatusCode.Conflict or HttpStatusCode.BadRequest))` (or equivalent),
  keep the exactly-10 count assertion, and drop the `Assert.Contains(Conflict, ...)` requirement (or
  make it conditional on the two requests having actually raced, if that can be observed).

- **[LOW / documentation gap]** Every audit `AuditEntryDto.OccurredOn` call site (19 files under
  `DomainEventHandlers/Account`, `DomainEventHandlers/Cat`, plus
  `AuditChangeCaptureInterceptor.cs:82`) stamps `DateTimeOffset.UtcNow` directly rather than via an
  injected `TimeProvider`. This is consistent enough across the whole audit-writing surface that it
  reads as a deliberate choice (audit metadata about when the row was physically written, as opposed
  to business-relevant timestamps like session expiry or lockout, which do all go through
  `TimeProvider` correctly everywhere I sampled) rather than an oversight — but it is not written
  down anywhere as an intentional exception to the "`TimeProvider` everywhere" rule the rest of this
  handover holds to, and it means audit timestamps cannot be pinned to a fixed value in a unit test.
  Not blocking; worth either a one-line rule in `.ai/domain-events-and-outbox.md` or
  `AuditEntryAggregateTests.cs` recording the exception, or (if it's actually meant to be
  deterministic) threading `TimeProvider` through the same 19 handlers.

- **[LOW, carried, not new]** `src/Api/CatEcommerce.Api/Filters/ConcurrencyExceptionFilter.cs:12-13`
  still describes `UseHttpPipeline()`, which this branch no longer calls anywhere (ADR-342, rewritten).
  Already recorded by ARCHITECTURE (fourth pass) as a carried integrator item since this file is
  outside HO-ACC-001's own §14 scope to touch; repeating it here only for completeness, not as a new
  finding against this branch.

## Dead code / duplication

None found beyond what earlier gates already recorded. The `RefreshCsrfToken`/`Violation` duplication
between the customer and staff CSRF-refresh handlers (`bfa3ce6`) is an intentional, explicitly
cross-referenced mirror ("see RefreshCustomerSessionCsrfTokenCommandHandler's identical comment") —
consistent with how every other customer/staff pair in this slice is written (two aggregates, two
handlers, shared value objects), not accidental copy-paste.

SCOPE CHECK
- Reviewed: `git diff master...ho/ACC-001` in full (407 files, prioritising `src/`), with a
  line-by-line pass over `git diff 64883ec..df6739f` (19 files) for the three post-gate commits.
- Files outside this handover's declared scope: none beyond what ARCHITECTURE's SCOPE CHECK already
  named and resolved (all accounted for by Amended (18) or integrator authorisation).
