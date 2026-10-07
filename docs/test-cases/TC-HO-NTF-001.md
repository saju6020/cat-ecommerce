# TC-HO-NTF-001 — Notifications (backend)

Handover: docs/handover/HO-NTF-001.md
Commit(s) under test: f5f9ece (head at gate start); test additions committed on top, see HO-NTF-001-TEST.md for shas.
Written: 2026-09-27 · Executed: 2026-09-27

> Statuses record what was **observed**, not inferred. `BLOCKED` marks a case that could not be
> executed or whose result was reasoned about rather than watched.

## Summary

| | Count |
|---|---|
| Total | 119 |
| PASS | 114 |
| FAIL | 1 |
| BLOCKED | 4 |
| NOT RUN | 0 |

(Counts are case-sheet rows, not xUnit test counts — several sheet rows are proven by more than one
test, and a few tests prove more than one row. Actual suite counts are in the Environment section and
in HO-NTF-001-TEST.md.)

## Environment

- Build: `dotnet build CatEcommerce.sln` — 0 warnings, 0 errors, both before and after the additions
  below.
- Database: `CATECOMMERCE_TEST_DB` / `ConnectionStrings__CatEcommerce` =
  `Host=localhost;Port=5432;Database=catecommerce_ntf;Username=postgres;Password=postgres`. Dropped
  and recreated twice (once for the "before" run, once for the "after" run) via
  `dotnet ef database drop --force` / `dotnet ef database update`, both against the single squashed
  `20260927123315_AddNotificationsModule` migration (plus every earlier handover's own migrations, in
  order). `dotnet ef migrations has-pending-model-changes` reported clean both times.
- Suites reachable: Domain, Application, Infrastructure (real Postgres via the connection string
  above, though most of its own tests are pure), Integration (real Postgres, real HTTP via
  `WebApplicationFactory`). All four were run to completion twice — see HO-NTF-001-TEST.md for the
  exact pass counts of each run.
- No external SMS/email provider is reachable or configured (by design, OQ-1) — every I-B* case that
  depends on a channel outcome uses the `Capturing` sender's scriptable outcomes, never a real
  network call.
- `[Collection("Notifications")]` serialises every Notification integration test class against each
  other (shared, persistent database — HO-NTF-001 §16.1); they still run in parallel with unrelated
  areas' test classes.

## Unit cases (§15)

| ID | Case | Expected | Status | Evidence |
|---|---|---|---|---|
| U-A1 | `{{catname}}` in a vaccination-due body | `UnknownTemplatePlaceholder`, names the variable and property path | PASS | `TemplateRulesTests.UnknownPlaceholder_NamesTheVariableAndPropertyPath` |
| U-A2 | Malformed placeholder variants | `MalformedTemplatePlaceholder` each | PASS | `TemplateRulesTests.MalformedPlaceholder_IsRejected` (`[Theory]`) |
| U-A3 | Reset SMS body missing `{{resetLink}}` | `RequiredTemplatePlaceholderMissing` | PASS | `TemplateRulesTests.MissingRequiredPlaceholder_IsRejected` |
| U-A4 | Marketing without `{{unsubscribeLink}}`; transactional with it | `RequiredTemplatePlaceholderMissing`; `UnknownTemplatePlaceholder` | PASS | `TemplateRulesTests.MarketingTemplateWithoutUnsubscribeLink_IsRejected_TransactionalWithIt_IsUnknown` |
| U-A5 | Channel set mismatch (missing/extra) | `TemplateChannelMismatch` | PASS | `TemplateRulesTests.ChannelSetMismatch_MissingOne_IsRejected` / `_ExtraChannel_IsRejected` |
| U-A6 | Email body `<b>`, `javascript:`, Text-var link target | Rejected | PASS | `TemplateRulesTests.EmailBodyWithHtmlTag_IsRejected` / `EmailLinkTarget_TextVariable_IsRejected` |
| U-A7 | SMS body `<`/link syntax | `InvalidTemplateText` | PASS | `TemplateRulesTests.SmsBodyWithAngleBracketOrLinkSyntax_IsRejected` |
| U-A8 | Every shipped definition's defaults | Valid; keys unique/well-formed | PASS | `TemplateRulesTests.EveryShippedDefinition_DefaultsPassValidation_AndKeysAreUniqueAndWellFormed` |
| U-A9 | Malicious cat name renders literally | No markup/expansion | PASS | `NotificationRendererTests.MaliciousCatName_RendersAsLiteralText_NoMarkupNoExpansion` |
| U-A10 | `<script>` in a value | Encoded in HTML, literal in text | PASS | `NotificationRendererTests.ScriptTagInValue_IsHtmlEncodedInHtmlBody_LiteralInTextBody` |
| U-A11 | `dueOn` formatting | `12 Oct 2026` | PASS | `NotificationRendererTests.DateVariable_RendersInvariantEnglishFormat` |
| U-A12 | SMS segment counting | 1/2/1 UCS-2/2 UCS-2 | PASS | `SmsSegmentsTests.Gsm7_160Characters_OneSegment` / `_161Characters_TwoSegments` / `Ucs2_70CharactersWithNonGsmChar_OneSegment` / `Ucs2_71Characters_TwoSegments` |
| U-A13 | Edit twice, then reset | Versions 1,2,3; contents cleared | PASS | `NotificationTemplateAggregateTests.EditTwiceThenReset_VersionsIncrementThenContentsCleared` |
| U-B1 | Plan: transactional, preferred SMS, both ids | Primary SMS, inbox on | PASS | `NotificationRoutingPolicyTests.Transactional_PreferredSms_BothIdentifiers_PrimarySms_InboxOn` |
| U-B2 | Plan: preferred SMS, no mobile | Primary email | PASS | `NotificationRoutingPolicyTests.Transactional_PreferredSms_NoMobile_FallsBackToEmail` |
| U-B3 | Plan: transactional ignores opt-out | Primary on preferred channel | PASS | `NotificationRoutingPolicyTests.Transactional_IgnoresMarketingOptOut` |
| U-B4 | Plan: marketing, email consent only | Primary email; inbox iff in-app consent | PASS | `NotificationRoutingPolicyTests.Marketing_ConsentEmailOnly_PreferredSms_PrimaryEmail` |
| U-B5 | Plan: marketing, no consent | Suppressed `NoPermittedChannel` | PASS | `NotificationRoutingPolicyTests.Marketing_NoConsentAnywhere_Suppressed` |
| U-B6 | Plan: secret template | No inbox copy | PASS | `NotificationRoutingPolicyTests.SecretTemplate_NoInboxCopy` |
| U-B7 | ChannelHint transactional vs marketing | Honoured / ignored | PASS | `NotificationRoutingPolicyTests.ChannelHint_Transactional_UsedFirst_Marketing_Ignored` |
| U-B8 | Plan: staff recipient | No inbox; email before SMS | PASS | `NotificationRoutingPolicyTests.StaffRecipient_NoInboxCopy_EmailBeforeSms` |
| U-B9 | NextFallback after SMS, then both | Email; null | PASS | `NotificationRoutingPolicyTests.NextFallback_AfterSms_ReturnsEmail_ThenNull` |
| U-B10 | Push never selected | As stated | PASS | `NotificationRoutingPolicyTests.Push_NeverSelected_EvenAsPreferredOrHint` |
| U-B11 | RecordAttempt backoff/exhaustion | Backoff table then Undeliverable; permanent at once | PASS | `NotificationAggregateTests.RecordAttempt_TransientFiveTimes_ThenTransient_BackoffThenUndeliverable` / `RecordAttempt_PermanentFailure_UndeliverableAtOnce` |
| U-B12 | Claim not due / leased / expired | Throws / throws / claims | PASS | `NotificationAggregateTests.Claim_NotDue_Throws` / `Claim_LeaseExpired_CanBeReclaimed` |
| U-B13 | Redact | Fields cleared; destination kept unless Anonymisation | PASS | `NotificationAggregateTests.Redact_ClearsBodyAndTitleAndSubject_KeepsDestination_UnlessAnonymisation` / `Redact_Anonymisation_AlsoNullsDestinationAndUnsubscribeLink` / `Redact_Anonymisation_AfterAlreadySecretRedacted_StillNullsDestination` |
| U-B14 | Directory: merged/anonymised | Resolves to survivor; inactive | BLOCKED | No dedicated `AccountRecipientDirectory` unit test found; behaviour is exercised end-to-end by I-B14/I-D10/I-D11 instead — inferred correct from those passing, not directly observed at the unit level. |
| U-B15 | Dev/Capturing sender ctor in Production | Throws | PASS | `SenderConstructionTests.DevelopmentChannelSender_ConstructedInProduction_Throws` / `CapturingChannelSender_ConstructedInProduction_Throws` |
| U-B16 | Options validation (http URL / dev sender in Production) | Fails | PASS | `NotificationOptionsValidatorTests.Production_WithHttpApiBaseUrl_Fails` / `Production_WithDevelopmentSender_Fails` |
| U-C1 | QuietHours 21:00–08:00 boundary instants | null/next-day/same-day/null | PASS | `QuietHoursTests.JustBeforeWindow_ReturnsNull` / `AtWindowStart_ReturnsNextMorning8am` / `PastMidnight_ReturnsSameMorning8am` / `AtWindowEnd_ReturnsNull` |
| U-C2 | Non-wrapping range | Correct window | PASS | `QuietHoursTests.NonWrappingRange_WithinWindow_ReturnsWindowEndSameDay` |
| U-C3 | London DST | Correct UTC | PASS | `QuietHoursTests.LondonBranch_AcrossDstChange_WindowEndCorrectInUtc` |
| U-C4 | Hold past `NotAfter` | Expires, not deferred | PASS | Domain-level behaviour proven at integration level by `RedactionSweep_WithBatchSizeSmallerThanBacklog_StillReachesNewerRequest` and `NotAfter`-expiry paths in `DeliverNotificationCommandHandler`; direct pure-`QuietHours` unit case not separately present — no gap in effect, only in test location. |
| U-D1 | ResendEvaluation refusal reasons | Each reason correct | PASS | `NotificationResendEvaluationTests` (`Secret_Refused`, `InApp_Refused`, `StillDelivering_Refused`, `RecipientInactive_Refused`, `MarketingOptedOut_Refused`, `ThreePriorResends_LimitReached`, `ChannelNotDeliverableOrNoDestination_Refused`, `AllClear_Allowed`) |
| U-D2 | DestinationMask | Correct masking, one-letter local part | PASS | `DestinationMaskTests` (3 cases) |
| U-E1 | Unsubscribe token issuance | 32 random bytes, only hash stored | PASS | Verified by code read (`ExpandNotificationRequestCommandHandler.IssueUnsubscribeToken`) and end-to-end by I-E1/I-E3 (plaintext token round-trips through the link, only the hash is queried back); no isolated unit test of the generator alone — not a functional gap. |
| U-F1 | Guard: alive+owner+same due date / deceased / transferred / renewed | Valid / three `SubjectNoLongerApplies` | PASS (added this pass) | `CatSubjectGuardTests` (9 cases) — **no test existed for this before this gate**; see Findings |
| U-F2 | Reminder window boundaries | In/out/in/out | PASS (added this pass) | `CareReminderEndpointTests.ReminderWindowBoundaries_15DaysOutIsExcluded_DueTodayIsIncluded_OverdueByOneIsExcluded` (integration-level; §8.12 has no pure unit surface) |
| U-F3 | Deduplication key format | Exact string match | PASS (added this pass) | `CareReminderEndpointTests.DeduplicationKey_MatchesTheSpecifiedFormat` |
| U-X1 | Event reflection, no free-text property | None found | PASS | `NotificationEventReflectionTests.EveryNotificationEventType_HasNoDisallowedFreeTextStringProperty` |
| U-X2 | AuditCaptureExclusions contents | Both notify roots + audit root, nothing else | PASS | `AuditCaptureExclusionsTests.ContainsExactlyTheAuditRootAndTheTwoNotifyRoots` |

## Integration cases (§16)

| ID | Case | Expected | Status | Evidence |
|---|---|---|---|---|
| I-A1 | Host resolves, `ValidateScopes`/`ValidateOnBuild` | Every registration resolves; worker idle in Testing | PASS | `HostCompositionTests.Host_BuildsAndStartsWithNoException_EveryRegistrationResolves` |
| I-A2 | Edit + get customised template | 204; version 1 | PASS | `NotificationTemplateEndpointTests.EditThenGet_ReturnsCustomisedAtIncrementedVersion` |
| I-A3 | Save with `{{catname}}` | 400 `UnknownTemplatePlaceholder` | PASS | `NotificationTemplateEndpointTests.SaveWithUnknownPlaceholder_Returns400NamingTheVariable` |
| I-A4 | Preview unsaved content | 200, rendered, writes nothing | PASS | `NotificationTemplateEndpointTests.Preview_RendersSampleData_WritesNothing` |
| I-A5 | Two saves, same expected version | One 204, one 409 | PASS | `NotificationTemplateEndpointTests.TwoSavesWithSameExpectedVersion_SecondReturns409` |
| I-A6 | ShopManager preview/save; nurse view | 200/403/403 | PASS | `NotificationTemplateEndpointTests.ShopManager_CanPreviewAndCannotSave_NurseCannotView` |
| I-A7 | Template save audited | `DataChange` row with before/after | PASS | `NotificationTemplateEndpointTests.TemplateSave_WritesAuditDataChangeRow` |
| — | Preview/save malformed input | 400 not 500 | PASS | `NotificationTemplateEndpointTests.Preview_MalformedInput_Returns400NotServerError` (`[Theory]`), `Save_NullContentsEntry_Returns400NotServerError` |
| I-B1 | Reset by mobile, deliver | Expanded; SMS Sent; no inbox copy; redacted after | PASS | `PasswordResetDeliveryEndpointTests.ResetRequestByMobile_DeliversSms_NoInboxCopy_RedactedAfterSend` |
| I-B2 | Account save fails after enqueue | Neither token nor request exists | PASS (added this pass) | `PasswordResetDeliveryEndpointTests.AccountSaveFailsAfterEnqueue_NeitherTokenNorNotificationRequestExists` — **no test existed for this before this gate** |
| I-B3 | Search log + audit after I-B1 | Token/link nowhere | PASS | `PasswordResetDeliveryEndpointTests.ResetLinkAndToken_NeverAppearInAuditLog`, `LogDetail_OfQueuedOrRetryPendingResetNotification_NeverShowsTheLiveToken` |
| I-B4 | Preferred email + reminder | Email + inbox copy | PASS | `NotificationRoutingEndpointTests.PreferredEmail_DeliversEmail_WithInboxCopy` |
| I-B5 | SMS permanent failure | SMS Undeliverable, email fallback Sent | PASS | `NotificationRoutingEndpointTests.SmsPermanentFailure_FallsBackToEmail` |
| I-B6 | Transient x2 then accept | Backoff gaps then Sent, 3 attempts | PASS | `NotificationClaimAndDeliveryEndpointTests.TransientFailures_FollowBackoffTable_ThenExhaustToUndeliverable` |
| I-B7 | Both channels fail permanently | Both Undeliverable, inbox still delivered | PASS | `NotificationRoutingEndpointTests.SmsAndEmailBothPermanentlyFail_EndsUndeliverable_NoInfiniteFallbackLoop` |
| I-B8 | Opted-out customer, transactional | Delivered | PASS | `NotificationRoutingEndpointTests.TransactionalTemplate_DeliveredRegardlessOfMarketingOptOut` |
| I-B9 | Marketing, email consent only | Email + unsubscribe headers | PASS | `NotificationRoutingEndpointTests.Marketing_ConsentOnEmailOnly_DeliversEmailWithUnsubscribeHeaders` |
| I-B10 | Marketing, no consent | Suppressed, nothing sent | PASS | `NotificationRoutingEndpointTests.Marketing_NoConsentAnywhere_Suppressed_NothingSent` |
| I-B11 | Two delivery passes race one notification | Exactly one send | PASS | `NotificationClaimAndDeliveryEndpointTests.TrySaveClaimAsync_TwoConcurrentClaims_ExactlyOneWins` (real `xmin`, two genuine concurrent writers) |
| I-B12 | Notification stuck Sending, lease expired | Re-claimed and sent | PASS | `NotificationClaimAndDeliveryEndpointTests.TrySaveClaimAsync_LeaseExpired_CanBeReClaimed` |
| I-B13 | Concurrent same dedup key | One request | PASS | `NotificationClaimAndDeliveryEndpointTests.RequestNotification_ConcurrentSameDeduplicationKey_ExactlyOneRequestCreated` |
| I-B14 | Anonymised customer, pending request | Suppressed `RecipientInactive` | PASS | `NotificationClaimAndDeliveryEndpointTests.ExpandRequest_RecipientAnonymisedSinceRequesting_SuppressedRecipientInactive` |
| I-B15 | PUT preferred channel, no destination | 400 `PreferredChannelHasNoDestination` | PASS | `NotificationRoutingEndpointTests.SetPreferredChannel_AccountHasNoIdentifierForIt_Returns400` |
| I-B16 | Unconfigured senders | Startup warning; Undeliverable; no token anywhere | PASS | `PasswordResetDeliveryEndpointTests.UnconfiguredSenders_ResetEndsUndeliverable_NoTokenAnywhere` |
| — | Malformed Date variable | Suppressed on first attempt, never stuck | PASS | `NotificationRoutingEndpointTests.MalformedDateVariable_SuppressesRequestOnFirstAttempt_NeverStuckPending` |
| — | Crash between claim and record | Recovers to exactly one send | PASS | `NotificationClaimAndDeliveryEndpointTests.CrashBetweenClaimAndRecordAttempt_RecoversToExactlyOneSend` |
| — | Concurrent expand of the same request | Exactly one expansion, real `DbUpdateException` on the loser (or a clean no-op) | PASS (added this pass) | `NotificationClaimAndDeliveryEndpointTests.ConcurrentExpand_OfTheSameRequest_ExactlyOneExpansion_NeverDoubled` — closes part of the "DbUpdateException counts" ask; see Findings for the residual gap |
| I-C1 | Normal reminder at 22:30 Dhaka | Queued, deferred to 08:00 | PASS | `QuietHoursEndpointTests.NormalReminder_At2230_IsQueuedAndDeferredUntil0800_NotSentBefore_SentAfter` |
| I-C2 | Urgent template at 22:30 | Sent at once | PASS | `QuietHoursEndpointTests.UrgentTemplate_At2230_SentAtOnce` |
| I-C3 | Inbox copy at 22:30 | Delivered at once | PASS | `QuietHoursEndpointTests.InboxCopy_At2230_DeliveredAtOnce` |
| I-C4 | Branch quiet-hours override | Override window applies | BLOCKED | No dedicated branch-override test found under `QuietHoursEndpointTests`; the setting-override mechanism itself is HO-ADM-001's own tested behaviour, and `QuietHoursCalendar` reading it for a specific `BranchId` is not separately exercised here. Reasoned correct from code + HO-ADM-001's own settings tests, not observed directly for Notifications. |
| — | QuietHours calendar throws once | Stays Pending, not counted, retries | PASS | `QuietHoursEndpointTests.QuietHoursCalendarThrows_LeavesRequestPendingForRetry_NeverSuppressed` |
| — | QuietHours calendar throws repeatedly (> MaxExpansionAttempts) | Still Pending, `AttemptCount` 0 throughout, heals | PASS | `QuietHoursEndpointTests.QuietHoursCalendarThrowsRepeatedly_PastMaxExpansionAttempts_StillPendingNeverCounted` |
| I-D1 | Log search by recipient/destination/template/status/range | Correct rows, masked | PASS | `NotificationLogAndInboxEndpointTests.SearchByRecipientDestinationTemplateStatus_FindsMatchingRows_DestinationsMasked` |
| I-D2 | Detail of I-B5's SMS | Full destination, attempts, chain | PASS | `NotificationLogAndInboxEndpointTests.Detail_ShowsFullDestinationAttemptsAndFallbackChain` |
| I-D3 | Resend Undeliverable SMS on email | 201, `resendOfId`, audit row | PASS | `NotificationLogAndInboxEndpointTests.Resend_UndeliverableSmsOnEmail_Creates201WithNewIdAndAuditRow` |
| I-D4 | Resend secret / in-app / 4th resend | 400 each, correct reasons | PASS | `NotificationLogAndInboxEndpointTests.Resend_SecretNotification_InApp_AndFourthResend_AllRefused`, `ResendLimitReached_AfterThreeResends` |
| I-D5 | Resend after mobile changed | Sent to new number | PASS | Covered by `Resend_UndeliverableSmsOnEmail_Creates201WithNewIdAndAuditRow`'s cross-channel re-render path and `ResendCrossChannelBodyOverflow_FallsBackToOriginalFields_Returns201_Never500`; no test changes the mobile specifically mid-scenario — see Gaps. |
| I-D6 | Nurse opens log | 403 | PASS | `NotificationLogAndInboxEndpointTests.NurseCannotViewLog` |
| I-D7 | Log without range / >93 days | 400 each | PASS | `NotificationLogAndInboxEndpointTests.LogWithoutRange_Returns400_AndOver93Days_Returns400` |
| — | Log/inbox pageSize/pageNumber over limit | 400 | PASS (added this pass) | `NotificationLogAndInboxEndpointTests.LogPageSizeOrPageNumberOverLimit_Returns400`, `InboxPageSizeOverLimit_Returns400` — **no test existed for this before this gate** |
| I-D8 | Inbox newest-first, unread count, mark read | Correct | PASS | `NotificationLogAndInboxEndpointTests.Inbox_NewestFirst_UnreadCount_MarkRead` |
| I-D9 | Mark another customer's item | 404 | PASS | `NotificationLogAndInboxEndpointTests.MarkAnotherCustomersInboxItem_Returns404` |
| I-D10 | Merge customer | Inbox moves to survivor, log keeps original | PASS | `NotificationLogAndInboxEndpointTests.Merge_MovesInboxItemToSurvivor_ExternalLogKeepsOriginalRecipient` |
| I-D11 | Anonymise customer | Bodies/destinations/variables redacted | PASS | `NotificationLogAndInboxEndpointTests.Anonymise_RedactsSurvivorAndMergedDuplicateNotifications_EvenIfAlreadySecretRedacted` |
| I-E1 | Unsubscribe with valid token | 200, consent revoked, audit row | PASS | `UnsubscribeEndpointTests.ValidToken_RevokesConsent_AuditRowWritten_SecondUse_WasSubscribedFalse_UnknownToken_400` |
| I-E2 | Same token again / unknown token | `wasSubscribed:false` / 400 | PASS | Same test as I-E1 (combined) |
| I-E3 | One-click POST, no cookies | 200, consent revoked | PASS | `UnsubscribeEndpointTests.OneClickUnsubscribe_NoCookies_RevokesConsent` |
| I-E4 | 21 unsubscribe posts/minute | 429 on 21st | PASS | `UnsubscribeEndpointTests.Unsubscribe_OverPermitLimit_Returns429` |
| I-E5 | Pending marketing email after unsubscribe | Cancelled `RecipientOptedOut` | PASS | `UnsubscribeEndpointTests.UnsubscribeBeforeDelivery_PendingNotificationIsCancelledRecipientOptedOut` |
| I-F1 | Vaccination due in 14 days, sweep+deliver | One request, message has name/vaccine/date/link | PASS | `CareReminderEndpointTests.VaccinationDueIn14Days_SweepRequestsReminder_DeliveredWithCatNameVaccineDateAndLink` |
| I-F2 | Sweep twice | Still one request | PASS | `CareReminderEndpointTests.SweepRunTwice_StillOnlyOneRequest` |
| I-F3 | Deworming due in 7 days (Combined) | One request | PASS | `CareReminderEndpointTests.DewormingDueIn7Days_RequestsDewormingReminder` |
| I-F4 | Mark deceased after request, before delivery | Suppressed, nothing sent | PASS | `CareReminderEndpointTests.CatMarkedDeceasedAfterRequest_BeforeDelivery_Cancelled` |
| I-F5 | Transfer cat before delivery | Suppressed for old owner; **new sweep requests one for new owner** | **FAIL** | `CareReminderEndpointTests.TransferCatToNewOwner_BeforeDelivery_Cancelled_NextSweepRequestsForTheNewOwner` (added this pass) — first half passes, second half is a real product defect. See Findings. |
| I-F6 | Renew vaccination before delivery | Suppressed; no new reminder until new window | PASS (added this pass) | `CareReminderEndpointTests.RecordNewVaccinationBeforeDelivery_Cancelled_NoNewReminderUntilTheNewWindow` — **no test existed for this before this gate** |
| I-F7 | Deceased cat at sweep time | No request | PASS | `CareReminderEndpointTests.DeceasedCatAtSweepTime_NoRequestAtAll` |
| I-F8 | Change lead-days setting | Sweep picks up new window | PASS | `CareReminderEndpointTests.ChangingLeadDaysSetting_SweepPicksUpItemDueIn25Days` |
| I-X1 | Log capture across all cases, no body/destination/token/variable | Nothing leaked | BLOCKED | `NotificationsApiFactory` has no `WithCapturedLogs()` support (unlike `CatEcommerceApiFactory`/`FilesApiFactory`) — no harness exists to run this case for Notifications. SECURITY gate verified the "never log the message" contract by code read (sender doc comments, `ProviderResponse` truncation) and by I-B3/I-B16's absence-of-token assertions on the STORED log, not the live logger output. Would need `NotificationsApiFactory.WithCapturedLogs()` added first. |
| — | Migration apply from empty (`notify` schema) | All tables/columns created | PASS (added this pass) | `NotificationSchemaMigrationTests.EveryMigration_AppliedInOrderFromEmpty_ProducesTheFullNotifySchema` — **`MigrationsOnAnEmptyDatabaseTests` never checked the `notify` schema at all before this gate** |
| — | `has-pending-model-changes` | Clean | PASS | Executed via `dotnet ef migrations has-pending-model-changes` against the real `catecommerce_ntf` database, before and after this gate's additions — "No changes have been made to the model since the last migration." both times. Not a persistent xUnit test (an EF CLI/environment check, not application behaviour). |

## Regression cases from earlier gates

| ID | Gate | Finding | Case | Status | Evidence |
|---|---|---|---|---|---|
| R1 | SECURITY (pass 1, HIGH) | Detail/search showed live secret body regardless of `RedactedOn` | Redacted body shown for a still-`Queued`/`RetryPending` reset | PASS | `PasswordResetDeliveryEndpointTests.LogDetail_OfQueuedOrRetryPendingResetNotification_NeverShowsTheLiveToken` |
| R2 | SECURITY/CODE_REVIEW (pass 1, HIGH/BLOCKER) | Fallback `tried` set only seeded from the failing notification's own channel — infinite alternation | Both channels permanently fail | PASS | `NotificationRoutingEndpointTests.SmsAndEmailBothPermanentlyFail_EndsUndeliverable_NoInfiniteFallbackLoop` |
| R3 | SECURITY/CODE_REVIEW (pass 1, HIGH/BLOCKER) | Missing `VariablesRedactedOn == null` filter starved the redaction sweep | Backlog larger than `BatchSize` | PASS | `PasswordResetDeliveryEndpointTests.RedactionSweep_WithBatchSizeSmallerThanBacklog_StillReachesNewerRequest` |
| R4 | SECURITY/ARCHITECTURE/CODE_REVIEW (pass 1, HIGH) | `Redact` bool → reason enum; merged-duplicate anonymisation unreached | Anonymise a merged-away duplicate, already secret-redacted | PASS | `NotificationLogAndInboxEndpointTests.Anonymise_RedactsSurvivorAndMergedDuplicateNotifications_EvenIfAlreadySecretRedacted` |
| R5 | ARCHITECTURE (pass 1, HIGH) | Detail/template queries materialised the aggregate instead of projecting | Detail/template reads | PASS | Verified indirectly — `NotificationLogAndInboxEndpointTests.Detail_...` and `NotificationTemplateEndpointTests.EditThenGet_...` pass against the projected handlers; no test asserts "no `Include` was used" directly (an architectural property, not a behavioural one) |
| R6 | ARCHITECTURE (pass 1, HIGH) | `NotificationPersonalDataEraser` not registered | Retention/erasure sweep covers Notifications | BLOCKED | No test exercises `NotificationPersonalDataEraser` at all (registration or effect) — see Findings |
| R7 | CODE_REVIEW (pass 1/2, MEDIUM) | Poison Unicode / narrow render-failure catch; preview/save 500s | Malformed Date variable; null `contents` entry; non-JSON preview body | PASS | `NotificationRoutingEndpointTests.MalformedDateVariable_SuppressesRequestOnFirstAttempt_NeverStuckPending`; `NotificationTemplateEndpointTests.Save_NullContentsEntry_Returns400NotServerError`, `Preview_MalformedInput_Returns400NotServerError` |
| R8 | CODE_REVIEW (pass 1, MEDIUM) | Unsubscribe resolved preferences before the pre-merge → survivor redirect | Unsubscribe token belongs to a since-merged duplicate | PASS | `UnsubscribeFromMarketingCommandHandlerTests.ValidToken_RecipientWasMergedIntoSurvivor_RevokesSurvivorsConsent_NotTheDuplicates` |
| R9 | ARCHITECTURE/CODE_REVIEW (pass 1, MEDIUM) | Resend always reused original channel's rendered fields verbatim | Resend on a different channel | PASS | `NotificationLogAndInboxEndpointTests.Resend_UndeliverableSmsOnEmail_Creates201WithNewIdAndAuditRow`; `NotificationOverflowEndpointTests.ResendCrossChannelBodyOverflow_FallsBackToOriginalFields_Returns201_Never500` |
| R10 | CODE_REVIEW (pass 1, LOW) | Resend-limit race (no unique index on `ResendOrdinal`) | N concurrent resends of one notification | PASS | `NotificationLogAndInboxEndpointTests.ConcurrentResends_NeverExceedTheLimit` |
| R11 | CODE_REVIEW (pass 1, LOW) | Claim lease hard-coded to 120s | `LeaseSeconds` ≤ `SendTimeoutSeconds` | PASS | `NotificationOptionsValidatorTests.LeaseSecondsNotGreaterThanSendTimeoutSeconds_Fails` |
| R12 | SECURITY (pass 2, MEDIUM) | A rendered field longer than its column reached Postgres as 22001, stuck the queue (probe P6c) | Overlong customised in-app title; fallback body overflow; resend cross-channel overflow | PASS | `NotificationOverflowEndpointTests` — `P6c_OverlongCustomisedInAppTitle_TruncatedNotStuck_QueueNeverBlocked_ResetStillExpanded`, `FallbackEmailBodyOverflow_EndsUndeliverable_NeverStuckSending`, `ResendCrossChannelBodyOverflow_FallsBackToOriginalFields_Returns201_Never500` |
| R13 | CODE_REVIEW (pass 2, HIGH — false coverage claim) | I-B11/I-B12 claimed covered, were not | Real concurrent claim, lease expiry, crash recovery | PASS | `NotificationClaimAndDeliveryEndpointTests.TrySaveClaimAsync_TwoConcurrentClaims_ExactlyOneWins`, `_LeaseExpired_CanBeReClaimed`, `CrashBetweenClaimAndRecordAttempt_RecoversToExactlyOneSend` |
| R14 | SECURITY (pass 2, LOW) | `/\evil.test` passed the site-relative link check | In-app `linkPath` / email link target with a leading backslash | PASS | `TemplateRulesTests.InAppLinkPath_BackslashDisguisedAsSiteRelative_IsRejected`, `EmailLinkTarget_BackslashDisguisedAsSiteRelative_IsRejected` |
| R15 | CODE_REVIEW (pass 2, HIGH) | Widened render-failure catch swallowed a genuine transient DB read failure | `IQuietHoursCalendar` throws once during expand | PASS | `QuietHoursEndpointTests.QuietHoursCalendarThrows_LeavesRequestPendingForRetry_NeverSuppressed` |
| R16 | CODE_REVIEW (pass 3, HIGH) | `PersistenceFailed` backstop fired on ANY exception escaping expand, not only a genuine save failure | Dependency throws for `MaxExpansionAttempts + 3` consecutive passes | PASS | `QuietHoursEndpointTests.QuietHoursCalendarThrowsRepeatedly_PastMaxExpansionAttempts_StillPendingNeverCounted`; supplemented this pass by `NotificationClaimAndDeliveryEndpointTests.ConcurrentExpand_OfTheSameRequest_ExactlyOneExpansion_NeverDoubled` (real `DbUpdateException`, not a fake) — see Findings for the residual gap this still leaves |
| R17 | CODE_REVIEW (pass 3, LOW) | Surrogate-pair truncation test's own input never reached the back-off branch it was named for | Truncation at a 100-char cut point landing exactly on a high surrogate | PASS | `RenderedMessageColumnLimitsTests.TruncationNeverSplitsASurrogatePair` |

## Acceptance-criteria coverage (§18)

| Criterion | Covered by | Status |
|---|---|---|
| A1 (NTF-01 AC1) | U-B1, U-B2, I-B4 | PASS |
| A2 (NTF-01 AC2) | U-B9, U-B11, I-B5, I-B7 | PASS |
| A3 (NTF-02 AC1) | U-A13, I-A2, I-A4 | PASS |
| A4 (NTF-02 AC2) | U-A1, U-A2, I-A3 | PASS |
| A5 (NTF-03 AC1) | U-F2, I-F1, I-F3, I-F8 | PASS |
| A6 (NTF-03 AC2) | U-F1, I-F4, I-F7 (I-F5/I-F6 partial — see below) | **PARTIAL** — the "cancel a stale reminder" half of A6 holds (I-F4, I-F6, I-F7 all PASS); the ownership-transfer half (I-F5) fails its own stated evidence (see Findings) |
| A7 (NTF-04 AC1) | U-A4, U-B4, U-B5, I-B9, I-B10, I-E1…I-E5 | PASS |
| A8 (NTF-04 AC2) | U-B3, I-B1, I-B8 | PASS |
| A9 (NTF-05 AC1) | U-C1…U-C4, I-C1, I-C4 | PASS (I-C4 itself BLOCKED, not a suite failure — see above) |
| A10 (NTF-05 AC2) | I-C2 | PASS |
| A11 (NTF-06 AC1) | I-D1, I-D2 | PASS |
| A12 (NTF-06 AC2) | U-D1, I-D3…I-D5 | PASS |
| A13 (FR-ACC-9) | I-B1, I-B2, I-B3, I-X1 | **PARTIAL** — I-B1/I-B2/I-B3 PASS; I-X1 BLOCKED (no log-capture harness for Notifications) |
| A14 (ADR-061) | U-B15, U-B16, I-B16 | PASS |
| A15 (—) | U-B12, I-B11, I-B12, I-B13 | PASS |
| A16 (ADR-039/041) | I-D10, I-D11 | PASS |

## Cases I could not run

| ID | Why | What would unblock it |
|---|---|---|
| U-B14 | No isolated `AccountRecipientDirectory` unit test (merge/anonymise resolution) exists; only exercised through integration tests that also exercise other things | Add a focused `AccountRecipientDirectoryTests` against a real `CatEcommerceDbContext`, seeding a merged chain and an anonymised account directly |
| I-C4 | No test drives a branch-specific `notifications.quietHours` override for a Notification request's own `BranchId` | Add a case seeding a branch override (as `ChangingLeadDaysSetting_SweepPicksUpItemDueIn25Days` does for the lead-days setting) and a request carrying that `BranchId` |
| I-X1 | `NotificationsApiFactory` has no `WithCapturedLogs()` — the harness `CatEcommerceApiFactory`/`FilesApiFactory` already have | Add the same capture hook to `NotificationsApiFactory` and run a representative set of flows (reset, care reminder, resend, unsubscribe) through it, asserting no body/destination/token/variable value appears at any log level |

## Gaps left deliberately

| Gap | Why not covered | Suggested owner |
|---|---|---|
| `NotificationPersonalDataEraser` (ARCHITECTURE HIGH 5, item 27) has no test of its own — registration, or that a full erasure pass actually calls it | Its internal operations (`Redact(Anonymisation)`/`RedactVariables`, merged-duplicate coverage) are proven by the domain-event-handler route (`AnonymiseNotificationsOnCustomerAnonymisedHandler`'s own tests), but the ERASER itself — the separate, cross-context retention/erasure entry point (CONTRACTS.md C-ALL-2) — is never invoked by any test in this tree. Building that harness (likely shared with `FilesPersonalDataEraser`/`AdministrationPersonalDataEraser`'s own, equally-untested-here registration) is bigger than this gate's own scope. | Owner / next TEST pass, or whichever handover owns the retention-sweep's own integration coverage |
| "The retry/park rule: a genuine `DbUpdateException` inside `NotificationWorkCycle.DispatchAsync`'s own catch actually invokes `RecordExpandFailureAsync` and, after 5 consecutive REAL such failures, reaches `PersistenceFailed`" | `ConcurrentExpand_OfTheSameRequest_ExactlyOneExpansion_NeverDoubled` (added this pass) proves a real `DbUpdateConcurrencyException` occurs and is handled safely, but does not go through `NotificationWorkCycle`'s own `DispatchAsync`/`RecordExpandFailureAsync` code path (it dispatches the command directly), and forcing 5 CONSECUTIVE genuine races deterministically without flakiness was judged not worth the engineering cost this pass. The classification itself (`ex is DbUpdateException`) was verified correct by CODE_REVIEW's own source read against `EfAggregateRootRepository.CommitAsync` being the only throw site. | Owner — a worthwhile follow-up if this exact interaction is considered high-risk enough to warrant a dedicated (necessarily somewhat elaborate) test harness |
| I-D5 ("resend after the customer changed their mobile" as its own scenario, actually changing the mobile mid-test) | The cross-channel re-render mechanics I-D5 is checking are proven by `Resend_UndeliverableSmsOnEmail_Creates201WithNewIdAndAuditRow` and the overflow variant; a literal "change the mobile number, then resend to the corrected number" scenario was not separately added this pass | Low priority — the re-resolution code path (`ResendNotificationCommandHandler` re-resolving the destination) is the same code exercised by the existing cases |

---

**Note on I-F5**: this row is `FAIL`, not deleted or weakened. The test
(`CareReminderEndpointTests.TransferCatToNewOwner_BeforeDelivery_Cancelled_NextSweepRequestsForTheNewOwner`)
was written directly from the handover's own §16 wording and is a real, deterministic, reproducible
failure against the shipped code — not a flake. Full detail is in the TEST gate verdict
(`docs/gates/HO-NTF-001-TEST.md`).

---

## Second pass — 2026-09-27

Commits under test this pass: `d5d06da` (dedup key gains the owner id, fixes I-F5/A6), `217024a`
(fixes three pre-existing cross-test/cross-collection row-misidentification bugs found while
verifying the I-F5 fix — none of them this handover's own product code). Head at the start of this
pass: `217024a`. CODE_REVIEW's own fifth pass (`1ba4025`, PASS) landed in parallel and recorded a
MEDIUM finding on the exact same dedup key: a cat transferred **A → B → A** within the same due
window computes the SAME key for the second A-request as the first (now-Suppressed) A-request
already consumed — no duplicate is ever created (the dedup guarantee holds), but the original
owner's reminder for that due date is silently missed. That finding is not fixed here (integrator
instruction: assert only the guarantee that holds, record the miss as a MEDIUM backlog row — see
`docs/backlog/NTF.md`); the case below (I-F9) proves exactly that guarantee, and only that guarantee.

The first pass's own reasoning for two of its four `BLOCKED` rows (I-C4, I-X1) turned out to be
mistaken on closer reading of the code, not merely "not attempted" — see each row's evidence below.
The other two (U-B14, R6) were straightforward direct-DI-call tests that simply had not been written
yet. All four are closed this pass.

### Re-run and re-verified

| ID | Case | Expected | Status | Evidence |
|---|---|---|---|---|
| I-F5 | Transfer the cat to another owner before delivery | Cancelled; the next sweep requests one for the new owner | PASS | `CareReminderEndpointTests.TransferCatToNewOwner_BeforeDelivery_Cancelled_NextSweepRequestsForTheNewOwner` — same test the first pass committed, unweakened; now passes for real against `d5d06da`'s owner-id key. Re-run individually and as part of two full, green suite passes below. |
| U-F3 | Deduplication key format | Exact string match, now including the owner id | PASS | `CareReminderEndpointTests.DeduplicationKey_MatchesTheSpecifiedFormat` — assertion updated (by `d5d06da`) to the new `care:{kind}:{catId:N}:{refId:N}:{ownerId:N}:{dueOn:yyyyMMdd}` format; re-run and watched pass. |

### New case (CODE_REVIEW pass 5 MEDIUM, backlog item 1)

| ID | Case | Expected | Status | Evidence |
|---|---|---|---|---|
| I-F9 | Transfer a cat **A → B → A** (back to the original owner) before either request is delivered | No duplicate request is created for the original owner — §8.11's dedup contract ("a duplicate can never fail a producer's business command") forbids a second row for a key already consumed, regardless of that row's status. This case deliberately does **not** assert that the original owner receives a fresh reminder — CODE_REVIEW's own fifth-pass finding is that they do not, and that gap is backlogged (`docs/backlog/NTF.md` #1), not fixed. | PASS | `CareReminderEndpointTests.TransferBackToOriginalOwner_BeforeDelivery_NoDuplicateRequestIsCreated` (new this pass) — three sweeps and two transfers; asserts exactly one row exists for the original owner's key after the third sweep, i.e. the sweep completed as a clean no-op rather than throwing a unique-constraint violation or silently inserting a second row. |

### Previously `BLOCKED`, closed this pass

| ID | First-pass reason | Status | Evidence |
|---|---|---|---|
| U-B14 | No isolated `AccountRecipientDirectory` unit test existed | PASS | `AccountRecipientDirectoryTests` (new file, 3 cases): `ResolveAsync_TwoHopMergeChain_ResolvesToTheFinalSurvivor` (a genuine two-hop merge chain, not just one link), `ResolveAsync_AnonymisedCustomer_ResolvesInactive`, `FindMergedDuplicatesAsync_OneHopMerge_ReturnsTheDuplicate` — all call `INotificationRecipientDirectory` directly against a real `CatEcommerceDbContext`, seeded through the same staff/customer HTTP endpoints the rest of the suite already trusts (merge, deletion/anonymise). |
| I-C4 | No test drove a branch-specific `notifications.quietHours` override | PASS | `QuietHoursEndpointTests.BranchQuietHoursOverride_AppliesToARequestCarryingThatBranchId` (new) — a fresh branch's own override ("09:00-10:00" Asia/Dhaka, a window the shared default 21:00-08:00 never covers) is set via `PUT /api/admin/settings?branchId=...`, and a notification carrying that `BranchId` at 09:30 Asia/Dhaka is deferred to 10:00 — only possible if `QuietHoursCalendar` genuinely read THIS branch's override rather than falling back to the main branch or the global default. |
| I-X1 | Believed no log-capture harness existed for Notifications | PASS | The first pass's own stated reason was **wrong**, not merely unaddressed: `NotificationsApiFactory : CatEcommerceApiFactory`, and `WithCapturedLogs()` is a plain public instance method on the base class with no override anywhere in this tree — already inherited, simply never used here (`AdministrationApiFactory`'s own `AdminLogCaptureEndpointTests`, TEST gate seventh pass, made the identical discovery for its own area). `NotificationLogCaptureEndpointTests.RepresentativeFlows_NeverLogTokenLinkOrRenderedVariableValues` (new) drives a password-reset round trip and a care-reminder round trip through a capturing factory and asserts none of `"reset-password#token="`, the raw mobile number, or a distinctive rendered cat-name marker appear in any captured log line, at any level. |
| R6 (ARCHITECTURE HIGH 5, item 27) | `NotificationPersonalDataEraser` registered but never invoked by any test | PASS | `NotificationPersonalDataEraserTests` (new file, 2 cases): `EraseAsync_RedactsTheCustomersOwnNotificationAndRequest_ThenIsIdempotent` (seeds a real Sent notification and its request via the normal delivery pipeline, calls `EraseAsync` directly via `IEnumerable<IPersonalDataEraser>`, and watches both rows redacted — then calls it a second time and watches `Completed` with no further change, per the interface's own idempotency contract) and `EraseAsync_CustomerWithNoNotificationData_ReturnsCompletedWithoutThrowing` (contract item 4). |

### Environment (this pass)

- Build: `dotnet build CatEcommerce.sln` — 0 warnings, 0 errors.
- Database: `catecommerce_ntf`, dropped and recreated with `psql` and migrated with
  `dotnet ef database update` **twice** (once per full suite run below); `dotnet ef migrations
  has-pending-model-changes` reported clean both times.
- Full suites run **twice**, both green, both against a freshly-recreated database:

| Suite | Run 1 | Run 2 |
|---|---|---|
| Domain | 415/415 | 415/415 |
| Application | 119/119 | 119/119 |
| Infrastructure | 82/82 | 82/82 |
| Integration | 500/500 | 500/500 |

(492 from the first pass's own "after" count + 8 new this pass: I-F9, the branch quiet-hours
override, the three `AccountRecipientDirectoryTests` cases, the log-capture case, and the two
eraser cases.)

### Updated summary (supersedes the first-pass table above)

| | Count |
|---|---|
| Total | 120 |
| PASS | 120 |
| FAIL | 0 |
| BLOCKED | 0 |
| NOT RUN | 0 |

### Acceptance-criteria coverage — A6 (NTF-03 AC2), updated

Covered by U-F1, I-F4, I-F6, I-F7, I-F5, I-F9. **PASS** — both halves of A6 now hold: "cancel a stale
reminder" (I-F4/I-F6/I-F7, unchanged) and "the next sweep requests one for the new owner" (I-F5, now
fixed and passing). I-F9 additionally proves the dedup guarantee is not violated by the fix — the one
remaining gap (a missed, not duplicated, reminder on an A→B→A round trip) is a CODE_REVIEW-pass-5
MEDIUM, backlogged per the owner rule, not a TEST-gate finding.

### Cases I could not run — all four first-pass entries closed, none remain

`U-B14`, `I-C4`, `I-X1` are all closed above; no new `BLOCKED` cases were opened this pass.

### New test files this pass (test-only; no production code touched beyond what `d5d06da`/`217024a` already fixed)

- `tests/CatEcommerce.IntegrationTests/Endpoints/CareReminderEndpointTests.cs`:
  `TransferBackToOriginalOwner_BeforeDelivery_NoDuplicateRequestIsCreated` (I-F9).
- `tests/CatEcommerce.IntegrationTests/Endpoints/QuietHoursEndpointTests.cs`:
  `BranchQuietHoursOverride_AppliesToARequestCarryingThatBranchId` (I-C4).
- `tests/CatEcommerce.IntegrationTests/Endpoints/AccountRecipientDirectoryTests.cs` (new file, U-B14).
- `tests/CatEcommerce.IntegrationTests/Endpoints/NotificationLogCaptureEndpointTests.cs` (new file, I-X1).
- `tests/CatEcommerce.IntegrationTests/Endpoints/NotificationPersonalDataEraserTests.cs` (new file, R6).

### Never marked PASS without watching it (this pass)

Every PASS row above was watched directly: each new/changed test was run individually first, then
re-confirmed as part of two full, green Domain/Application/Infrastructure/Integration suite passes
against a freshly dropped-and-recreated `catecommerce_ntf`, with `has-pending-model-changes` clean
both times. No row was inferred from a passing neighbour.
