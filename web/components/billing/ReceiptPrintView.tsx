'use client';

import Link from 'next/link';
import { useSearchParams } from 'next/navigation';
import { Printer } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { usePaymentReceipt } from '@/lib/hooks/billing/useBillingQueries';
import { PageHeader } from './PageHeader';
import { QueryBoundary } from './QueryBoundary';
import { ReceiptSheet, receiptPrintCss } from './ReceiptSheet';

/**
 * `components/billing/ReceiptPrintView.tsx` — HO-BIL-002 §11.4, ADR-FE-085 (OQ-2: A4 and 80 mm).
 * Route `/staff/billing/payments/[id]/receipt`, `?width=80` for the narrow variant. The receipt is
 * text built from the receipt endpoint; **Print** calls `window.print()`, and the print stylesheet
 * below hides every other part of the page so only the receipt reaches the paper, at A4 or 80 mm.
 */
export function ReceiptPrintView({ paymentId }: { paymentId: string }) {
  const compact = useSearchParams().get('width') === '80';
  const receipt = usePaymentReceipt(paymentId);

  return (
    <div className="flex flex-col gap-5">
      <style>{receiptPrintCss(compact)}</style>
      <PageHeader
        title="Receipt"
        back={{ href: `/staff/billing/payments/${paymentId}`, label: 'Back to the payment' }}
        actions={
          <>
            <Link
              href={`/staff/billing/payments/${paymentId}/receipt${compact ? '' : '?width=80'}`}
              className="text-sm font-semibold text-accent-ink hover:underline"
            >
              {compact ? 'Use A4 layout' : 'Use 80 mm layout'}
            </Link>
            <Button onClick={() => window.print()}>
              <Printer size={14} aria-hidden="true" />
              Print
            </Button>
          </>
        }
      />
      <QueryBoundary query={receipt} notFoundTitle="Receipt not found" loadingLabel="Loading the receipt…">
        {(data) => (
          <div className="rounded-[var(--radius-md)] border border-border bg-surface-sunken p-4">
            <ReceiptSheet receipt={data} compact={compact} />
          </div>
        )}
      </QueryBoundary>
    </div>
  );
}
