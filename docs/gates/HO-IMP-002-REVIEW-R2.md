GATE: CODE_REVIEW (combined follow-up: architecture + security + code, round 2)
HANDOVER: HO-IMP-002
COMMITS: bb7e3faf fcb1e21c 91153b9f 824d756d 54ee1ceb (delta ceb0137e..54ee1ceb; master's own files from merge 0b0ce849 excluded)
DATE: 2026-10-09
STATUS: PASS

Run here: `npx tsc --noEmit` clean; vitest `tests/components/inventory/gate-fixes.test.tsx` 7/7 pass. Not run: Playwright, real API.

ROUND-1 HIGH: CLOSED
- web/lib/api/inventory-download.ts:42-47 and web/components/inventory/ExportButton.tsx:26-36. Every branch-scoped CSV (ledger, near-expiry, stock-on-hand) now goes through `ExportButton` -> `downloadInventoryFile` -> XHR that sends `X-Branch-Id` (getActiveBranchId) and `X-CSRF-Token` (`pc_csrf`, same cookie as client.ts:248), with credentials. Callers: LedgerSearch:62, NearExpiryReport:42, StockOnHandReport:32. The only remaining plain link is the recall trace (`ExportLink`, RecallTraceTable:128), which is keyed by batch and not branch-scoped, as the round-1 gate ruled. `useExportBranchNote` is deleted.
- Error handling: 400/413/401/403/404/409/429/other map onto the same error classes as `apiFetch`; the button shows `describeInventoryError` in a `role=alert` and re-enables. Blob handling: the file is the server's, saved via `createObjectURL` + `download` anchor, anchor removed, URL revoked after 10 s.
- Test: `gate-fixes.test.tsx` "requests the file with X-Branch-Id and the CSRF token" asserts both headers on the XHR (branch mocked to `branch-two`), the saved blob and the revoke; it fails against the round-1 anchor (no XHR sent). The 403 path is tested too.

FINDINGS (new, none HIGH/BLOCKER)
- [LOW] web/lib/api/inventory-download.ts:77 — 429 is mapped to a fixed 60 s; `Retry-After` is not read. Backlog 57.
- [LOW] gate-fixes.test.tsx tests `ExportButton` in isolation; nothing pins that the three report pages pass their paths to it (a revert to an anchor in a page would be caught only by the component tests). Backlog 57.
- [LOW] web/tests/e2e/inventory-real-api.spec.ts:67 — R-D2 skipped on a stale awaiting-approval count; 11 of 12 pass. Backlog 58.

Other delta checks (all fine): line errors are keyed by row (`stampRowKeys`/`errorsForRow`, unit-tested, applied in GoodsReceiptForm; fallback to index when unstamped); SupplierForm re-seeds only for a different id or an untouched form (3 tests); autosave flush on `pagehide`/unmount reads latest state via ref and skips when a restore is pending, not dirty or empty (post clears the draft and dirty before unmount); restore keeps lines added during the prompt; ScanField selects the text after a miss or error and announces a typed Enter or a scan during a lookup; `encodeURIComponent` applied to every id path segment in the inventory/procurement api modules (grep finds no unencoded one); currency uses `FALLBACK_CURRENCY_CODE` everywhere in the slice. No `.ai/**`, `src/` or backend test file was touched by the five fix commits (only `web/**` and `docs/test-cases/TC-HO-IMP-002.md`); the `.ai/` and `src/` files in the raw range diff are master's merge.

CLASSIFICATION OF THE OWED ITEMS
- react-hook-form deviation ADR still not written (web/docs/decisions.md has none): MEDIUM, backlog 48. The properties §8.3 protects hold; does not block.
- HO-IMP-002 §1 Amended lacks the nav-registry row: LOW (documentation), backlog 50.

ROUND-1 MEDIUM/LOW STATUS
- Closed: all five CODE_REVIEW mediums (ScanField miss, autosave flush, restore overwrite, error index, SupplierForm reset); ARCH currency fallback; SECURITY L2 (encoded ids) and L3 (CSV).
- Open (now backlog rows in docs/backlog/IMP.md 46-58): ARCH recall notify count (47), RHF ADR (48), components fetching data (49), nav row (50), count sheet on `status` (51), ClearanceDocumentsMissing (52); SECURITY L1 role mirror (53); CODE ledger date arithmetic (54), formatUnitCost Number (55), E-C1 fixture date (56).
- New backend row 46: `StockCountDetailViewModel.AllowedActions` lists `approve` for the counter though the server returns 400 on self-approval.

SCOPE CHECK
- files in allowed scope: all delta files (web/**, docs/test-cases/TC-HO-IMP-002.md)
- files outside allowed scope: none
