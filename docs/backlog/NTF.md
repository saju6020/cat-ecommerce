# Backlog — Notifications (HO-NTF-001)

Medium/low gate findings not blocking the merge (owner rule 2026-09-27: only BLOCKER/HIGH block a gate).
Fix in a later batch. Source: `docs/gates/HO-NTF-001-*.md`.

All four rows the TEST gate's first pass left `BLOCKED` (U-B14, I-C4, I-X1, R6) were closed in the
second pass — see `docs/test-cases/TC-HO-NTF-001.md`'s "Second pass" section for the tests (moved
from `docs/gates/`, ACCEPTANCE A-1, matching every other area's location). Only genuinely open items
remain below.

Rows 5-17 are the ACCEPTANCE gate's consolidated "OPEN MEDIUM/LOW FINDINGS" table, minus the ones
that gate fix round itself closed: the TC sheet's location (A-1), the ADR-342 pipeline sketches, and
the stale §15/§8.8 text and §1 overflow record (A-5/A-6) — all fixed by `9a87428`, so they are not
carried here as still open.

| # | Severity | Item | Source |
|---|---|---|---|
| 1 | ~~MEDIUM~~ Resolved | **Resolved 2026-10-07 in ho/BUGS-MIGRATIONS.** Was — A cat transferred A→B→A within the same due window computes the SAME `DeduplicationKey` for the second A-request as the original (now-Suppressed) A-request already consumed — nothing is ever sent twice (the dedup guarantee holds, proven by `CareReminderEndpointTests.TransferBackToOriginalOwner_BeforeDelivery_NoDuplicateRequestIsCreated`), but the original owner's reminder for that due date is silently missed rather than re-requested. Needs a transfer-generation field (or similar) in the key to distinguish "the Nth time this owner has held this cat for this due date" from a stale collision. Not fixed by the 2026-09-27 owner-id fix (d5d06da), which only distinguishes *different* owners, not the *same* owner returning. | CODE_REVIEW (pass 5) / TEST (second pass) |
| 2 | LOW | The DbUpdateException "retry/park" backstop (`RecordExpandFailureAsync` after 5 consecutive REAL save failures) is proven for a real `xmin` race via `ConcurrentExpand_OfTheSameRequest_ExactlyOneExpansion_NeverDoubled`, but that test dispatches the command directly rather than through `NotificationWorkCycle.DispatchAsync`'s own catch, so it does not itself prove the specific backstop wiring after 5 consecutive real failures. | TEST (first pass) |
| 3 | LOW | I-D5 ("resend after the customer changed their mobile") is covered by the underlying cross-channel re-render mechanics, but no test literally changes the mobile number mid-scenario before resending. | TEST (first pass) |
| 4 | LOW | R5 (detail/template reads project instead of materialising) is verified indirectly through the handlers' own passing behaviour, not by a direct "no `Include` was used" architectural assertion. | TEST (first pass) |
| 5 | ~~LOW~~ Resolved | **Resolved 2026-10-07 in ho/BUGS-MIGRATIONS.** Was — C5 (delivery) has no attempt counter of its own: a non-overflow save failure after the claim commits leaves the row `Sending`, and it is re-claimed and re-sent every lease indefinitely. | SECURITY (pass 3) |
| 6 | ~~LOW~~ Accepted | **Accepted as is — owner decision 2026-10-07** (latent: no Marketing template in v1). Was — The plaintext unsubscribe token/link is stored beside its hash (`UnsubscribeTokenHash`). Latent today: v1's shipped catalogue has no Marketing template, so the path is never exercised in production. | SECURITY (passes 1-3, carried); ARCHITECTURE (pass 2, carried) |
| 7 | ~~LOW~~ Resolved | **Resolved 2026-10-07 in ho/BUGS-NTF.** Was — `PublicSiteStartupGuardHostedService` accepts an empty or placeholder `PublicSite:WebBaseUrl` in every non-Production environment (Staging included). A reset/care/unsubscribe link built there would be relative and broken. The fix belongs to the integrator, in the guard, not to this handover's own files. | ARCHITECTURE (pass 2, LOW); SECURITY (passes 2-3, carried) |
| 8 | LOW | `appsettings.Development.json.example` is stale: it still has `Account:WebBaseUrl` (removed) and has no `Notifications:ApiBaseUrl`. A developer who copies it gets a Development host that fails `ValidateOnStart`. | ARCHITECTURE (pass 2) |
| 9 | LOW | A third copy of the variable-assembly-and-render block (C4 expand, C5 fallback, C7 resend) risks drift between the three if one is changed without the others. | ARCHITECTURE (pass 2) |
| 10 | LOW | A third single-save exception-translation seam (`EfNotificationResendStore`, after the request store and the claim store). If a fourth appears anywhere in the solution, it should become one generic ADR-recorded pattern instead of a fourth bespoke port. | ARCHITECTURE (pass 2) |
| 11 | LOW | The anonymise loop is duplicated verbatim between `NotificationPersonalDataEraser` and `AnonymiseNotificationsOnCustomerAnonymisedHandler`. Both call the same aggregate methods today, so behaviour cannot yet diverge, but a future change landing in only one of them would be a silent gap. | ARCHITECTURE (pass 2) |
| 12 | LOW | `GetDueCareRemindersQueryHandler` re-derives "latest per vaccine" in memory (`GroupBy`) instead of reusing `CatVaccinationStatusCalculator` (F26). It re-derives over every alive cat on every hourly sweep — acceptable at today's data volume, unbounded as it grows. | ARCHITECTURE (passes 1-2, carried); CODE_REVIEW (pass 1) |
| 13 | LOW | The admin notification log returns hand-built `{ error }` 400s for range and paging errors instead of the shared `ValidationResponse` shape. Accepted per the existing Administration-area precedent for the same kind of query-string guard. | ARCHITECTURE (passes 1-2, carried) |
| 14 | LOW | Integrator notes, not yet acted on: keep the environment name "Testing" reserved for the automated test host (a real QA deployment named "Testing" would silently run with the worker off and the Capturing senders permitted); consider excluding `appsettings.Testing.json` from the publish output (a `.csproj` change, forbidden within this handover's own §14 scope). | ARCHITECTURE (pass 2) |
| 15 | LOW | A handful of TC-sheet rows are marked PASS with evidence that does not show the case as written: U-C4 cites a test unrelated to a hold running past `NotAfter`; I-B6 ("transient ×2, then accepted → `Sent`, 3 attempts") cites a test of six failures ending `Undeliverable` — no test anywhere covers a retry that later succeeds; U-B12's "not due" sub-case (a future `NotBefore`) has no dedicated unit test (I-C1 covers it end to end only); I-B3's audit query filters by `actorUserId=customerId` on an anonymous reset, so it may match nothing (the ACCEPTANCE gate's own direct database query closed this for that one run only). | ACCEPTANCE |
| 16 | ~~LOW~~ Resolved | **Resolved 2026-10-07 in ho/BUGS-NTF.** Was — §8.14 says a hold that would run past `NotAfter` expires the notification instead of sending it late. As implemented, `Defer` still holds the notification to the end of the quiet-hours window, and it expires on the *next pass after* that — never sent late (safe), but not expired the instant the hold would have run past `NotAfter` either. Latent today: the only template with a `NotAfter` (password reset) is Urgent and is never held. An undeclared deviation from the literal §8.14 wording. | ACCEPTANCE (A-3) |
| 17 | LOW | I-B16 (unconfigured senders) does not itself assert the startup warning log line or the provider's own "No SMS/email provider is configured" response text — both are shown correct by source reading only, not by a test watching for them. | ACCEPTANCE (A-4) |
| 18 | LOW | `PublicSiteOptions.cs:12` doc comment still names the removed `AccountOptions.WebBaseUrl` (integrator-owned file). | ACCEPTANCE (third pass) |

## HO-NTF-002 (notifications web)

Medium/low findings from `docs/gates/HO-NTF-002-*.md` (all five gates PASS; the one CODE_REVIEW HIGH
was fixed in `a50a2c7`). Real-API cells of E-A1…E-X2 are owed once the HO-NTF-001 Api runs (row N16).

| # | Severity | Item | Source |
|---|---|---|---|
| N1 | MEDIUM | `useLiveTemplatePreview` (called from `TemplateEditor.tsx:66`) and `InboxBadge.tsx:25` fetch through `lib/hooks` from inside components, against HO-CAT-002 §8.2 rule 4 (page wires the data). Master has precedent (`FileUploader`, `PolicyAcceptancePrompt`, `ActiveBranchPicker`) — either enforce the rule or record the exception in an ADR. | ARCHITECTURE |
| N2 | ~~MEDIUM~~ Resolved | **Resolved 2026-10-07 in ho/BUGS-NTF.** Was — `staff/notifications/page.tsx:107` — clearing From or To renders a blank table area with no "choose a date range" prompt, and drops the URL param so a reload silently restores the default range. | CODE_REVIEW |
| N3 | MEDIUM | Components over the ~150-line guide: `TemplateEditor` 215, `NotificationLogFilters` 204, `NotificationLogTable` 187 (seams: banners + ConfirmDialogs; a labelled-field wrapper; table body vs card list). | ARCHITECTURE / CODE_REVIEW |
| N4 | ~~MEDIUM~~ Resolved | **Resolved 2026-10-07 in ho/BUGS-NTF.** Was — `notification-templates/[key]/page.tsx:50-58` — `canManage` comes from the session roles, so while the session loads the editor shows read-only, then flips to editable. | CODE_REVIEW |
| N5 | MEDIUM | U-C5's vitest "no script ran" assertion cannot fail under jsdom (srcdoc never executes); the real proof is the e2e test. Strengthen or drop the unit assertion. | TEST |
| N6 | LOW | `useChangeNotificationPreferences` (`useAccountMutations.ts:71`) has no callers; the comment at `NotificationPreferenceMatrix.tsx:26` still names it. | ARCHITECTURE |
| N7 | LOW | `useCustomerLookup.ts:12` is a second hook over `findCustomers`, which `useFindCustomers` already wraps. | ARCHITECTURE |
| N8 | LOW | srcDoc lint rule (`eslint.config.mjs:139-158`) misses a computed spread key and `setAttribute("srcdoc", …)`; nothing requires `sandbox` on other iframes. Ban `<iframe>` JSX outside `SandboxedEmailPreview.tsx` and add a `setAttribute('srcdoc')` selector. | SECURITY |
| N9 | ~~LOW~~ Resolved | **Resolved 2026-10-07 in ho/BUGS-NTF.** Was — `SandboxedEmailPreview.tsx:17-20` — `referrerPolicy` does nothing for a srcDoc frame; images in previewed HTML still load and reveal the admin's IP/origin. Prefix the srcDoc with a CSP meta (`default-src 'none'; img-src data:; style-src 'unsafe-inline'`) and `<meta name="referrer" content="no-referrer">`; fix the comment. | SECURITY |
| N10 | ~~LOW~~ Resolved | **Resolved 2026-10-07 in ho/BUGS-NTF.** Was — The log's destination search term (email/mobile) is kept in the page URL (`NotificationLogFilters.tsx:16`) and the API query string (`notifications-log.ts:168`) — browser history and access logs. Keep it out of URL state or search by `recipientId`. | SECURITY |
| N11 | LOW | `useTemplateWrites.ts:92` — the focus `setTimeout` is never cleared. | CODE_REVIEW |
| N12 | ~~LOW~~ Resolved | **Resolved 2026-10-07 in ho/BUGS-NTF.** Was — Pagination (`notifications/page.tsx:71`) uses `router.push` outside `useUrlFilterState`, so a filter edit inside the 300 ms debounce is lost on a page change. | CODE_REVIEW |
| N13 | ~~LOW~~ Resolved | **Resolved 2026-10-07 in ho/BUGS-NTF.** Was — `NotificationLogTable.tsx:55-62` — `keepPreviousData` shows old rows with no fetching indicator, and a failed refetch with old rows shows no error. | CODE_REVIEW |
| N14 | LOW | Test tightening: U-C2's abort assertion is `signals.some(aborted)` with ~3 s real-timer sleeps; `inbox-badge.test.tsx:87` never restores `visibilityState`; U-C6 takes ~72 s under parallel load (timeout-sensitive). Promote the TEST gate's temporary browser checks (hidden-tab polling, preview abort) to permanent specs. | CODE_REVIEW / TEST / ACCEPTANCE (L-1) |
| N15 | ~~LOW~~ Resolved | **Resolved 2026-10-07 in ho/BUGS-NTF.** Was — `InboxBadge.tsx:25` — count changes are not announced (no live region); the "couldn't refresh" banner on the template page has no retry/dismiss and stays until a later fetch succeeds. | CODE_REVIEW (passes 1–2) |
| N16 | LOW | Integrator: run the real-API column of E-A1…E-X2 once the HO-NTF-001 Api is running; `GET /api/admin/notifications` answers a bad range with `400 {"error"}`, not `ValidationResponse` (§20.4, UI refuses client-side first). | ACCEPTANCE / build |

## Bug-fix batch ho/BUGS-NTF (combined review PASS — `docs/gates/BUGS-NTF-REVIEW.md`)

| # | Severity | Item | Source |
|---|---|---|---|
| B1 | MEDIUM | N9 preview: the CSP blocks subresources but not navigation — a previewed `<meta http-equiv=refresh>` or a clicked link still loads a remote page in the sandbox and reveals the admin's IP. Strip meta-refresh and links from the preview HTML. The metas sit before any DOCTYPE (quirks mode). | review |
| B2 | MEDIUM | #16: the expire-and-redact block (`DeliverNotificationCommandHandler.cs:83`) copies the one at :44; the test uses VaccinationDue (no secret), so redaction on this path is unproven. Extract one method and add a secret-bearing case. | review |
| B3 | LOW | #7: "todo" placeholder marker is a substring match (a real host like `todos.example.org` would fail Production startup). | review |
| B4 | LOW | N10: the term still goes to the API as `?destination=` (server logs); Back/reload no longer restore it; the effect's eslint-disable hides a deps list missing `pageNumber`, `values`, `router`; `NotificationLogFilters.tsx:33-37` doc comment garbled. | review |
| B5 | LOW | Live regions: the inbox badge announces the count on the first poll of every portal page; the log's "Updating…" is announced on every background poll. | review |
| B6 | LOW | FIL row 4: the held count and eligible ids are still two statements (not one snapshot). | review |

## BUGS-MIGRATIONS combined review (2026-10-07)

| # | Severity | Finding | Source |
|---|---|---|---|
| BM1 | LOW | `NotificationAggregate.MustPark` — a slow-but-alive 6th send whose lease lapsed can be parked by another worker while still in flight (the xmin conflict then fails the original's save). Consider a lease longer than the provider timeout. | CODE_REVIEW (`docs/gates/BUGS-MIGRATIONS-REVIEW.md`) |
| BM3 | LOW | `DueCareReminderRow` defaults `OwnershipGeneration = 0`, which can hide a caller forgetting to pass it; make it required. | CODE_REVIEW |
