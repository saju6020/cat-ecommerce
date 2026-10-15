import { apiFetch } from './client';
import type { Money } from '@/lib/format/money';
import { wireMoney } from '@/lib/money';

/**
 * `lib/api/billing-cashup.ts` — HO-BIL-002 §8.2, §11.6. The daily till count, verified against
 * `StaffCashUpController` and `BillingCashUpViewModels`. Every total and the variance are the
 * server's (`CashUpCalculator`); this module and the screens never add or subtract an amount.
 */

export interface CashUpLine {
  id: string;
  kind: string;
  method: string | null;
  collector: string | null;
  refundMethod: string | null;
  amount: Money;
  countsInCash: boolean;
  carriedIn: boolean;
  label: string | null;
}

export interface CashUpCarriedIn {
  id: string;
  kind: string;
  label: string | null;
  amount: Money;
}

export interface CashUpExpected {
  cash: Money;
  cashCount: number;
  card: Money;
  cardCount: number;
  transfer: Money;
  transferCount: number;
  cashRefunds: Money;
  cashReversals: Money;
  carriedInTotal: Money;
  carriedIn: CashUpCarriedIn[];
  expectedCash: Money;
}

export interface CashUp {
  businessDate: string;
  status: 'Open' | 'Closed';
  version: number;
  expected: CashUpExpected;
  lines: CashUpLine[];
  counted: Money | null;
  variance: Money | null;
  explanation: string | null;
  pendingApprovals: number;
  countedBy: string | null;
  countedOn: string | null;
  closedBy: string | null;
  closedByName: string | null;
  closedOn: string | null;
}

const BASE = '/api/staff/billing/cash-ups';

export async function getCashUp(date: string): Promise<CashUp> {
  return apiFetch<CashUp>(`${BASE}/${encodeURIComponent(date)}`);
}

export async function recordCashCount(
  date: string,
  request: { countedCash: string; explanation?: string },
): Promise<CashUp> {
  return apiFetch<CashUp>(`${BASE}/${encodeURIComponent(date)}/count`, {
    method: 'PUT',
    body: {
      countedCash: wireMoney(request.countedCash),
      explanation: request.explanation || undefined,
    },
  });
}

export async function closeCashUp(date: string, expectedVersion: number): Promise<void> {
  await apiFetch<void>(`${BASE}/${encodeURIComponent(date)}/close`, {
    method: 'POST',
    body: { expectedVersion },
  });
}
