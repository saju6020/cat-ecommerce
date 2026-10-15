import { http, HttpResponse, type HttpHandler } from 'msw';
import { looksLikeCardNumber } from '@/lib/validation/cardNumberGuard';
import { addDaysIso } from '@/lib/format/appointment';
import { billingIds as ids, forbidden, isAccountsRole, isAdministrator, notFound, pageParams, paged, violation } from './fixtures';
import {
  cashUpFor,
  customerName,
  expectedCashCents,
  findPayment,
  getDb,
  money,
  save,
  toCents,
  today,
  type Cents,
  type DbPayment,
  type DbStatement,
} from './store';

/**
 * `mocks/handlers/billing/ops.ts` — HO-BIL-002 §12. Cash-up, COD and bank reconciliation, the
 * accounting export and the billing profile. Accountant/administrator-only endpoints answer `403`
 * for any other staff role the accounts mock knows about.
 */

const BASE = '*/api/staff/billing';

function empty204() {
  return new HttpResponse(null, { status: 204 });
}

function codStatus(p: DbPayment): 'Unremitted' | 'Shortfall' | 'Remitted' {
  if (p.remittedCents === 0) return 'Unremitted';
  if (p.remittedCents >= p.amountCents) return 'Remitted';
  return 'Shortfall';
}

function codView(p: DbPayment) {
  const status = codStatus(p);
  const outstanding = p.amountCents - p.remittedCents;
  const age = Math.round((Date.parse(`${today()}T00:00:00Z`) - Date.parse(`${p.receivedOn}T00:00:00Z`)) / 86_400_000);
  return {
    paymentId: p.id,
    receiptNumber: p.receiptSeq ? `R-${String(p.receiptSeq).padStart(6, '0')}` : null,
    customerId: p.customerId,
    customerName: customerName(p.customerId),
    courierCode: p.collector?.courierCode ?? null,
    courierName: p.collector?.courierName ?? null,
    receivedOn: p.receivedOn,
    collected: money(p.amountCents),
    remitted: money(p.remittedCents),
    outstanding: money(outstanding),
    shortfall: money(status === 'Shortfall' ? outstanding : 0),
    status,
    overdue: status !== 'Remitted' && age > 7,
  };
}

function codPayments(): DbPayment[] {
  return getDb().payments.filter((p) => p.method === 'CashOnDelivery' && p.status === 'Accepted');
}

function fnv(text: string): string {
  let hash = 0x811c9dc5;
  let out = '';
  for (let round = 0; round < 8; round += 1) {
    for (let i = 0; i < text.length; i += 1) {
      hash ^= text.charCodeAt(i) + round;
      hash = Math.imul(hash, 0x01000193) >>> 0;
    }
    out += hash.toString(16).padStart(8, '0');
  }
  return out;
}

function statementSummary(s: DbStatement) {
  const db = getDb();
  return {
    statementId: s.statementId,
    bankAccountLabel: s.bankAccountLabel,
    periodFrom: s.periodFrom,
    periodTo: s.periodTo,
    sourceFileName: s.sourceFileName,
    lineCount: s.lines.length,
    matchedCount: s.lines.filter((l) => l.matchId && db.matches.some((m) => m.matchId === l.matchId && !m.unmatchedOn)).length,
    importedOn: s.importedOn,
    importedBy: ids.meId,
    importedByName: ids.meName,
  };
}

function activeMatch(paymentId: string) {
  return getDb().matches.find((m) => m.paymentId === paymentId && !m.unmatchedOn);
}

function reconciliationOf(s: DbStatement) {
  const db = getDb();
  const lineActive = (matchId: string | null) =>
    !!matchId && db.matches.some((m) => m.matchId === matchId && !m.unmatchedOn);
  return {
    statement: statementSummary(s),
    unmatchedStatementLines: s.lines
      .filter((l) => l.direction === 'Credit' && !lineActive(l.matchId))
      .map((l) => ({
        lineId: l.lineId,
        lineNumber: l.lineNumber,
        transactionDate: l.transactionDate,
        amount: money(l.cents),
        direction: l.direction,
        reference: l.reference,
        description: l.description,
      })),
    unmatchedPayments: db.payments
      .filter((p) => p.method === 'BankTransfer' && p.status === 'Accepted' && !activeMatch(p.id))
      .map((p) => ({
        paymentId: p.id,
        receiptNumber: p.receiptSeq ? `R-${String(p.receiptSeq).padStart(6, '0')}` : null,
        customerId: p.customerId,
        customerName: customerName(p.customerId),
        amount: money(p.amountCents),
        receivedOn: p.receivedOn,
        reference: p.reference,
      })),
    matches: db.matches
      .filter((m) => m.statementId === s.statementId && !m.unmatchedOn)
      .map((m) => ({
        matchId: m.matchId,
        statementLineId: m.statementLineId,
        paymentId: m.paymentId,
        receiptNumber: findPayment(m.paymentId)?.receiptSeq
          ? `R-${String(findPayment(m.paymentId)!.receiptSeq).padStart(6, '0')}`
          : null,
        amount: money(s.lines.find((l) => l.lineId === m.statementLineId)?.cents ?? 0),
        method: m.method,
        matchedOn: m.matchedOn,
        matchedBy: ids.meId,
        unmatchedOn: null,
        unmatchReason: null,
      })),
    debitLineCount: s.lines.filter((l) => l.direction === 'Debit').length,
  };
}

/** Minimal RFC-4180-ish parse of the canonical statement CSV (the mock's `BankStatementCsvParser`). */
function parseStatement(text: string): { errors: string[]; lines: DbStatement['lines'] } {
  const rows = text
    .replace(/^﻿/, '')
    .split(/\r?\n/)
    .filter((r) => r.trim() !== '');
  const header = (rows[0] ?? '').split(',').map((c) => c.trim().toLowerCase());
  const missing = ['date', 'amount', 'direction'].filter((c) => !header.includes(c));
  if (rows.length === 0 || missing.length > 0) {
    return { errors: [`Row 1: The header is missing: ${missing.join(', ')}.`], lines: [] };
  }
  const at = (name: string) => header.indexOf(name);
  const errors: string[] = [];
  const lines: DbStatement['lines'] = [];
  rows.slice(1).forEach((row, index) => {
    const cells = row.split(',');
    const rowNumber = index + 2;
    const date = cells[at('date')]?.trim() ?? '';
    const amount = cells[at('amount')]?.trim() ?? '';
    const direction = (cells[at('direction')]?.trim() ?? '').toUpperCase();
    if (!/^\d{4}-\d{2}-\d{2}$/.test(date)) {
      errors.push(`Row ${rowNumber}: The date must be written yyyy-MM-dd.`);
    } else if (!/^\d+(\.\d{1,2})?$/.test(amount) || amount === '0') {
      errors.push(`Row ${rowNumber}: The amount must be a positive number with a . decimal point, at most two decimals and no thousands separator.`);
    } else if (direction !== 'C' && direction !== 'D') {
      errors.push(`Row ${rowNumber}: The direction must be C (credit) or D (debit).`);
    } else {
      const reference = at('reference') >= 0 ? (cells[at('reference')]?.trim() ?? null) : null;
      const description = at('description') >= 0 ? (cells[at('description')]?.trim() ?? null) : null;
      lines.push({
        lineId: crypto.randomUUID(),
        lineNumber: rowNumber,
        transactionDate: date,
        cents: toCents(amount),
        direction: direction === 'C' ? 'Credit' : 'Debit',
        reference: reference ? (looksLikeCardNumber(reference) ? '••••' : reference) : null,
        description: description ? (looksLikeCardNumber(description) ? '••••' : description) : null,
        matchId: null,
      });
    }
  });
  return { errors: errors.slice(0, 10), lines };
}

export const opsBillingHandlers: HttpHandler[] = [
  // ---- cash-up --------------------------------------------------------------------------
  http.get(`${BASE}/cash-ups/:date`, ({ params }) =>
    HttpResponse.json(cashUpFor(params.date as string, ids.mainBranchId)),
  ),

  http.put(`${BASE}/cash-ups/:date/count`, async ({ params, request }) => {
    const date = params.date as string;
    const body = (await request.json()) as { countedCash: number; explanation?: string };
    const db = getDb();
    if (date > today()) {
      return violation('BusinessDate', 'CashUpDateInFuture', 'You cannot count the till for a future day.');
    }
    let record = db.cashUps.find((c) => c.date === date);
    if (record?.status === 'Closed') {
      return violation('BusinessDate', 'CashUpClosed', 'This day has been closed and can no longer be changed.');
    }
    if (!record) {
      record = { date, status: 'Open', version: 0, countedCents: null, explanation: null, countedOn: null, closedByName: null, closedOn: null };
      db.cashUps.push(record);
    }
    record.countedCents = toCents(body.countedCash);
    record.explanation = body.explanation?.trim() || null;
    if (record.explanation && looksLikeCardNumber(record.explanation)) {
      return violation('Explanation', 'ReferenceLooksLikeCardNumber', 'This looks like a card number. Card numbers must never be recorded.');
    }
    record.countedOn = new Date().toISOString();
    record.version += 1;
    save();
    return HttpResponse.json(cashUpFor(date, ids.mainBranchId));
  }),

  http.post(`${BASE}/cash-ups/:date/close`, async ({ params, request }) => {
    if (!isAccountsRole(request)) return forbidden();
    const date = params.date as string;
    const body = (await request.json()) as { expectedVersion: number };
    const db = getDb();
    const record = db.cashUps.find((c) => c.date === date);
    if (record?.status === 'Closed') {
      return violation('BusinessDate', 'CashUpClosed', 'This day has been closed and can no longer be changed.');
    }
    if (!record || record.countedCents === null) {
      return violation('CountedCash', 'CountRequired', 'Count the cash before closing the day.');
    }
    if (record.version !== body.expectedVersion) {
      return violation('ExpectedVersion', 'StaleVersion', 'This record changed while you were working. Reload and try again.', 409);
    }
    const view = cashUpFor(date, ids.mainBranchId);
    if (view.pendingApprovals > 0) {
      return violation('BusinessDate', 'PendingApprovalsExist', 'Payments recorded for this day are still waiting for approval. Approve or reject them before closing.');
    }
    if (record.countedCents !== expectedCashCents(date, ids.mainBranchId) && !record.explanation) {
      return violation('Explanation', 'VarianceExplanationRequired', 'The counted cash differs from the expected cash. Enter an explanation before closing.');
    }
    record.status = 'Closed';
    record.closedByName = ids.meName;
    record.closedOn = new Date().toISOString();
    record.version += 1;
    save();
    return empty204();
  }),

  // ---- COD ------------------------------------------------------------------------------
  http.get(`${BASE}/cod/collections`, ({ request }) => {
    if (!isAccountsRole(request)) return forbidden();
    const url = new URL(request.url);
    const { pageNumber, pageSize } = pageParams(url);
    const courier = url.searchParams.get('courierCode');
    const status = url.searchParams.get('status')?.toLowerCase();
    const items = codPayments()
      .filter((p) => !courier || p.collector?.courierCode === courier)
      .map(codView)
      .filter((c) => !status || c.status.toLowerCase() === status)
      .sort((a, b) => b.receivedOn.localeCompare(a.receivedOn));
    return HttpResponse.json(paged(items, pageNumber, pageSize));
  }),

  http.post(`${BASE}/cod/remittances`, async ({ request }) => {
    if (!isAccountsRole(request)) return forbidden();
    const body = (await request.json()) as {
      courierCode: string;
      receivedOn: string;
      amount: number;
      method: string;
      reference?: string;
      lines: Array<{ paymentId: string; amountRemitted: number }>;
    };
    const db = getDb();
    if (looksLikeCardNumber(body.reference) || looksLikeCardNumber(body.courierCode)) {
      return violation('Reference', 'ReferenceLooksLikeCardNumber', 'This looks like a card number. Card numbers must never be recorded.');
    }
    let applied = 0;
    for (const line of body.lines) {
      const payment = findPayment(line.paymentId);
      if (!payment) return notFound();
      if (payment.collector?.courierCode !== body.courierCode) {
        return violation('Lines', 'CollectionCourierMismatch', 'That collection belongs to a different courier.');
      }
      const cents = toCents(line.amountRemitted);
      if (cents > payment.amountCents - payment.remittedCents) {
        return violation('Lines', 'RemittanceExceedsCollection', 'The amount remitted is more than was collected.');
      }
      applied += cents;
    }
    const amount = toCents(body.amount);
    if (amount < applied) {
      return violation('Amount', 'RemittanceExceedsCollection', 'The amount received is less than the total of the lines.');
    }
    for (const line of body.lines) {
      findPayment(line.paymentId)!.remittedCents += toCents(line.amountRemitted);
    }
    const remittanceId = crypto.randomUUID();
    db.remittances.push({
      remittanceId,
      courierCode: body.courierCode,
      receivedOn: body.receivedOn,
      cents: amount,
      method: body.method,
      reference: body.reference ?? null,
      appliedCents: applied,
      unappliedCents: amount - applied,
      lineCount: body.lines.length,
      recordedByName: ids.meName,
      recordedOn: new Date().toISOString(),
    });
    save();
    return HttpResponse.json({ remittanceId, unapplied: (amount - applied) / 100 }, { status: 201 });
  }),

  http.get(`${BASE}/cod/remittances`, ({ request }) => {
    if (!isAccountsRole(request)) return forbidden();
    const url = new URL(request.url);
    const { pageNumber, pageSize } = pageParams(url);
    const courier = url.searchParams.get('courierCode');
    const items = getDb()
      .remittances.filter((r) => !courier || r.courierCode === courier)
      .map((r) => ({
        remittanceId: r.remittanceId,
        courierCode: r.courierCode,
        receivedOn: r.receivedOn,
        amount: money(r.cents),
        method: r.method,
        reference: r.reference,
        applied: money(r.appliedCents),
        unapplied: money(r.unappliedCents),
        lineCount: r.lineCount,
        recordedBy: ids.meId,
        recordedByName: r.recordedByName,
        recordedOn: r.recordedOn,
      }));
    return HttpResponse.json(paged(items, pageNumber, pageSize));
  }),

  http.get(`${BASE}/cod/reconciliation`, ({ request }) => {
    if (!isAccountsRole(request)) return forbidden();
    const courier = new URL(request.url).searchParams.get('courierCode');
    const byCourier = new Map<string, DbPayment[]>();
    for (const p of codPayments()) {
      const code = p.collector?.courierCode ?? 'UNKNOWN';
      if (courier && code !== courier) continue;
      byCourier.set(code, [...(byCourier.get(code) ?? []), p]);
    }
    const db = getDb();
    return HttpResponse.json({
      couriers: [...byCourier.entries()].map(([code, payments]) => {
        const sum = (pick: (p: DbPayment) => Cents) => payments.reduce((s, p) => s + pick(p), 0);
        const shortfall = payments.filter((p) => codStatus(p) === 'Shortfall');
        const unremitted = payments.filter((p) => codStatus(p) === 'Unremitted');
        return {
          courierCode: code,
          collected: money(sum((p) => p.amountCents)),
          remitted: money(sum((p) => p.remittedCents)),
          shortfall: money(shortfall.reduce((s, p) => s + p.amountCents - p.remittedCents, 0)),
          unremitted: money(unremitted.reduce((s, p) => s + p.amountCents, 0)),
          unappliedRemittances: money(
            db.remittances.filter((r) => r.courierCode === code).reduce((s, r) => s + r.unappliedCents, 0),
          ),
          shortfallLines: shortfall.map(codView),
        };
      }),
    });
  }),

  // ---- bank -----------------------------------------------------------------------------
  http.get(`${BASE}/bank-statements`, ({ request }) => {
    if (!isAccountsRole(request)) return forbidden();
    const { pageNumber, pageSize } = pageParams(new URL(request.url));
    return HttpResponse.json(paged(getDb().statements.map(statementSummary), pageNumber, pageSize));
  }),

  http.post(`${BASE}/bank-statements`, async ({ request }) => {
    if (!isAccountsRole(request)) return forbidden();
    const form = await request.formData();
    const file = form.get('file');
    if (!(file instanceof File) || file.size === 0) {
      return violation('file', 'StatementMalformed', 'Choose the statement file (a .csv).');
    }
    if (file.size > 1_048_576) {
      return violation('file', 'StatementTooLarge', 'The statement file is larger than 1 MiB.', 413);
    }
    const text = await file.text();
    const parsed = parseStatement(text);
    if (parsed.errors.length > 0) {
      return HttpResponse.json(
        {
          errors: parsed.errors.map((message) => ({
            propertyName: 'file',
            errorCode: 'StatementMalformed',
            errorMessage: message,
            resourceName: 'Billing',
          })),
          isValid: false,
        },
        { status: 400 },
      );
    }
    const db = getDb();
    const label = String(form.get('bankAccountLabel') ?? '');
    const sha = fnv(text);
    if (db.statements.some((s) => s.bankAccountLabel === label && s.sha === sha)) {
      return violation('file', 'StatementDuplicate', 'This statement file has already been imported for this account.');
    }
    const statementId = crypto.randomUUID();
    db.statements.push({
      statementId,
      bankAccountLabel: label,
      periodFrom: String(form.get('periodFrom') ?? today()),
      periodTo: String(form.get('periodTo') ?? today()),
      sourceFileName: file.name,
      sha,
      importedOn: new Date().toISOString(),
      lines: parsed.lines,
    });
    save();
    return HttpResponse.json({ statementId, lineCount: parsed.lines.length }, { status: 201 });
  }),

  http.get(`${BASE}/bank-statements/:statementId/reconciliation`, ({ params, request }) => {
    if (!isAccountsRole(request)) return forbidden();
    const statement = getDb().statements.find((s) => s.statementId === params.statementId);
    return statement ? HttpResponse.json(reconciliationOf(statement)) : notFound();
  }),

  http.post(`${BASE}/bank-statements/:statementId/auto-match`, ({ params, request }) => {
    if (!isAccountsRole(request)) return forbidden();
    const db = getDb();
    const statement = db.statements.find((s) => s.statementId === params.statementId);
    if (!statement) return notFound();
    let matched = 0;
    let ambiguous = 0;
    let none = 0;
    const lineActive = (matchId: string | null) =>
      !!matchId && db.matches.some((m) => m.matchId === matchId && !m.unmatchedOn);
    for (const line of statement.lines) {
      if (line.direction !== 'Credit' || lineActive(line.matchId)) continue;
      const candidates = db.payments.filter(
        (p) => p.method === 'BankTransfer' && p.status === 'Accepted' && p.amountCents === line.cents && !activeMatch(p.id),
      );
      if (candidates.length === 1) {
        const matchId = crypto.randomUUID();
        db.matches.push({
          matchId,
          statementId: statement.statementId,
          statementLineId: line.lineId,
          paymentId: candidates[0]!.id,
          method: 'Auto',
          matchedOn: new Date().toISOString(),
          unmatchedOn: null,
          unmatchReason: null,
        });
        line.matchId = matchId;
        matched += 1;
      } else if (candidates.length > 1) {
        ambiguous += 1;
      } else {
        none += 1;
      }
    }
    save();
    return HttpResponse.json({ matched, ambiguous, none });
  }),

  http.post(`${BASE}/bank-matches`, async ({ request }) => {
    if (!isAccountsRole(request)) return forbidden();
    const body = (await request.json()) as { statementId: string; statementLineId: string; paymentId: string };
    const db = getDb();
    const statement = db.statements.find((s) => s.statementId === body.statementId);
    const line = statement?.lines.find((l) => l.lineId === body.statementLineId);
    const payment = findPayment(body.paymentId);
    if (!statement || !line) return notFound();
    if (!payment) return notFound();
    if (line.matchId && db.matches.some((m) => m.matchId === line.matchId && !m.unmatchedOn)) {
      return violation('StatementLineId', 'BankLineAlreadyMatched', 'This statement line is already matched to a payment.', 409);
    }
    if (activeMatch(payment.id)) {
      return violation('PaymentId', 'PaymentAlreadyMatched', 'This payment is already matched to a statement line.', 409);
    }
    if (payment.method !== 'BankTransfer') {
      return violation('PaymentId', 'MatchMethodNotTransfer', 'Only a bank transfer payment can be matched to a statement line.');
    }
    if (payment.amountCents !== line.cents) {
      return violation('PaymentId', 'MatchAmountMismatch', 'The statement line and the payment are not the same amount.');
    }
    const matchId = crypto.randomUUID();
    db.matches.push({
      matchId,
      statementId: statement.statementId,
      statementLineId: line.lineId,
      paymentId: payment.id,
      method: 'Manual',
      matchedOn: new Date().toISOString(),
      unmatchedOn: null,
      unmatchReason: null,
    });
    line.matchId = matchId;
    save();
    return HttpResponse.json({ matchId }, { status: 201 });
  }),

  http.post(`${BASE}/bank-matches/:matchId/unmatch`, async ({ params, request }) => {
    if (!isAccountsRole(request)) return forbidden();
    const body = (await request.json()) as { reason?: string };
    const db = getDb();
    const match = db.matches.find((m) => m.matchId === params.matchId);
    if (!match) return notFound();
    if (!body.reason?.trim()) return violation('Reason', 'ReasonRequired', 'Enter a reason.');
    match.unmatchedOn = new Date().toISOString();
    match.unmatchReason = body.reason;
    const line = db.statements.flatMap((s) => s.lines).find((l) => l.lineId === match.statementLineId);
    if (line) line.matchId = null;
    save();
    return empty204();
  }),

  // ---- accounting export ----------------------------------------------------------------
  http.get(`${BASE}/accounting-exports`, ({ request }) => {
    if (!isAccountsRole(request)) return forbidden();
    const { pageNumber, pageSize } = pageParams(new URL(request.url), 20);
    const items = getDb()
      .exportRuns.slice()
      .sort((a, b) => b.runOn.localeCompare(a.runOn))
      .map((r) => ({
        runId: r.runId,
        from: r.from,
        to: r.to,
        branchId: r.branchId,
        invoiceCount: r.counts[0],
        invoiceLineCount: r.counts[1],
        paymentCount: r.counts[2],
        allocationCount: r.counts[3],
        creditNoteCount: r.counts[4],
        refundCount: r.counts[5],
        sha256: r.sha256,
        runBy: ids.meId,
        runByName: r.runByName,
        runOn: r.runOn,
      }));
    return HttpResponse.json(paged(items, pageNumber, pageSize));
  }),

  http.get(`${BASE}/accounting-exports/download`, ({ request }) => {
    if (!isAccountsRole(request)) return forbidden();
    const url = new URL(request.url);
    const from = url.searchParams.get('from') ?? today();
    const to = url.searchParams.get('to') ?? today();
    const span = Math.round((Date.parse(`${to}T00:00:00Z`) - Date.parse(`${from}T00:00:00Z`)) / 86_400_000) + 1;
    if (from > to || span > 366) {
      return violation('to', 'ExportRangeTooLarge', 'An export can cover at most 366 days.');
    }
    const sha = fnv(`${from}|${to}|${url.searchParams.get('branchId') ?? ''}`);
    getDb().exportRuns.push({
      runId: crypto.randomUUID(),
      from,
      to,
      branchId: url.searchParams.get('branchId'),
      counts: [3, 5, 4, 4, 1, 0],
      sha256: sha,
      runByName: ids.meName,
      runOn: new Date().toISOString(),
    });
    save();
    return new HttpResponse(new Uint8Array([0x50, 0x4b, 0x05, 0x06, ...new Array(18).fill(0)]), {
      headers: {
        'Content-Type': 'application/zip',
        'X-Export-Sha256': sha,
        'Content-Disposition': `attachment; filename="purrcare-accounts-${from}-${to}.zip"`,
      },
    });
  }),

  // ---- billing profile ------------------------------------------------------------------
  http.get('*/api/admin/billing-profiles/:branchId', ({ params }) => {
    const profile = getDb().profiles.find((p) => p.branchId === params.branchId);
    return profile ? HttpResponse.json(profile) : notFound();
  }),

  http.put('*/api/admin/billing-profiles/:branchId', async ({ params, request }) => {
    if (!isAdministrator(request)) return forbidden();
    const body = (await request.json()) as {
      legalName: string;
      taxRegistrationNumber: string;
      addressLines: string[];
      phone?: string;
      email?: string;
      invoiceFooter?: string;
      bankAccounts: Array<{ bankName: string; accountName: string; accountNumber: string; branchName?: string; routingNumber?: string }>;
    };
    if (!body.legalName?.trim()) return violation('LegalName', 'ReasonRequired', 'Enter the legal name.');
    if (!body.taxRegistrationNumber?.trim()) {
      return violation('TaxRegistrationNumber', 'ReasonRequired', 'Enter the tax registration number.');
    }
    const guarded: Array<[string, string | undefined]> = [
      ['LegalName', body.legalName],
      ['AddressLines', body.addressLines?.join(' ')],
      ['Phone', body.phone],
      ['Email', body.email],
      ['InvoiceFooter', body.invoiceFooter],
    ];
    for (const [property, value] of guarded) {
      if (looksLikeCardNumber(value)) {
        return violation(property, 'ReferenceLooksLikeCardNumber', 'This looks like a card number. Card numbers must never be recorded.');
      }
    }
    const db = getDb();
    const branchId = params.branchId as string;
    const existing = db.profiles.find((p) => p.branchId === branchId);
    const next = {
      branchId,
      legalName: body.legalName.trim(),
      taxRegistrationNumber: body.taxRegistrationNumber.trim(),
      addressLines: body.addressLines ?? [],
      phone: body.phone || null,
      email: body.email || null,
      invoiceFooter: body.invoiceFooter || null,
      bankAccounts: (body.bankAccounts ?? []).map((a) => ({
        bankName: a.bankName,
        accountName: a.accountName,
        accountNumber: a.accountNumber,
        branchName: a.branchName || null,
        routingNumber: a.routingNumber || null,
      })),
      version: (existing?.version ?? 0) + 1,
    };
    if (existing) Object.assign(existing, next);
    else db.profiles.push(next);
    save();
    return empty204();
  }),
];

export { addDaysIso };
