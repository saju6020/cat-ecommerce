'use client';

import Link from 'next/link';
import { useEffect, useRef } from 'react';
import { Amount } from '@/components/billing/Amount';
import { Button, buttonVariants } from '@/components/ui/button';
import type { CounterSale } from '@/lib/api/pos';

/**
 * `components/pos/PosSaleComplete.tsx` — HO-ORD-002 §11.11. The sale is recorded: order and invoice
 * number, the **change due** in large type (the server's figure), **Print receipt** (opens the receipt
 * route; focused so the keyboard path continues) and **New sale** (clears the basket, focus back to scan).
 */
export function PosSaleComplete({ sale, onNewSale }: { sale: CounterSale; onNewSale: () => void }) {
  const printRef = useRef<HTMLAnchorElement>(null);
  useEffect(() => printRef.current?.focus(), []);
  return (
    <section aria-labelledby="pos-done" className="flex flex-col gap-4 rounded-[var(--radius-md)] border-2 border-accent bg-accent-soft p-5">
      <h2 id="pos-done" className="font-display text-xl font-semibold text-accent-ink">Sale complete — {sale.orderNumber}</h2>
      <dl className="m-0 grid grid-cols-[auto_1fr] gap-x-4 gap-y-1 text-sm">
        <dt className="text-ink-muted">Invoice</dt>
        <dd className="m-0">{sale.invoiceNumber}</dd>
        <dt className="text-ink-muted">Total</dt>
        <dd className="m-0 font-semibold"><Amount money={sale.grandTotal} /></dd>
      </dl>
      <p className="text-2xl font-bold text-ink" role="status">
        Change due: <Amount money={sale.changeDue} />
      </p>
      <div className="flex flex-wrap gap-2">
        <Link ref={printRef} href={`/staff/pos/sales/${sale.orderId}/receipt`} className={buttonVariants({})}>
          Print receipt
        </Link>
        <Button variant="secondary" onClick={onNewSale}>New sale</Button>
      </div>
    </section>
  );
}
