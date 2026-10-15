import { useMutation, useQueryClient } from '@tanstack/react-query';
import {
  allocatePayment,
  applyCustomerCredit,
  applyLoyaltyRedemption,
  approvePayment,
  captureSource,
  createCorrectedDraft,
  createDraftInvoice,
  discardDraftInvoice,
  issueCreditNote,
  issueInvoice,
  issueRefund,
  recordManualCharge,
  recordPayment,
  rejectPayment,
  releaseEarmark,
  removeLoyaltyRedemption,
  reversePayment,
  setCorrectedLines,
  setDraftCharges,
  voidCharge,
  type CorrectedLineInput,
  type IssueCreditNoteRequest,
  type IssueRefundRequest,
  type ManualChargeRequest,
  type RecordPaymentRequest,
} from '@/lib/api/billing-staff';
import { closeCashUp, recordCashCount } from '@/lib/api/billing-cashup';
import {
  autoMatchStatement,
  importBankStatement,
  matchBankLine,
  recordCodRemittance,
  unmatchBankLine,
  type ImportStatementRequest,
  type RecordRemittanceRequest,
} from '@/lib/api/billing-reconciliation';
import { setBillingProfile, type SetBillingProfileRequest } from '@/lib/api/billing-profile';
import { invalidateAfterPosting } from './invalidate';

/**
 * `lib/hooks/billing/useBillingMutations.ts` — HO-BIL-002 §8.3. Every Billing write. Each one calls
 * `invalidateAfterPosting` on success, so the customer's summary, the invoices touched, the payment,
 * the approvals queue and the day's cash-up are refetched together. The idempotency key (money-
 * moving forms) is passed in by the form, which owns its lifetime (`useIdempotencyKey`).
 */

export function useRecordManualCharge() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (request: ManualChargeRequest) => recordManualCharge(request),
    onSuccess: (_result, request) =>
      invalidateAfterPosting(queryClient, { customerId: request.customerId }),
  });
}

export function useVoidCharge(customerId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (input: { chargeId: string; reason: string }) =>
      voidCharge(input.chargeId, input.reason),
    onSuccess: () => invalidateAfterPosting(queryClient, { customerId }),
  });
}

export function useCaptureSource() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (input: { sourceKind: string; sourceId: string }) =>
      captureSource(input.sourceKind, input.sourceId),
    onSuccess: () => invalidateAfterPosting(queryClient),
  });
}

export function useCreateDraftInvoice() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (input: { customerId: string; chargeIds: string[]; notes?: string }) =>
      createDraftInvoice(input),
    onSuccess: (_result, input) =>
      invalidateAfterPosting(queryClient, { customerId: input.customerId }),
  });
}

export function useSetDraftCharges(invoiceId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (input: { expectedVersion: number; chargeIds: string[]; invoiceId?: string }) =>
      setDraftCharges(input.invoiceId ?? invoiceId, { expectedVersion: input.expectedVersion, chargeIds: input.chargeIds }),
    onSuccess: (invoice) =>
      invalidateAfterPosting(queryClient, {
        customerId: invoice.customerId,
        invoiceIds: [invoice.invoiceId],
      }),
  });
}

export function useDiscardDraftInvoice(invoiceId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: () => discardDraftInvoice(invoiceId),
    onSuccess: () => invalidateAfterPosting(queryClient, { invoiceIds: [invoiceId] }),
  });
}

export function useIssueInvoice(invoiceId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (input: { expectedVersion: number; idempotencyKey: string; invoiceId?: string }) =>
      issueInvoice(input.invoiceId ?? invoiceId, input.expectedVersion, input.idempotencyKey),
    onSuccess: (_result, input) =>
      invalidateAfterPosting(queryClient, { invoiceIds: [input.invoiceId ?? invoiceId] }),
  });
}

export function useApplyLoyalty(invoiceId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (points: string) => applyLoyaltyRedemption(invoiceId, points),
    onSuccess: ({ invoice }) =>
      invalidateAfterPosting(queryClient, {
        customerId: invoice.customerId,
        invoiceIds: [invoiceId],
      }),
  });
}

export function useRemoveLoyalty(invoiceId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: () => removeLoyaltyRedemption(invoiceId),
    onSuccess: ({ invoice }) =>
      invalidateAfterPosting(queryClient, {
        customerId: invoice.customerId,
        invoiceIds: [invoiceId],
      }),
  });
}

export function useRecordPayment() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (input: { request: RecordPaymentRequest; idempotencyKey: string }) =>
      recordPayment(input.request, input.idempotencyKey),
    onSuccess: (result, input) =>
      invalidateAfterPosting(queryClient, {
        customerId: input.request.customerId,
        invoiceIds: result.invoiceOutstanding.map((row) => row.invoiceId),
        paymentId: result.paymentId,
      }),
  });
}

export function useApprovePayment() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (paymentId: string) => approvePayment(paymentId),
    onSuccess: (result, paymentId) =>
      invalidateAfterPosting(queryClient, {
        invoiceIds: result.applied.map((row) => row.invoiceId),
        paymentId,
      }),
  });
}

export function useRejectPayment() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (input: { paymentId: string; reason: string }) =>
      rejectPayment(input.paymentId, input.reason),
    onSuccess: (_result, input) =>
      invalidateAfterPosting(queryClient, { paymentId: input.paymentId }),
  });
}

export function useAllocatePayment(paymentId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (allocations: Array<{ invoiceId: string; amount: string }>) =>
      allocatePayment(paymentId, allocations),
    onSuccess: (result) =>
      invalidateAfterPosting(queryClient, {
        invoiceIds: result.applied.map((row) => row.invoiceId),
        paymentId,
      }),
  });
}

export function useReleaseEarmark(paymentId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (reason: string) => releaseEarmark(paymentId, reason),
    onSuccess: () => invalidateAfterPosting(queryClient, { paymentId }),
  });
}

export function useReversePayment(paymentId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (reason: string) => reversePayment(paymentId, reason),
    onSuccess: () => invalidateAfterPosting(queryClient, { paymentId }),
  });
}

export function useApplyCustomerCredit(customerId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (input: { invoiceId: string; amount?: string }) =>
      applyCustomerCredit(customerId, input),
    onSuccess: (result) =>
      invalidateAfterPosting(queryClient, {
        customerId,
        invoiceIds: result.applied.map((row) => row.invoiceId),
      }),
  });
}

export function useIssueCreditNote(invoiceId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (input: { request: IssueCreditNoteRequest; idempotencyKey: string }) =>
      issueCreditNote(invoiceId, input.request, input.idempotencyKey),
    onSuccess: () => invalidateAfterPosting(queryClient, { invoiceIds: [invoiceId] }),
  });
}

export function useCreateCorrectedDraft(invoiceId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (creditNoteId: string) => createCorrectedDraft(invoiceId, creditNoteId),
    onSuccess: () => invalidateAfterPosting(queryClient, { invoiceIds: [invoiceId] }),
  });
}

export function useSetCorrectedLines(invoiceId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (input: { expectedVersion: number; lines: CorrectedLineInput[] }) =>
      setCorrectedLines(invoiceId, input.expectedVersion, input.lines),
    onSuccess: () => invalidateAfterPosting(queryClient, { invoiceIds: [invoiceId] }),
  });
}

export function useIssueRefund() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (input: { request: IssueRefundRequest; idempotencyKey: string }) =>
      issueRefund(input.request, input.idempotencyKey),
    onSuccess: (_result, input) =>
      invalidateAfterPosting(queryClient, { paymentId: input.request.paymentId }),
  });
}

export function useRecordCashCount(date: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (input: { countedCash: string; explanation?: string }) =>
      recordCashCount(date, input),
    onSuccess: () => invalidateAfterPosting(queryClient),
  });
}

export function useCloseCashUp(date: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (expectedVersion: number) => closeCashUp(date, expectedVersion),
    onSuccess: () => invalidateAfterPosting(queryClient),
  });
}

export function useRecordCodRemittance() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (request: RecordRemittanceRequest) => recordCodRemittance(request),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: ['billing', 'cod'] });
      invalidateAfterPosting(queryClient);
    },
  });
}

export function useImportBankStatement() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (request: ImportStatementRequest) => importBankStatement(request),
    onSuccess: () => void queryClient.invalidateQueries({ queryKey: ['billing', 'bank'] }),
  });
}

export function useAutoMatch(statementId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: () => autoMatchStatement(statementId),
    onSuccess: () => void queryClient.invalidateQueries({ queryKey: ['billing', 'bank'] }),
  });
}

export function useMatchBankLine(statementId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (input: { statementLineId: string; paymentId: string }) =>
      matchBankLine({ statementId, ...input }),
    onSuccess: () => void queryClient.invalidateQueries({ queryKey: ['billing', 'bank'] }),
  });
}

export function useUnmatchBankLine() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (input: { matchId: string; reason: string }) =>
      unmatchBankLine(input.matchId, input.reason),
    onSuccess: () => void queryClient.invalidateQueries({ queryKey: ['billing', 'bank'] }),
  });
}

export function useSetBillingProfile(branchId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (request: SetBillingProfileRequest) => setBillingProfile(branchId, request),
    onSuccess: () => void queryClient.invalidateQueries({ queryKey: ['billing', 'profile'] }),
  });
}
