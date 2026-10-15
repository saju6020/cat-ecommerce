'use client';

import { use } from 'react';
import { ReceiptPage } from '@/components/pos/ReceiptPage';

/** `/staff/pos/sales/[orderId]/receipt` — HO-ORD-002 §11.11 (US-ORD-16 AC1): the 80 mm print route. */
export default function ReceiptRoute({ params }: { params: Promise<{ orderId: string }> }) {
  const { orderId } = use(params);
  return <ReceiptPage orderId={orderId} />;
}
