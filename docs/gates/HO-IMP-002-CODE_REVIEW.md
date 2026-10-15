GATE: CODE_REVIEW
HANDOVER: HO-IMP-002
COMMITS: 9d2346f0..1a1c139d
DATE: 2026-10-08
STATUS: PASS

Checks run: tsc --noEmit clean; eslint (components/{inventory,procurement,stock,recall}, lib, inventory app routes) clean;
vitest tests/components/{inventory,procurement,stock,recall}: 7 files, 78 tests pass. Playwright not run (out of scope for this gate).

Verified clean: no Number()/parseFloat/float maths on quantities or costs in components or api modules (U-X4 has its own
grep test, cross-cutting.test.tsx:368); every line/row editor (receipt, transfer, adjustment, PO, consignment costs/rates/HS codes)
binds by a stable row key and mutates by key, not array index; mutations invalidate the right families (post receipt covers
receipts/stock/batches/POs/consignments/reorder/ledger); only the alert badge polls; receipt draft is sessionStorage,
allow-listed, restore prompt suppresses autosave until answered, PO-line prefill is once-per-order and not re-applied after restore.

FINDINGS
- [MEDIUM] web/components/common/ScanField.tsx:55-71,84-90 — after a "No SKU has barcode X" result (or a lookup error) the value stays in the input. The next wedge scan appends to it, so the Enter handler's "burst === whole field" check fails and the scan is dropped with no announcement. At a delivery, one unknown barcode silently kills every following scan until the field is cleared by hand. A second scan arriving while the first lookup is pending is dropped the same way. Fix: on no-match/error select() the field text (so the next scan overwrites it) or clear it; announce when Enter is ignored. Add a "second scan after a miss" test; shared-pieces.test.tsx:105 covers only the first miss.
- [MEDIUM] web/components/inventory/GoodsReceiptForm.tsx:99-111 — autosave is a bare 400 ms debounce with no flush on unmount or pagehide. Edits made in the last 400 ms before a reload, tab close or in-app navigation are not in the draft, which is exactly the case ADR-FE-043 exists for. Fix: flush the pending save in the effect cleanup / a pagehide listener. U-C4 does not test this window.
- [MEDIUM] web/components/inventory/GoodsReceiptForm.tsx:93-96,142-152 — the restore prompt is offered on mount; lines scanned or added before the answer are overwritten by restore() (and autosave is paused meanwhile). Fix: merge, or disable the line inputs while the prompt is open.
- [MEDIUM] web/components/inventory/GoodsReceiptForm.tsx:331-340 (also AdjustmentForm.tsx:205-216, TransferForm.tsx:157-168) — server/validation errors are held as errors.lines[index] from the submit and rendered by the row's CURRENT index. Removing or inserting a row (or the PO-line prefill, which prepends) before fixing re-attaches messages to the wrong row. Row identity is keyed correctly; the error map is not. Fix: key the distributed errors by row key at distribution time, or clear line errors when the line set changes.
- [MEDIUM] web/components/procurement/SupplierForm.tsx:94-96 — useEffect reset(defaultsFor(initial)) on [initial] wipes in-progress input if the supplier refetch returns changed data (another user's edit, window-focus refetch). Same class as the earlier BIL-002/IMP-002 reset bug. Fix: reset only when the id/version changes AND the form is not dirty, or surface "changed elsewhere" instead.
- [LOW] web/app/(staff)/staff/inventory/ledger/page.tsx:25-27,42 — hand-rolled date arithmetic with new Date(...)/DAY_MS in a page; the gate table says time goes only through lib/format. Move shiftDate/day-count into lib/format/time.
- [LOW] web/lib/format/inventory.ts:35 — formatUnitCost uses Number(cost.amount) for Intl display. Display-only and follows the formatMoney precedent, and it lies outside the U-X4 grep scope, but it is the one float conversion of a money string in the slice; consider formatting the string directly (4 dp) to keep U-X4 absolute.
- [LOW] web/tests/e2e/inventory-flows.spec.ts:27-52 (E-C1) — depends on a fixture lot that "expires today" in Dhaka time and on the mock store surviving page.goto reloads; a run crossing Dhaka midnight changes the expected first batch. Not run here. Pin the fixture date or assert on the relative order only.

SCOPE CHECK
- files in allowed scope: 166/166 (web/ only; no backend, migration or .ai/ files in the diff)
- files outside allowed scope: none
