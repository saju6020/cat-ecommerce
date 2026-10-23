'use client';

import { use } from 'react';
import { useSearchParams } from 'next/navigation';
import { MyOrderDetail } from '@/components/orders/MyOrderDetail';

/** `/portal/orders/[orderId]` — HO-ORD-002 §11.6: detail, cancel, transfer reference, track, re-order.
 *  Owner, 2026-10-19: checkout lands here (`?placed=1`), which adds the "order placed" thank-you. */
export default function MyOrderPage({ params }: { params: Promise<{ orderId: string }> }) {
  const { orderId } = use(params);
  const justPlaced = useSearchParams().get('placed') === '1';
  return <MyOrderDetail orderId={orderId} justPlaced={justPlaced} />;
}
