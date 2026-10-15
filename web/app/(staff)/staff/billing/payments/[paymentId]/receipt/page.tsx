'use client';

import { Suspense, use } from 'react';
import { ReceiptPrintView } from '@/components/billing/ReceiptPrintView';

/** `/staff/billing/payments/[paymentId]/receipt` — HO-BIL-002 §11.4 (ADR-FE-085): the print view. */
export default function ReceiptPage({ params }: { params: Promise<{ paymentId: string }> }) {
  const { paymentId } = use(params);
  return (
    <Suspense fallback={null}>
      <ReceiptPrintView paymentId={paymentId} />
    </Suspense>
  );
}
