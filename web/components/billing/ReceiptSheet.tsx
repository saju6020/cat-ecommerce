import type { PaymentReceipt } from '@/lib/api/billing-staff';
import { formatDateOnly, formatInZone } from '@/lib/format/time';
import { Amount } from './Amount';
import { methodLabel } from './labels';
import { cn } from '@/lib/utils';

/** The print stylesheet (A4 or 80 mm): hides everything except the receipt (a plain static string). */
export function receiptPrintCss(compact: boolean): string {
  return `
@media print {
  @page { size: ${compact ? '80mm auto' : 'A4'}; margin: ${compact ? '3mm' : '12mm'}; }
  body * { visibility: hidden !important; }
  .receipt-sheet, .receipt-sheet * { visibility: visible !important; }
  .receipt-sheet { position: absolute; left: 0; top: 0; width: 100% !important; max-width: none !important; box-shadow: none !important; }
}`;
}

/**
 * `components/billing/ReceiptSheet.tsx` — HO-BIL-002 §11.4, ADR-FE-085. A receipt as **text only**,
 * built from the receipt endpoint's data: the seller header (the branch's billing profile), receipt
 * number, customer, method, reference, amount, date, what it was applied to, who recorded it. No HTML
 * from the server and no images. `compact` is the 80 mm variant for a receipt printer driven through
 * the browser's print dialog (thermal ESC/POS is POS's, HO-ORD-002).
 */
export function ReceiptSheet({ receipt, compact }: { receipt: PaymentReceipt; compact?: boolean }) {
  return (
    <article
      aria-label={receipt.receiptNumber ? `Receipt ${receipt.receiptNumber}` : 'Receipt'}
      className={cn(
        'receipt-sheet mx-auto bg-surface p-6 text-ink',
        compact ? 'w-[80mm] max-w-full text-[12px] leading-snug' : 'w-full max-w-[210mm] text-sm',
      )}
    >
      <header className="mb-3 border-b border-border-strong pb-3 text-center">
        <p className="text-base font-semibold [overflow-wrap:anywhere]">{receipt.seller?.legalName ?? receipt.branchName}</p>
        {receipt.seller && (
          <>
            <p className="[overflow-wrap:anywhere]">{receipt.seller.addressText}</p>
            <p>Tax registration: {receipt.seller.taxRegistrationNumber}</p>
            {receipt.seller.phone && <p>{receipt.seller.phone}</p>}
          </>
        )}
      </header>
      <h2 className="mb-2 text-center text-base font-semibold">
        {receipt.status === 'Reversed' ? 'Receipt (reversed)' : 'Payment receipt'}
      </h2>
      <dl className="grid grid-cols-[auto_1fr] gap-x-4 gap-y-1">
        <dt className="text-ink-muted">Receipt</dt>
        <dd className="font-semibold">{receipt.receiptNumber ?? 'Awaiting approval'}</dd>
        <dt className="text-ink-muted">Customer</dt>
        <dd className="[overflow-wrap:anywhere]">{receipt.customerName}</dd>
        <dt className="text-ink-muted">Method</dt>
        <dd>{methodLabel(receipt.method)}</dd>
        {receipt.reference && (
          <>
            <dt className="text-ink-muted">Reference</dt>
            <dd className="[overflow-wrap:anywhere]">{receipt.reference}</dd>
          </>
        )}
        <dt className="text-ink-muted">Amount</dt>
        <dd className="font-semibold">
          <Amount money={receipt.amount} />
        </dd>
        <dt className="text-ink-muted">Date received</dt>
        <dd>{formatDateOnly(receipt.receivedOn)}</dd>
        {receipt.acceptedOn && (
          <>
            <dt className="text-ink-muted">Accepted</dt>
            <dd>{formatInZone(receipt.acceptedOn, receipt.branchTimeZoneId)}</dd>
          </>
        )}
        <dt className="text-ink-muted">Applied to</dt>
        <dd>
          {receipt.appliedTo.length === 0 ? (
            'Not applied to an invoice (kept as credit)'
          ) : (
            <ul>
              {receipt.appliedTo.map((line) => (
                <li key={line} className="[overflow-wrap:anywhere]">{line}</li>
              ))}
            </ul>
          )}
        </dd>
        <dt className="text-ink-muted">Recorded by</dt>
        <dd>{receipt.recordedByName}</dd>
      </dl>
      <p className="mt-3 text-center text-xs text-ink-muted">{receipt.branchName}</p>
    </article>
  );
}
