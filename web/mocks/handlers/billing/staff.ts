import { http, HttpResponse, type HttpHandler } from 'msw';
import { billingIds as ids, forbidden, isAccountsRole, notFound, pageParams, paged, violation } from './fixtures';
import {
  APPROVAL_THRESHOLD_CENTS,
  allowedActionsOf,
  applyToInvoice,
  cashUpFor,
  chargeOnInvoice,
  chargeView,
  customerName,
  distributeLoyalty,
  fail,
  findInvoice,
  findPayment,
  formatCents,
  getDb,
  guardReference,
  invoiceDetail,
  invoiceSummary,
  invoiceTotal,
  isFailure,
  linesFromCharges,
  lineAmounts,
  money,
  nextCreditNoteNumber,
  nextInvoiceNumber,
  nextReceiptSeq,
  outstandingOf,
  paymentDetail,
  paymentSummary,
  previewOf,
  qtyText,
  rateOf,
  receiptView,
  recomputeStatus,
  save,
  sellerOf,
  summaryFor,
  toCents,
  toMilli,
  today,
  totalsOf,
  type Cents,
  type DbCharge,
  type DbInvoice,
  type DbPayment,
  type Failure,
} from './store';
import { addDaysIso } from '@/lib/format/appointment';

/**
 * `mocks/handlers/billing/staff.ts` — HO-BIL-002 §12. The staff Billing endpoints, generated from
 * HO-BIL-001 §12.4 and checked against the real controllers. Idempotent endpoints honour
 * `Idempotency-Key` the way the API does: a repeat answers `200` with the original body.
 */

const BASE = '*/api/staff/billing';

const METHODS = [
  { value: 1, name: 'CashInStore', countsInTill: true, requiresReference: false, referenceLabel: 'Receipt number', requiresCollector: false, isOnline: false, allowedRefundMethods: ['CashInStore'] },
  { value: 2, name: 'CardInStore', countsInTill: false, requiresReference: true, referenceLabel: 'Terminal slip number', requiresCollector: false, isOnline: false, allowedRefundMethods: ['CashInStore'] },
  { value: 3, name: 'BankTransfer', countsInTill: false, requiresReference: true, referenceLabel: 'Transfer reference', requiresCollector: false, isOnline: false, allowedRefundMethods: ['BankTransfer'] },
  { value: 4, name: 'CashOnDelivery', countsInTill: false, requiresReference: false, referenceLabel: 'Reference', requiresCollector: true, isOnline: false, allowedRefundMethods: ['CashInStore', 'BankTransfer'] },
];

function failed(result: Failure) {
  const { status, property, code, message } = result.failure;
  return violation(property, code, message, status);
}

function stale() {
  return violation(
    'ExpectedVersion',
    'StaleVersion',
    'This record changed while you were working. Reload and try again.',
    409,
  );
}

function empty(status = 200) {
  return new HttpResponse(null, { status, headers: { 'content-length': '0' } });
}

function replayed(request: Request): unknown | undefined {
  const key = request.headers.get('Idempotency-Key');
  return key ? getDb().idempotency[key] : undefined;
}

function remember(request: Request, body: unknown): void {
  const key = request.headers.get('Idempotency-Key');
  if (key) getDb().idempotency[key] = body;
}

export const staffBillingHandlers: HttpHandler[] = [
  http.get('*/api/billing/payment-methods', () => HttpResponse.json(METHODS)),

  // ---- customer summary -----------------------------------------------------------------
  http.get(`${BASE}/customers/:customerId/summary`, ({ params }) =>
    HttpResponse.json(summaryFor([params.customerId as string], ids.mainBranchId)),
  ),

  http.get(`${BASE}/customers/:customerId/loyalty`, ({ request }) => {
    const db = getDb();
    const { pageNumber, pageSize } = pageParams(new URL(request.url), 20);
    return HttpResponse.json({
      enabled: db.loyaltyEnabled,
      balance: db.loyaltyBalance,
      entries: paged(db.loyaltyEntries, pageNumber, pageSize),
    });
  }),

  // ---- charges --------------------------------------------------------------------------
  http.get(`${BASE}/charges`, ({ request }) => {
    const url = new URL(request.url);
    const { pageNumber, pageSize } = pageParams(url);
    const customerId = url.searchParams.get('customerId');
    const status = url.searchParams.get('status');
    const items = getDb().charges.filter((c) => {
      if (customerId && c.customerId !== customerId) return false;
      const billed = !!chargeOnInvoice(c.id);
      if (status === 'unbilled') return c.status === 'Active' && !billed;
      if (status === 'billed') return c.status === 'Active' && billed;
      if (status === 'voided') return c.status === 'Voided';
      return true;
    });
    return HttpResponse.json(paged(items.map(chargeView), pageNumber, pageSize));
  }),

  http.post(`${BASE}/charges`, async ({ request }) => {
    const body = (await request.json()) as Record<string, unknown>;
    const db = getDb();
    let description = String(body.description ?? '');
    let unitCents = body.unitPrice === undefined ? 0 : toCents(body.unitPrice);
    let taxClassId = String(body.taxClassId ?? ids.taxStd);
    if (body.serviceId) {
      description = 'Consultation';
      unitCents = 80_000;
      taxClassId = ids.taxStd;
    } else if (!description.trim()) {
      return violation('Description', 'ReasonRequired', 'Enter a description for the fee.');
    }
    const guard = guardReference('Description', description);
    if (guard) return failed(guard);
    const chargeId = crypto.randomUUID();
    db.charges.push({
      id: chargeId,
      customerId: String(body.customerId),
      branchId: ids.mainBranchId,
      businessLine: (body.businessLine as DbCharge['businessLine']) ?? 'Clinic',
      itemKind: body.serviceId ? 'Service' : 'Fee',
      sourceKind: 'Manual',
      sourceId: chargeId,
      description,
      qtyMilli: toMilli(body.quantity ?? 1),
      unitCents,
      discountCents: toCents(body.lineDiscount ?? 0),
      taxClassId,
      serviceDate: String(body.serviceDate ?? today()),
      status: 'Active',
      recordedOn: new Date().toISOString(),
      voidReason: null,
    });
    save();
    return HttpResponse.json({ chargeId }, { status: 201 });
  }),

  http.post(`${BASE}/charges/:chargeId/void`, async ({ params, request }) => {
    const body = (await request.json()) as { reason?: string };
    const charge = getDb().charges.find((c) => c.id === params.chargeId);
    if (!charge) return notFound();
    if (!body.reason?.trim()) return violation('Reason', 'ReasonRequired', 'Enter a reason.');
    if (chargeOnInvoice(charge.id)) {
      return violation('ChargeId', 'ChargeAlreadyInvoiced', 'This charge is already on an invoice.', 409);
    }
    charge.status = 'Voided';
    charge.voidReason = body.reason;
    save();
    return new HttpResponse(null, { status: 204 });
  }),

  http.get(`${BASE}/uncaptured-sources`, () =>
    HttpResponse.json(
      getDb().capturable.map((s) => ({
        sourceKind: s.sourceKind,
        sourceId: s.sourceId,
        customerId: s.customerId,
        completedOn: s.completedOn,
        label: s.label,
      })),
    ),
  ),

  http.post(`${BASE}/sources/:kind/:sourceId/capture`, ({ params }) => {
    const db = getDb();
    const index = db.capturable.findIndex((s) => s.sourceId === params.sourceId);
    if (index < 0) return notFound();
    const source = db.capturable[index]!;
    if (source.skipReason) {
      return HttpResponse.json({ captured: 0, skipped: [{ reason: source.skipReason }] });
    }
    db.capturable.splice(index, 1);
    const chargeId = crypto.randomUUID();
    db.charges.push({
      id: chargeId,
      customerId: source.customerId,
      branchId: ids.mainBranchId,
      businessLine: 'Clinic',
      itemKind: 'Service',
      sourceKind: source.sourceKind,
      sourceId: source.sourceId,
      description: source.label,
      qtyMilli: 1000,
      unitCents: 80_000,
      discountCents: 0,
      taxClassId: ids.taxStd,
      serviceDate: today(),
      status: 'Active',
      recordedOn: new Date().toISOString(),
      voidReason: null,
    });
    save();
    return HttpResponse.json({ captured: 1, skipped: [] });
  }),

  // ---- invoices -------------------------------------------------------------------------
  http.post(`${BASE}/invoices/preview`, async ({ request }) => {
    const body = (await request.json()) as { customerId: string; chargeIds: string[]; loyaltyPoints?: number };
    return HttpResponse.json(previewOf(body.customerId, body.chargeIds ?? [], Number(body.loyaltyPoints ?? 0)));
  }),

  http.post(`${BASE}/invoices/issue-from-charges`, () => new HttpResponse(null, { status: 501 })),

  http.post(`${BASE}/invoices`, async ({ request }) => {
    const body = (await request.json()) as { customerId: string; chargeIds: string[]; notes?: string };
    const db = getDb();
    if (!body.chargeIds?.length) {
      return violation('ChargeIds', 'ChargeNotFound', 'Select at least one charge.');
    }
    for (const chargeId of body.chargeIds) {
      const charge = db.charges.find((c) => c.id === chargeId);
      if (!charge) return violation('ChargeIds', 'ChargeNotFound', 'That charge could not be found.');
      if (charge.racy || chargeOnInvoice(chargeId)) {
        charge.taken = true;
        save();
        return violation('ChargeIds', 'ChargeAlreadyInvoiced', 'This charge is already on an invoice.', 409);
      }
    }
    const invoice: DbInvoice = {
      id: crypto.randomUUID(),
      number: null,
      status: 'Draft',
      version: 1,
      branchId: ids.mainBranchId,
      customerId: body.customerId,
      issuedOn: null,
      issueDate: null,
      dueOn: null,
      lines: linesFromCharges(body.chargeIds),
      loyaltyPoints: 0,
      paidCents: 0,
      creditedCents: 0,
      notes: body.notes ?? null,
      correctsInvoiceId: null,
      correctsCreditNoteId: null,
      correctedByInvoiceId: null,
      issueKey: null,
    };
    db.invoices.push(invoice);
    save();
    return HttpResponse.json({ invoiceId: invoice.id }, { status: 201 });
  }),

  http.put(`${BASE}/invoices/:invoiceId/charges`, async ({ params, request }) => {
    const body = (await request.json()) as { expectedVersion: number; chargeIds: string[] };
    const invoice = findInvoice(params.invoiceId as string);
    if (!invoice) return notFound();
    if (invoice.status !== 'Draft') {
      return violation('InvoiceId', 'InvoiceNotDraft', 'Only a draft invoice can be changed. Issued invoices are corrected with a credit note.');
    }
    if (invoice.version !== body.expectedVersion) return stale();
    const db = getDb();
    for (const chargeId of body.chargeIds) {
      const charge = db.charges.find((c) => c.id === chargeId);
      if (!charge) return violation('ChargeIds', 'ChargeNotFound', 'That charge could not be found.');
      const onOther = chargeOnInvoice(chargeId);
      if (charge.racy || (onOther && onOther.id !== invoice.id)) {
        charge.taken = true;
        save();
        return violation('ChargeIds', 'ChargeAlreadyInvoiced', 'This charge is already on an invoice.', 409);
      }
    }
    invoice.lines = linesFromCharges(body.chargeIds);
    if (invoice.loyaltyPoints > 0) distributeLoyalty(invoice.lines, invoice.loyaltyPoints * 100);
    invoice.version += 1;
    save();
    return HttpResponse.json(invoiceDetail(invoice));
  }),

  http.post(`${BASE}/invoices/:invoiceId/discard`, ({ params }) => {
    const invoice = findInvoice(params.invoiceId as string);
    if (!invoice) return notFound();
    if (invoice.status !== 'Draft') {
      return violation('InvoiceId', 'InvoiceNotDraft', 'Only a draft invoice can be changed. Issued invoices are corrected with a credit note.');
    }
    invoice.status = 'Discarded';
    save();
    return new HttpResponse(null, { status: 204 });
  }),

  http.post(`${BASE}/invoices/:invoiceId/issue`, async ({ params, request }) => {
    const body = (await request.json()) as { expectedVersion: number };
    const invoice = findInvoice(params.invoiceId as string);
    if (!invoice) return notFound();
    const key = request.headers.get('Idempotency-Key');
    if (key && invoice.issueKey === key && invoice.number) {
      return HttpResponse.json({ invoiceId: invoice.id, number: invoice.number });
    }
    if (invoice.status !== 'Draft') {
      return violation('InvoiceId', 'InvoiceNotDraft', 'Only a draft invoice can be changed. Issued invoices are corrected with a credit note.', 409);
    }
    if (invoice.version !== body.expectedVersion) return stale();
    if (!sellerOf(invoice.branchId)) {
      return violation('BranchId', 'BillingProfileMissing', 'This branch has no billing profile yet. An administrator must set one before invoices can be issued.');
    }
    for (const l of invoice.lines) {
      if (!rateOf(l.taxClassId)) {
        return violation('Lines', 'TaxRateNotConfigured', 'The tax class NORATE has no rate configured. Set a rate before issuing.');
      }
    }
    for (const l of invoice.lines) l.taxRatePercent = rateOf(l.taxClassId)!.rate;
    invoice.number = nextInvoiceNumber(invoice.branchId);
    invoice.status = 'Issued';
    invoice.issuedOn = new Date().toISOString();
    invoice.issueDate = today();
    invoice.dueOn = today();
    invoice.issueKey = key;
    invoice.version += 1;
    if (invoice.loyaltyPoints > 0) getDb().loyaltyBalance -= invoice.loyaltyPoints;
    save();
    return HttpResponse.json({ invoiceId: invoice.id, number: invoice.number });
  }),

  http.post(`${BASE}/invoices/:invoiceId/loyalty-redemption`, async ({ params, request }) => {
    const body = (await request.json()) as { points: number };
    const db = getDb();
    const invoice = findInvoice(params.invoiceId as string);
    if (!invoice) return notFound();
    if (!db.loyaltyEnabled) {
      return violation('Points', 'LoyaltyNotEnabled', 'Loyalty points are not enabled.');
    }
    if (invoice.status !== 'Draft') {
      return violation('InvoiceId', 'InvoiceNotDraft', 'Only a draft invoice can be changed. Issued invoices are corrected with a credit note.');
    }
    const points = Number(body.points);
    if (points < 100) {
      return violation('Points', 'LoyaltyBelowMinimum', 'The minimum redemption is 100 points.');
    }
    if (points > db.loyaltyBalance) {
      return violation('Points', 'LoyaltyInsufficientPoints', `The customer has only ${db.loyaltyBalance} points.`);
    }
    const net = totalsOf(invoice.lines, false).net + invoice.lines.reduce((s, l) => s + l.shareCents, 0);
    if (points * 100 > net) {
      return violation('Points', 'LoyaltyRedemptionExceedsNet', 'The points are worth more than the invoice.');
    }
    invoice.loyaltyPoints = points;
    distributeLoyalty(invoice.lines, points * 100);
    invoice.version += 1;
    save();
    return HttpResponse.json({ invoice: invoiceDetail(invoice) });
  }),

  http.delete(`${BASE}/invoices/:invoiceId/loyalty-redemption`, ({ params }) => {
    const invoice = findInvoice(params.invoiceId as string);
    if (!invoice) return notFound();
    invoice.loyaltyPoints = 0;
    for (const l of invoice.lines) l.shareCents = 0;
    invoice.version += 1;
    save();
    return HttpResponse.json({ invoice: invoiceDetail(invoice) });
  }),

  http.get(`${BASE}/invoices/export.csv`, () =>
    new HttpResponse('invoiceId,number,status\n', { headers: { 'Content-Type': 'text/csv' } }),
  ),

  http.get(`${BASE}/invoices/:invoiceId.pdf`, ({ params }) => {
    const invoice = findInvoice(params.invoiceId as string);
    if (!invoice) return notFound();
    return new HttpResponse('%PDF-1.4\n%mock\n', {
      headers: { 'Content-Type': 'application/pdf', 'Content-Disposition': `attachment; filename="${invoice.number ?? 'draft'}.pdf"` },
    });
  }),

  http.get(`${BASE}/invoices/:invoiceId`, ({ params }) => {
    const invoice = findInvoice(params.invoiceId as string);
    if (!invoice || invoice.branchId !== ids.mainBranchId) return notFound();
    return HttpResponse.json(invoiceDetail(invoice));
  }),

  http.get(`${BASE}/invoices`, ({ request }) => {
    const url = new URL(request.url);
    const { pageNumber, pageSize } = pageParams(url);
    const customerId = url.searchParams.get('customerId');
    const status = url.searchParams.get('status');
    const number = url.searchParams.get('number')?.toLowerCase();
    const from = url.searchParams.get('from');
    const to = url.searchParams.get('to');
    const line = url.searchParams.get('businessLine');
    const items = getDb()
      .invoices.filter((i) => {
        if (status) {
          if (i.status !== status) return false;
        } else if (i.status === 'Discarded') return false;
        if (customerId && i.customerId !== customerId) return false;
        if (number && !(i.number ?? '').toLowerCase().includes(number)) return false;
        if (from && (i.issueDate ?? '') < from) return false;
        if (to && (i.issueDate ?? '9999') > to) return false;
        if (line && !i.lines.some((l) => l.businessLine === line)) return false;
        return true;
      })
      .sort((a, b) => (b.number ?? '~').localeCompare(a.number ?? '~'));
    return HttpResponse.json(paged(items.map(invoiceSummary), pageNumber, pageSize));
  }),

  // ---- credit notes ---------------------------------------------------------------------
  http.post(`${BASE}/invoices/:invoiceId/credit-notes`, async ({ params, request }) => {
    const again = replayed(request);
    if (again) return HttpResponse.json(again);
    const body = (await request.json()) as {
      kind: string;
      reason: string;
      fullCredit: boolean;
      lines?: Array<{ invoiceLineId: string; quantity?: number; amount?: number }>;
    };
    const db = getDb();
    const invoice = findInvoice(params.invoiceId as string);
    if (!invoice) return notFound();
    if (!body.reason?.trim()) return violation('Reason', 'ReasonRequired', 'Enter a reason.');
    const guard = guardReference('Reason', body.reason);
    if (guard) return failed(guard);
    const total = invoiceTotal(invoice);
    const remaining = total - invoice.creditedCents;
    const lines: Array<{
      invoiceLineId: string;
      description: string;
      quantity: string | null;
      requestedCents: Cents | null;
      netCents: Cents;
      taxRatePercent: string;
      taxCents: Cents;
      grossCents: Cents;
    }> = [];
    let credit = 0;
    if (body.fullCredit) {
      for (const l of invoice.lines) {
        const a = lineAmounts(l, true);
        lines.push({ invoiceLineId: l.lineId, description: l.description, quantity: qtyText(l.qtyMilli), requestedCents: null, netCents: a.netCents, taxRatePercent: a.rateText ?? '0.00', taxCents: a.taxCents, grossCents: a.grossCents });
      }
      credit = remaining;
    } else {
      for (const input of body.lines ?? []) {
        const l = invoice.lines.find((x) => x.lineId === input.invoiceLineId);
        if (!l) return violation('Lines', 'CreditExceedsLine', 'That line is not on the invoice.');
        const a = lineAmounts(l, true);
        let gross = a.grossCents;
        let quantity: string | null = null;
        let requested: Cents | null = null;
        if (input.quantity !== undefined) {
          const milli = toMilli(input.quantity);
          if (milli > l.qtyMilli) {
            return violation('Lines', 'CreditExceedsLine', `The credit for "${l.description}" is more than what remains on that line.`);
          }
          gross = Math.round((a.grossCents * milli) / Math.max(l.qtyMilli, 1));
          quantity = qtyText(milli);
        } else if (input.amount !== undefined) {
          requested = toCents(input.amount);
          if (requested > a.grossCents) {
            return violation('Lines', 'CreditExceedsLine', `The credit for "${l.description}" is more than what remains on that line.`);
          }
          gross = requested;
        }
        const net = Math.round((gross * 100) / (100 + Number(a.rateText ?? 0)));
        lines.push({ invoiceLineId: l.lineId, description: l.description, quantity, requestedCents: requested, netCents: net, taxRatePercent: a.rateText ?? '0.00', taxCents: gross - net, grossCents: gross });
        credit += gross;
      }
      if (credit > remaining) {
        return violation('Lines', 'CreditExceedsInvoice', `Only ${formatCents(remaining)} can still be credited on this invoice.`);
      }
    }
    if (credit <= 0) return violation('Lines', 'CreditExceedsInvoice', 'Choose what to credit.');
    invoice.creditedCents += credit;
    const excess = Math.max(0, invoice.paidCents + invoice.creditedCents - total);
    let released = 0;
    if (excess > 0) {
      invoice.paidCents -= excess;
      released = excess;
      const payment = db.payments.find((p) => p.entries.some((e) => e.invoiceId === invoice.id && e.kind === 'Allocate'));
      if (payment) {
        payment.unallocatedCents += excess;
        payment.entries.push({ entryId: crypto.randomUUID(), kind: 'Release', invoiceId: invoice.id, cents: excess, occurredOn: new Date().toISOString(), by: ids.meId, releaseReason: 'Credit note excess' });
      }
    }
    recomputeStatus(invoice);
    invoice.version += 1;
    const net = lines.reduce((s, l) => s + l.netCents, 0);
    const note = {
      id: crypto.randomUUID(),
      number: nextCreditNoteNumber(),
      kind: body.kind,
      reason: body.reason,
      invoiceId: invoice.id,
      customerId: invoice.customerId,
      branchId: invoice.branchId,
      lines,
      netCents: net,
      taxCents: credit - net,
      totalCents: credit,
      releasedCents: released,
      issuedOn: new Date().toISOString(),
      issueDate: today(),
      correctedByInvoiceId: null,
    };
    db.creditNotes.push(note);
    // the real API returns a bare decimal here (HO-BIL-001 StaffCreditNotesController)
    const result = { creditNoteId: note.id, number: note.number, releasedToCustomerCredit: released / 100 };
    remember(request, result);
    save();
    return HttpResponse.json(result, { status: 201 });
  }),

  http.post(`${BASE}/invoices/:invoiceId/corrected-draft`, async ({ params, request }) => {
    const body = (await request.json()) as { creditNoteId: string };
    const db = getDb();
    const original = findInvoice(params.invoiceId as string);
    const note = db.creditNotes.find((c) => c.id === body.creditNoteId);
    if (!original) return notFound();
    if (!note) return violation('CreditNoteId', 'CreditNoteNotFound', 'That credit note could not be found.', 404);
    const draft: DbInvoice = {
      id: crypto.randomUUID(),
      number: null,
      status: 'Draft',
      version: 1,
      branchId: original.branchId,
      customerId: original.customerId,
      issuedOn: null,
      issueDate: null,
      dueOn: null,
      lines: original.lines.map((l, index) => ({ ...l, lineId: crypto.randomUUID(), chargeId: null, taxRatePercent: null, shareCents: 0, sortOrder: index + 1 })),
      loyaltyPoints: 0,
      paidCents: 0,
      creditedCents: 0,
      notes: null,
      correctsInvoiceId: original.id,
      correctsCreditNoteId: note.id,
      correctedByInvoiceId: null,
      issueKey: null,
    };
    db.invoices.push(draft);
    original.correctedByInvoiceId = draft.id;
    note.correctedByInvoiceId = draft.id;
    save();
    return HttpResponse.json({ invoiceId: draft.id }, { status: 201 });
  }),

  http.put(`${BASE}/invoices/:invoiceId/lines`, async ({ params, request }) => {
    const body = (await request.json()) as {
      expectedVersion: number;
      lines: Array<{ description: string; quantity: number; unitPrice: number; lineDiscount: number; taxClassId: string }>;
    };
    const invoice = findInvoice(params.invoiceId as string);
    if (!invoice) return notFound();
    if (invoice.status !== 'Draft' || !invoice.correctsInvoiceId) {
      return violation('InvoiceId', 'InvoiceNotDraft', 'Only a draft invoice can be changed. Issued invoices are corrected with a credit note.');
    }
    if (invoice.version !== body.expectedVersion) return stale();
    invoice.lines = body.lines.map((l, index) => ({
      lineId: crypto.randomUUID(),
      chargeId: null,
      businessLine: 'Clinic' as const,
      itemKind: 'Service',
      sourceKind: 'Manual',
      sourceId: crypto.randomUUID(),
      description: l.description,
      qtyMilli: toMilli(l.quantity),
      unitCents: toCents(l.unitPrice),
      discountCents: toCents(l.lineDiscount),
      shareCents: 0,
      taxClassId: l.taxClassId,
      taxRatePercent: null,
      sortOrder: index + 1,
    }));
    invoice.version += 1;
    save();
    return empty();
  }),

  http.get(`${BASE}/credit-notes/:creditNoteId.pdf`, ({ params }) => {
    const note = getDb().creditNotes.find((c) => c.id === params.creditNoteId);
    if (!note) return notFound();
    return new HttpResponse('%PDF-1.4\n%mock\n', { headers: { 'Content-Type': 'application/pdf' } });
  }),

  http.get(`${BASE}/credit-notes/:creditNoteId`, ({ params }) => {
    const db = getDb();
    const note = db.creditNotes.find((c) => c.id === params.creditNoteId);
    if (!note) return notFound();
    return HttpResponse.json({
      creditNoteId: note.id,
      number: note.number,
      financialYear: '2026-27',
      kind: note.kind,
      reason: note.reason,
      invoiceId: note.invoiceId,
      invoiceNumber: findInvoice(note.invoiceId)?.number ?? null,
      customerId: note.customerId,
      branchId: note.branchId,
      lines: note.lines.map((l) => ({
        invoiceLineId: l.invoiceLineId,
        description: l.description,
        quantity: l.quantity,
        requestedAmount: l.requestedCents === null ? null : money(l.requestedCents),
        net: money(l.netCents),
        taxRatePercent: l.taxRatePercent,
        tax: money(l.taxCents),
        gross: money(l.grossCents),
      })),
      net: money(note.netCents),
      tax: money(note.taxCents),
      total: money(note.totalCents),
      releasedToCustomerCredit: money(note.releasedCents),
      issuedOn: note.issuedOn,
      issueDate: note.issueDate,
      issuedBy: ids.meId,
      issuedByName: ids.meName,
      correctedByInvoiceId: note.correctedByInvoiceId,
    });
  }),

  // ---- payments -------------------------------------------------------------------------
  http.post(`${BASE}/payments`, async ({ request }) => {
    const again = replayed(request);
    if (again) return HttpResponse.json(again);
    const body = (await request.json()) as {
      customerId: string;
      method: string;
      amount: number;
      receivedOn: string;
      externalReference?: string;
      collector?: { kind: string; staffAccountId?: string; courierCode?: string; courierName?: string };
      earmark?: { kind: string; sourceId: string; label: string };
      allocations: Array<{ invoiceId: string; amount: number }>;
      notes?: string;
    };
    const db = getDb();
    const method = METHODS.find((m) => m.name === body.method);
    if (!method) return violation('Method', 'ReferenceRequired', 'Choose a payment method.');
    const cardGuard = guardReference('ExternalReference', body.externalReference) ?? guardReference('Notes', body.notes);
    if (cardGuard) return failed(cardGuard);
    if (method.requiresReference && !body.externalReference?.trim()) {
      return violation('ExternalReference', 'ReferenceRequired', `Enter the ${method.referenceLabel.toLowerCase()}.`);
    }
    if (method.requiresCollector && !body.collector) {
      return violation('Collector', 'CollectorRequired', 'Choose who collected this payment.');
    }
    if (body.receivedOn > addDaysIso(today(), 0)) {
      return violation('ReceivedOn', 'ReceivedDateInFuture', 'The date received cannot be in the future.');
    }
    const amount = toCents(body.amount);
    if (amount <= 0) return violation('Amount', 'AllocationExceedsUnallocated', 'Enter an amount above zero.');
    const allocations = (body.allocations ?? []).map((a) => ({ invoiceId: a.invoiceId, cents: toCents(a.amount) }));
    const pending = amount > APPROVAL_THRESHOLD_CENTS;
    const payment: DbPayment = {
      id: crypto.randomUUID(),
      receiptSeq: pending ? null : nextReceiptSeq(),
      customerId: body.customerId,
      branchId: ids.mainBranchId,
      method: body.method,
      status: pending ? 'PendingApproval' : 'Accepted',
      amountCents: amount,
      unallocatedCents: pending ? 0 : amount,
      receivedOn: body.receivedOn,
      recordedOn: new Date().toISOString(),
      recordedBy: ids.meId,
      recordedByName: ids.meName,
      reference: body.externalReference?.trim() || null,
      notes: body.notes?.trim() || null,
      collector: body.collector
        ? {
            kind: body.collector.kind,
            staffAccountId: body.collector.staffAccountId ?? null,
            staffName: body.collector.staffAccountId ? ids.meName : null,
            courierCode: body.collector.courierCode ?? null,
            courierName: body.collector.courierName ?? null,
          }
        : null,
      earmark: body.earmark ? { ...body.earmark, isReleased: false, releasedOn: null, releaseReason: null } : null,
      entries: [],
      refunds: [],
      requested: pending ? allocations : [],
      businessDate: pending ? null : body.receivedOn,
      acceptedOn: pending ? null : new Date().toISOString(),
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
    };
    const touched: DbInvoice[] = [];
    if (!pending) {
      let sum = 0;
      for (const allocation of allocations) {
        const invoice = findInvoice(allocation.invoiceId);
        if (!invoice) return notFound();
        const outstanding = outstandingOf(invoice);
        if (allocation.cents > outstanding) {
          return violation('Allocations', 'AllocationExceedsOutstanding', `Only ${formatCents(outstanding)} is outstanding on ${invoice.number}.`);
        }
        sum += allocation.cents;
        touched.push(invoice);
      }
      if (sum > amount) {
        return violation('Allocations', 'AllocationExceedsUnallocated', `Only ${formatCents(amount)} of this payment is still unallocated.`);
      }
      for (const allocation of allocations) {
        applyToInvoice(payment, findInvoice(allocation.invoiceId)!, allocation.cents, ids.meId);
      }
    }
    db.payments.push(payment);
    const requestedInvoices = pending ? allocations.map((a) => findInvoice(a.invoiceId)).filter((i): i is DbInvoice => !!i) : touched;
    const result = {
      paymentId: payment.id,
      status: payment.status,
      receiptNumber: pending ? null : `R-${String(payment.receiptSeq).padStart(6, '0')}`,
      applied: pending
        ? []
        : payment.entries.map((e) => ({ invoiceId: e.invoiceId, number: findInvoice(e.invoiceId)?.number ?? null, amount: money(e.cents) })),
      unallocated: money(payment.unallocatedCents),
      invoiceOutstanding: requestedInvoices.map((i) => ({ invoiceId: i.id, number: i.number, outstanding: money(outstandingOf(i)), status: i.status })),
    };
    remember(request, result);
    save();
    return HttpResponse.json(result, { status: 201 });
  }),

  http.post(`${BASE}/customers/:customerId/apply-credit`, async ({ params, request }) => {
    const body = (await request.json()) as { invoiceId: string; amount?: number };
    const db = getDb();
    const invoice = findInvoice(body.invoiceId);
    if (!invoice) return notFound();
    const customerId = params.customerId as string;
    const sources = db.payments
      .filter((p) => p.customerId === customerId && p.status === 'Accepted' && (!p.earmark || p.earmark.isReleased) && p.unallocatedCents > 0)
      .sort((a, b) => (a.receiptSeq ?? 0) - (b.receiptSeq ?? 0));
    const free = sources.reduce((s, p) => s + p.unallocatedCents, 0);
    const outstanding = outstandingOf(invoice);
    let want = body.amount === undefined ? Math.min(free, outstanding) : toCents(body.amount);
    if (want > outstanding) {
      return violation('Amount', 'AllocationExceedsOutstanding', `Only ${formatCents(outstanding)} is outstanding on ${invoice.number}.`);
    }
    if (want > free) {
      return violation('Amount', 'AllocationExceedsUnallocated', `You can apply at most ${formatCents(free)} of this customer's credit.`);
    }
    const applied: Array<{ invoiceId: string; number: string | null; amount: number }> = [];
    for (const payment of sources) {
      if (want <= 0) break;
      const take = Math.min(want, payment.unallocatedCents);
      applyToInvoice(payment, invoice, take, ids.meId);
      applied.push({ invoiceId: invoice.id, number: invoice.number, amount: take / 100 });
      want -= take;
    }
    save();
    return HttpResponse.json({
      applied,
      freeCreditRemaining: sources.reduce((s, p) => s + p.unallocatedCents, 0) / 100,
      invoiceOutstanding: outstandingOf(invoice) / 100,
    });
  }),

  http.get(`${BASE}/payments/export.csv`, () =>
    new HttpResponse('paymentId,receiptNumber\n', { headers: { 'Content-Type': 'text/csv' } }),
  ),

  http.get(`${BASE}/payments`, ({ request }) => {
    const url = new URL(request.url);
    const { pageNumber, pageSize } = pageParams(url);
    const customerId = url.searchParams.get('customerId');
    const status = url.searchParams.get('status');
    const method = url.searchParams.get('method');
    const from = url.searchParams.get('from');
    const to = url.searchParams.get('to');
    const items = getDb()
      .payments.filter((p) => {
        if (customerId && p.customerId !== customerId) return false;
        if (status && p.status !== status) return false;
        if (method && p.method !== method) return false;
        if (from && p.receivedOn < from) return false;
        if (to && p.receivedOn > to) return false;
        return true;
      })
      .sort((a, b) => b.recordedOn.localeCompare(a.recordedOn));
    return HttpResponse.json(paged(items.map(paymentSummary), pageNumber, pageSize));
  }),

  http.get(`${BASE}/payments/:paymentId/receipt`, ({ params }) => {
    const payment = findPayment(params.paymentId as string);
    return payment ? HttpResponse.json(receiptView(payment)) : notFound();
  }),

  http.get(`${BASE}/payments/:paymentId`, ({ params }) => {
    const payment = findPayment(params.paymentId as string);
    return payment ? HttpResponse.json(paymentDetail(payment)) : notFound();
  }),

  http.get(`${BASE}/approvals`, ({ request }) => {
    if (!isAccountsRole(request)) return forbidden();
    const items = getDb().payments.filter((p) => p.status === 'PendingApproval');
    return HttpResponse.json(
      items.map((p) => ({
        paymentId: p.id,
        customerId: p.customerId,
        customerName: customerName(p.customerId),
        method: p.method,
        amount: money(p.amountCents),
        receivedOn: p.receivedOn,
        recordedOn: p.recordedOn,
        recordedBy: p.recordedBy,
        recordedByName: p.recordedByName,
        externalReference: p.reference,
        canApprove: p.recordedBy !== ids.meId,
        requestedAllocations: p.requested.map((r) => ({
          invoiceId: r.invoiceId,
          invoiceNumber: findInvoice(r.invoiceId)?.number ?? null,
          amount: money(r.cents),
        })),
      })),
    );
  }),

  http.post(`${BASE}/payments/:paymentId/approve`, ({ params, request }) => {
    if (!isAccountsRole(request)) return forbidden();
    const payment = findPayment(params.paymentId as string);
    if (!payment) return notFound();
    if (payment.status !== 'PendingApproval') {
      return violation('PaymentId', 'PaymentNotPending', 'This payment is no longer waiting for approval.');
    }
    if (payment.recordedBy === ids.meId) {
      return violation('ApproverId', 'ApproverMustDiffer', 'A payment must be approved by a different staff member than the one who recorded it.');
    }
    payment.status = 'Accepted';
    payment.receiptSeq = nextReceiptSeq();
    payment.unallocatedCents = payment.amountCents;
    payment.businessDate = today();
    payment.acceptedOn = new Date().toISOString();
    payment.approvedBy = ids.meId;
    payment.approvedByName = ids.meName;
    payment.approvedOn = payment.acceptedOn;
    const applied: Array<{ invoiceId: string; number: string | null; amount: ReturnType<typeof money> }> = [];
    for (const r of payment.requested) {
      const invoice = findInvoice(r.invoiceId);
      if (!invoice) continue;
      const take = Math.min(r.cents, outstandingOf(invoice), payment.unallocatedCents);
      if (take <= 0) continue;
      applyToInvoice(payment, invoice, take, ids.meId);
      applied.push({ invoiceId: invoice.id, number: invoice.number, amount: money(take) });
    }
    save();
    return HttpResponse.json({ applied, leftUnallocated: money(payment.unallocatedCents) });
  }),

  http.post(`${BASE}/payments/:paymentId/reject`, async ({ params, request }) => {
    if (!isAccountsRole(request)) return forbidden();
    const body = (await request.json()) as { reason?: string };
    const payment = findPayment(params.paymentId as string);
    if (!payment) return notFound();
    if (!body.reason?.trim()) return violation('Reason', 'ReasonRequired', 'Enter a reason.');
    payment.status = 'Rejected';
    payment.rejectedBy = ids.meId;
    payment.rejectedByName = ids.meName;
    payment.rejectedOn = new Date().toISOString();
    payment.rejectionReason = body.reason;
    save();
    return new HttpResponse(null, { status: 204 });
  }),

  http.post(`${BASE}/payments/:paymentId/allocations`, async ({ params, request }) => {
    const body = (await request.json()) as { allocations: Array<{ invoiceId: string; amount: number }> };
    const payment = findPayment(params.paymentId as string);
    if (!payment) return notFound();
    const applied: Array<{ invoiceId: string; number: string | null; amount: ReturnType<typeof money>; outstanding: ReturnType<typeof money> }> = [];
    let sum = 0;
    for (const a of body.allocations) {
      const invoice = findInvoice(a.invoiceId);
      if (!invoice) return notFound();
      const cents = toCents(a.amount);
      if (cents > outstandingOf(invoice)) {
        return violation('Allocations', 'AllocationExceedsOutstanding', `Only ${formatCents(outstandingOf(invoice))} is outstanding on ${invoice.number}.`);
      }
      sum += cents;
    }
    if (sum > payment.unallocatedCents) {
      return violation('Allocations', 'AllocationExceedsUnallocated', `Only ${formatCents(payment.unallocatedCents)} of this payment is still unallocated.`);
    }
    for (const a of body.allocations) {
      const invoice = findInvoice(a.invoiceId)!;
      const cents = toCents(a.amount);
      applyToInvoice(payment, invoice, cents, ids.meId);
      applied.push({ invoiceId: invoice.id, number: invoice.number, amount: money(cents), outstanding: money(outstandingOf(invoice)) });
    }
    save();
    return HttpResponse.json({ applied, unallocated: money(payment.unallocatedCents) });
  }),

  http.post(`${BASE}/payments/:paymentId/release-earmark`, async ({ params, request }) => {
    const body = (await request.json()) as { reason?: string };
    const payment = findPayment(params.paymentId as string);
    if (!payment?.earmark) return notFound();
    if (!body.reason?.trim()) return violation('Reason', 'ReasonRequired', 'Enter a reason.');
    payment.earmark.isReleased = true;
    payment.earmark.releasedOn = new Date().toISOString();
    payment.earmark.releaseReason = body.reason;
    save();
    return new HttpResponse(null, { status: 204 });
  }),

  http.post(`${BASE}/payments/:paymentId/reverse`, async ({ params, request }) => {
    if (!isAccountsRole(request)) return forbidden();
    const body = (await request.json()) as { reason?: string };
    const payment = findPayment(params.paymentId as string);
    if (!payment) return notFound();
    if (!body.reason?.trim()) return violation('Reason', 'ReasonRequired', 'Enter a reason.');
    const guard = guardReference('Reason', body.reason);
    if (guard) return failed(guard);
    if (payment.status !== 'Accepted') {
      return violation('PaymentId', 'PaymentNotAccepted', 'Only an accepted payment can be changed this way.');
    }
    if (payment.refunds.length > 0) {
      return violation('PaymentId', 'PaymentHasRefunds', 'A payment that has been refunded cannot be reversed.');
    }
    for (const e of [...payment.entries]) {
      if (e.kind !== 'Allocate') continue;
      const invoice = findInvoice(e.invoiceId);
      if (!invoice) continue;
      invoice.paidCents -= e.cents;
      recomputeStatus(invoice);
      invoice.version += 1;
      payment.entries.push({ entryId: crypto.randomUUID(), kind: 'Release', invoiceId: e.invoiceId, cents: e.cents, occurredOn: new Date().toISOString(), by: ids.meId, releaseReason: 'Payment reversed' });
    }
    payment.status = 'Reversed';
    payment.unallocatedCents = 0;
    payment.reversedBy = ids.meId;
    payment.reversedByName = ids.meName;
    payment.reversedOn = new Date().toISOString();
    payment.reversalReason = body.reason;
    save();
    return new HttpResponse(null, { status: 204 });
  }),

  // ---- refunds --------------------------------------------------------------------------
  http.post(`${BASE}/refunds`, async ({ request }) => {
    const again = replayed(request);
    if (again) return HttpResponse.json(again);
    const body = (await request.json()) as {
      paymentId: string;
      creditNoteId?: string;
      method: string;
      amount: number;
      reference?: string;
      reason: string;
    };
    const db = getDb();
    const payment = findPayment(body.paymentId);
    if (!payment) return notFound();
    const guard = guardReference('Reference', body.reference) ?? guardReference('Reason', body.reason);
    if (guard) return failed(guard);
    if (!body.reason?.trim()) return violation('Reason', 'ReasonRequired', 'Enter a reason.');
    const method = METHODS.find((m) => m.name === payment.method);
    if (!method?.allowedRefundMethods.includes(body.method)) {
      return violation('Method', 'RefundMethodNotAllowed', 'That refund method is not allowed for this payment.');
    }
    if (body.method === 'BankTransfer' && !body.reference?.trim()) {
      return violation('Reference', 'ReferenceRequired', 'Enter the transfer reference.');
    }
    const cents = toCents(body.amount);
    if (cents > payment.unallocatedCents) {
      return violation('Amount', 'RefundExceedsAvailable', `Only ${formatCents(payment.unallocatedCents)} of this payment is available to refund.`);
    }
    payment.unallocatedCents -= cents;
    const refundId = crypto.randomUUID();
    payment.refunds.push({ refundId, method: body.method, cents, businessDate: today(), creditNoteId: body.creditNoteId ?? null });
    db.refunds.push({
      refundId,
      paymentId: payment.id,
      creditNoteId: body.creditNoteId ?? null,
      method: body.method,
      cents,
      reference: body.reference ?? null,
      reason: body.reason,
      businessDate: today(),
      issuedOn: new Date().toISOString(),
      issuedBy: ids.meId,
      issuedByName: ids.meName,
    });
    const result = { refundId };
    remember(request, result);
    save();
    return HttpResponse.json(result, { status: 201 });
  }),

  http.get(`${BASE}/refunds`, ({ request }) => {
    const { pageNumber, pageSize } = pageParams(new URL(request.url));
    const db = getDb();
    return HttpResponse.json(
      paged(
        db.refunds.map((r) => {
          const payment = findPayment(r.paymentId);
          return {
            refundId: r.refundId,
            paymentId: r.paymentId,
            receiptNumber: payment?.receiptSeq ? `R-${String(payment.receiptSeq).padStart(6, '0')}` : null,
            creditNoteId: r.creditNoteId,
            creditNoteNumber: db.creditNotes.find((c) => c.id === r.creditNoteId)?.number ?? null,
            customerId: payment?.customerId ?? ids.customerId,
            customerName: customerName(payment?.customerId ?? ids.customerId),
            method: r.method,
            amount: money(r.cents),
            reference: r.reference,
            reason: r.reason,
            businessDate: r.businessDate,
            issuedOn: r.issuedOn,
            issuedBy: r.issuedBy,
            issuedByName: r.issuedByName,
          };
        }),
        pageNumber,
        pageSize,
      ),
    );
  }),

  // ---- receivables ----------------------------------------------------------------------
  http.get(`${BASE}/receivables/ageing`, ({ request }) => {
    const url = new URL(request.url);
    const { pageNumber, pageSize } = pageParams(url);
    const asOf = url.searchParams.get('asOf') ?? today();
    const rows = new Map<string, { current: Cents; b1: Cents; b2: Cents; b3: Cents; b4: Cents; oldest: string }>();
    const daysBetween = (from: string, to: string) =>
      Math.round((Date.parse(`${to}T00:00:00Z`) - Date.parse(`${from}T00:00:00Z`)) / 86_400_000);
    for (const invoice of getDb().invoices) {
      if (invoice.status !== 'Issued' && invoice.status !== 'PartPaid') continue;
      const outstanding = outstandingOf(invoice);
      if (outstanding <= 0 || !invoice.dueOn) continue;
      const row = rows.get(invoice.customerId) ?? { current: 0, b1: 0, b2: 0, b3: 0, b4: 0, oldest: invoice.dueOn };
      const overdue = daysBetween(invoice.dueOn, asOf);
      if (overdue <= 0) row.current += outstanding;
      else if (overdue <= 30) row.b1 += outstanding;
      else if (overdue <= 60) row.b2 += outstanding;
      else if (overdue <= 90) row.b3 += outstanding;
      else row.b4 += outstanding;
      if (invoice.dueOn < row.oldest) row.oldest = invoice.dueOn;
      rows.set(invoice.customerId, row);
    }
    const all = [...rows.entries()].sort((a, b) => a[1].oldest.localeCompare(b[1].oldest));
    const sum = (pick: (r: { current: Cents; b1: Cents; b2: Cents; b3: Cents; b4: Cents }) => Cents) =>
      all.reduce((s, [, r]) => s + pick(r), 0);
    return HttpResponse.json({
      asOf,
      totals: {
        current: money(sum((r) => r.current)),
        days1To30: money(sum((r) => r.b1)),
        days31To60: money(sum((r) => r.b2)),
        days61To90: money(sum((r) => r.b3)),
        over90: money(sum((r) => r.b4)),
      },
      rows: paged(
        all.map(([customerId, r]) => ({
          customerId,
          customerName: customerName(customerId),
          current: money(r.current),
          days1To30: money(r.b1),
          days31To60: money(r.b2),
          days61To90: money(r.b3),
          over90: money(r.b4),
          total: money(r.current + r.b1 + r.b2 + r.b3 + r.b4),
          oldestDueOn: r.oldest,
        })),
        pageNumber,
        pageSize,
      ),
    });
  }),
];

// Re-exported for the cash-up handlers.
export { cashUpFor, allowedActionsOf, isFailure, fail };
