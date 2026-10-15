GATE: ARCHITECTURE + CODE_REVIEW + SECURITY (combined follow-up, round 2)
HANDOVER: HO-BIL-002
COMMITS: ecd7f867..006e5a4b (reviewed as c9aecb50..HEAD, excluding docs/gates)
DATE: 2026-10-08
STATUS: PASS

VERIFIED CLOSED
- [ARCH BLOCKER] .ai/decisions/adr.md: `git diff 0442f9ff HEAD -- .ai` is empty. Closed.
- [ARCH HIGH] Staff PDF/CSV downloads: DownloadButton -> downloadBillingFile -> xhrRequest sends X-CSRF-Token and X-Branch-Id,
  maps 400/413/401/403/404/409/429/other onto the same error classes as apiFetch (429 uses the 60 s default, as the old upload did),
  shows describeBillingError next to the button. Blob URL is revoked after 10 s; the blob is held only for the click. No anchor
  remains for a branch-scoped staff file. Statement upload moved onto xhrRequest with identical headers/status mapping; the 1 MiB
  check lives in the caller and is unchanged. Closed.
- [CR HIGH] Allocations keyed by invoice id; rows, checkbox, amount, error and "pay the rest" all index by invoiceId; submit is built
  from the live `open` list by id. A row that vanishes on refetch is simply not submitted. No positional path remains. Closed.
- [CR HIGH] syncDraft: creates once (createdDraftId ref), reads the real version via getInvoice, calls setCharges when the selection
  differs, issues with the returned version. Double click: Issue confirm and Save are disabled synchronously (ConfirmDialog isPending, busy).
  Failure between setCharges and issue on the new-invoice route: retry re-reads the server draft, sees equal sets, does not repeat
  setCharges. Idempotency key kept across the retry. Kept-unsaved-ticks notice works and is tested. Closed.
- [SEC MEDIUM] Refund key renewed on open, on close, on success and when the normalised request differs; an identical retry (same
  normalised amount, trimmed text) keeps it. Success shows amount and method. Closed.
- Filter card guard (invoice number, COD courier) refuses and writes nothing to the URL; export defaults to the active branch with an
  explicit "all". Closed.
- Tests U-A2, U-X2, download, allocation (refetched/reordered list), composer retry, saved-draft save, unsaved ticks, refund (new key
  after a different request, same key on retry, amount+method shown) assert the fixed behaviour. tsc clean, eslint clean on the
  touched dirs, vitest tests/components/billing 48/48 pass.

FINDINGS (none blocking)
- [MEDIUM] web/components/billing/InvoiceComposer.tsx syncDraft: on the saved-draft route `server = draft` is the prop, which is stale
  after a successful setCharges until onDraftChanged runs. If issue then fails (network/5xx), the retry compares against the old
  charge set and sends setCharges with the old expectedVersion -> 409 -> "This draft changed - reloaded." Recoverable, no wrong
  charge set is issued, but the retry costs a round trip. Fix: always getInvoice(existing) in syncDraft, or call onDraftChanged after
  setCharges. No test covers a 409 after setCharges.
- [LOW] RefundDialog: the key is renewed on Cancel/close, and A -> B -> A edits get a fresh key for the second A. After a lost
  response a user who closes and re-submits the same refund gets a new key, so a refund the server did record could be duplicated
  (server-side over-refund cap still applies). Accepted trade-off from round 1; consider warning "check the payment's refunds first".
- [LOW] downloadBillingFile: filename*= (RFC 5987) in Content-Disposition is ignored; fallback name is used. 10 s revoke timer is
  fixed. No test asserts revokeObjectURL or the statement-upload headers (existing reconciliation tests pass).
- [LOW] ExportPanel still uses an anchor to exportDownloadUrl with a branch query parameter (not X-Branch-Id); acceptable while that
  endpoint takes the branch as a parameter, but it bypasses the CSRF/error-mapping path.

SCOPE CHECK
- files in allowed scope: 24/24 (web/ and docs/ only; ecd7f867 reverts the earlier .ai change, net .ai diff vs 0442f9ff is empty)
- files outside allowed scope: none
