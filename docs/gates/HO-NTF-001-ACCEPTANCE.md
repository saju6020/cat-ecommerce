## Third pass

GATE: ACCEPTANCE
HANDOVER: HO-NTF-001
COMMITS: 0254f30 (docs only), following the second-pass FAIL record c635177; head 0254f30
DATE: 2026-09-27
STATUS: PASS

The one open HIGH is closed, and every §18 criterion keeps the evidence recorded in the first
pass. The code is unchanged since a432765, so the second pass's run still stands: a clean build,
415/119/82/534 green on a fresh catecommerce_ntfacc2, and a clean model. No tests were re-run this
pass, as instructed.

WHAT CHANGED
- `git diff --stat c635177 0254f30` touches exactly one file, `.ai/decisions/adr.md` (55
  insertions, 26 deletions).
- Every hunk falls inside ADR-056, 057, 061, 063 and 065 (adr.md lines 2467-2700). Nothing
  outside ADR-055 to ADR-066 is touched.
- ADR-055, 058, 059, 060, 062, 064 and 066 are unchanged, and they already matched §9 and the
  shipped design in the second pass.
- The edits are in place, not appended. That is acceptable: these are this handover's own ADRs,
  still unmerged and never on master, so there is no ratified text they overwrite.

SECOND-PASS FINDINGS: DISPOSITION
- [HIGH] ADR-057, the wrong producer mechanism: **CLOSED.**
  - The Decision now names `INotificationOutbox.Enqueue(NotificationRequestDto)` and
    `EfNotificationOutbox` adding to the scoped DbContext without saving (F23).
  - It restates §8.4's four producer rules:
    1. `Enqueue` in the handler, before `SaveAsync`/`UpdateAsync`.
    2. Never from a domain event handler (F24).
    3. Timed producers use `RequestNotificationCommand` with a `DeduplicationKey`.
    4. No business decisions in `Variables`.
  - The dedup mechanism is now correct: `ExistAsync` before insert, plus the store translating a
    23505 into success. That matches `RequestNotificationCommandHandler` lines 42 and 54.
  - It now agrees with CONTRACTS.md #16 and HO-APT-001:84, HO-BIL-001:113 and HO-IMP-001:110.
- [MEDIUM] ADR-061, the adapter contract: **CLOSED.**
  - All six points match §8.9: HTTPS and credentials outside committed files; a timeout counts as
    transient; the 429/5xx classification; `NotificationId` as the idempotency key; no body,
    subject or full destination in logs; the response truncated to 1000 characters.
  - The ADR-320 citation sentence is corrected to say where the citation actually lives
    (HO-NTF-001 §1).
- [LOW] ADR-056, fallback: **CLOSED.** C5's fallback commit is now named as the second instance of
  the rule-1 exception.
- [LOW] ADR-063, variable redaction: **CLOSED.** It now says the variables are redacted at
  `NotAfter` unconditionally, via `GetRequestsDueForVariableRedactionAsync`, which matches §8.10
  point 3 and the code.
- [LOW] ADR-065, guard timing and residual: **CLOSED.** It now says the guard re-checks at
  expansion and again before every delivery attempt (§8.7), and it records the A→B→A residual with
  a pointer to backlog #1.
- [LOW, carried] `PublicSiteOptions.cs:12`'s doc comment still refers to the removed
  `AccountOptions.WebBaseUrl`. **Still open, and not in docs/backlog/NTF.md.** It is outside NTF's
  own files; for the integrator.

CRITERIA
- A1 to A16 are all MET, on the evidence recorded in the first pass (named tests and source
  reads). The code is unchanged since the second pass's green run at a432765.
- As the handover states them:
  - A5's link targets `/portal/cats/{catId}` until HO-APT-001.
  - A8 is proven for the reset and care templates only.
  - OQ-1 is recorded as a gap: no provider, the `Unconfigured` senders and the startup warning.

PRIOR GATES
- ARCHITECTURE PASS (pass 2), SECURITY PASS (pass 3), CODE_REVIEW PASS (pass 5), TEST PASS (pass
  2). All are recorded in docs/gates/, and the TC sheet is at docs/test-cases/TC-HO-NTF-001.md.
- The first-pass note still applies. ARCHITECTURE last ran at 4efe96f; later product commits were
  covered by SECURITY, CODE_REVIEW and TEST, and have now been read by ACCEPTANCE.

SCOPE
- Out-of-scope work: none.
- Files outside allowed scope: none. `.ai/decisions/adr.md` is inside §13's allowance for
  ADR-055…066.

UNDECLARED DEVIATIONS
- None new. The second pass's ADR-057/061 mismatch is corrected.

FINDINGS
- [LOW] src/Infrastructure/CatEcommerce.Infrastructure/Configuration/PublicSiteOptions.cs:12 — the
  doc comment names the removed `AccountOptions.WebBaseUrl`. Carried from the second pass and not
  in the backlog. Integrator.

OPEN MEDIUM/LOW NOT IN docs/backlog/NTF.md
- The PublicSiteOptions.cs:12 comment LOW above. That is the only one. Everything else still open
  is in rows 1-17.

SCOPE CHECK
- files in allowed scope: 1/1
- files outside allowed scope: none

---

# Second pass


GATE: ACCEPTANCE
HANDOVER: HO-NTF-001
COMMITS: 8d2f329 (ADR-055 to ADR-066), 9a87428 (A-1, A-5, A-6, ADR-342 amendment), 3e764a5 (backlog rows 5-17), a432765 (merge of master 08e873e, HO-ACC-002); head a432765
DATE: 2026-09-27
STATUS: FAIL

This pass fails on one HIGH. The ADRs now exist, but ADR-057 (the producer rules) states the wrong
producer mechanism and contradicts every document that cites it. The first pass's HIGH is therefore
only partly closed. Everything else checked this pass is clean. The fix is a rewrite of one ADR
paragraph.

RUN BY THIS GATE (head a432765)
- `dotnet build CatEcommerce.sln --no-incremental`: 0 warnings, 0 errors.
- Fresh database `catecommerce_ntfacc2`, created with psql and migrated with `dotnet ef database update`.
  - History ends with `AddFiles`, then `AddNotificationsModule`.
  - `has-pending-model-changes` reports no changes.
- Suites against it: Domain 415/415, Application 119/119, Infrastructure 82/82, Integration 534/534.
  These match the developer's report.
- `CsrfCookieDomainTests`: all 31 cases passed in that run (the valid and invalid startup theories,
  sign-in and staff sign-in setting the domain only on the CSRF cookie, and deletion expiring it
  with the same domain).
- Dropped catecommerce_ntfacc2 (confirmed gone) and deleted this gate's scratch folder.

MERGE WITH MASTER 08e873e (HO-ACC-002): nothing lost
- Every file ACC-002 changed (180 files, cc5737e..08e873e) is byte-identical at HEAD, except four
  that NTF also touches. Each of the four is only NTF's own recorded change:
  - `AccountOptions.cs`: only `WebBaseUrl` is removed. `CsrfCookieDomain` and its
    `[RegularExpression]` validation are intact (lines 91-92).
  - `appsettings.json`: only `Account:WebBaseUrl` is removed and the `Notifications` block added.
    `"CsrfCookieDomain": ""` is intact.
  - `CustomerProfileController.cs`: only the `PreferredChannel` pass-through (§8.5) is added.
  - `HO-ACC-001.md` Amended cell: I compared it sentence by sentence against both parents. Every
    sentence from the NTF side (94) and the ACC-002 side (102) is present at HEAD. The two
    apparent misses were end-of-cell text only ("(50)." and "*(append further rows here)*"), and
    both are present. All dated rows from both sides are present, plus NTF's 2026-09-27 row.
- Dropping `Account:WebBaseUrl` is safe. No `.cs` or `.json` under `src/` or `tests/` reads it (the
  only hit is the git-ignored local `appsettings.Development.json`). §11.4 deleted its one reader.

FIRST-PASS FINDINGS: DISPOSITION
- HIGH, ADRs missing: **PARTLY CLOSED.**
  - ADR-055 to ADR-066 are appended, append-only (251 lines added, none removed). Every citation
    now resolves to a heading: CONTRACTS.md #16, APT (055/056/057), BIL (056/057), BRD
    (056/057/060), EMR (056/057/066), IMP (056/057).
  - ADR-055, 056, 058, 059, 060, 062, 063, 064, 065 and 066 match §9 and the shipped design. The
    amendments for (25), (40), (43)/(50) and (52) are folded in.
  - ADR-057 does not match. See HIGH 1.
- LOW A-1 (TC sheet location): **CLOSED.** It was moved to `docs/test-cases/TC-HO-NTF-001.md` as a
  100% rename.
- LOW A-5 (stale handover text): **CLOSED.**
  - §8.8 now names `PublicSiteOptions.WebBaseUrl`.
  - §15 U-F3 now shows the owner-id format, which matches `CareReminderSweep.cs:93`.
- LOW A-6 (overflow record): **CLOSED.** `appsettings.Testing.json` and the FileStorageModule
  comment are recorded in §1.
- Backlog item 9 (ADR-342's sketches out of date): **CLOSED** by an append-only amendment under
  ADR-342 that matches the shipped `Program.cs`.
- A-2, A-3 and A-4 and the other open items are backlog rows 5-17. Every open MEDIUM/LOW from the
  first pass's consolidated table is now either a backlog row or closed above.

FINDINGS
- [HIGH] .ai/decisions/adr.md:2492-2506 (ADR-057) — **the recorded producer rule is the wrong
  mechanism.**
  - What §9, §8.4 and the citing documents say:
    - §9 ADR-057 and §8.4: producers call `INotificationOutbox.Enqueue` in the command handler,
      before the aggregate's own save. The outbox adds the request to the scoped DbContext without
      saving, so the producer's save commits both together (F23).
    - Only timed producers use `RequestNotificationCommand` with a deduplication key.
    - CONTRACTS.md #16 cites "ADR-056/057" for exactly that `INotificationOutbox.Enqueue` contract.
    - HO-APT-001:84, HO-BIL-001:113 and HO-IMP-001:110 cite ADR-057 for "Messages only through
      `INotificationOutbox.Enqueue`, in the command handler before the save".
  - What the recorded ADR-057 says:
    - "Any command handler or timed producer that wants a notification sent dispatches
      `RequestNotificationCommand` inside its OWN command's unit of work, before that command's own
      save."
    - It never mentions `INotificationOutbox` or `Enqueue`.
  - Why following it would be wrong: `RequestNotificationCommandHandler` commits on its own
    (`requestStore.TrySaveAsync`, line 54). A producer that followed ADR-057 as written would
    either commit its own tracked changes early (the same scoped DbContext) or commit the request
    in a separate transaction. Both break the atomicity that ADR-056 records and §2 point 1
    promises: "never a reset token without its message".
  - Why this is HIGH: this is the rulebook that twelve later areas' ARCHITECTURE gates check
    against (§19 row 1; the ADR's own Reason says so). It also contradicts the shipped code: the
    password-reset producer uses `outbox.Enqueue` via `NotificationPasswordResetTokenDispatcher`,
    and the only `new RequestNotificationCommand` in `src/` is `CareReminderSweep.cs:72`.
  - Two smaller errors in the same paragraph:
    - The duplicate check is `requestRepository.ExistAsync` plus the store's 23505 translation,
      not `INotificationRequestStore` "against a unique index before insert".
    - §8.4 rule 4 ("never pass a business decision in `Variables`; if a message should not be
      sent, do not enqueue it") is replaced by a different rule about "rendered decisions".
      "Use the Sms wording" also sits awkwardly with the sanctioned `ChannelHint`.
  - Fix: rewrite the Decision to match §8.4's four producer rules word for word: `Enqueue` in the
    handler before the save; never from an event handler; timed producers use
    `RequestNotificationCommand` with a `DeduplicationKey`; no business decisions in `Variables`.
- [MEDIUM] .ai/decisions/adr.md:2570-2572 (ADR-061) — **the adapter contract is paraphrased
  wrongly, not recorded.**
  - §8.9 says the six-point contract is "recorded in ADR-061 so the adapter is reviewed against
    it". The ADR lists "never throw for a provider rejection, never log the message body, record
    the provider's own message id, and so on".
  - Two of those three points are not in §8.9.
  - The ADR omits the real points: HTTPS and secret-store credentials; a timeout that counts as
    transient; 429/5xx transient versus other 4xx permanent; `NotificationId` as the idempotency
    key; no body, subject or full destination in logs; provider response truncated to 1000
    characters.
  - ADR-320's `SmtpEmailSender`/`HttpSmsGateway` would be reviewed against a misstated contract.
    Its closing sentence ("ADR-320 already cites this ADR") is also inaccurate: ADR-320's own text
    in adr.md does not cite ADR-061. The citation lives in HO-NTF-001 §1 Amended.
  - Fix: copy §8.9's six points verbatim.
- [LOW] ADR-056 records the multi-root commit exception for expansion only. §9 names expansion
  **and fallback** (C5 enlists the fallback notification in the same commit).
- [LOW] ADR-065 says validity is re-checked "at expansion time". §8.7, and
  `DeliverNotificationCommandHandler` in the shipped code, also re-check before every delivery
  attempt. It also does not mention the known A→B→A residual (backlog #1).
- [LOW] ADR-063's "the request-level variables are redacted at `NotAfter` if the notification has
  not already reached [a terminal state]" reads as though variables stay in plain text once the
  notification is Sent. The shipped sweep (`GetRequestsDueForVariableRedactionAsync`) redacts them
  at `NotAfter` unconditionally, which is what §8.10 point 3 requires.
- [LOW, carried, pre-existing] `PublicSiteOptions.cs:12`'s doc comment still refers to the removed
  `AccountOptions.WebBaseUrl`. The file is not NTF's; for the integrator.

SCOPE
- Out-of-scope work found: none. The three docs commits touch only `.ai/decisions/adr.md`
  (append-only, allowed by §13, plus the ADR-342 note the ARCHITECTURE gate asked for),
  `docs/handover/HO-NTF-001.md`, `docs/backlog/NTF.md` and the TC rename. The merge brings only
  master's own ACC-002 content.
- Files outside allowed scope: none. The TC sheet is now at the §13 path.

UNDECLARED DEVIATIONS
- ADR-057 and ADR-061 as recorded do not match §9/§8.4/§8.9, and the commit message presents them
  as matching the handover.

OPEN MEDIUM/LOW NOT YET IN docs/backlog/NTF.md
- The ADR-061 MEDIUM and the four LOWs above (ADR-056 fallback, ADR-065 delivery re-check, ADR-063
  wording, PublicSiteOptions comment). Everything else open is in rows 1-17.

SCOPE CHECK
- files in allowed scope: 5/5 in the NTF commits (the merge's ACC-002 files are master's own,
  verified byte-identical above)
- files outside allowed scope: none

---

# First pass

GATE: ACCEPTANCE
HANDOVER: HO-NTF-001
COMMITS: range cc5737e..e7a3cdb on ho/NTF-001 (master cc5737e merged at 0988eda; the migration was regenerated at f5f9ece; last product fix d5d06da; head e7a3cdb)
DATE: 2026-09-27
STATUS: FAIL

The gate fails on one HIGH: the handover's twelve ADRs (ADR-055 to ADR-066) were never recorded.
Everything else passes. Every §18 criterion has concrete evidence, the build and all four suites are
green on a fresh database, the migration is correct and the merge with master lost nothing. The fix
is documentation only and should be quick.

PRIOR GATES (checked from docs/gates/ and the TC sheet, not from commit messages)
- ARCHITECTURE: PASS, pass 2, head 4efe96f (docs/gates/HO-NTF-001-ARCHITECTURE.md).
- SECURITY: PASS, pass 3, head 5aad2d7, recorded in ebeba86 (docs/gates/HO-NTF-001-SECURITY.md).
- CODE_REVIEW: PASS, pass 5, 1ba4025. Pass 4 reviewed 471e52f, the merge 0988eda and f5f9ece; pass 5
  reviewed d5d06da and 217024a.
- TEST: PASS, pass 2, e7a3cdb. TC sheet: 120 rows, all PASS, none BLOCKED or NOT RUN.
- Coverage gap (not a failure): ARCHITECTURE last ran at 4efe96f. The later product commits ce1991c,
  5cf5de4, 54faffb and 8fd5a37 (the new `RecordNotificationExpansionFailureCommand`, which opens a
  fresh scope), the 5aad2d7 squash, 471e52f and d5d06da were reviewed by SECURITY and/or CODE_REVIEW
  but never by ARCHITECTURE.

RUN BY THIS GATE (head e7a3cdb)
- `dotnet build CatEcommerce.sln`: 0 warnings, 0 errors.
- Created a fresh database `catecommerce_ntfacc` with psql, then ran `dotnet ef database update`.
  - `__EFMigrationsHistory` ends with `20260927071352_AddFiles`, then
    `20260927123315_AddNotificationsModule`, so the order is correct.
  - `has-pending-model-changes` reports "No changes have been made to the model since the last
    migration."
- Suites, with CATECOMMERCE_TEST_DB pointing at catecommerce_ntfacc:

  | Suite | Result |
  |---|---|
  | Domain | 415/415 |
  | Application | 119/119 |
  | Infrastructure | 82/82 |
  | Integration | 500/500 |

  These match the TEST gate's pass-2 counts. Rows landed in `notify.*` in this database, so the run
  really used it.
- Checked the database directly after the full run:
  - 5,295 audit rows. None contains `token=`.
  - No audit row has an EntityType of either notify root, apart from the `NotificationResent`
    business event.
  - This is independent evidence for ADR-066 and A13. See the I-B3 note below for why it was needed.
- Dropped catecommerce_ntfacc afterwards (confirmed gone) and deleted this gate's scratch folder.

MIGRATION
- Every object `AddNotificationsModule` creates is in the `notify` schema: the schema itself, 5
  tables, 17 indexes and 2 foreign keys, whose principal tables are also in `notify`.
- The only operation outside `notify` is
  `AddColumn account.customer_notification_preferences.preferred_channel integer NULL`. §12.1
  requires it as the Account amendment. It adds a column; it creates no object.
- There is no `Sql()`, nothing touching `cat_photo`/`stored_file`, and no drop of anything that
  master owns.
- `Down` reverses everything that `Up` does.
- The snapshot diff against master contains no removed lines.

MERGE WITH MASTER (cc5737e)
- `git diff cc5737e HEAD` changes three master-owned files that are not Notifications files:
  - `FileStorageModule.cs`: a comment correction only. The code is byte-identical to master.
  - `SettingKeys.cs`: a doc comment, plus this area's keys moving to `SettingKeys.Notifications.cs`,
    which is sanctioned. `SettingKeys.Files.cs` is untouched.
  - `AccountsModule.cs`: the recorded rate-limiter move (32) and the deletion of the two old
    dispatchers.
- The only deleted files are the two HO-ACC-001 dispatchers that §11.4 orders deleted.
- No FIL-001 file, migration or config key is missing.
- Conclusion: the merge lost nothing.

CRITERIA (§18)
- A1 MET. Evidence:
  - U-B1 `NotificationRoutingPolicyTests.Transactional_PreferredSms_BothIdentifiers_PrimarySms_InboxOn`
  - U-B2 `..._PreferredSms_NoMobile_FallsBackToEmail`
  - I-B4 `NotificationRoutingEndpointTests.PreferredEmail_DeliversEmail_WithInboxCopy`
- A2 MET. Evidence:
  - U-B9 `NotificationRoutingPolicyTests.NextFallback_AfterSms_ReturnsEmail_ThenNull`
  - U-B11 `NotificationAggregateTests.RecordAttempt_*`
  - I-B5 `NotificationRoutingEndpointTests.SmsPermanentFailure_FallsBackToEmail`
  - I-B7 `SmsAndEmailBothPermanentlyFail_EndsUndeliverable_NoInfiniteFallbackLoop`
- A3 MET. Evidence:
  - U-A13 `NotificationTemplateAggregateTests.EditTwiceThenReset_VersionsIncrementThenContentsCleared`
  - I-A2 `NotificationTemplateEndpointTests.EditThenGet_ReturnsCustomisedAtIncrementedVersion`
  - I-A4 `Preview_RendersSampleData_WritesNothing`
- A4 MET. Evidence:
  - U-A1 `TemplateRulesTests.UnknownPlaceholder_NamesTheVariableAndPropertyPath`
  - U-A2 `MalformedPlaceholder_IsRejected`
  - I-A3 `SaveWithUnknownPlaceholder_Returns400NamingTheVariable`
- A5 MET. Evidence:
  - U-F2 and I-F2 `CareReminderEndpointTests.ReminderWindowBoundaries_*`
  - I-F1 `VaccinationDueIn14Days_SweepRequestsReminder_DeliveredWithCatNameVaccineDateAndLink`,
    which asserts the body contains `/portal/cats/{catId}`
  - I-F3 `DewormingDueIn7Days_RequestsDewormingReminder`
  - I-F8 `ChangingLeadDaysSetting_SweepPicksUpItemDueIn25Days`
  - The link is recorded as the handover states it. `NotificationLinkBuilder.CatCareLink` returns
    `{WebBaseUrl}/portal/cats/{catId}` (source line 20). HO-APT-001's Amended change (3) to
    `/book?catId=` is correctly not built here, and the booking-flow link stays deferred per §4.2.
- A6 MET. Evidence:
  - U-F1 `CatSubjectGuardTests`, 9 cases
  - I-F4 `CatMarkedDeceasedAfterRequest_BeforeDelivery_Cancelled`
  - I-F5 `TransferCatToNewOwner_BeforeDelivery_Cancelled_NextSweepRequestsForTheNewOwner`: fixed by
    d5d06da and passing unweakened
  - I-F6 `RecordNewVaccinationBeforeDelivery_Cancelled_NoNewReminderUntilTheNewWindow`
  - I-F7 `DeceasedCatAtSweepTime_NoRequestAtAll`
  - One residual remains, the A→B→A missed reminder. It is a MEDIUM already in the backlog (#1). It
    produces no duplicate (I-F9), and it is not a reminder for a deceased cat or an inactive record.
- A7 MET. Evidence:
  - U-A4, U-B4 and U-B5 (named tests in TemplateRulesTests and NotificationRoutingPolicyTests)
  - I-B9 `Marketing_ConsentOnEmailOnly_DeliversEmailWithUnsubscribeHeaders`
  - I-B10 `Marketing_NoConsentAnywhere_Suppressed_NothingSent`
  - I-E1 to I-E5 in `UnsubscribeEndpointTests`: valid, repeat and unknown tokens, one-click, the 429
    on the 21st request, and cancellation with `RecipientOptedOut`
  - U-E1 by source: `ExpandNotificationRequestCommandHandler` lines 216-218 use
    `RandomNumberGenerator.GetBytes(32)`, base64url encoding and SHA-256, and only the hash is stored.
- A8 MET, within the scope the handover states. Evidence:
  - U-B3 `Transactional_IgnoresMarketingOptOut`
  - I-B1 `PasswordResetDeliveryEndpointTests.ResetRequestByMobile_DeliversSms_NoInboxCopy_RedactedAfterSend`
  - I-B8 `TransactionalTemplate_DeliveredRegardlessOfMarketingOptOut`
  - This is proven only for the templates that exist (reset and care). Payment and incident
    templates are later areas', and they inherit the rule by category.
- A9 MET. Evidence:
  - U-C1 to U-C3 in `QuietHoursTests`
  - I-C1 `NormalReminder_At2230_IsQueuedAndDeferredUntil0800_NotSentBefore_SentAfter`
  - I-C4 `BranchQuietHoursOverride_AppliesToARequestCarryingThatBranchId`
  - U-C4's TC evidence cites an unrelated redaction test, so I checked the source instead.
    `DeliverNotificationCommandHandler` lines 46-51 check `NotAfter` before anything else, so a held
    notification is never sent late: it becomes Expired on the first pass after the hold ends. See
    LOW A-3.
- A10 MET. Evidence: I-C2 `QuietHoursEndpointTests.UrgentTemplate_At2230_SentAtOnce`.
- A11 MET. Evidence:
  - I-D1 `NotificationLogAndInboxEndpointTests.SearchByRecipientDestinationTemplateStatus_FindsMatchingRows_DestinationsMasked`
  - I-D2 `Detail_ShowsFullDestinationAttemptsAndFallbackChain`
- A12 MET. Evidence:
  - U-D1 `NotificationResendEvaluationTests`, 8 cases
  - I-D3 `Resend_UndeliverableSmsOnEmail_Creates201WithNewIdAndAuditRow`
  - I-D4 `Resend_SecretNotification_InApp_AndFourthResend_AllRefused` and `ResendLimitReached_AfterThreeResends`
  - I-D5 by source: `ResendNotificationCommandHandler` lines 45-48 re-resolve the recipient and take
    `targetDestination = recipient.DestinationFor(targetChannel)` from the live directory. No test
    changes the mobile number part-way through; that gap is backlog #3.
- A13 MET. Evidence:
  - I-B1 (above)
  - I-B2 `AccountSaveFailsAfterEnqueue_NeitherTokenNorNotificationRequestExists`, which uses a real
    PK violation and asserts that neither the request nor a second token exists
  - I-B3 `ResetLinkAndToken_NeverAppearInAuditLog` and
    `LogDetail_OfQueuedOrRetryPendingResetNotification_NeverShowsTheLiveToken`
  - I-X1 `NotificationLogCaptureEndpointTests.RepresentativeFlows_NeverLogTokenLinkOrRenderedVariableValues`
  - This gate's own database query above (no audit row contains `token=`)
  - Source: `RequestPasswordResetCommandHandler` dispatches at lines 64 and 83, before the saves at
    lines 68 and 87.
- A14 MET, behaviour only. Evidence:
  - U-B15 `SenderConstructionTests.*_ConstructedInProduction_Throws`
  - U-B16 `NotificationOptionsValidatorTests.Production_WithHttpApiBaseUrl_Fails` and `Production_WithDevelopmentSender_Fails`
  - I-B16 `UnconfiguredSenders_ResetEndsUndeliverable_NoTokenAnywhere`
  - The "says so" half is shown by source only: `NotificationWorker.LogUnconfiguredSendersOnce`
    logs `NotificationMessages.StartupNo{Sms,Email}ProviderWarning`, and `UnconfiguredChannelSender`
    returns "No SMS/email provider is configured". I-B16 asserts neither of these (LOW A-4).
  - OQ-1 is recorded as a gap, not a pass: §4.3 and §9.1, the startup warning, and
    `channelsConfigured` in the summary endpoint.
  - The ADR that this criterion names, ADR-061, does not exist. See HIGH 1.
- A15 MET. Evidence:
  - U-B12 `NotificationAggregateTests.Claim_NotDue_Throws` (a live lease refuses a second claim) and
    `Claim_LeaseExpired_CanBeReclaimed`
  - I-B11 `TrySaveClaimAsync_TwoConcurrentClaims_ExactlyOneWins`, with real `xmin`
  - I-B12 `TrySaveClaimAsync_LeaseExpired_CanBeReClaimed` and
    `CrashBetweenClaimAndRecordAttempt_RecoversToExactlyOneSend`
  - I-B13 `RequestNotification_ConcurrentSameDeduplicationKey_ExactlyOneRequestCreated`
- A16 MET. Evidence:
  - I-D10 `Merge_MovesInboxItemToSurvivor_ExternalLogKeepsOriginalRecipient`
  - I-D11 `Anonymise_RedactsSurvivorAndMergedDuplicateNotifications_EvenIfAlreadySecretRedacted`
  - `NotificationPersonalDataEraserTests`, 2 cases

§1 AMENDED ROWS: I spot-checked items (22)-(53) against the source and all are accurate:
- `AnonymisedOn` and `FindMergedDuplicatesAsync` exist.
- There is no `Include` in any Notification query handler.
- The eraser is registered.
- `ContainsPrivateUseOrFormat` and `RenderFailed` exist.
- `Enlist` has exactly two typed overloads.
- `ResendNotificationResult` and `NotificationChannelSenderOptions` exist.
- The link builder uses `PublicSiteOptions`.
- `PersistenceFailed` and `RecordNotificationExpansionFailureCommand` exist.
- `IsSiteRelativePath` exists.
- The worker's catch tests `ex is DbUpdateException`.
- The dedup key includes `{ownerId:N}` (`CareReminderSweep.cs:93`).
- There is exactly one `UseRateLimiter()`, in `Program.cs`.
- The APT row (1) items `SendAfter` and `AppointmentReminder` are correctly absent from the code;
  that row says HO-APT-001 builds them.
- The only inaccuracy: (45) cites the backoff test as I-B6 coverage, but that test covers exhaustion,
  not "transient ×2, then accepted" (LOW A-2).

FINDINGS
- [HIGH] .ai/decisions/adr.md — **ADR-055 to ADR-066 were never recorded, and this was not
  declared.**
  - What the handover requires:
    - §9 says "Append to `.ai/decisions/adr.md` … this handover owns ADR-055…066", and each row
      gives the reason it "must be recorded".
    - §13 allows exactly that append.
    - README's ADR table allocates 055-066 to HO-NTF-001.
  - What exists:
    - `adr.md` has ADR-001 to ADR-054, ADR-317 to ADR-320 and ADR-341/342. It has no heading
      between 054 and 317.
    - `git log -S "ADR-056" --all -- .ai/decisions/adr.md` finds nothing, so the ADRs were never
      written on any branch.
    - Neither §1 Amended nor any gate file mentions the omission, and all four gates missed it.
  - Why this is HIGH and not a paperwork LOW:
    - ADR-056 is the recorded exception to `.ai/bounded-contexts.md` rule 1. Expansion and fallback
      commit several roots in one save. Without the ADR, the governing documents say the shipped
      code breaks rule 1.
    - ADR-057 is the producer rulebook that twelve later areas' ARCHITECTURE gates are told to
      check against.
    - ADR-063 is the recorded acceptance of the 15-minute plaintext reset-link window (OQ-3). The
      handover says the window "must be a decision, not a discovery".
    - ADR-055 is how the handover closes OQ-2.
    - ADR-061 holds the six-point adapter contract that §18 A14 names. The existing ADR-320 already
      cites it as recorded ("meet the §8.9 adapter contract recorded in ADR-061"). CONTRACTS.md and
      at least five other handovers (APT, BIL, BRD, EMR, IMP) also cite these numbers.
  - Fix: append ADR-055 to ADR-066 as specified in §9, and fold in the approved amendments:
    - the reason-typed `Redact` and `AnonymisedOn` (25);
    - the owner-id dedup key (52);
    - the 5-strike `PersistenceFailed` backstop limited to `DbUpdateException` (43)/(50);
    - `PublicSite:WebBaseUrl` (40).
  - It is documentation only and needs no code change. A re-run of this gate can then be a short
    delta.
- [LOW] A-1 docs/gates/TC-HO-NTF-001.md is in the wrong place.
  - §13 allows `docs/test-cases/TC-HO-NTF-001.md`. Every other area's sheet is in `docs/test-cases/`
    (ACC, ADM, CAT, FIL).
  - Fix: move it with `git mv`. Its content is sound.
- [LOW] A-2 Some TC rows are marked PASS with evidence that does not show the case as written:
  - U-C4 cites `RedactionSweep_WithBatchSizeSmallerThanBacklog_*`, which is not about a hold that
    runs past `NotAfter`.
  - I-B6 ("transient ×2 then accept → Sent, 3 attempts") cites a test of six failures that end
    Undeliverable. No test anywhere covers a retry that later succeeds.
  - U-B12's "not due" sub-case (a future `NotBefore`) has no unit test. I-C1 covers it end to end.
  - I-B3's audit query filters by `actorUserId=customerId` on an anonymous reset, so it may match
    nothing. This gate's direct database query above closes that gap for this run only.
- [LOW] A-3 §8.14 says "a hold that would pass `NotAfter` expires the notification instead of
  sending late".
  - Implemented behaviour: `Defer` still holds the notification to the end of the window, and it
    expires on the next pass after that. It is never sent late (safe), but it stays Queued/deferred
    rather than expiring straight away.
  - This is an undeclared deviation. It is latent today: the only template with a `NotAfter`
    (password reset) is Urgent and is never held.
- [LOW] A-4 I-B16 does not assert the startup warning or the provider-response text that the case
  lists. Both are shown by source only (A14).
- [LOW] A-5 Handover text is out of step after the gate fixes:
  - §15 U-F3 still shows the old dedup-key format (`care:vaccination:{catId}:{vaccineId}:{date}`),
    while §8.12 and the test use the owner-id format.
  - §8.8 still names `NotificationOptions.WebBaseUrl`, which (40) removed.
  - Integrator: fix these when (52) and (40) are next touched.
- [LOW] A-6 Two files outside the §1 Amended overflow list:
  - `src/Api/CatEcommerce.Api/appsettings.Testing.json`: authorised in ARCHITECTURE pass 2 but not
    recorded in §1.
  - `src/Api/CatEcommerce.Api/Modules/FileStorageModule.cs`: a comment-only merge correction that
    ARCHITECTURE asked for.
  - Both were authorised by a gate. Record them in §1.

SCOPE
- Out-of-scope work found: none.
  - No provider SDK, HTTP client or MailKit.
  - No `SmtpEmailSender` or `HttpSmsGateway`. ADR-320's transports are correctly not built here.
  - No templating library and no HTML template input.
  - No enqueue from a domain event handler.
  - No subscription to `CatMarkedDeceasedEvent` or `CatOwnershipTransferredEvent`.
  - No `*.csproj`, `NuGet.Config`, `global.json` or `.sln` change.
  - No `web/**` or Cat-area change.
- Files outside allowed scope: `docs/gates/TC-HO-NTF-001.md` (A-1, misplaced).
  - Everything else outside the literal §13 list is covered by one of:
    - the §1 Amended overflow record;
    - ADR-341's per-area partial files;
    - the integrator/gate authorisations (`.ai/host-composition.md`, `docs/handover/HO-ACC-001.md`,
      `HostPipelineExtensions.cs`, `appsettings.Testing.json`, the FileStorageModule comment);
    - `docs/backlog/NTF.md`, as the owner rule directs.
  - 233 files changed against master in total.

UNDECLARED DEVIATIONS
- The missing ADR-055 to ADR-066 (HIGH above).
- A hold that runs past `NotAfter` is deferred rather than expired immediately (A-3).
- The TC sheet is in the wrong place (A-1).

OPEN MEDIUM/LOW FINDINGS FROM ALL GATES (consolidated). "IN BACKLOG" means it is a row in
docs/backlog/NTF.md.
| # | Sev | Finding | Source | In backlog |
|---|---|---|---|---|
| 1 | MEDIUM | A→B→A transfer inside one due window reuses the consumed dedup key, so the original owner's reminder is missed (never duplicated) | CODE_REVIEW p5 / TEST p2 | yes (#1) |
| 2 | LOW | `DbUpdateException` 5-strike backstop not proven through `NotificationWorkCycle.DispatchAsync` with real consecutive failures | TEST p1 | yes (#2) |
| 3 | LOW | I-D5 has no test that changes the mobile number part-way through | TEST p1 | yes (#3) |
| 4 | LOW | R5 projection is verified only indirectly (no "no `Include`" assertion) | TEST p1 | yes (#4) |
| 5 | LOW | C5 has no attempt counter: a non-overflow save failure after the claim leaves the row `Sending`, and it is re-sent every lease | SECURITY p3 | **no** |
| 6 | LOW | Plaintext unsubscribe token/link is stored beside its hash. Latent: v1 has no marketing template | SECURITY p1-p3 carried; ARCH p2 carried | **no** |
| 7 | LOW | `PublicSiteStartupGuardHostedService` accepts an empty or placeholder `PublicSite:WebBaseUrl` in every non-Production environment (Staging included). Reset links there would be relative and broken. The fix belongs to the integrator, in the guard | ARCH p2 LOW; SECURITY p2/p3 carried | **no** |
| 8 | LOW | `appsettings.Development.json.example` is stale: it still has `Account:WebBaseUrl` and has no `Notifications:ApiBaseUrl`. A developer who copies it gets a Development host that fails `ValidateOnStart`. Still open at e7a3cdb | ARCH p2 | **no** |
| 9 | LOW | ADR-342's pipeline sketches (`adr.md:1905,1988`) still say `UseAccountsModule()` adds `UseRateLimiter`. It needs an append-only amendment note. Still open at e7a3cdb | ARCH p2 | **no** |
| 10 | LOW | Third copy of the variable-assembly and render block (C4/C5/C7) risks drift. The missing log was fixed by (48); the duplication remains | ARCH p2 | **no** |
| 11 | LOW | Third single-save exception-translation seam (`EfNotificationResendStore`). If a fourth appears, record a generic ADR | ARCH p2 | **no** |
| 12 | LOW | The anonymise loop is duplicated verbatim in `NotificationPersonalDataEraser` and `AnonymiseNotificationsOnCustomerAnonymisedHandler` | ARCH p2 | **no** |
| 13 | LOW | `GetDueCareRemindersQueryHandler` re-derives "latest per vaccine" (`GroupBy`, line 42) instead of reusing `CatVaccinationStatusCalculator` (F26). It re-derives over every live cat on every hourly sweep. CODE_REVIEW p2 called this fixed, but it is still open at e7a3cdb, and it is not recorded in §1 Amended | ARCH p1/p2 carried; CODE_REVIEW p1 | **no** |
| 14 | LOW | The admin log returns hand-built `{ error }` 400s for range and paging instead of `ValidationResponse`. Accepted per the ADM precedent | ARCH p1/p2 carried | **no** |
| 15 | LOW | Integrator notes: keep the environment name "Testing" reserved for the test host (a QA deployment named "Testing" would run with the worker off and Capturing senders allowed); consider excluding `appsettings.Testing.json` from publish (csproj, §14-forbidden here) | ARCH p2 | **no** |
| 16 | LOW | TC sheet misplaced (A-1) | ACCEPTANCE | **no** |
| 17 | LOW | TC rows whose evidence does not match the case: U-C4, I-B6 (no retry-then-success test anywhere), U-B12 not-due, I-B3 audit filter (A-2) | ACCEPTANCE | **no** |
| 18 | LOW | A hold past `NotAfter` is deferred and then expired rather than expired at once (A-3) | ACCEPTANCE | **no** |
| 19 | LOW | I-B16 does not assert the startup warning or the provider response (A-4) | ACCEPTANCE | **no** |
| 20 | LOW | Handover §15 U-F3 and §8.8 text is stale after (52)/(40) (A-5) | ACCEPTANCE | **no** |
| 21 | LOW | `appsettings.Testing.json` and the FileStorageModule comment are missing from the §1 overflow record (A-6) | ACCEPTANCE | **no** |

Closed items I checked rather than assumed:
- The SECURITY p3 LOW (transient failures counted toward the park limit) is closed by 471e52f. The
  catch tests `ex is DbUpdateException`, and
  `QuietHoursCalendarThrowsRepeatedly_PastMaxExpansionAttempts_StillPendingNeverCounted` passes.
- The CODE_REVIEW p1 constant-time-compare note was informational (it follows the ADR-028
  precedent), not a finding.

SCOPE CHECK
- files in allowed scope: 232/233 (covered by §13, §1 Amended, ADR-341 per-area partials, or recorded
  integrator/gate authorisation)
- files outside allowed scope: docs/gates/TC-HO-NTF-001.md (misplaced; should be docs/test-cases/)
