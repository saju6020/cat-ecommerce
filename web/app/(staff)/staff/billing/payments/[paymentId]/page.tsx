'use client';

import { use } from 'react';
import { PaymentDetailScreen } from '@/components/billing/PaymentDetailScreen';

/** `/staff/billing/payments/[paymentId]` — HO-BIL-002 §11.4. */
export default function PaymentPage({ params }: { params: Promise<{ paymentId: string }> }) {
  const { paymentId } = use(params);
  return <PaymentDetailScreen paymentId={paymentId} />;
}
