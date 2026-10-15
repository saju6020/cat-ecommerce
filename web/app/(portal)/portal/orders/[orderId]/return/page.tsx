'use client';

import { use } from 'react';
import { ReturnRequestScreen } from '@/components/orders/ReturnRequestForm';

/** `/portal/orders/[orderId]/return` — HO-ORD-002 §11.7: request a return (reason and condition per line). */
export default function ReturnRequestPage({ params }: { params: Promise<{ orderId: string }> }) {
  const { orderId } = use(params);
  return <ReturnRequestScreen orderId={orderId} />;
}
