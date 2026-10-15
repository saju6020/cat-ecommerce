import { http, HttpResponse, type HttpHandler } from 'msw';
import { billingIds as ids, notFound, pageParams, paged } from './fixtures';
import {
  findInvoice,
  findPayment,
  getDb,
  invoiceDetail,
  invoiceSummary,
  paymentSummary,
  receiptView,
  summaryFor,
} from './store';

/**
 * `mocks/handlers/billing/portal.ts` — HO-BIL-002 §12. The customer's own billing (`/api/customers/me`).
 * Answers for the seeded signed-in customer only; another customer's document is `404`, never `403`;
 * drafts, discarded invoices and pending or rejected payments are never returned (OQ-4).
 */
const BASE = '*/api/customers/me';

function visibleInvoice(id: string) {
  const invoice = findInvoice(id);
  if (!invoice || invoice.customerId !== ids.customerId) return undefined;
  if (invoice.status === 'Draft' || invoice.status === 'Discarded') return undefined;
  return invoice;
}

export const portalBillingHandlers: HttpHandler[] = [
  http.get(`${BASE}/billing/summary`, () =>
    HttpResponse.json(summaryFor([ids.customerId], ids.mainBranchId)),
  ),

  http.get(`${BASE}/loyalty`, ({ request }) => {
    const db = getDb();
    const { pageNumber, pageSize } = pageParams(new URL(request.url), 20);
    return HttpResponse.json({
      enabled: db.loyaltyEnabled,
      balance: db.loyaltyBalance,
      entries: paged(db.loyaltyEntries, pageNumber, pageSize),
    });
  }),

  http.get(`${BASE}/invoices`, ({ request }) => {
    const { pageNumber, pageSize } = pageParams(new URL(request.url), 20);
    const items = getDb()
      .invoices.filter((i) => i.customerId === ids.customerId && i.status !== 'Draft' && i.status !== 'Discarded')
      .sort((a, b) => (b.number ?? '').localeCompare(a.number ?? ''));
    return HttpResponse.json(paged(items.map(invoiceSummary), pageNumber, pageSize));
  }),

  http.get(`${BASE}/invoices/:invoiceId.pdf`, ({ params }) => {
    const invoice = visibleInvoice(params.invoiceId as string);
    if (!invoice) return notFound();
    return new HttpResponse('%PDF-1.4\n%mock\n', { headers: { 'Content-Type': 'application/pdf' } });
  }),

  http.get(`${BASE}/invoices/:invoiceId`, ({ params }) => {
    const invoice = visibleInvoice(params.invoiceId as string);
    if (!invoice) return notFound();
    const detail = invoiceDetail(invoice);
    return HttpResponse.json({
      invoice: detail,
      howToPay: {
        bankAccounts: detail.seller?.bankAccounts ?? [],
        referenceInstruction: `Use your invoice number${invoice.number ? ` ${invoice.number}` : ''} as the payment reference.`,
      },
    });
  }),

  http.get(`${BASE}/payments`, ({ request }) => {
    const { pageNumber, pageSize } = pageParams(new URL(request.url), 20);
    const items = getDb()
      .payments.filter(
        (p) => p.customerId === ids.customerId && (p.status === 'Accepted' || p.status === 'Reversed'),
      )
      .sort((a, b) => b.recordedOn.localeCompare(a.recordedOn));
    return HttpResponse.json(paged(items.map(paymentSummary), pageNumber, pageSize));
  }),

  http.get(`${BASE}/payments/:paymentId`, ({ params }) => {
    const payment = findPayment(params.paymentId as string);
    if (!payment || payment.customerId !== ids.customerId) return notFound();
    if (payment.status !== 'Accepted' && payment.status !== 'Reversed') return notFound();
    return HttpResponse.json(receiptView(payment));
  }),
];
