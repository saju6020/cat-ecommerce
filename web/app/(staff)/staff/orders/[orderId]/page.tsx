'use client';

import { use } from 'react';
import { StaffOrderDetail } from '@/components/orders/StaffOrderDetail';

/** `/staff/orders/[orderId]` — HO-ORD-002 §11.8: the order and every staff action the server allows. */
export default function StaffOrderPage({ params }: { params: Promise<{ orderId: string }> }) {
  const { orderId } = use(params);
  return <StaffOrderDetail orderId={orderId} />;
}
