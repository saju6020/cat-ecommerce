'use client';

import { Printer } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { useMyReceipt } from '@/lib/hooks/billing/useMyBilling';
import { PageHeader } from './PageHeader';
import { QueryBoundary } from './QueryBoundary';
import { ReceiptSheet, receiptPrintCss } from './ReceiptSheet';

/**
 * `components/billing/MyReceipt.tsx` — HO-BIL-002 §11.11 (US-BIL-11, customer). The receipt of one of
 * the customer's own payments — text only, from the receipt data — with a **Print** button (the
 * browser's print dialog; only the receipt reaches the paper). Someone else's payment is a `404`.
 */
export function MyReceipt({ paymentId }: { paymentId: string }) {
  const receipt = useMyReceipt(paymentId);
  return (
    <div className="flex flex-col gap-5">
      <style>{receiptPrintCss(false)}</style>
      <PageHeader
        title="Receipt"
        back={{ href: '/portal/billing', label: 'My billing' }}
        actions={
          <Button onClick={() => window.print()}>
            <Printer size={14} aria-hidden="true" />
            Print
          </Button>
        }
      />
      <QueryBoundary
        query={receipt}
        notFoundTitle="Receipt not found"
        notFoundDescription="We can’t find that receipt on your account."
        loadingLabel="Loading your receipt…"
      >
        {(data) => (
          <div className="rounded-[var(--radius-md)] border border-border bg-surface-sunken p-4">
            <ReceiptSheet receipt={data} />
          </div>
        )}
      </QueryBoundary>
    </div>
  );
}
