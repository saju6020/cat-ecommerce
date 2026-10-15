'use client';

import { useState } from 'react';
import { Amount } from '@/components/billing/Amount';
import { QueryBoundary } from '@/components/billing/QueryBoundary';
import { PrintSheet } from '@/components/print/PrintSheet';
import { Button } from '@/components/ui/button';
import type { Receipt } from '@/lib/api/pos';
import { tenderMethodLabel } from '@/lib/format/orders';
import { useReceipt } from '@/lib/hooks/usePos';

/**
 * `components/pos/ReceiptPage.tsx` — ⭐ HO-ORD-002 §11.11, US-ORD-16 AC1, ADR-FE-094 / HW-1. The counter
 * receipt as an 80 mm `PrintSheet`, rendered from the server's receipt data (lines with tax, totals,
 * payments, change). **Plain text** switches to the server's 48-column text for a narrow printer. The
 * browser and the OS printer driver do the printing; there is no driver code here.
 */
function Sheet({ receipt }: { receipt: Receipt }) {
  return (
    <div className="flex flex-col gap-2">
      <header className="border-b border-dashed border-ink pb-2 text-center">
        <p className="text-sm font-bold [overflow-wrap:anywhere]">{receipt.legalName ?? receipt.branchName}</p>
        <p>{receipt.branchName}</p>
        {receipt.branchAddress && <p className="[overflow-wrap:anywhere]">{receipt.branchAddress}</p>}
        {receipt.branchPhone && <p>{receipt.branchPhone}</p>}
        {receipt.taxRegistrationNumber && <p>Tax reg. {receipt.taxRegistrationNumber}</p>}
      </header>
      <p>
        {receipt.orderNumber} · {receipt.invoiceNumber}
        <br />
        {receipt.soldOnLocal} ({receipt.branchTimeZoneId})
        <br />
        Cashier: {receipt.cashier}
        {receipt.customerName ? <><br />Customer: {receipt.customerName}</> : null}
      </p>
      <table className="w-full border-collapse text-left">
        <caption className="sr-only">Items sold</caption>
        <thead>
          <tr className="border-y border-dashed border-ink">
            <th scope="col" className="py-0.5">Item</th>
            <th scope="col" className="py-0.5 text-right">Qty</th>
            <th scope="col" className="py-0.5 text-right">Total</th>
          </tr>
        </thead>
        <tbody>
          {receipt.lines.map((line, index) => (
            <tr key={`${line.description}-${index}`} className="align-top">
              <td className="py-0.5 pr-1 [overflow-wrap:anywhere]">
                {line.description}
                <br />
                <span className="text-[10px]">tax {line.taxRatePercent}%</span>
              </td>
              <td className="py-0.5 text-right tabular-nums">{line.quantity}</td>
              <td className="py-0.5 text-right tabular-nums"><Amount money={line.lineTotal} /></td>
            </tr>
          ))}
        </tbody>
      </table>
      <dl className="m-0 border-t border-dashed border-ink pt-1">
        {[
          ['Subtotal', receipt.totals.subtotal],
          ['Discounts', receipt.totals.lineDiscountTotal],
          ['Order discount', receipt.totals.orderDiscount],
          ['Tax', receipt.totals.taxTotal],
        ].map(([label, money]) => (
          <div key={String(label)} className="flex justify-between"><dt>{String(label)}</dt><dd className="m-0"><Amount money={money as Receipt['totals']['subtotal']} /></dd></div>
        ))}
        <div className="flex justify-between text-sm font-bold"><dt>Total</dt><dd className="m-0"><Amount money={receipt.totals.grandTotal} /></dd></div>
        {receipt.tenders.map((t, index) => (
          <div key={`${t.method}-${index}`} className="flex justify-between"><dt>{tenderMethodLabel(t.method)}</dt><dd className="m-0"><Amount money={t.amount} /></dd></div>
        ))}
        <div className="flex justify-between font-bold"><dt>Change</dt><dd className="m-0"><Amount money={receipt.changeDue} /></dd></div>
      </dl>
      <p className="text-center">Thank you.</p>
    </div>
  );
}

export function ReceiptPage({ orderId }: { orderId: string }) {
  const query = useReceipt(orderId);
  const [plain, setPlain] = useState(false);
  return (
    <PrintSheet
      title="Receipt"
      back={{ href: '/staff/pos', label: 'Point of sale' }}
      size="80mm"
      label="Receipt"
      actions={
        <Button variant="secondary" size="sm" aria-pressed={plain} onClick={() => setPlain((p) => !p)}>
          {plain ? 'Show formatted' : 'Plain text'}
        </Button>
      }
    >
      <QueryBoundary query={query} notFoundTitle="Receipt not found" loadingLabel="Loading the receipt…">
        {(receipt) =>
          plain ? <pre aria-label="Receipt as plain text" className="m-0 whitespace-pre-wrap font-mono text-[11px]">{receipt.text}</pre> : <Sheet receipt={receipt} />
        }
      </QueryBoundary>
    </PrintSheet>
  );
}
