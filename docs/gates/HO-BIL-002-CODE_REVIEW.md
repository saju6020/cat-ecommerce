GATE: CODE_REVIEW
HANDOVER: HO-BIL-002
COMMITS: 81396c75..c9aecb50 (branch ho/BIL-002, base 0442f9ff, HEAD c9aecb50)
DATE: 2026-10-08
STATUS: FAIL

Checks run: `npx tsc --noEmit` clean; eslint over components/billing, lib/hooks/billing, lib/money,
lib/api/billing-*.ts clean; vitest tests/components/billing 37/37 pass. No dev server, Playwright or
database was used. Money: no `Number(`/`parseFloat`/`+`/`*` on amounts in components/billing,
lib/api/billing-*, lib/money (the only `Number(` hits are in other areas' pages). Dates: no
`new Date`/`toLocaleDateString` in billing code; everything goes through lib/format/time.
Hooks live in lib/hooks/billing and every posting mutation calls `invalidateAfterPosting` (bank
statement/profile mutations invalidate their own keys, see LOW).

FINDINGS
- [HIGH] web/components/billing/RecordPaymentForm.tsx:84-104 + AllocationRows.tsx:49-66 — allocation rows are
  rendered from the LIVE `open` invoice list and registered by array index (`allocations.${index}`), but the
  form's `allocations[].invoiceId` is seeded ONCE (`seeded.current`) from the `open` list at mount. When
  `useCustomerInvoices` refetches (invalidateAfterPosting after the first split-tender payment, window focus,
  another cashier paying) and an invoice drops out of or enters the list or changes order, row i displays
  invoice X but submits `allocations[i].invoiceId` = the seeded Y. After "Record a different payment" the
  customer can tick the invoice they see and the money is sent against another invoice id (server caps usually
  reject it with a 400, but not when the stale invoice still has enough outstanding). Fix: key the allocation
  field array by invoiceId (look up the field by `invoiceId`, or re-derive `allocations` from `open` with
  `replace()` while preserving ticked/typed rows), and never trust an index across a refetch; add a component
  test that completes one invoice and then records a second payment.
- [HIGH] web/components/billing/InvoiceComposer.tsx:186-222 (saveDraft / issueNow) — for a new composition,
  `createdDraftId.current` is reused on retry with `version = draft?.version ?? 1` and with NO `setCharges`
  (the `draft && dirty` branch needs `draft`, which is undefined on the new-invoice route). If `createDraft`
  succeeds and `issue` then fails with a retryable error (network, 5xx, preview problem) and the user changes
  the selection, Issue (and Save draft) sends the OLD draft's charge set while the confirm dialog showed the
  total of the NEW selection: an irreversible invoice that differs from what was confirmed, and edits silently
  lost on Save draft. The `1` is also a guessed version. Fix: after creating the draft, always
  `getInvoice(id)` for the real version; when `createdDraftId` exists and the selection differs, call
  `setCharges` first (or navigate to the draft route immediately after create). Add a test: create, issue
  fails, change selection, issue again, assert the request carries the new charge ids.
- [MEDIUM] web/lib/hooks/billing/useBillingMutations.ts (useReversePayment) + e2e billing-real-api.spec.ts:203 —
  the payment read Reversed before its invoice returned to Issued, and the workaround is a Playwright
  `expect.poll` on the API only; the UI has no equivalent. `invalidateAfterPosting` fires once on success, so
  if the invoice refetch lands before the allocation release the customer panel / invoice detail stays at Paid
  until the next focus refetch. Judgement: the poll is acceptable inside the test, but it papers over a
  backend ordering gap (reversal marks the payment before releasing the allocation, i.e. not atomic); report
  it against HO-BIL-001 and, on the UI side, optionally refetch once more after a short delay. Not UI-fixable
  alone, so not blocking here.
- [MEDIUM] web/lib/money/wire.ts:42-55 — `moneyFromWire` takes a bare .NET decimal that `JSON.parse` has
  already turned into a float, then truncates (`slice(0,2)`) rather than rounds, and mishandles exponent forms
  (`1e-7`). It is "no arithmetic", but not decimal-safe for values with more than 2 decimals or very
  small/large values. Also `emit()` falls back to sending a string when `JSON.rawJSON` is missing: on a browser
  without it (older Safari/Firefox) EVERY money post is refused by the server, with only the generic server
  message shown. Fix: have the backend return money objects (the §20.4 disagreement), and feature-detect
  rawJSON once with a visible "browser not supported" message.
- [MEDIUM] tests — U-A2 (400 ms debounce, one request, previous totals kept, announced), U-A6 (draft 409
  reload), U-X2 (dirty composer/payment/credit/cash-up + navigate shows the confirm) and U-B3 have no vitest;
  U-X1 is only checked for ApproverMustDiffer and StatementMalformed (no assertion that errorCode is absent
  from the DOM on the other forms). U-A2 and U-X2 are real holes: they protect the preview debounce
  (`useDebouncedValue` + `isRecomputing`, which gates Issue) and the guard whose `dirty` logic differs per
  form, and HIGH finding 2 sits in exactly the untested composer retry path. U-A6/U-B3 are partly covered by
  e2e (Refund now, reverse dialog text) but not the 409 reload.
- [MEDIUM] web/components/billing/InvoiceComposer.tsx:113-117 — `useEffect(() => { if (draft) setSelected(draftChargeIds) })`
  resets the user's ticks whenever the draft prop's data changes (another user edits it, a loyalty change,
  any `['billing','invoice']` invalidation that returns a new version). Same class as the late-reset bug
  already fixed in RecordPaymentForm. Fix: only follow the server when the user has no unsaved selection, or
  tell them before overwriting.
- [LOW] web/components/billing/ExportPanel.tsx:31 — day-span arithmetic with `Date.parse` and `Math.round` in a
  component; dates should go through lib/format.
- [LOW] web/eslint.config.mjs — the money lint rule covers components/billing, lib/api/billing-*, lib/money but
  not lib/hooks/billing/** or mocks; add the hooks folder.
- [LOW] web/lib/hooks/billing/useBillingMutations.ts — bank match/unmatch/auto-match invalidate only
  `['billing','bank']`; a payment's reconciliation state shown on payment lists/detail stays stale. Also
  useCaptureSource/useRecordCashCount/useCloseCashUp call `invalidateAfterPosting` with no ids, which refetches
  every cached customer/invoice/payment query (broad, harmless at this scale).
- [LOW] web/components/billing/ReasonDialog.tsx:79-86 — Cancel calls `onOpenChange(false)` without `reset()`,
  so a typed reason survives a cancel and shows on the next open (e.g. a reversal reason for a different
  payment). Reset on close.

CONSISTENCY: uses the shared UnsavedChangesGuard, a submitWithServerErrors-style wrapper (`submitBillingForm`),
QueryBoundary/ErrorState idioms and `useDebouncedValue` like ACC-002/ADM-002/APT-002/SHP-002. Server codes map
through `errorCode` to area fallback text only, never rendered. `web/lib/api/errors.ts` appears as a new
157-line file in the diff against 0442f9ff (earlier-area content); confirm the merge base is not masking a
duplicate of the earlier file.

SCOPE CHECK
- files in allowed scope: 124/126 (web/** billing, mocks, tests, nav, eslint config, decisions)
- files outside allowed scope: .ai/decisions/adr.md (ADR-FE-079..086 recorded, per the handover commit),
  docs/test-cases/TC-HO-BIL-002.md (expected doc output)
