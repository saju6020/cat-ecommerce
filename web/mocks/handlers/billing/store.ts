import type { Money } from '@/lib/format/money';
import { addDaysIso } from '@/lib/format/appointment';
import { todayInZone } from '@/lib/format/time';
import { looksLikeCardNumber } from '@/lib/validation/cardNumberGuard';
import { clearPersisted, loadPersisted, savePersisted } from '../admin/fixtures';
import { billingIds as ids } from './fixtures';

/**
 * `mocks/handlers/billing/store.ts` — HO-BIL-002 §12. One in-memory (sessionStorage-persisted, like
 * the other mock stores) Billing database, plus the *mock server's* rules: tax, totals, caps,
 * statuses, the approval threshold, idempotency. Amounts are integer minor units here and ONLY here;
 * every response renders them as the API does (`{ amount: "1500.00", currencyCode: "BDT" }`). The
 * real UI never computes any of this (ADR-FE-080, HO-BIL-002 §20.2).
 */

const PERSIST_KEY = '__purrcare_billing_mock_db__';
export const ZONE = 'Asia/Dhaka';
export const CURRENCY = 'BDT';
export const APPROVAL_THRESHOLD_CENTS = 5_000_000; // BDT 50,000.00
const FY = '2026-27';
const FY_COMPACT = '2627';

export type Cents = number;

export function money(cents: Cents): Money {
  const sign = cents < 0 ? '-' : '';
  const abs = Math.abs(cents);
  const whole = Math.trunc(abs / 100);
  const fraction = String(abs % 100).padStart(2, '0');
  return { amount: `${sign}${whole}.${fraction}`, currencyCode: CURRENCY };
}

export function qtyText(milli: number): string {
  return `${Math.trunc(milli / 1000)}.${String(milli % 1000).padStart(3, '0')}`;
}

/** A request amount (a JSON number from `JSON.rawJSON`, or a string) → minor units. */
export function toCents(value: unknown): Cents {
  const n = typeof value === 'number' ? value : Number.parseFloat(String(value ?? '0'));
  return Number.isFinite(n) ? Math.round(n * 100) : 0;
}

export function toMilli(value: unknown): number {
  const n = typeof value === 'number' ? value : Number.parseFloat(String(value ?? '0'));
  return Number.isFinite(n) ? Math.round(n * 1000) : 0;
}

export function today(): string {
  return todayInZone(ZONE);
}

export function formatCents(cents: Cents): string {
  const { amount } = money(cents);
  const [whole = '0', fraction = '00'] = amount.split('.');
  return `${CURRENCY} ${whole.replace(/\B(?=(\d{3})+(?!\d))/g, ',')}.${fraction}`;
}

export function receiptText(seq: number | null): string | null {
  return seq === null ? null : `R-${String(seq).padStart(6, '0')}`;
}

// ---- database types -------------------------------------------------------------------------

export interface DbCharge {
  id: string;
  customerId: string;
  branchId: string;
  businessLine: 'Clinic' | 'Shop' | 'Boarding';
  itemKind: string;
  sourceKind: string;
  sourceId: string;
  description: string;
  qtyMilli: number;
  unitCents: Cents;
  discountCents: Cents;
  taxClassId: string;
  serviceDate: string;
  status: 'Active' | 'Voided';
  recordedOn: string;
  voidReason: string | null;
  /** Mock-only trigger: saving a draft that holds this charge answers 409 `ChargeAlreadyInvoiced`. */
  racy?: boolean;
  /** Set once a racy save was refused: the charge now reads as billed on another invoice. */
  taken?: boolean;
}

export interface DbLine {
  lineId: string;
  chargeId: string | null;
  businessLine: 'Clinic' | 'Shop' | 'Boarding';
  itemKind: string;
  sourceKind: string;
  sourceId: string;
  description: string;
  qtyMilli: number;
  unitCents: Cents;
  discountCents: Cents;
  shareCents: Cents;
  taxClassId: string;
  /** Frozen at issue; `null` on a draft. */
  taxRatePercent: string | null;
  sortOrder: number;
}

export interface DbInvoice {
  id: string;
  number: string | null;
  status: 'Draft' | 'Issued' | 'PartPaid' | 'Paid' | 'Credited' | 'Discarded';
  version: number;
  branchId: string;
  customerId: string;
  issuedOn: string | null;
  issueDate: string | null;
  dueOn: string | null;
  lines: DbLine[];
  loyaltyPoints: number;
  paidCents: Cents;
  creditedCents: Cents;
  notes: string | null;
  correctsInvoiceId: string | null;
  correctsCreditNoteId: string | null;
  correctedByInvoiceId: string | null;
  issueKey: string | null;
}

export interface DbEntry {
  entryId: string;
  kind: 'Allocate' | 'Release';
  invoiceId: string;
  cents: Cents;
  occurredOn: string;
  by: string;
  releaseReason: string | null;
}

export interface DbPayment {
  id: string;
  receiptSeq: number | null;
  customerId: string;
  branchId: string;
  method: string;
  status: 'PendingApproval' | 'Accepted' | 'Rejected' | 'Reversed';
  amountCents: Cents;
  unallocatedCents: Cents;
  receivedOn: string;
  recordedOn: string;
  recordedBy: string;
  recordedByName: string;
  reference: string | null;
  notes: string | null;
  collector: {
    kind: string;
    staffAccountId: string | null;
    staffName: string | null;
    courierCode: string | null;
    courierName: string | null;
  } | null;
  earmark: {
    kind: string;
    sourceId: string;
    label: string;
    isReleased: boolean;
    releasedOn: string | null;
    releaseReason: string | null;
  } | null;
  entries: DbEntry[];
  refunds: Array<{ refundId: string; method: string; cents: Cents; businessDate: string; creditNoteId: string | null }>;
  requested: Array<{ invoiceId: string; cents: Cents }>;
  businessDate: string | null;
  acceptedOn: string | null;
  approvedBy: string | null;
  approvedByName: string | null;
  approvedOn: string | null;
  rejectedBy: string | null;
  rejectedByName: string | null;
  rejectedOn: string | null;
  rejectionReason: string | null;
  reversedBy: string | null;
  reversedByName: string | null;
  reversedOn: string | null;
  reversalReason: string | null;
  version: number;
  /** COD: how much the courier has handed over so far. */
  remittedCents: Cents;
}

export interface DbCreditNote {
  id: string;
  number: string;
  kind: string;
  reason: string;
  invoiceId: string;
  customerId: string;
  branchId: string;
  lines: Array<{
    invoiceLineId: string;
    description: string;
    quantity: string | null;
    requestedCents: Cents | null;
    netCents: Cents;
    taxRatePercent: string;
    taxCents: Cents;
    grossCents: Cents;
  }>;
  netCents: Cents;
  taxCents: Cents;
  totalCents: Cents;
  releasedCents: Cents;
  issuedOn: string;
  issueDate: string;
  correctedByInvoiceId: string | null;
}

export interface DbRefund {
  refundId: string;
  paymentId: string;
  creditNoteId: string | null;
  method: string;
  cents: Cents;
  reference: string | null;
  reason: string;
  businessDate: string;
  issuedOn: string;
  issuedBy: string;
  issuedByName: string;
}

export interface DbCashUp {
  date: string;
  status: 'Open' | 'Closed';
  version: number;
  countedCents: Cents | null;
  explanation: string | null;
  countedOn: string | null;
  closedByName: string | null;
  closedOn: string | null;
}

export interface DbStatementLine {
  lineId: string;
  lineNumber: number;
  transactionDate: string;
  cents: Cents;
  direction: 'Credit' | 'Debit';
  reference: string | null;
  description: string | null;
  matchId: string | null;
}

export interface DbStatement {
  statementId: string;
  bankAccountLabel: string;
  periodFrom: string;
  periodTo: string;
  sourceFileName: string;
  sha: string;
  importedOn: string;
  lines: DbStatementLine[];
}

export interface DbMatch {
  matchId: string;
  statementId: string;
  statementLineId: string;
  paymentId: string;
  method: 'Auto' | 'Manual';
  matchedOn: string;
  unmatchedOn: string | null;
  unmatchReason: string | null;
}

export interface DbProfile {
  branchId: string;
  legalName: string;
  taxRegistrationNumber: string;
  addressLines: string[];
  phone: string | null;
  email: string | null;
  invoiceFooter: string | null;
  bankAccounts: Array<{
    bankName: string;
    accountName: string;
    accountNumber: string;
    branchName: string | null;
    routingNumber: string | null;
  }>;
  version: number;
}

export interface DbRemittance {
  remittanceId: string;
  courierCode: string;
  receivedOn: string;
  cents: Cents;
  method: string;
  reference: string | null;
  appliedCents: Cents;
  unappliedCents: Cents;
  lineCount: number;
  recordedByName: string;
  recordedOn: string;
}

export interface DbExportRun {
  runId: string;
  from: string;
  to: string;
  branchId: string | null;
  counts: [number, number, number, number, number, number];
  sha256: string;
  runByName: string;
  runOn: string;
}

export interface Db {
  charges: DbCharge[];
  invoices: DbInvoice[];
  payments: DbPayment[];
  creditNotes: DbCreditNote[];
  refunds: DbRefund[];
  cashUps: DbCashUp[];
  carriedIn: Array<{ date: string; id: string; kind: string; label: string; cents: Cents }>;
  statements: DbStatement[];
  matches: DbMatch[];
  profiles: DbProfile[];
  remittances: DbRemittance[];
  exportRuns: DbExportRun[];
  loyaltyBalance: number;
  loyaltyEntries: Array<{
    entryId: string;
    kind: string;
    points: number;
    invoiceId: string | null;
    invoiceNumber: string | null;
    occurredOn: string;
  }>;
  loyaltyEnabled: boolean;
  /** Completed appointments with no charge yet (`GET …/uncaptured-sources`). `skipReason` is a mock trigger: capture answers it as skipped. */
  capturable: Array<{
    sourceKind: string;
    sourceId: string;
    customerId: string;
    completedOn: string;
    label: string;
    skipReason?: string;
  }>;
  seq: { invoice: number; creditNote: number; receipt: number };
  /** `Idempotency-Key` → the original (status-free) body, per money-moving endpoint. */
  idempotency: Record<string, unknown>;
}

// ---- reference data -------------------------------------------------------------------------

export function rateOf(taxClassId: string): { code: string; rate: string } | null {
  if (taxClassId === ids.taxStd) return { code: 'STD', rate: '15.00' };
  return null;
}

export const SERVICES: Array<{ id: string; name: string; priceCents: Cents; taxClassId: string }> = [];

function rateBasisPoints(rate: string): number {
  const [whole = '0', fraction = ''] = rate.split('.');
  return Number.parseInt(whole, 10) * 100 + Number.parseInt(fraction.padEnd(2, '0').slice(0, 2), 10);
}

export function branchZone(branchId: string): string {
  return branchId === ids.londonBranchId ? 'Europe/London' : ZONE;
}

export function customerName(customerId: string): string {
  if (customerId === ids.customerId) return ids.customerName;
  if (customerId === ids.otherCustomerId) return ids.otherCustomerName;
  return 'Customer';
}

// ---- line and invoice arithmetic ------------------------------------------------------------

export interface LineAmounts {
  netCents: Cents;
  taxCents: Cents;
  grossCents: Cents;
  rateText: string | null;
  code: string | null;
}

export function lineAmounts(line: DbLine, frozen: boolean): LineAmounts {
  const gross0 = Math.round((line.qtyMilli * line.unitCents) / 1000);
  const netCents = gross0 - line.discountCents - line.shareCents;
  const live = rateOf(line.taxClassId);
  const rateText = frozen ? line.taxRatePercent : (live?.rate ?? null);
  const taxCents = rateText === null ? 0 : Math.round((netCents * rateBasisPoints(rateText)) / 10_000);
  return { netCents, taxCents, grossCents: netCents + taxCents, rateText, code: live?.code ?? null };
}

export function totalsOf(lines: DbLine[], frozen: boolean) {
  let net = 0;
  let tax = 0;
  let discount = 0;
  for (const line of lines) {
    const a = lineAmounts(line, frozen);
    net += a.netCents;
    tax += a.taxCents;
    discount += line.discountCents + line.shareCents;
  }
  return { net, tax, discount, total: net + tax };
}

export function invoiceTotal(invoice: DbInvoice): Cents {
  return totalsOf(invoice.lines, invoice.status !== 'Draft').total;
}

export function outstandingOf(invoice: DbInvoice): Cents {
  if (invoice.status === 'Draft' || invoice.status === 'Discarded') return 0;
  return invoiceTotal(invoice) - invoice.creditedCents - invoice.paidCents;
}

function derive(invoice: DbInvoice): void {
  if (invoice.status === 'Draft' || invoice.status === 'Discarded') return;
  const total = invoiceTotal(invoice);
  const outstanding = total - invoice.creditedCents - invoice.paidCents;
  if (invoice.creditedCents === total) invoice.status = 'Credited';
  else if (outstanding === 0 && invoice.paidCents > 0) invoice.status = 'Paid';
  else if (invoice.paidCents > 0) invoice.status = 'PartPaid';
  else invoice.status = 'Issued';
}

// ---- seed -----------------------------------------------------------------------------------

let n = 0;
function uid(prefix: string): string {
  n += 1;
  return `${prefix}-0000-0000-0000-${String(1000 + n).padStart(12, '0')}`.replace(/^(.{8})/, (m) => m.slice(0, 8));
}

function line(
  partial: Partial<DbLine> & Pick<DbLine, 'description' | 'unitCents'>,
  sortOrder: number,
): DbLine {
  return {
    lineId: uid('11111111'),
    chargeId: null,
    businessLine: 'Clinic',
    itemKind: 'Service',
    sourceKind: 'Manual',
    sourceId: uid('22222222'),
    qtyMilli: 1000,
    discountCents: 0,
    shareCents: 0,
    taxClassId: ids.taxStd,
    taxRatePercent: '15.00',
    sortOrder,
    ...partial,
  };
}

function charge(partial: Partial<DbCharge> & Pick<DbCharge, 'id' | 'description' | 'unitCents'>): DbCharge {
  return {
    customerId: ids.customerId,
    branchId: ids.mainBranchId,
    businessLine: 'Clinic',
    itemKind: 'Service',
    sourceKind: 'Appointment',
    sourceId: uid('33333333'),
    qtyMilli: 1000,
    discountCents: 0,
    taxClassId: ids.taxStd,
    serviceDate: today(),
    status: 'Active',
    recordedOn: new Date().toISOString(),
    voidReason: null,
    ...partial,
  };
}

function blankPayment(partial: Partial<DbPayment> & Pick<DbPayment, 'id' | 'amountCents' | 'method'>): DbPayment {
  return {
    receiptSeq: null,
    customerId: ids.customerId,
    branchId: ids.mainBranchId,
    status: 'Accepted',
    unallocatedCents: 0,
    receivedOn: today(),
    recordedOn: new Date().toISOString(),
    recordedBy: ids.meId,
    recordedByName: ids.meName,
    reference: null,
    notes: null,
    collector: null,
    earmark: null,
    entries: [],
    refunds: [],
    requested: [],
    businessDate: today(),
    acceptedOn: new Date().toISOString(),
    approvedBy: null,
    approvedByName: null,
    approvedOn: null,
    rejectedBy: null,
    rejectedByName: null,
    rejectedOn: null,
    rejectionReason: null,
    reversedBy: null,
    reversedByName: null,
    reversedOn: null,
    reversalReason: null,
    version: 1,
    remittedCents: 0,
    ...partial,
  };
}

function entry(invoiceId: string, cents: Cents): DbEntry {
  return {
    entryId: uid('44444444'),
    kind: 'Allocate',
    invoiceId,
    cents,
    occurredOn: new Date().toISOString(),
    by: ids.meId,
    releaseReason: null,
  };
}

function seed(): Db {
  n = 0;
  const day = today();
  const issued = (
    id: string,
    seq: number,
    lines: DbLine[],
    extra: Partial<DbInvoice> = {},
  ): DbInvoice => ({
    id,
    number: `INV-MAIN-${FY_COMPACT}-${String(seq).padStart(6, '0')}`,
    status: 'Issued',
    version: 1,
    branchId: ids.mainBranchId,
    customerId: ids.customerId,
    issuedOn: new Date().toISOString(),
    issueDate: addDaysIso(day, -25),
    dueOn: addDaysIso(day, 14),
    lines,
    loyaltyPoints: 0,
    paidCents: 0,
    creditedCents: 0,
    notes: null,
    correctsInvoiceId: null,
    correctsCreditNoteId: null,
    correctedByInvoiceId: null,
    issueKey: null,
    ...extra,
  });

  const charges: DbCharge[] = [
    charge({ id: ids.chargeConsultId, description: 'Consultation — Momo', unitCents: 80_000 }),
    charge({ id: ids.chargeVaccineId, description: 'Vaccination (FVRCP) — Momo', unitCents: 120_000 }),
    charge({
      id: ids.chargeFoodId,
      description: 'Royal Canin Kitten 2 kg',
      unitCents: 145_000,
      qtyMilli: 2000,
      businessLine: 'Shop',
      itemKind: 'Product',
      sourceKind: 'Order',
    }),
    charge({
      id: ids.chargeBoardingId,
      description: 'Boarding — 3 nights, Momo',
      unitCents: 90_000,
      qtyMilli: 3000,
      businessLine: 'Boarding',
      itemKind: 'Service',
      sourceKind: 'BoardingStay',
    }),
    charge({ id: ids.chargeRacyId, description: 'Nail trim', unitCents: 15_000, racy: true }),
    charge({
      id: ids.chargeNoRateId,
      description: 'Special handling fee',
      unitCents: 10_000,
      taxClassId: ids.taxNoRate,
      itemKind: 'Fee',
      sourceKind: 'Manual',
    }),
    charge({ id: ids.chargeDentalId, description: 'Dental check — Momo', unitCents: 60_000 }),
  ];

  const draft: DbInvoice = {
    id: ids.draftInvoiceId,
    number: null,
    status: 'Draft',
    version: 3,
    branchId: ids.mainBranchId,
    customerId: ids.customerId,
    issuedOn: null,
    issueDate: null,
    dueOn: null,
    lines: [
      line(
        {
          chargeId: ids.chargeDentalId,
          description: 'Dental check — Momo',
          unitCents: 60_000,
          taxRatePercent: null,
        },
        1,
      ),
    ],
    loyaltyPoints: 0,
    paidCents: 0,
    creditedCents: 0,
    notes: null,
    correctsInvoiceId: null,
    correctsCreditNoteId: null,
    correctedByInvoiceId: null,
    issueKey: null,
  };

  const i1 = issued(ids.issuedInvoiceId, 101, [line({ description: 'Consultation — Momo', unitCents: 80_000 }, 1)]);
  const i2 = issued(
    ids.partPaidInvoiceId,
    102,
    [
      line(
        {
          description: 'Royal Canin Kitten 2 kg',
          unitCents: 145_000,
          qtyMilli: 2000,
          businessLine: 'Shop',
          itemKind: 'Product',
          sourceKind: 'Order',
        },
        1,
      ),
    ],
    { issueDate: addDaysIso(day, -40), dueOn: addDaysIso(day, -20), paidCents: 100_000, status: 'PartPaid' },
  );
  const i3 = issued(
    ids.paidInvoiceId,
    103,
    [line({ description: 'Vaccination (FVRCP) — Momo', unitCents: 100_000 }, 1)],
    { paidCents: 115_000, status: 'Paid', issueDate: addDaysIso(day, -10) },
  );
  const i4 = issued(
    ids.creditedInvoiceId,
    104,
    [line({ description: 'Grooming — Momo', unitCents: 50_000 }, 1)],
    { creditedCents: 57_500, status: 'Credited', issueDate: addDaysIso(day, -8), correctedByInvoiceId: ids.correctedInvoiceId },
  );
  const i5 = issued(
    ids.correctedInvoiceId,
    105,
    [line({ description: 'Grooming (corrected) — Momo', unitCents: 45_000 }, 1)],
    { issueDate: addDaysIso(day, -7), correctsInvoiceId: ids.creditedInvoiceId, correctsCreditNoteId: ids.creditNoteId },
  );
  const otherLines = (sd: number, due: number, seq: number, id?: string): DbInvoice =>
    issued(
      id ?? uid('d2000000'),
      seq,
      [line({ description: `Treatment ${seq}`, unitCents: 100_000 }, 1)],
      { customerId: ids.otherCustomerId, issueDate: addDaysIso(day, sd), dueOn: addDaysIso(day, due) },
    );
  const others = [
    otherLines(-5, 9, 201, ids.otherCustomerInvoiceId),
    otherLines(-20, -10, 202),
    otherLines(-60, -45, 203),
    otherLines(-90, -75, 204),
    otherLines(-140, -120, 205),
  ];

  const cn: DbCreditNote = {
    id: ids.creditNoteId,
    number: `CN-MAIN-${FY_COMPACT}-000101`,
    kind: 'Correction',
    reason: 'Wrong service billed — grooming was not performed.',
    invoiceId: ids.creditedInvoiceId,
    customerId: ids.customerId,
    branchId: ids.mainBranchId,
    lines: [
      {
        invoiceLineId: i4.lines[0]!.lineId,
        description: 'Grooming — Momo',
        quantity: '1.000',
        requestedCents: null,
        netCents: 50_000,
        taxRatePercent: '15.00',
        taxCents: 7_500,
        grossCents: 57_500,
      },
    ],
    netCents: 50_000,
    taxCents: 7_500,
    totalCents: 57_500,
    releasedCents: 0,
    issuedOn: new Date().toISOString(),
    issueDate: addDaysIso(day, -7),
    correctedByInvoiceId: ids.correctedInvoiceId,
  };

  const payments: DbPayment[] = [
    blankPayment({
      id: ids.cashPaymentId,
      receiptSeq: 101,
      method: 'CashInStore',
      amountCents: 115_000,
      entries: [entry(ids.paidInvoiceId, 115_000)],
    }),
    blankPayment({
      id: ids.partPaymentId,
      receiptSeq: 102,
      method: 'CashInStore',
      amountCents: 100_000,
      entries: [entry(ids.partPaidInvoiceId, 100_000)],
    }),
    blankPayment({
      id: ids.freeCreditPaymentId,
      receiptSeq: 103,
      method: 'CardInStore',
      amountCents: 75_000,
      unallocatedCents: 75_000,
      reference: 'SLIP-48213',
    }),
    blankPayment({
      id: ids.depositPaymentId,
      receiptSeq: 104,
      method: 'BankTransfer',
      amountCents: 200_000,
      unallocatedCents: 200_000,
      reference: 'TRF-DEP-77',
      receivedOn: addDaysIso(day, -2),
      businessDate: addDaysIso(day, -2),
      earmark: {
        kind: 'BoardingStay',
        sourceId: uid('55555555'),
        label: 'Boarding deposit — Momo',
        isReleased: false,
        releasedOn: null,
        releaseReason: null,
      },
    }),
    blankPayment({
      id: ids.pendingOtherPaymentId,
      method: 'BankTransfer',
      status: 'PendingApproval',
      amountCents: 6_000_000,
      reference: 'TRF-BIG-9921',
      recordedBy: ids.otherStaffId,
      recordedByName: ids.otherStaffName,
      requested: [{ invoiceId: ids.issuedInvoiceId, cents: 92_000 }],
      businessDate: null,
      acceptedOn: null,
    }),
    blankPayment({
      id: ids.pendingOwnPaymentId,
      method: 'CashInStore',
      status: 'PendingApproval',
      amountCents: 5_500_000,
      requested: [],
      businessDate: null,
      acceptedOn: null,
    }),
    blankPayment({
      id: ids.reversedPaymentId,
      receiptSeq: 99,
      method: 'CashInStore',
      status: 'Reversed',
      amountCents: 50_000,
      receivedOn: addDaysIso(day, -1),
      businessDate: addDaysIso(day, -1),
      reversedBy: ids.meId,
      reversedByName: ids.meName,
      reversedOn: new Date().toISOString(),
      reversalReason: 'Entered against the wrong customer.',
    }),
    blankPayment({
      id: ids.rejectedPaymentId,
      method: 'BankTransfer',
      status: 'Rejected',
      amountCents: 5_200_000,
      reference: 'TRF-REJ-1',
      recordedBy: ids.otherStaffId,
      recordedByName: ids.otherStaffName,
      rejectedBy: ids.meId,
      rejectedByName: ids.meName,
      rejectedOn: new Date().toISOString(),
      rejectionReason: 'The transfer never arrived in the bank account.',
      businessDate: null,
      acceptedOn: null,
    }),
    blankPayment({
      id: ids.otherCustomerPaymentId,
      receiptSeq: 98,
      customerId: ids.otherCustomerId,
      method: 'CashInStore',
      amountCents: 30_000,
      unallocatedCents: 30_000,
    }),
  ];

  // COD collections (by courier) and bank transfers for the reconciliation screens.
  const cod = (id: string, seq: number, collected: Cents, remitted: Cents, days: number) =>
    blankPayment({
      id,
      receiptSeq: seq,
      method: 'CashOnDelivery',
      amountCents: collected,
      unallocatedCents: collected,
      customerId: ids.otherCustomerId,
      receivedOn: addDaysIso(day, days),
      businessDate: addDaysIso(day, days),
      collector: { kind: 'Courier', staffAccountId: null, staffName: null, courierCode: 'PATHAO', courierName: 'Pathao Courier' },
      remittedCents: remitted,
    });
  payments.push(cod(ids.codPaymentUnremittedId, 110, 150_000, 0, -3));
  payments.push(cod(ids.codPaymentShortfallId, 111, 200_000, 180_000, -9));
  payments.push(cod(ids.codPaymentRemittedId, 112, 100_000, 100_000, -12));
  const transfer = (suffix: number, seq: number, cents: Cents, ref: string) =>
    blankPayment({
      id: `f3000000-0000-0000-0000-${String(suffix).padStart(12, '0')}`,
      receiptSeq: seq,
      method: 'BankTransfer',
      amountCents: cents,
      unallocatedCents: cents,
      customerId: ids.otherCustomerId,
      reference: ref,
      receivedOn: addDaysIso(day, -4),
      businessDate: addDaysIso(day, -4),
    });
  payments.push(transfer(1, 120, 100_000, 'TRF-20261001-001'));
  payments.push(transfer(2, 121, 50_000, 'TRF-20261002-002'));
  payments.push(transfer(3, 122, 50_000, 'TRF-20261002-003'));
  payments.push(transfer(4, 123, 32_000, 'TRF-20261004-004'));
  payments.push(transfer(5, 124, 40_000, 'TRF-MATCHED-005'));

  const lineId = (n2: number) => `b5100000-0000-0000-0000-${String(n2).padStart(12, '0')}`;
  const statement: DbStatement = {
    statementId: ids.statementId,
    bankAccountLabel: 'City Bank — current account',
    periodFrom: addDaysIso(day, -30),
    periodTo: day,
    sourceFileName: 'city-bank-oct.csv',
    sha: 'a'.repeat(64),
    importedOn: new Date().toISOString(),
    lines: [
      { lineId: lineId(1), lineNumber: 2, transactionDate: addDaysIso(day, -4), cents: 100_000, direction: 'Credit', reference: 'TRF-20261001-001', description: 'Transfer from customer', matchId: null },
      { lineId: lineId(2), lineNumber: 3, transactionDate: addDaysIso(day, -4), cents: 50_000, direction: 'Credit', reference: 'TRF-20261002-002', description: 'Transfer from customer', matchId: null },
      { lineId: lineId(3), lineNumber: 4, transactionDate: addDaysIso(day, -3), cents: 77_700, direction: 'Credit', reference: 'UNKNOWN-REF', description: 'Deposit — no matching receipt', matchId: null },
      { lineId: lineId(4), lineNumber: 5, transactionDate: addDaysIso(day, -2), cents: 25_050, direction: 'Debit', reference: 'BANK-FEE', description: 'Monthly account fee', matchId: null },
      { lineId: lineId(5), lineNumber: 6, transactionDate: addDaysIso(day, -4), cents: 40_000, direction: 'Credit', reference: 'TRF-MATCHED-005', description: 'Transfer from customer', matchId: 'b5200000-0000-0000-0000-000000000001' },
    ],
  };
  const matches: DbMatch[] = [
    {
      matchId: 'b5200000-0000-0000-0000-000000000001',
      statementId: ids.statementId,
      statementLineId: lineId(5),
      paymentId: payments[payments.length - 1]!.id,
      method: 'Auto',
      matchedOn: new Date().toISOString(),
      unmatchedOn: null,
      unmatchReason: null,
    },
  ];

  const yesterday = addDaysIso(day, -1);
  return {
    charges,
    invoices: [draft, i1, i2, i3, i4, i5, ...others],
    payments,
    creditNotes: [cn],
    refunds: [],
    cashUps: [
      {
        date: yesterday,
        status: 'Closed',
        version: 4,
        countedCents: 99_000,
        explanation: 'BDT 10.00 short — a coin jar was miscounted.',
        countedOn: new Date().toISOString(),
        closedByName: ids.otherStaffName,
        closedOn: new Date().toISOString(),
      },
    ],
    carriedIn: [
      { date: day, id: 'ca110000-0000-0000-0000-000000000001', kind: 'CodRemittance', label: 'COD remittance — Pathao Courier', cents: 50_000 },
    ],
    statements: [statement],
    matches,
    profiles: [
      {
        branchId: ids.mainBranchId,
        legalName: 'PurrCare Veterinary Clinic Ltd',
        taxRegistrationNumber: 'BIN 004512345-0101',
        addressLines: ['House 12, Road 5, Dhanmondi', 'Dhaka 1209'],
        phone: '+88 02 5511 0000',
        email: 'accounts@purrcare.test',
        invoiceFooter: 'Thank you for trusting us with your cat. Goods sold are not returnable once opened.',
        bankAccounts: [
          { bankName: 'City Bank', accountName: 'PurrCare Veterinary Clinic Ltd', accountNumber: '1234-567890-01', branchName: 'Dhanmondi', routingNumber: '225261719' },
          { bankName: 'BRAC Bank', accountName: 'PurrCare Veterinary Clinic Ltd', accountNumber: '2001-554433-02', branchName: 'Gulshan', routingNumber: null },
        ],
        version: 2,
      },
    ],
    remittances: [],
    exportRuns: [
      {
        runId: 'e5000000-0000-0000-0000-000000000001',
        from: addDaysIso(day, -60),
        to: addDaysIso(day, -31),
        branchId: null,
        counts: [14, 22, 19, 21, 2, 1],
        sha256: '7d1e3f0c9b2a4685f1c0d9e8a7b6c5d4e3f2a1b0c9d8e7f6a5b4c3d2e1f0a9b8',
        runByName: ids.otherStaffName,
        runOn: new Date().toISOString(),
      },
      {
        runId: 'e5000000-0000-0000-0000-000000000002',
        from: addDaysIso(day, -60),
        to: addDaysIso(day, -31),
        branchId: null,
        counts: [14, 22, 19, 21, 2, 1],
        sha256: '7d1e3f0c9b2a4685f1c0d9e8a7b6c5d4e3f2a1b0c9d8e7f6a5b4c3d2e1f0a9b8',
        runByName: ids.meName,
        runOn: new Date().toISOString(),
      },
    ],
    loyaltyBalance: 450,
    loyaltyEntries: [
      { entryId: 'ee000000-0000-0000-0000-000000000001', kind: 'Accrual', points: 12, invoiceId: ids.paidInvoiceId, invoiceNumber: 'INV-MAIN-2627-000103', occurredOn: new Date().toISOString() },
    ],
    loyaltyEnabled: true,
    capturable: [
      {
        sourceKind: 'Appointment',
        sourceId: 'a7000000-0000-0000-0000-000000000001',
        customerId: ids.customerId,
        completedOn: new Date().toISOString(),
        label: 'Consultation — Momo (Sam Rahman)',
      },
      {
        sourceKind: 'Appointment',
        sourceId: 'a7000000-0000-0000-0000-000000000002',
        customerId: ids.otherCustomerId,
        completedOn: new Date().toISOString(),
        label: 'Nail trim — Biscuit (Rahima Akter)',
        skipReason: 'The service has no price set, so no charge could be created.',
      },
    ],
    seq: { invoice: 106, creditNote: 102, receipt: 130 },
    idempotency: {},
  };
}

// ---- the store ------------------------------------------------------------------------------

let db: Db = loadPersisted<Db>(PERSIST_KEY) ?? seed();

export function getDb(): Db {
  return db;
}

export function save(): void {
  savePersisted(PERSIST_KEY, db);
}

export function resetBillingMockData(): void {
  clearPersisted(PERSIST_KEY);
  db = seed();
}

/** Test seam: loyalty "on and off" (HO-BIL-002 §12). */
export function setLoyaltyEnabledForMock(enabled: boolean): void {
  db.loyaltyEnabled = enabled;
}

// ---- queries --------------------------------------------------------------------------------

export function findInvoice(id: string): DbInvoice | undefined {
  return db.invoices.find((i) => i.id === id);
}

export function findPayment(id: string): DbPayment | undefined {
  return db.payments.find((p) => p.id === id);
}

export function chargeOnInvoice(chargeId: string): DbInvoice | undefined {
  const real = db.invoices.find(
    (i) => i.status !== 'Discarded' && i.lines.some((l) => l.chargeId === chargeId),
  );
  if (real) return real;
  if (db.charges.find((c) => c.id === chargeId)?.taken) {
    return { id: 'd1000000-0000-0000-0000-000000000999', number: 'INV-MAIN-2627-000199', status: 'Issued' } as DbInvoice;
  }
  return undefined;
}

export function sellerOf(branchId: string) {
  const profile = db.profiles.find((p) => p.branchId === branchId);
  if (!profile) return null;
  return {
    legalName: profile.legalName,
    taxRegistrationNumber: profile.taxRegistrationNumber,
    addressText: profile.addressLines.join(', '),
    phone: profile.phone,
    email: profile.email,
    invoiceFooter: profile.invoiceFooter,
    bankAccounts: profile.bankAccounts,
  };
}

export function lineView(l: DbLine, invoice: DbInvoice) {
  const a = lineAmounts(l, invoice.status !== 'Draft');
  return {
    lineId: l.lineId,
    chargeId: l.chargeId,
    businessLine: l.businessLine,
    itemKind: l.itemKind,
    sourceKind: l.sourceKind,
    sourceId: l.sourceId,
    catId: null,
    description: l.description,
    quantity: qtyText(l.qtyMilli),
    unitPrice: money(l.unitCents),
    lineDiscount: money(l.discountCents),
    invoiceDiscountShare: money(l.shareCents),
    taxClassId: l.taxClassId,
    taxClassCode: a.code,
    taxRatePercent: a.rateText ?? '0.00',
    netAmount: money(a.netCents),
    taxAmount: money(a.taxCents),
    grossAmount: money(a.grossCents),
    sortOrder: l.sortOrder,
  };
}

export function allowedActionsOf(invoice: DbInvoice): string[] {
  const outstanding = outstandingOf(invoice);
  const actions: string[] = [];
  if (invoice.status === 'Draft') actions.push('edit', 'discard', 'issue', 'applyLoyalty');
  if (invoice.status !== 'Draft' && invoice.status !== 'Discarded') actions.push('pdf');
  if ((invoice.status === 'Issued' || invoice.status === 'PartPaid') && outstanding > 0) {
    actions.push('recordPayment', 'allocate', 'applyCredit');
  }
  if (['Issued', 'PartPaid', 'Paid'].includes(invoice.status)) actions.push('creditNote');
  return actions;
}

export function invoiceDetail(invoice: DbInvoice) {
  const frozen = invoice.status !== 'Draft';
  const totals = totalsOf(invoice.lines, frozen);
  const seller = frozen ? sellerOf(invoice.branchId) : null;
  const loyaltyValue = invoice.lines.reduce((sum, l) => sum + l.shareCents, 0);
  const payments = db.payments
    .filter((p) => p.entries.some((e) => e.invoiceId === invoice.id))
    .map((p) => {
      const applied = p.entries
        .filter((e) => e.invoiceId === invoice.id)
        .reduce((sum, e) => sum + (e.kind === 'Allocate' ? e.cents : -e.cents), 0);
      return {
        paymentId: p.id,
        receiptNumber: receiptText(p.receiptSeq) ?? '',
        method: p.method,
        amount: money(applied),
        on: p.acceptedOn ?? p.recordedOn,
        status: p.status,
      };
    });
  const creditNotes = db.creditNotes
    .filter((c) => c.invoiceId === invoice.id)
    .map((c) => ({
      creditNoteId: c.id,
      number: c.number,
      kind: c.kind,
      total: money(c.totalCents),
      issueDate: c.issueDate,
    }));
  const customer = customerName(invoice.customerId);
  return {
    invoiceId: invoice.id,
    number: invoice.number,
    financialYear: frozen ? FY : null,
    status: invoice.status,
    version: invoice.version,
    branchId: invoice.branchId,
    branchTimeZoneId: branchZone(invoice.branchId),
    customerId: invoice.customerId,
    issuedOn: invoice.issuedOn,
    issueDate: invoice.issueDate,
    dueOn: invoice.dueOn,
    seller,
    billTo: frozen
      ? { fullName: customer, mobile: '01799999999', email: null, addressText: 'Flat 4B, Road 11, Banani, Dhaka' }
      : null,
    lines: invoice.lines.map((l) => lineView(l, invoice)),
    totals: {
      net: money(totals.net),
      discount: money(totals.discount),
      tax: money(totals.tax),
      total: money(totals.total),
    },
    isEstimate: !frozen,
    paid: money(invoice.paidCents),
    credited: money(invoice.creditedCents),
    outstanding: money(outstandingOf(invoice)),
    loyaltyRedemption:
      invoice.loyaltyPoints > 0
        ? { points: invoice.loyaltyPoints, value: money(loyaltyValue), loyaltyEntryId: 'ee000000-0000-0000-0000-0000000000f1' }
        : null,
    notes: invoice.notes,
    correctsInvoiceId: invoice.correctsInvoiceId,
    correctsCreditNoteId: invoice.correctsCreditNoteId,
    correctedByInvoiceId: invoice.correctedByInvoiceId,
    payments,
    creditNotes,
    allowedActions: allowedActionsOf(invoice),
  };
}

export function invoiceSummary(invoice: DbInvoice) {
  const total = invoiceTotal(invoice);
  return {
    invoiceId: invoice.id,
    number: invoice.number,
    status: invoice.status,
    customerId: invoice.customerId,
    customerName: customerName(invoice.customerId),
    branchId: invoice.branchId,
    issueDate: invoice.issueDate,
    dueOn: invoice.dueOn,
    total: money(total),
    paid: money(invoice.paidCents),
    credited: money(invoice.creditedCents),
    outstanding: money(outstandingOf(invoice)),
    version: invoice.version,
  };
}

export function chargeView(c: DbCharge) {
  const onInvoice = chargeOnInvoice(c.id);
  const lineTotal = Math.round((c.qtyMilli * c.unitCents) / 1000) - c.discountCents;
  return {
    chargeId: c.id,
    customerId: c.customerId,
    customerName: customerName(c.customerId),
    branchId: c.branchId,
    catId: null,
    businessLine: c.businessLine,
    itemKind: c.itemKind,
    sourceKind: c.sourceKind,
    sourceId: c.sourceId,
    sourceLineRef: c.sourceKind === 'Manual' ? '' : '1',
    billingGroupKind: null,
    billingGroupId: null,
    description: c.description,
    quantity: qtyText(c.qtyMilli),
    unitPrice: money(c.unitCents),
    lineDiscount: money(c.discountCents),
    lineTotal: money(lineTotal),
    taxClassId: c.taxClassId,
    serviceDate: c.serviceDate,
    status: c.status,
    recordedOn: c.recordedOn,
    voidReason: c.voidReason,
    billedOnInvoice: onInvoice
      ? { invoiceId: onInvoice.id, number: onInvoice.number, status: onInvoice.status }
      : null,
  };
}

export function paymentSummary(p: DbPayment) {
  return {
    paymentId: p.id,
    receiptNumber: receiptText(p.receiptSeq),
    customerId: p.customerId,
    customerName: customerName(p.customerId),
    branchId: p.branchId,
    method: p.method,
    status: p.status,
    amount: money(p.amountCents),
    unallocated: money(p.status === 'Accepted' ? p.unallocatedCents : 0),
    receivedOn: p.receivedOn,
    recordedOn: p.recordedOn,
    recordedBy: p.recordedBy,
    recordedByName: p.recordedByName,
    externalReference: p.reference,
    version: p.version,
  };
}

export function paymentAllowedActions(p: DbPayment): string[] {
  const unallocated = p.status === 'Accepted' ? p.unallocatedCents : 0;
  const refunded = p.refunds.reduce((s, r) => s + r.cents, 0);
  const actions: string[] = [];
  if (p.status === 'PendingApproval') actions.push('approve', 'reject');
  if (p.status === 'Accepted') {
    actions.push('receipt');
    if (unallocated > 0) actions.push('allocate', 'refund');
    if (p.earmark && !p.earmark.isReleased) actions.push('releaseEarmark');
    if (refunded === 0) actions.push('reverse');
  }
  if (p.status === 'Reversed') actions.push('receipt');
  return actions;
}

export function paymentDetail(p: DbPayment) {
  const invoiceNumber = (id: string) => findInvoice(id)?.number ?? null;
  return {
    summary: paymentSummary(p),
    notes: p.notes,
    acceptedOn: p.acceptedOn,
    businessDate: p.businessDate,
    collector: p.collector,
    earmark: p.earmark,
    entries: p.entries.map((e) => ({
      entryId: e.entryId,
      kind: e.kind,
      invoiceId: e.invoiceId,
      invoiceNumber: invoiceNumber(e.invoiceId),
      amount: money(e.cents),
      occurredOn: e.occurredOn,
      by: e.by,
      releaseReason: e.releaseReason,
    })),
    refunds: p.refunds.map((r) => ({
      refundId: r.refundId,
      method: r.method,
      amount: money(r.cents),
      businessDate: r.businessDate,
      creditNoteId: r.creditNoteId,
      creditNoteNumber: db.creditNotes.find((c) => c.id === r.creditNoteId)?.number ?? null,
    })),
    trail: {
      recordedBy: p.recordedBy,
      recordedByName: p.recordedByName,
      recordedOn: p.recordedOn,
      approvedBy: p.approvedBy,
      approvedByName: p.approvedByName,
      approvedOn: p.approvedOn,
      rejectedBy: p.rejectedBy,
      rejectedByName: p.rejectedByName,
      rejectedOn: p.rejectedOn,
      rejectionReason: p.rejectionReason,
      reversedBy: p.reversedBy,
      reversedByName: p.reversedByName,
      reversedOn: p.reversedOn,
      reversalReason: p.reversalReason,
    },
    refunded: money(p.refunds.reduce((s, r) => s + r.cents, 0)),
    requestedAllocations: p.requested.map((r) => ({
      invoiceId: r.invoiceId,
      invoiceNumber: invoiceNumber(r.invoiceId),
      amount: money(r.cents),
    })),
    allowedActions: paymentAllowedActions(p),
  };
}

export function receiptView(p: DbPayment) {
  return {
    receiptNumber: receiptText(p.receiptSeq),
    seller: sellerOf(p.branchId),
    branchName: p.branchId === ids.londonBranchId ? 'London branch' : 'Main clinic',
    customerName: customerName(p.customerId),
    method: p.method,
    reference: p.reference,
    amount: money(p.amountCents),
    receivedOn: p.receivedOn,
    acceptedOn: p.acceptedOn,
    status: p.status,
    appliedTo: p.entries
      .filter((e) => e.kind === 'Allocate')
      .map((e) => `${findInvoice(e.invoiceId)?.number ?? 'Invoice'} — ${formatCents(e.cents)}`),
    recordedByName: p.recordedByName,
    branchTimeZoneId: branchZone(p.branchId),
  };
}

export function summaryFor(customerIds: string[], branchId: string) {
  const invoices = db.invoices.filter(
    (i) => customerIds.includes(i.customerId) && (i.status === 'Issued' || i.status === 'PartPaid'),
  );
  const outstanding = invoices.reduce((s, i) => s + outstandingOf(i), 0);
  const day = today();
  const overdue = invoices
    .filter((i) => i.dueOn !== null && i.dueOn < day)
    .reduce((s, i) => s + outstandingOf(i), 0);
  const accepted = db.payments.filter(
    (p) => customerIds.includes(p.customerId) && p.status === 'Accepted' && p.branchId === branchId,
  );
  const freeCredit = accepted
    .filter((p) => !p.earmark || p.earmark.isReleased)
    .reduce((s, p) => s + p.unallocatedCents, 0);
  const earmarked = accepted.filter((p) => p.earmark && !p.earmark.isReleased && p.unallocatedCents > 0);
  const unbilled = db.charges
    .filter((c) => customerIds.includes(c.customerId) && c.status === 'Active' && !chargeOnInvoice(c.id))
    .reduce((s, c) => s + Math.round((c.qtyMilli * c.unitCents) / 1000) - c.discountCents, 0);
  const dues = invoices.map((i) => i.dueOn).filter((d): d is string => d !== null).sort();
  return {
    outstanding: money(outstanding),
    overdue: money(overdue),
    freeCredit: money(freeCredit),
    earmarkedCredit: earmarked.map((p) => ({
      paymentId: p.id,
      label: p.earmark!.label,
      amount: money(p.unallocatedCents),
    })),
    unbilledTotal: money(unbilled),
    loyaltyPoints: db.loyaltyEnabled ? db.loyaltyBalance : null,
    loyaltyEnabled: db.loyaltyEnabled,
    oldestDueOn: dues[0] ?? null,
  };
}

// ---- operations (each returns a violation or its result) ------------------------------------

export interface Failure {
  failure: { status: number; property: string; code: string; message: string };
}

export function fail(status: number, property: string, code: string, message: string): Failure {
  return { failure: { status, property, code, message } };
}

export function isFailure(value: unknown): value is Failure {
  return typeof value === 'object' && value !== null && 'failure' in value;
}

export interface PreviewResult {
  customerId: string;
  lines: Array<Record<string, unknown>>;
  totals: Record<string, Money>;
  isEstimate: true;
  problems: string[];
}

export function distributeLoyalty(lines: DbLine[], valueCents: Cents): void {
  const base = lines.map((l) => lineAmounts({ ...l, shareCents: 0 }, false).netCents);
  const total = base.reduce((a, b) => a + b, 0);
  let left = valueCents;
  lines.forEach((l, index) => {
    const share =
      index === lines.length - 1 ? left : Math.round((valueCents * (base[index] ?? 0)) / Math.max(total, 1));
    l.shareCents = share;
    left -= share;
  });
}

export function linesFromCharges(chargeIds: string[]): DbLine[] {
  return chargeIds
    .map((id) => db.charges.find((c) => c.id === id))
    .filter((c): c is DbCharge => !!c)
    .map((c, index) =>
      line(
        {
          chargeId: c.id,
          businessLine: c.businessLine,
          itemKind: c.itemKind,
          sourceKind: c.sourceKind,
          sourceId: c.sourceId,
          description: c.description,
          qtyMilli: c.qtyMilli,
          unitCents: c.unitCents,
          discountCents: c.discountCents,
          taxClassId: c.taxClassId,
          taxRatePercent: null,
        },
        index + 1,
      ),
    );
}

export function previewOf(customerId: string, chargeIds: string[], points: number): PreviewResult {
  const lines = linesFromCharges(chargeIds);
  const problems: string[] = [];
  if (points > 0) distributeLoyalty(lines, points * 100);
  for (const l of lines) {
    if (!rateOf(l.taxClassId)) {
      problems.push(`The tax class NORATE has no rate configured. Set a rate before issuing.`);
      break;
    }
  }
  const totals = totalsOf(lines, false);
  return {
    customerId,
    lines: lines.map((l) => {
      const a = lineAmounts(l, false);
      return {
        chargeId: l.chargeId,
        businessLine: l.businessLine,
        itemKind: l.itemKind,
        description: l.description,
        quantity: qtyText(l.qtyMilli),
        unitPrice: money(l.unitCents),
        lineDiscount: money(l.discountCents),
        invoiceDiscountShare: money(l.shareCents),
        taxClassCode: a.code,
        taxRatePercent: a.rateText,
        netAmount: money(a.netCents),
        taxAmount: money(a.taxCents),
        grossAmount: money(a.grossCents),
      };
    }),
    totals: {
      net: money(totals.net),
      discount: money(totals.discount),
      tax: money(totals.tax),
      total: money(totals.total),
    },
    isEstimate: true,
    problems,
  };
}

export function recomputeStatus(invoice: DbInvoice): void {
  derive(invoice);
}

export function nextInvoiceNumber(branchId: string): string {
  const seq = db.seq.invoice;
  db.seq.invoice += 1;
  const code = branchId === ids.londonBranchId ? 'LON' : 'MAIN';
  return `INV-${code}-${FY_COMPACT}-${String(seq).padStart(6, '0')}`;
}

export function nextCreditNoteNumber(): string {
  const seq = db.seq.creditNote;
  db.seq.creditNote += 1;
  return `CN-MAIN-${FY_COMPACT}-${String(seq).padStart(6, '0')}`;
}

export function nextReceiptSeq(): number {
  const seq = db.seq.receipt;
  db.seq.receipt += 1;
  return seq;
}

export function guardReference(property: string, value: unknown): Failure | null {
  if (typeof value === 'string' && looksLikeCardNumber(value)) {
    return fail(
      400,
      property,
      'ReferenceLooksLikeCardNumber',
      'This looks like a card number. Enter the terminal slip number instead — card numbers must never be recorded.',
    );
  }
  return null;
}

export function applyToInvoice(
  payment: DbPayment,
  invoice: DbInvoice,
  cents: Cents,
  by: string,
): void {
  invoice.paidCents += cents;
  payment.unallocatedCents -= cents;
  payment.entries.push({
    entryId: `e${String(payment.entries.length + 1).padStart(7, '0')}-0000-0000-0000-${payment.id.slice(-12)}`,
    kind: 'Allocate',
    invoiceId: invoice.id,
    cents,
    occurredOn: new Date().toISOString(),
    by,
    releaseReason: null,
  });
  derive(invoice);
  invoice.version += 1;
}

/** Cash-up arithmetic (integer minor units) for one business date — the mock's `CashUpCalculator`. */
export function cashUpFor(date: string, branchId: string) {
  const dayPayments = db.payments.filter(
    (p) => p.branchId === branchId && p.businessDate === date && (p.status === 'Accepted' || p.status === 'Reversed'),
  );
  const lines: Array<Record<string, unknown>> = [];
  let cash = 0;
  let cashCount = 0;
  let card = 0;
  let cardCount = 0;
  let transfer = 0;
  let transferCount = 0;
  let cashReversals = 0;
  for (const p of dayPayments) {
    if (p.method === 'CashOnDelivery') continue;
    const isCash = p.method === 'CashInStore';
    lines.push({
      id: p.id,
      kind: 'Payment',
      method: p.method,
      collector: null,
      refundMethod: null,
      amount: money(p.amountCents),
      countsInCash: isCash,
      carriedIn: false,
      label: `${receiptText(p.receiptSeq) ?? ''} · ${customerName(p.customerId)}`,
    });
    if (isCash) {
      cash += p.amountCents;
      cashCount += 1;
    } else if (p.method === 'CardInStore') {
      card += p.amountCents;
      cardCount += 1;
    } else {
      transfer += p.amountCents;
      transferCount += 1;
    }
    if (p.status === 'Reversed' && isCash) cashReversals += p.amountCents;
  }
  const refunds = db.refunds.filter((r) => r.businessDate === date && r.method === 'CashInStore');
  const cashRefunds = refunds.reduce((s, r) => s + r.cents, 0);
  for (const r of refunds) {
    lines.push({
      id: r.refundId,
      kind: 'Refund',
      method: null,
      collector: null,
      refundMethod: r.method,
      amount: money(r.cents),
      countsInCash: true,
      carriedIn: false,
      label: r.reason,
    });
  }
  const carried = db.carriedIn.filter((c) => c.date === date);
  const carriedTotal = carried.reduce((s, c) => s + c.cents, 0);
  for (const c of carried) {
    lines.push({
      id: c.id,
      kind: c.kind,
      method: null,
      collector: null,
      refundMethod: null,
      amount: money(c.cents),
      countsInCash: true,
      carriedIn: true,
      label: c.label,
    });
  }
  const expectedCash = cash - cashReversals - cashRefunds + carriedTotal;
  const record = db.cashUps.find((c) => c.date === date);
  const counted = record?.countedCents ?? null;
  const pending = db.payments.filter(
    (p) => p.branchId === branchId && p.status === 'PendingApproval' && p.receivedOn === date,
  ).length;
  return {
    businessDate: date,
    status: record?.status ?? 'Open',
    version: record?.version ?? 0,
    expected: {
      cash: money(cash),
      cashCount,
      card: money(card),
      cardCount,
      transfer: money(transfer),
      transferCount,
      cashRefunds: money(cashRefunds),
      cashReversals: money(cashReversals),
      carriedInTotal: money(carriedTotal),
      carriedIn: carried.map((c) => ({ id: c.id, kind: c.kind, label: c.label, amount: money(c.cents) })),
      expectedCash: money(expectedCash),
    },
    lines,
    counted: counted === null ? null : money(counted),
    variance: counted === null ? null : money(counted - expectedCash),
    explanation: record?.explanation ?? null,
    pendingApprovals: pending,
    countedBy: counted === null ? null : ids.meId,
    countedOn: record?.countedOn ?? null,
    closedBy: record?.status === 'Closed' ? ids.otherStaffId : null,
    closedByName: record?.closedByName ?? null,
    closedOn: record?.closedOn ?? null,
  };
}

/** The mock's expected cash, for its own variance checks (never sent to the client). */
export function expectedCashCents(date: string, branchId: string): Cents {
  const view = cashUpFor(date, branchId);
  return toCents(view.expected.expectedCash.amount);
}
