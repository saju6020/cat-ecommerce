import { apiFetch } from './client';
import { query, xhrRequest } from './billing-staff';
import type { PagedResult } from './types';
import type { Money } from '@/lib/format/money';
import { moneyFromWire, wireMoney } from '@/lib/money';

/**
 * `lib/api/billing-reconciliation.ts` — HO-BIL-002 §8.2, §11.7–§11.8. COD collections and
 * remittances, bank statements and matching, verified against `StaffReconciliationController`
 * and `BillingReconciliationViewModels`. The statement upload is the one multipart request: the
 * file goes once, as `FormData`, and is never kept in the browser (NFR-SEC-5).
 */

const BASE = '/api/staff/billing';

// ---- COD ------------------------------------------------------------------------------------

export type CodStatus = 'unremitted' | 'shortfall' | 'remitted';

export interface CodCollection {
  paymentId: string;
  receiptNumber: string | null;
  customerId: string;
  customerName: string;
  courierCode: string | null;
  courierName: string | null;
  receivedOn: string;
  collected: Money;
  remitted: Money;
  outstanding: Money;
  shortfall: Money;
  status: string;
  overdue: boolean;
}

export interface CodCollectionsParams {
  courierCode?: string;
  status?: CodStatus | '';
  from?: string;
  to?: string;
  pageNumber?: number;
  pageSize?: number;
}

export async function getCodCollections(
  params: CodCollectionsParams,
): Promise<PagedResult<CodCollection>> {
  return apiFetch<PagedResult<CodCollection>>(`${BASE}/cod/collections${query({ ...params })}`);
}

export interface CodCourierReconciliation {
  courierCode: string;
  collected: Money;
  remitted: Money;
  shortfall: Money;
  unremitted: Money;
  unappliedRemittances: Money;
  shortfallLines: CodCollection[];
}

export async function getCodReconciliation(params: {
  courierCode?: string;
  from?: string;
  to?: string;
}): Promise<{ couriers: CodCourierReconciliation[] }> {
  return apiFetch<{ couriers: CodCourierReconciliation[] }>(
    `${BASE}/cod/reconciliation${query({ ...params })}`,
  );
}

export interface RecordRemittanceRequest {
  courierCode: string;
  receivedOn: string;
  amount: string;
  method: string;
  reference?: string;
  lines: Array<{ paymentId: string; amountRemitted: string }>;
}

export async function recordCodRemittance(
  request: RecordRemittanceRequest,
): Promise<{ remittanceId: string; unapplied: Money }> {
  const raw = await apiFetch<{ remittanceId: string; unapplied: unknown }>(`${BASE}/cod/remittances`, {
    method: 'POST',
    body: {
      courierCode: request.courierCode,
      receivedOn: request.receivedOn,
      amount: wireMoney(request.amount),
      method: request.method,
      reference: request.reference || undefined,
      lines: request.lines.map((line) => ({
        paymentId: line.paymentId,
        amountRemitted: wireMoney(line.amountRemitted),
      })),
    },
  });
  return { remittanceId: raw.remittanceId, unapplied: moneyFromWire(raw.unapplied) };
}

export interface CodRemittance {
  remittanceId: string;
  courierCode: string;
  receivedOn: string;
  amount: Money;
  method: string;
  reference: string | null;
  applied: Money;
  unapplied: Money;
  lineCount: number;
  recordedBy: string;
  recordedByName: string;
  recordedOn: string;
}

export async function getCodRemittances(params: {
  courierCode?: string;
  pageNumber?: number;
}): Promise<PagedResult<CodRemittance>> {
  return apiFetch<PagedResult<CodRemittance>>(`${BASE}/cod/remittances${query({ ...params })}`);
}

// ---- bank statements ------------------------------------------------------------------------

export interface BankStatementSummary {
  statementId: string;
  bankAccountLabel: string;
  periodFrom: string;
  periodTo: string;
  sourceFileName: string;
  lineCount: number;
  matchedCount: number;
  importedOn: string;
  importedBy: string;
  importedByName: string;
}

export async function getBankStatements(
  pageNumber: number | string = 1,
): Promise<PagedResult<BankStatementSummary>> {
  return apiFetch<PagedResult<BankStatementSummary>>(
    `${BASE}/bank-statements${query({ pageNumber })}`,
  );
}

export interface ImportStatementRequest {
  file: File;
  bankAccountLabel: string;
  periodFrom: string;
  periodTo: string;
}

export async function importBankStatement(
  request: ImportStatementRequest,
): Promise<{ statementId: string; lineCount: number }> {
  const form = new FormData();
  form.append('file', request.file, request.file.name);
  form.append('bankAccountLabel', request.bankAccountLabel);
  form.append('periodFrom', request.periodFrom);
  form.append('periodTo', request.periodTo);
  const { body } = await xhrRequest<{ statementId: string; lineCount: number }>(`${BASE}/bank-statements`, {
    method: 'POST',
    body: form,
    as: 'json',
  });
  return body;
}

export interface BankLine {
  lineId: string;
  lineNumber: number;
  transactionDate: string;
  amount: Money;
  direction: string;
  reference: string | null;
  description: string | null;
}

export interface UnmatchedPayment {
  paymentId: string;
  receiptNumber: string | null;
  customerId: string;
  customerName: string;
  amount: Money;
  receivedOn: string;
  reference: string | null;
}

export interface BankMatch {
  matchId: string;
  statementLineId: string;
  paymentId: string;
  receiptNumber: string | null;
  amount: Money;
  method: string;
  matchedOn: string;
  matchedBy: string;
  unmatchedOn: string | null;
  unmatchReason: string | null;
}

export interface BankStatementReconciliation {
  statement: BankStatementSummary;
  unmatchedStatementLines: BankLine[];
  unmatchedPayments: UnmatchedPayment[];
  matches: BankMatch[];
  debitLineCount: number;
}

export async function getBankReconciliation(
  statementId: string,
): Promise<BankStatementReconciliation> {
  return apiFetch<BankStatementReconciliation>(
    `${BASE}/bank-statements/${encodeURIComponent(statementId)}/reconciliation`,
  );
}

export async function autoMatchStatement(
  statementId: string,
): Promise<{ matched: number; ambiguous: number; none: number }> {
  return apiFetch<{ matched: number; ambiguous: number; none: number }>(
    `${BASE}/bank-statements/${encodeURIComponent(statementId)}/auto-match`,
    { method: 'POST' },
  );
}

export async function matchBankLine(request: {
  statementId: string;
  statementLineId: string;
  paymentId: string;
}): Promise<{ matchId: string }> {
  return apiFetch<{ matchId: string }>(`${BASE}/bank-matches`, { method: 'POST', body: request });
}

export async function unmatchBankLine(matchId: string, reason: string): Promise<void> {
  await apiFetch<void>(`${BASE}/bank-matches/${encodeURIComponent(matchId)}/unmatch`, {
    method: 'POST',
    body: { reason },
  });
}
