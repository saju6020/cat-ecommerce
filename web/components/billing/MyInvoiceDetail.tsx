'use client';

import Link from 'next/link';
import { Download } from 'lucide-react';
import { buttonVariants } from '@/components/ui/button';
import type { BankAccount } from '@/lib/api/billing-staff';
import { myInvoicePdfUrl, type HowToPay as HowToPayData } from '@/lib/api/my-billing';
import { moneySign } from '@/lib/money';
import { useMyInvoice } from '@/lib/hooks/billing/useMyBilling';
import { InvoiceDocumentView } from './InvoiceDocumentView';
import { QueryBoundary } from './QueryBoundary';
import { Section } from './PageHeader';

/**
 * `components/billing/MyInvoiceDetail.tsx` — HO-BIL-002 §11.11 (US-BIL-02, customer). The same
 * `InvoiceDocumentView` in read-only customer mode with **Download PDF** (the server's rendering),
 * and `HowToPay` — the issuing branch's bank accounts from the response and the server's instruction
 * to use the invoice number as the payment reference, or to pay at the clinic. **No pay-online
 * button** exists (SRS Phase 5). Someone else's invoice is a `404` ("not found"), never an error.
 */
function BankAccountCard({ account }: { account: BankAccount }) {
  return (
    <li className="rounded-[var(--radius-sm)] border border-border bg-surface p-3 text-sm">
      <p className="font-semibold text-ink">{account.bankName}</p>
      <dl className="mt-1 grid grid-cols-[auto_1fr] gap-x-3 gap-y-0.5 [overflow-wrap:anywhere]">
        <dt className="text-ink-muted">Account name</dt>
        <dd>{account.accountName}</dd>
        <dt className="text-ink-muted">Account number</dt>
        <dd>{account.accountNumber}</dd>
        {account.branchName && (
          <>
            <dt className="text-ink-muted">Branch</dt>
            <dd>{account.branchName}</dd>
          </>
        )}
        {account.routingNumber && (
          <>
            <dt className="text-ink-muted">Routing number</dt>
            <dd>{account.routingNumber}</dd>
          </>
        )}
      </dl>
    </li>
  );
}

export function HowToPay({ howToPay, invoiceNumber }: { howToPay: HowToPayData; invoiceNumber: string | null }) {
  return (
    <Section title="How to pay" description="Pay by bank transfer, or at the clinic by cash or card.">
      <p className="text-sm font-semibold text-ink">{howToPay.referenceInstruction}</p>
      {invoiceNumber && (
        <p className="text-sm text-ink">
          Payment reference: <strong>{invoiceNumber}</strong>
        </p>
      )}
      {howToPay.bankAccounts.length > 0 && (
        <ul aria-label="Bank accounts" className="grid gap-3 sm:grid-cols-2">
          {howToPay.bankAccounts.map((account) => (
            <BankAccountCard key={account.accountNumber} account={account} />
          ))}
        </ul>
      )}
      <p className="text-sm text-ink-muted">Or pay at the clinic by cash or card.</p>
    </Section>
  );
}

function CorrectionLink({ invoiceId, kind }: { invoiceId: string; kind: 'corrects' | 'correctedBy' }) {
  const other = useMyInvoice(invoiceId);
  return (
    <>
      {kind === 'corrects' ? 'Corrects ' : 'Corrected by '}
      <Link href={`/portal/billing/invoices/${invoiceId}`} className="font-semibold text-accent-ink hover:underline">
        {other.data?.invoice.number ?? 'another invoice'}
      </Link>
    </>
  );
}

export function MyInvoiceDetail({ invoiceId }: { invoiceId: string }) {
  const query = useMyInvoice(invoiceId);
  return (
    <QueryBoundary
      query={query}
      notFoundTitle="Invoice not found"
      notFoundDescription="We can’t find that invoice on your account. Check the link, or go back to your invoices."
      loadingLabel="Loading your invoice…"
    >
      {({ invoice, howToPay }) => (
        <InvoiceDocumentView
          invoice={invoice}
          pdfAction={
            <a href={myInvoicePdfUrl(invoice.invoiceId)} className={buttonVariants({ variant: 'secondary', size: 'sm' })} download>
              <Download size={14} aria-hidden="true" />
              Download PDF
            </a>
          }
          backHref="/portal/billing"
          backLabel="My billing"
          links={{ payment: (id) => `/portal/billing/payments/${id}`, creditNote: () => null }}
          correctionLink={(id, kind) => <CorrectionLink invoiceId={id} kind={kind} />}
        >
          {moneySign(invoice.outstanding.amount) === 'positive' && (
            <HowToPay howToPay={howToPay} invoiceNumber={invoice.number} />
          )}
        </InvoiceDocumentView>
      )}
    </QueryBoundary>
  );
}
