'use client';

import { use } from 'react';
import { MyReceipt } from '@/components/billing/MyReceipt';

/** /portal/billing/payments/[paymentId] - HO-BIL-002 section 11.11: the receipt. */
export default function PortalReceiptPage({ params }: { params: Promise<{ paymentId: string }> }) {
  const { paymentId } = use(params);
  return <MyReceipt paymentId={paymentId} />;
}
