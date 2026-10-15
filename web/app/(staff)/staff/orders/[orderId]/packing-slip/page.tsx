'use client';

import { use } from 'react';
import { PackingSlipPage } from '@/components/fulfilment/PackingSlipPage';

/** `/staff/orders/[orderId]/packing-slip` — HO-ORD-002 §11.10 (US-ORD-10 AC2): the print route. */
export default function PackingSlipRoute({ params }: { params: Promise<{ orderId: string }> }) {
  const { orderId } = use(params);
  return <PackingSlipPage orderId={orderId} />;
}
