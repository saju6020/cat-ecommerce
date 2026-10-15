'use client';

import { useState } from 'react';
import type { InvoiceDetail } from '@/lib/api/billing-staff';
import { useInvoice } from '@/lib/hooks/billing/useBillingQueries';
import { CreditNoteWizard } from './CreditNoteWizard';
import { PageHeader } from './PageHeader';
import { QueryBoundary } from './QueryBoundary';

/**
 * The page body for `/staff/billing/invoices/[id]/credit` — the wizard, offered only when the server
 * lists `creditNote` in the invoice's `allowedActions`; any other state is explained, not hidden.
 */
export function CreditNotePage({ invoiceId }: { invoiceId: string }) {
  const invoice = useInvoice(invoiceId);
  // The wizard keeps the invoice it started with: issuing the credit note refetches the invoice into
  // a state that no longer allows one, and the result screen must not vanish with it.
  const [started, setStarted] = useState<InvoiceDetail | null>(null);
  if (!started && invoice.data?.allowedActions.includes('creditNote')) setStarted(invoice.data);
  return (
    <QueryBoundary
      query={invoice}
      notFoundTitle="Invoice not found"
      loadingLabel="Loading the invoice…"
    >
      {(data) => (
        <div className="flex flex-col gap-5">
          <PageHeader
            title={`Credit note for ${data.number ?? 'this invoice'}`}
            back={{ href: `/staff/billing/invoices/${invoiceId}`, label: 'Back to the invoice' }}
          />
          {started ? (
            <CreditNoteWizard invoice={started} />
          ) : (
            <p role="status" className="text-sm text-ink">
              A credit note cannot be issued for an invoice in this state.
            </p>
          )}
        </div>
      )}
    </QueryBoundary>
  );
}
