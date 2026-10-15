GATE: ARCHITECTURE
HANDOVER: HO-BIL-002
COMMITS: 81396c75 4beb916c da4c0972 a4e716b8 c08ce5af 130dec37 a3d87c77 6791f13f 8d04e00a 74802113 ed2bf958 cef89b69 f3c0039c c9aecb50 (base master 0442f9ff; 71bf4b1f is the CODE_REVIEW verdict file and was not reviewed)
DATE: 2026-10-08
STATUS: FAIL

FINDINGS
- [BLOCKER] .ai/decisions/adr.md:3661-3700 — §14 forbids `.ai/**`, and §6 says `.ai/` governs the backend only. Commit a3d87c77 appends ADR-FE-079…086 there as well as to `web/docs/decisions.md`, which is the place §9/§13 name. No earlier frontend handover has written an ADR-FE entry into `.ai/decisions/adr.md`, and §1 Amended records no authorisation. Fix: revert that hunk (+40/−0). The `web/docs/decisions.md` copy is correct and stays.
- [HIGH] web/lib/api/billing-staff.ts:471-487, web/components/billing/InvoiceDocumentView.tsx:109, CreditNoteScreens.tsx:69, InvoiceListScreen.tsx:64, PaymentListScreen.tsx:63 — the staff invoice PDF, credit-note PDF, invoice CSV and payment CSV are plain `<a href>` links. An anchor cannot send `X-Branch-Id`. The endpoints resolve the branch with `TryResolveBranch` (StaffInvoicesController.cs:148/181, StaffCreditNotesController.cs:91, and the payments CSV). `HttpContextActiveBranchAccessor` falls back to the caller's **primary** branch when the header is missing. So for staff working at a non-primary branch:
  - the PDFs return 404 for that branch's documents;
  - the CSVs **silently export the primary branch's rows** under the other branch's picker, which is wrong data with no error.

  This breaks the active-branch rule of HO-ADM-002 and HO-ADM-001 ADR-046, and §8.3's "every key includes the active branch where branch-scoped". It will spread if copied, because `cats.ts`, `admin-audit.ts` and `staff-shop.ts` already use the same plain-link shape.

  **The fix belongs in the frontend.** Add a GET-as-blob helper in `lib/api/billing-staff.ts` that sends `X-Branch-Id`, using the same `XMLHttpRequest` exception and error mapping as the statement upload (ADR-FE-150 precedent). Then save the file through `URL.createObjectURL` plus `download`, which `AgeingTable` already does.

  A backend fix (a `?branchId=` query parameter) would add a second branch-resolution channel to the cross-area accessor that HO-ADM-001 owns, and would put the branch into URLs and logs. That needs an owner decision and is not this handover's to make.

  **Not affected:**
  - The accounting-export ZIP link (ExportPanel.tsx:81). `StaffAccountingExportsController.Download` takes an explicit `branchId` query and does not call `TryResolveBranch`, so §11.9's plain link is correct as built. The developer's statement that the ZIP fails is inaccurate.
  - The portal PDF (`my-billing.ts:39`). Customers have no branch.
- [MEDIUM] web/components/billing/CustomerBillingSummary.tsx:27-29 — §11.1 says *Apply credit* shows "only when free credit > 0 and an invoice has an outstanding — both flags from the server". `CustomerBillingSummaryViewModel` has no such flags, and the client derives visibility with `moneySign` on two amounts. This is a sign test, not arithmetic, and the server still decides. But it was neither declared nor reported under §20.5. Backlog BIL: add `canApplyCredit` to the summary.
- [MEDIUM] web/components/billing/UnbilledChargesTable.tsx:67 — declared deviation: *Void* shows on every unbilled charge. ADR-FE-083 says actions come only from `allowedActions`, but `ChargeViewModel` (BillingViewModels.cs:29) carries none, so the frontend cannot comply. The server refuses where voiding is not allowed. Accepted as a contract gap. Backlog BIL: add `allowedActions` to `ChargeViewModel`.
- [MEDIUM] web/components/billing/InvoiceActions.tsx:51 — declared deviation: *Corrected re-issue* visibility is inferred from "a credit note exists and no corrected invoice does". `InvoiceActionRules.AllowedActions` (InvoiceActionRules.cs:24) never emits a re-issue action. The server re-checks. Same class of gap as above. Backlog BIL: add `correctedReissue` to the invoice `allowedActions`.
- [MEDIUM] web/lib/money/wire.ts:39-55 — declared deviation: `moneyFromWire`. Verified against the real controllers. These results serialise a bare `decimal`:
  - `IssuedCreditNoteResult.ReleasedToCustomerCredit`;
  - `AppliedCustomerCreditResult.Applied[].Amount`, `FreeCreditRemaining` and `InvoiceOutstanding` (StaffPaymentsController.cs:68-74);
  - `RecordedRemittanceResult.Unapplied`.

  Everything else is `MoneyViewModel`. The helper does no arithmetic. `String()` of a JSON number with at most 12 significant digits round-trips exactly, and the money lint rule does not forbid it. The approach is accepted.

  It is still a tolerant shim where §20.4 says "report; do not shim". The developer reports it went to the integrator. Backlog BIL (backend): return `BillingMoney.Of(...)` for those five fields, as Approve and Allocate already do, and then remove the helper.
- [LOW] web/nav/areas/billing.ts:14-17, web/components/admin/adminNavItems.ts:53-58 — §13 names `(staff)/layout.tsx`, `(portal)/layout.tsx` and `(staff)/staff/admin/layout.tsx` for the nav entries. The build edits the area registry and `adminNavItems` instead. That is correct under `web/README.md` "Adding a functional area" (the layouts are shared and must not be edited), and it follows the HO-NTF-002 and HO-SHP-002 precedent. Both edits are one entry each, additive (+4/−1 and +6/−0), and the layouts are untouched. However, HO-BIL-002 §1 Amended does not record the substitution, as the precedents did. Add the Amended row. This is a handover defect: §13 names the wrong files.
- [LOW] web/components/billing/RecordPaymentForm.tsx (collector) — declared deviation: the collector is the signed-in user only. §11.4 specifies a staff picker. This is a functional narrowing, not a layering issue, and it is referred to ACCEPTANCE (§18 A12).

ACCEPTED DEVIATIONS (no finding)
- **`JSON.rawJSON` for amounts** (`lib/money/wire.ts`). It is a JavaScript built-in, so it adds no dependency, and the amount never passes through a `number`. The fallback on old browsers sends the string, the server refuses it, and the form shows the message, so a wrong amount is never written. 24 call sites go through `wireMoney`, `wireQuantity` or `wireInteger`.
- **Statement upload by XHR** (`lib/api/billing-reconciliation.ts:183-236`). This follows the HO-FIL-002 ADR-FE-150 precedent. It lives in `lib/api` only, sends CSRF and `X-Branch-Id`, notifies session errors, and maps statuses to the same error classes as `apiFetch`. `client.ts` is untouched.
- **Missing and partial tests** (U-A2 and U-X2 untested, U-X1 partial). These are referred to the TEST gate. They are not architecture findings.

CHECKS (clean)
- **§8 layering.**
  - No `fetch(` or `axios` anywhere in the diff's `app/`, `components/` or `lib/`. The single `XMLHttpRequest` is the accepted upload.
  - No `useQuery`, `useMutation` or `useQueryClient` outside `lib/hooks/billing/**`.
  - Nothing outside `mocks/` and `tests/` imports `@/mocks`.
  - Every query key carries `branchId` where the endpoint is branch-scoped.
  - `invalidateAfterPosting` exists as the one helper (§8.3). Whether every mutation uses it is CODE_REVIEW's focus.
- **No client-side money arithmetic.**
  - The ESLint block (`eslint.config.mjs` +43/−0) covers `components/billing/**`, `lib/api/billing-*`, `lib/api/my-billing.ts` and `lib/money/**`.
  - It re-states the earlier `srcDoc` selectors, because flat config replaces the earlier block's options for these files. That earlier block is the only other `no-restricted-syntax` block, so nothing is lost.
  - `npx eslint` over the billing folders is green (exit 0).
  - Linting a probe from stdin that contains `Number(a)`, `parseFloat(a)` and `+a` returns 3 errors, so U-X3 holds.
  - A grep for `Math.`, `toFixed`, `reduce`, `BigInt` and amount comparisons in billing code finds none. The only reads are `moneySign` (the variance wording, the split-loop end, visibility hints) and `localeCompare` for table sorting.
- **Method UI from flags (ADR-FE-082).**
  - No `switch` on a method value.
  - `isOnline` is filtered out (RecordPaymentForm.tsx:52).
  - The reference and collector fields are driven by `requiresReference` and `requiresCollector`.
  - The only hard-coded values are `Cash` and `BankTransfer` in `RecordRemittanceForm`. These belong to the separate `RemittanceMethod` enumeration, which has no list endpoint, so this is not the payment-method list.
- **Overlays.** `AllocatePaymentDialog`, `ApplyCreditDialog`, `ManualChargeDialog`, `ReasonDialog` and `RefundDialog` import `@/components/ui/dialog`. Every confirm uses `components/common/ConfirmDialog`, which is built on `ui/dialog`. There is no hand-rolled `role="dialog"`, `aria-modal`, `fixed inset-0` overlay or portal, and no `window.confirm`.
- **ADR-FE-079…086 match §9 word for word.**
  - Every Decision and Why text in `web/docs/decisions.md` was extracted and diffed against the §9 table by script. All 16 texts are identical.
  - The `.ai/` copy is also identical, but it must go (BLOCKER).
  - ADR-FE-087…088 are unused, as specified.
- **MSW mocks (HO-ACC-002 §5.1).**
  - All 385 object keys in `mocks/handlers/billing/*.ts` were cross-checked against the Billing C# view models, commands and controllers. Every key that does not appear there is store-internal (`*Cents`, fixture ids, `issueKey`, `capturable`, `skipReason`) and never reaches a response body.
  - Spot-checked wire shapes match HO-BIL-001 §12.4 and the controllers. Examples: `uncaptured-sources`, and capture's `{ captured, skipped: [{ reason }] }`.
  - The mocks mirror the real API's bare decimals for the credit-note, apply-credit and remittance results, so the mock and real runs exercise the same parser.
- **Scoped edits to other areas' files.**
  - `customers/[customerId]/page.tsx`: +3/−0, `BillingSummaryPanel` only.
  - `lib/api/errors.ts`: append-only (no removed lines).
  - `mocks/handlers/index.ts`: +4/−0, the `billingAreaHandlers` alias, following the HO-ADM-002 and HO-NTF-002 pattern.
  - `(staff)/layout.tsx`, `(portal)/layout.tsx`, `admin/layout.tsx`, `components/ui/*`, `components/common/*`, `client.ts` and `types.ts`: untouched.
- **No new dependency.** `web/package.json`, `package-lock.json`, `tsconfig.json` and `next.config.ts` are unchanged.
- **No online-payment surface or card field**, by grep. SECURITY owns the depth review.
- `git merge-tree master HEAD` is clean.

BACKLOG (MEDIUM/LOW, owner rule 2026-09-27)
- **BIL backend:**
  - money objects for the 5 bare-decimal result fields;
  - `canApplyCredit` on the summary;
  - `allowedActions` on `ChargeViewModel`;
  - `correctedReissue` in `InvoiceActionRules`.
- **BIL frontend:** a collector staff picker.
- **HO-BIL-002 §1 Amended:** record the nav-registry substitution.
- **Cross-cutting (integrator):** staff plain-link downloads in `cats.ts`, `admin-audit.ts` and `staff-shop.ts` share the missing-`X-Branch-Id` gap. Check each against its endpoint's branch resolution.

SCOPE CHECK
- files in allowed scope: 123/126 (reviewed files; the CODE_REVIEW verdict file 71bf4b1f is excluded)
- files outside allowed scope:
  - `.ai/decisions/adr.md` — §14 forbidden. BLOCKER above.
  - `web/nav/areas/billing.ts` and `web/components/admin/adminNavItems.ts` are outside literal §13 but are the convention-mandated equivalents of §13's three layout nav lines (web/README.md; HO-NTF-002 and HO-SHP-002 precedent). They are accepted, pending the §1 Amended row (LOW).
