GATE: SECURITY
HANDOVER: HO-BIL-002
COMMITS: 0442f9ff..c9aecb50 (branch ho/BIL-002, 14 commits, HEAD c9aecb50)
DATE: 2026-10-08
STATUS: PASS

CHECKS RUN
- I read the whole diff (127 files) for the §17 SECURITY row and the coordinator's focus list. No dev server, Playwright or database
  was used.
- C-5 guard probe: I copied `web/lib/validation/cardNumberGuard.ts` to a scratch folder (bil2-sec-scratch) and ran it under node 24
  (strip-types).
  - Refused: spaced, dashed, dotted and unbroken PANs; full-width digits; NBSP/ZWSP separators; a PAN with a trailing letter; a stray
    digit at the front of the run; and the FU1 shape `37085610 4003 3371 5286 9561`.
  - Allowed: `8801712345678` and `INV-DHK001-2627-000123`.
- Parity with master: 78b13a64 (HO-BIL-001-FU1) changed only the server's MASK. That review measured 0 refusal differences, old vs new.
  The client mirrors the refusal rule, so leaving 78b13a64 off this branch does not weaken the client guard.
- Guard coverage. Every free-text field that reaches a write runs `guardedText`, `requiredGuardedText` or `looksLikeCardNumber`:
  - payment reference, courier code/name and notes;
  - refund reference and reason; reason dialogs; credit-note reason;
  - manual-charge description; corrected-draft line descriptions; cash-up explanation; remittance reference; statement label;
  - billing-profile free text. Legal identifiers (TRN, account and routing numbers) are exempt by design (HO-BIL-001 §11.4 C1).
  - Forms submit through RHF `handleSubmit` with `zodResolver`, so a refused value never reaches `mutateAsync`.
  - The message is a constant and never repeats the value.
  - New billing code has no `console.*`, `localStorage` or `sessionStorage`.
- Idempotency. All four ADR-187 endpoints (payment, invoice issue, credit note, refund) are sent the key from `useIdempotencyKey`:
  - one `crypto.randomUUID()` per mount, renewed only on success;
  - submit buttons are disabled while in flight;
  - the server replays by (user, key) and refuses a different payment, invoice or customer with `IdempotencyKeyConflict`.
- Session:
  - `apiFetch` sends `X-CSRF-Token` on unsafe methods and `X-Branch-Id` on every call.
  - The statement-upload XHR (`billing-reconciliation.ts:189-198`) sets `withCredentials`, `X-CSRF-Token` and `X-Branch-Id`.
  - The upload is checked client-side for `.csv` and ≤ 1 MiB, and the input is cleared after every attempt.
- Portal: every read goes to `/api/customers/me/...`. The server resolves the caller, so another customer's document is a 404. Query keys
  are `['me','billing',…]`, separate from the staff keys, and both sign-in pages call `queryClient.clear()`. No staff API is imported
  by the portal components.
- Downloads:
  - The PDF, CSV and ZIP links are cookie-authenticated GETs whose URLs carry only IDs, dates and filters. A shared link needs the
    caller's own session.
  - No secret or token is in a URL, so a Referer leak exposes nothing.
  - The ageing CSV is built client-side from server strings, with formula-injection prefixing.
- Rendering: no `dangerouslySetInnerHTML`, `innerHTML`, `eval` or `window.open` in the diff. Receipts and invoices render as React text.
- Money:
  - No `Number(`, `parseFloat` or arithmetic on amounts.
  - Requests write the validated digit string with `JSON.rawJSON` (pattern-checked first, so nothing can be injected into the body).
  - The `moneyFromWire` fallback only reformats server decimals as text.
- Dependencies: `web/package.json` and the lockfile are unchanged.
- Online payment: methods are filtered `!isOnline`. There is no card field, gateway script or CSP change.
- Mocks: no file under app/, components/ or lib/ in this diff imports `@/mocks`. MSW start-up stays behind the existing dev-only
  dynamic import in `app/providers.tsx`, which this diff does not change.

FINDINGS
- [MEDIUM] web/components/billing/RefundDialog.tsx:67,82,118-122,136 — idempotency key reused across different refund intents.
  - The key lives for the dialog's mount. `close()` and the `open` effect reset the form but do not renew the key. It is renewed only
    on success.
  - Scenario: a refund of 1000 is recorded, but the response is lost (network error). Staff press Cancel, reopen the dialog, and enter
    a NEW refund, for example 500 with a different reason. The same key goes out. The server matches on (user, key, paymentId) and
    replays the first refund (`CreditCommandHandlers.cs:322-326`). The dialog says only "Refund recorded.", with no amount.
  - Result: staff believe the 500 refund was booked, and may pay cash out against it, when only the original 1000 exists. The ledger
    stays correct and there is no double refund, but the UI confirms an action that never ran. This is the "never reused across
    different intents" rule.
  - Fix:
    - Treat Cancel/close as ADR-FE-081's "explicit start again" and call `key.renew()` in `close()`.
    - Or keep the key, but pre-fill the reopened form with the last attempt's values, so the reopen is a retry, not a new intent.
    - Either way, show the server's refund (amount and method) in the success state, so a replay is visible.
- [LOW] web/components/billing/InvoiceListScreen.tsx:84, CodScreen.tsx:48 (with `invoicesCsvUrl`/`paymentsCsvUrl`,
  billing-staff.ts:477-488) — the invoice-number and courier-code filters have no C-5 guard.
  - Their values are written to the page URL (`useUrlFilterState`), browser history, the GET query string and the CSV link.
  - A PAN pasted into a filter would land in server access logs and history. ADR-FE-084 says the value is "never put in the URL".
  - Low likelihood: these are lookup boxes, not capture fields, and the server stores nothing from them.
  - Fix: run `looksLikeCardNumber` on filter input and refuse to apply it (field error, no URL write).
- [LOW] web/components/billing/ExportPanel.tsx:72-77, web/lib/api/billing-export.ts:9-12 — the UI says nothing about the known
  branch-scope gap.
  - The ZIP endpoint ignores the caller's branch assignment (HO-BIL-001 XI-15 / TEST F-4, MEDIUM, owner decision pending).
  - The UI offers "All branches" as the default, and lists every branch.
  - The gap is recorded only in code comments. Nothing on the screen says the export is not limited to the user's branches.
  - This is not a new server weakness and the UI does not widen it, because the endpoint takes `branchId` directly. But the screen
    makes the cross-branch pull a single click.
  - Fix belongs with XI-15 on the server. Until then, either default the selector to the active branch, or limit the list to branches
    in the session's assignment.
- [LOW] web/lib/api/billing-export.ts:38-40 — the download is a side-effecting GET that a cross-site page can trigger.
  - Every download writes an audit "run" (who, range, counts). As a plain link it is a GET with no CSRF header.
  - The session cookie is `SameSite=Lax` (`SessionCookieWriter.cs:46`), so a cross-site top-level navigation still sends it. Any page
    a logged-in accountant visits can trigger a download, which records a run in their name and pollutes the export history.
  - The attacker cannot read the data.
  - Fix (backend, not this FE): make the run-recording export a POST that returns a short-lived download token, or record runs only
    on an explicit POST.
- [LOW] web/lib/validation/cardNumberGuard.ts:105-120 — inherited server-rule gaps, shared by the client by design (parity).
  - These pass both guards: one stray digit glued to a card that does not start the run (`TRF 4574 54868861179406010`), a digit glued
    to the END of a card (`41111111111111110`), and letter-separated digits (`4111x1111x1111x1111`).
  - The first is the FU1 review's open LOW; glued digits are backlog BIL row 8(b).
  - Not a client defect. Any fix must change both guards together.

SCOPE CHECK
- files in allowed scope: 125/127 (web/** and docs/test-cases/TC-HO-BIL-002.md)
- files outside allowed scope:
  - .ai/decisions/adr.md (commit a3d87c77). §14 forbids `.ai/**`. The change is documentation only, with no security impact; it is
    left to ARCHITECTURE.
  - docs/gates/HO-BIL-002-CODE_REVIEW.md, which is a gate artifact, not developer output.

VERDICT
PASS. No BLOCKER or HIGH. The C-5 guard blocks sending on every free-text write field and never echoes, logs or stores the value. The
money-moving writes carry idempotency keys. CSRF and X-Branch-Id are sent on every mutating call, including the XHR upload. The portal
is scoped to the caller. Rendering is text-only. There is no new dependency, and the mocks are not shipped. The 1 MEDIUM and 4 LOW
findings go to docs/backlog/BIL.md under the gate blocking rule.
