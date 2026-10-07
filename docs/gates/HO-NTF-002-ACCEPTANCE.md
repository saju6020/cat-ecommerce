GATE: ACCEPTANCE
HANDOVER: HO-NTF-002
COMMITS: 83a4f10 275cf07 d7ce065 86c0098 778fae8 260e31b 66e8535 a50a2c7 (code; branch ho/NTF-002, range cffaa79..a50a2c7); gate records 1a5965d 8bb94ed ca9e2a5 981f2fa bce722e
DATE: 2026-10-04
STATUS: PASS

First pass. This gate read all of HO-NTF-002, including the three §1 Amended rows dated 2026-10-04, the four prior gate records, `docs/test-cases/TC-HO-NTF-002.md`, the code diff of the CODE_REVIEW fix (66e8535..a50a2c7) and the tests cited for each §18 row.

Owner rule (2026-09-27): only BLOCKER/HIGH findings fail a gate. MEDIUM/LOW findings belong in `docs/backlog/NTF.md`. This gate has not edited that file.

MSW-only evidence is judged the same way as in HO-ADM-002 and HO-FIL-002 ACCEPTANCE:
- A criterion is MET on the MSW e2e plus unit evidence.
- The real-API half is carried as a condition, not counted as a pass.
- §16's real-API column (10 cells) is "not run — no API" on the TC sheet. These cells are recorded in §1 Amended ("real-API runs not performed"), and none is counted as a pass here.
- E-D1 is MSW-only by §16's own wording.

PRIOR GATES (read from docs/gates/ and docs/test-cases/)
- ARCHITECTURE: PASS (1a5965d, head 66e8535). 0 BLOCKER, 0 HIGH, 1 MEDIUM, 4 LOW.
- SECURITY: PASS (8bb94ed, head 1a5965d). 0 BLOCKER, 0 HIGH, 3 LOW.
- CODE_REVIEW: PASS on the second pass (981f2fa). The first pass (ca9e2a5) FAILED on 1 HIGH, the editor unmounting on a failed refetch or Reload. a50a2c7 resolved it.
- TEST: PASS (bce722e, at a50a2c7). The sheet has 49 rows: 49 PASS, 0 FAIL, 0 BLOCKED, 0 NOT RUN, plus 10 real-API cells "not run — no API".
- a50a2c7 came after the ARCHITECTURE and SECURITY runs. This gate read its product diff:
  - `[key]/page.tsx`: ErrorState only when `!template`, plus a role="status" banner.
  - `TemplateEditor`/`useTemplateWrites`: a `reloading` state.
  - `useNotificationTemplate`: `refetchOnWindowFocus: false`.
  - It adds no I/O, no sink, no dependency and no new file outside §13. The CODE_REVIEW second pass and TEST cover it.
  - No ARCHITECTURE or SECURITY delta is needed. This follows the HO-ADM-002 precedent for small, gate-reviewed deltas.

VERIFICATION RUN BY THIS GATE (web/, worktree HEAD bce722e, code identical to a50a2c7)
- `npx vitest run` over tests/components/{inbox,notifications,templates}, account/preferred-channel, account/notification-preference-matrix and unit/is-safe-internal-path: 14 files, 105 tests. Result: 104 passed, 1 failed.
  - The failure is U-C6 "a file using srcDoc outside SandboxedEmailPreview fails the lint rule". It hit its 60 s test timeout (72 s) while 13 files ran in parallel.
  - Rerun alone: 4/4 passed. U-C6's first case took 4.3 s.
  - It is a load-sensitive timeout, not a product fault. The TEST gate saw the same pattern (6 files / 8 tests timing out under load, all green on rerun). V-C6 proved the real `npm run lint` failure independently. See L-1.
- Every §15/§16 ID cited by §18 maps to a named test in a HO-NTF-002 file (grep of tests/).
- E-X1/E-X2 (`notifications-a11y-responsive.spec.ts:24-71`) cover every §8.1 route at 360/768/1024/1440:
  - inbox, preferences, unsubscribe with and without a token;
  - template list, editor and secret-template editor;
  - log, failed detail and redacted detail;
  - the staff customer page.
- Playwright was not re-run. The e2e evidence is the TEST record's: notifications short specs 15/15 and a11y 61/61, each on chromium and on mobile-360.

CRITERIA (HO-NTF-002 §18)

| # | Result | Evidence |
|---|---|---|
| A1 | MET (MSW; real-API half carried) | U-A1 and U-A2 (`inbox-badge.test.tsx:35,66`) and U-A3 (`inbox-item.test.tsx:54`), all PASS on the TC sheet. V-A2 adds the real-browser proof that polling pauses while the tab is hidden. E-A1 and E-A2 (`notifications-inbox.spec.ts:17,29`) PASS on chromium and mobile-360: View link to `/portal/cats/…`, and the badge goes from "Inbox, 3 unread messages" to 2 on open. E-A1 as written is a real-API case (the sweep after an Api restart, badge 1); that run is "not run — no API" |
| A2 | MET (MSW) | U-B1, U-B2 and U-B3 (`preferred-channel.test.tsx:67,98,126`): email is disabled with the hint; a channel change and a cell change go in ONE PUT, and nothing is sent before Save; the field error has no code. E-B1 (`notifications-inbox.spec.ts:54`): the change survives a reload. All PASS |
| A3 | MET (MSW) | U-C1 (caret insert), U-C2 (debounce and abort; real-browser V-C2) and U-C4 (Unicode 2-segment) in `template-editor.test.tsx:73,107,175`. U-C5 (`sandboxed-email-preview.test.tsx:15`; behavioural proof in V-C5 and the e2e "script never runs", `notifications-templates.spec.ts:28`). E-C2 (`notifications-templates.spec.ts:73`): Customised -> Default. Sample values are sent with the preview, not saved (`template-editor.test.tsx:228`). All PASS |
| A4 | MET (MSW) | U-C3 (`template-editor.test.tsx:152`): the server message naming `{{catname}}` appears in the panel and on the field, and typing is not blocked. E-C1 (`notifications-templates.spec.ts:43`): preview and save. Both PASS |
| A5 | MET (MSW-only by §16) | U-D1, U-D2 and U-D3 (`unsubscribe.test.tsx:39,73-115,126`): no request on load; the hash is removed; the four messages; no token gives no button. E-D1 (`notifications-unsubscribe.spec.ts:9`): no POST for 500 ms after load, the hash is cleared, one POST on click, and the matrix shows Marketing by Email unchecked. All PASS |
| A6 | MET (MSW; real-API half carried) | U-E2 (`log-filters.test.tsx:84,112,122,134`): round-trip through the URL and Back; a recipientId deep link; >93 days refused before any request; from/to/channel/status/templateKey/destination all reach the API. U-E3 (`log-table.test.tsx:47,65`): every status shown as text; the held row in the branch zone. E-E1 (`notifications-log.spec.ts:14`): customer filter, then status filter, then the detail with 4 attempt rows. All PASS |
| A7 | MET (MSW; real-API half carried) | U-E4 (redacted reset: refusal sentence), U-E6 (confirm names channel and destination; held 201 with link) and U-E7 (limit sentence) in `notification-detail.test.tsx:78,109,161`. E-E1 resends a failed SMS by email and follows the chain both ways. All PASS |
| A8 | MET | U-E1 (`queue-summary.test.tsx:37`): role="status" banner when SMS is unconfigured; `:47` covers email and both. E-E1 also asserts the SMS warning in the browser. PASS |
| A9 | MET | U-A4 and U-A5 (`inbox-item.test.tsx:103,120`, plus `tests/unit/is-safe-internal-path.test.ts`). U-C5. U-C6 (3 lint cases), with V-C6 showing `npm run lint` exiting 1 on a temp violating file. U-E5 (`notification-detail.test.tsx:91`, `log-table.test.tsx:111`). Source check by this gate: `srcDoc` appears only at `components/notifications/SandboxedEmailPreview.tsx:18`. There is no `dangerouslySetInnerHTML`/`innerHTML` use in app/, components/ or lib/ |
| A10 | MET (MSW) | E-X1 and E-X2 (`notifications-a11y-responsive.spec.ts`): 61/61 on chromium and 61/61 on mobile-360 (TEST record). Every §8.1 route and four viewports, plus the error, open-preview, resend-confirm and open-item states. The serious/critical filter is at `:85-88` |

Carried condition for the integrator, not a gate failure: §16's real-API runs of E-A1…E-X2 must be recorded once HO-NTF-001's Api, Development senders and worker run with this slice (§5, §17 step 7). Until then A1, A2, A3, A4, A6, A7 and A10 rest on MSW generated from HO-NTF-001 §12.4. SECURITY cross-checked the role and endpoint mapping against the master controllers.

FINDINGS
- No BLOCKER or HIGH findings. The CODE_REVIEW HIGH is resolved: U-C7-R (`template-editor-page.test.tsx:54,73`) is PASS on the sheet, and this gate read the fix.
- [LOW] L-1 web/tests/components/templates/sandboxed-email-preview.test.tsx:40-44: U-C6 runs ESLint programmatically with a 60 s timeout.
  - The first call took 72 s when run alongside 13 other files, and 4.3 s alone.
  - The full suite (109 files) runs files in parallel, so this test is a likely contributor to the load-timeout reruns the TEST gate recorded.
  - Fix: warm or share the ESLint instance in a `beforeAll` with its own timeout, or move U-C6 to a dedicated lint-check script.
- Carried, not re-raised. These are the ARCHITECTURE (1 MEDIUM, 4 LOW), SECURITY (3 LOW) and CODE_REVIEW (3 MEDIUM, 5 open LOW plus 1 new LOW) items, and the TEST "gaps left deliberately":
  - permanent browser specs for V-A2 and V-C2;
  - the weak U-C5 jsdom canary;
  - the loose U-C2 abort assertion.
- Integrator items at merge, from ARCHITECTURE:
  - `docs/handover/README.md:71`: the ADR table still reads 019…023 and should read 159…163.
  - `docs/handover/README.md:106`: the next-ADR pointer should become ADR-FE-164.
  - `web/README.md:134`: the mocks note should describe the alias pattern.

OUT-OF-SCOPE CHECK (§4, §14)
- §4: none found.
  - No backend change. `git diff --name-only cffaa79 a50a2c7 -- src tests .ai .claude` is empty.
  - No WebSocket, EventSource, service worker or push permission. Polling is in `lib/hooks/useInbox.ts`.
  - No inbox delete; `lib/api/inbox.ts` has no delete call.
  - No rich-text editor or HTML template editing.
  - No client-side placeholder substitution. ARCHITECTURE checked `lib/validation/templateContent.ts`.
  - No campaign sending.
  - No new dependency. `web/package.json`, `package-lock.json`, `tsconfig.json` and `next.config.ts` are unchanged.
- §20: no condition was hit and worked around.
  - §20.4: the `400 { "error" }` range response is reported in §1 Amended and is not shimmed.
  - §20.5: the different-channel resend confirm does not invent a destination field. It uses existing data, as declared in §1 Amended deviations (3).

UNDECLARED DEVIATIONS
- None found. Every file outside literal §13 is itemised with a reason in §1 Amended "files outside §13" (1)-(6). The ten decisions are declared in §1 Amended "deviations" (1)-(10). The ADR renumbering 019…023 -> 159…163 is declared too.
- `web/lib/api/types.ts` (§13 "append") is not touched. That is an omission of an allowed edit, not a deviation.
- The handover diff is the §1 Amended cell only (+1/−1).

SCOPE CHECK
- files in allowed scope: 96/96.
  - That is the 94 files in `git diff --name-only cffaa79 a50a2c7` (89 web files, 3 gate records, the TC sheet and the handover §1 cell), plus `docs/gates/HO-NTF-002-TEST.md` (bce722e) and this record.
  - 83 of the web files are literal §13.
  - 6 are itemised in §1 Amended: `mocks/handlers/areas/accounts.ts`, `components/account/NotificationPreferenceMatrix.tsx`, `components/common/portal-header.tsx`, `nav/areas/notifications.ts`, `tests/unit/is-safe-internal-path.test.ts` and `components/admin/adminNavItems.ts`, whose one entry is in §13 and whose absence on master is explained.
- files outside allowed scope: none.
- The uncommitted `web/public/mockServiceWorker.js` in the worktree is not part of any reviewed commit and was left untouched.
