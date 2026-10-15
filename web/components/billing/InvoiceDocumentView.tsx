import type { ReactNode } from 'react';
import Link from 'next/link';
import type {
  InvoiceCreditNoteRow,
  InvoiceDetail,
  InvoiceLine,
  InvoicePaymentRow,
} from '@/lib/api/billing-staff';
import { formatDateOnly, formatInZone } from '@/lib/format/time';
import { Amount } from './Amount';
import { PageHeader, Section } from './PageHeader';
import { ResponsiveTable, type Column } from './ResponsiveTable';
import { InvoiceStatusPill } from './StatusPill';

/**
 * `components/billing/InvoiceDocumentView.tsx` — HO-BIL-002 §11.3 (US-BIL-01, 02; NFR-LEG-3). The
 * issued invoice as a document: number, status (text), issue and due dates; the **seller snapshot**
 * (legal name, tax registration number, address — as printed at issue, never today's settings) and
 * the **bill-to snapshot**; every line with its business line, kind, quantity, price, discounts,
 * tax class, **rate %**, net, tax and gross; the totals to *outstanding*; payments applied; credit
 * notes; the correction links; **Download PDF** (the server's one legal rendering, ADR-FE-085).
 * `mode="customer"` is the same document read-only on the portal. There is no edit control anywhere.
 */
export interface DocumentLinks {
  payment: (paymentId: string) => string | null;
  creditNote: (creditNoteId: string) => string | null;
}

export function InvoiceDocumentView({
  invoice,
  pdfAction,
  links,
  backHref,
  backLabel,
  actions,
  correctionLink,
  children,
}: {
  invoice: InvoiceDetail;
  /** The *Download PDF* control: a `DownloadButton` for staff, a plain link for the portal. */
  pdfAction: ReactNode;
  links: DocumentLinks;
  backHref: string;
  backLabel: string;
  actions?: ReactNode;
  /** Renders the *Corrects INV-…* / *Corrected by INV-…* link (it knows how to name the invoice). */
  correctionLink: (invoiceId: string, kind: 'corrects' | 'correctedBy') => ReactNode;
  children?: ReactNode;
}) {
  const zone = invoice.branchTimeZoneId;
  const lineColumns: Array<Column<InvoiceLine>> = [
    { id: 'description', header: 'Description', primary: true, cell: (l) => l.description },
    { id: 'line', header: 'Business line', cell: (l) => l.businessLine },
    { id: 'kind', header: 'Kind', cell: (l) => l.itemKind },
    { id: 'qty', header: 'Qty', numeric: true, cell: (l) => l.quantity },
    { id: 'unit', header: 'Unit price', numeric: true, cell: (l) => <Amount money={l.unitPrice} /> },
    { id: 'discount', header: 'Discount', numeric: true, cell: (l) => <Amount money={l.lineDiscount} /> },
    { id: 'loyalty', header: 'Loyalty share', numeric: true, cell: (l) => <Amount money={l.invoiceDiscountShare} /> },
    { id: 'class', header: 'Tax class', cell: (l) => l.taxClassCode ?? '—' },
    { id: 'rate', header: 'Rate %', numeric: true, cell: (l) => `${l.taxRatePercent}%` },
    { id: 'net', header: 'Net', numeric: true, cell: (l) => <Amount money={l.netAmount} /> },
    { id: 'tax', header: 'Tax', numeric: true, cell: (l) => <Amount money={l.taxAmount} /> },
    { id: 'gross', header: 'Gross', numeric: true, cell: (l) => <Amount money={l.grossAmount} /> },
  ];
  const paymentColumns: Array<Column<InvoicePaymentRow>> = [
    {
      id: 'receipt',
      header: 'Receipt',
      primary: true,
      cell: (p) => {
        const href = links.payment(p.paymentId);
        return href ? <Link href={href} className="font-semibold text-accent-ink hover:underline">{p.receiptNumber}</Link> : p.receiptNumber;
      },
    },
    { id: 'method', header: 'Method', cell: (p) => p.method },
    { id: 'on', header: 'Date', cell: (p) => formatInZone(p.on, zone, { dateStyle: 'medium' }) },
    { id: 'amount', header: 'Applied', numeric: true, cell: (p) => <Amount money={p.amount} /> },
  ];
  const noteColumns: Array<Column<InvoiceCreditNoteRow>> = [
    {
      id: 'number',
      header: 'Credit note',
      primary: true,
      cell: (c) => {
        const href = links.creditNote(c.creditNoteId);
        return href ? <Link href={href} className="font-semibold text-accent-ink hover:underline">{c.number}</Link> : c.number;
      },
    },
    { id: 'kind', header: 'Kind', cell: (c) => c.kind },
    { id: 'date', header: 'Date', cell: (c) => formatDateOnly(c.issueDate) },
    { id: 'total', header: 'Total', numeric: true, cell: (c) => <Amount money={c.total} /> },
  ];

  return (
    <div className="flex flex-col gap-5">
      <PageHeader
        title={invoice.number ?? 'Invoice'}
        back={{ href: backHref, label: backLabel }}
        description={
          <span className="flex flex-wrap items-center gap-x-3 gap-y-1">
            <InvoiceStatusPill status={invoice.status} />
            {invoice.issueDate && <span>Issued {formatDateOnly(invoice.issueDate)}</span>}
            {invoice.dueOn && <span>Due {formatDateOnly(invoice.dueOn)}</span>}
          </span>
        }
        actions={
          <>
            {pdfAction}
            {actions}
          </>
        }
      />
      {(invoice.correctsInvoiceId || invoice.correctedByInvoiceId) && (
        <ul className="flex flex-col gap-1 text-sm">
          {invoice.correctsInvoiceId && <li>{correctionLink(invoice.correctsInvoiceId, 'corrects')}</li>}
          {invoice.correctedByInvoiceId && <li>{correctionLink(invoice.correctedByInvoiceId, 'correctedBy')}</li>}
        </ul>
      )}
      <div className="grid gap-4 md:grid-cols-2">
        <Section title="Seller">
          {invoice.seller ? (
            <address className="text-sm not-italic text-ink [overflow-wrap:anywhere]">
              <strong>{invoice.seller.legalName}</strong>
              <br />
              Tax registration: {invoice.seller.taxRegistrationNumber}
              <br />
              {invoice.seller.addressText}
              {invoice.seller.phone && <><br />{invoice.seller.phone}</>}
              {invoice.seller.email && <><br />{invoice.seller.email}</>}
            </address>
          ) : (
            <p className="text-sm text-ink-muted">Seller details are fixed when the invoice is issued.</p>
          )}
        </Section>
        <Section title="Bill to">
          {invoice.billTo ? (
            <address className="text-sm not-italic text-ink [overflow-wrap:anywhere]">
              <strong>{invoice.billTo.fullName}</strong>
              {invoice.billTo.addressText && <><br />{invoice.billTo.addressText}</>}
              {invoice.billTo.mobile && <><br />{invoice.billTo.mobile}</>}
              {invoice.billTo.email && <><br />{invoice.billTo.email}</>}
            </address>
          ) : (
            <p className="text-sm text-ink-muted">Not issued yet.</p>
          )}
        </Section>
      </div>

      <Section title="Lines">
        <ResponsiveTable
          caption="Invoice lines"
          columns={lineColumns}
          rows={invoice.lines}
          getRowId={(l) => l.lineId}
          minDesktopWidth={1100}
          dense
          emptyState={{ title: 'No lines.' }}
        />
        <dl className="ml-auto grid w-full max-w-sm grid-cols-[1fr_auto] gap-x-4 gap-y-1 text-sm">
          <dt className="text-ink-muted">Net</dt>
          <dd className="text-right"><Amount money={invoice.totals.net} /></dd>
          <dt className="text-ink-muted">Discounts</dt>
          <dd className="text-right"><Amount money={invoice.totals.discount} /></dd>
          <dt className="text-ink-muted">Tax</dt>
          <dd className="text-right"><Amount money={invoice.totals.tax} /></dd>
          <dt className="font-semibold text-ink">Total</dt>
          <dd className="text-right font-semibold"><Amount money={invoice.totals.total} /></dd>
          <dt className="text-ink-muted">Credited</dt>
          <dd className="text-right"><Amount money={invoice.credited} /></dd>
          <dt className="text-ink-muted">Paid</dt>
          <dd className="text-right"><Amount money={invoice.paid} /></dd>
          <dt className="font-semibold text-ink">Outstanding</dt>
          <dd className="text-right font-semibold"><Amount money={invoice.outstanding} /></dd>
        </dl>
        {invoice.loyaltyRedemption && (
          <p className="text-sm text-ink">
            Loyalty redemption: {invoice.loyaltyRedemption.points} points, worth{' '}
            <Amount money={invoice.loyaltyRedemption.value} className="font-semibold" />, shared across the lines above.
          </p>
        )}
        {invoice.notes && <p className="text-sm text-ink-muted [overflow-wrap:anywhere]">Notes: {invoice.notes}</p>}
        {invoice.seller?.invoiceFooter && (
          <p className="text-xs text-ink-muted [overflow-wrap:anywhere]">{invoice.seller.invoiceFooter}</p>
        )}
      </Section>

      <Section title="Payments applied">
        <ResponsiveTable
          caption="Payments applied"
          columns={paymentColumns}
          rows={invoice.payments}
          getRowId={(p) => p.paymentId}
          emptyState={{ title: 'No payments yet.' }}
        />
      </Section>
      {invoice.creditNotes.length > 0 && (
        <Section title="Credit notes">
          <ResponsiveTable
            caption="Credit notes"
            columns={noteColumns}
            rows={invoice.creditNotes}
            getRowId={(c) => c.creditNoteId}
            emptyState={{ title: 'No credit notes.' }}
          />
        </Section>
      )}
      {children}
    </div>
  );
}
