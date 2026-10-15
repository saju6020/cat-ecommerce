import Link from 'next/link';
import { FilePenLine, HandCoins, Ticket, Undo2 } from 'lucide-react';
import { Button, buttonVariants } from '@/components/ui/button';
import type { InvoiceDetail } from '@/lib/api/billing-staff';

/**
 * `components/billing/InvoiceActions.tsx` — HO-BIL-002 §11.3, ADR-FE-083. The actions of an issued
 * invoice, **only what the server lists** in `allowedActions`: *Record payment*, *Apply credit*,
 * *Credit note*. *Corrected re-issue* appears once a credit note exists and no corrected invoice does
 * (the server re-checks it). There is no *Edit* anywhere: an issued invoice is corrected, never changed.
 */
export function InvoiceActions({
  invoice,
  onApplyCredit,
  onCorrectedReissue,
  busy,
}: {
  invoice: InvoiceDetail;
  onApplyCredit: () => void;
  onCorrectedReissue: (creditNoteId: string) => void;
  busy?: boolean;
}) {
  const allowed = invoice.allowedActions;
  const latestNote = invoice.creditNotes[invoice.creditNotes.length - 1];
  return (
    <>
      {allowed.includes('recordPayment') && (
        <Link
          href={`/staff/billing/payments/new?customerId=${invoice.customerId}&invoiceId=${invoice.invoiceId}`}
          className={buttonVariants({ variant: 'primary', size: 'sm' })}
        >
          <HandCoins size={14} aria-hidden="true" />
          Record payment
        </Link>
      )}
      {allowed.includes('applyCredit') && (
        <Button variant="secondary" size="sm" onClick={onApplyCredit}>
          <Ticket size={14} aria-hidden="true" />
          Apply credit
        </Button>
      )}
      {allowed.includes('creditNote') && (
        <Link
          href={`/staff/billing/invoices/${invoice.invoiceId}/credit`}
          className={buttonVariants({ variant: 'secondary', size: 'sm' })}
        >
          <Undo2 size={14} aria-hidden="true" />
          Credit note
        </Link>
      )}
      {latestNote && !invoice.correctedByInvoiceId && (
        <Button variant="secondary" size="sm" disabled={busy} onClick={() => onCorrectedReissue(latestNote.creditNoteId)}>
          <FilePenLine size={14} aria-hidden="true" />
          Corrected re-issue
        </Button>
      )}
    </>
  );
}
