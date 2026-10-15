'use client';

import { use } from 'react';
import { MyOrderDetail } from '@/components/orders/MyOrderDetail';

/** `/portal/orders/[orderId]` — HO-ORD-002 §11.6: detail, cancel, transfer reference, track, re-order. */
export default function MyOrderPage({ params }: { params: Promise<{ orderId: string }> }) {
  const { orderId } = use(params);
  return <MyOrderDetail orderId={orderId} />;
}
