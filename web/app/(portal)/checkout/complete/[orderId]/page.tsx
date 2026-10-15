'use client';

import { use } from 'react';
import { OrderPlacedScreen } from '@/components/checkout/OrderPlaced';

/** `/checkout/complete/[orderId]` — HO-ORD-002 §11.5: placed; transfer instructions when it is one. */
export default function CheckoutCompletePage({ params }: { params: Promise<{ orderId: string }> }) {
  const { orderId } = use(params);
  return <OrderPlacedScreen orderId={orderId} />;
}
