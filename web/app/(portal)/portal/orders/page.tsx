'use client';

import { Suspense } from 'react';
import { MyOrdersList } from '@/components/orders/MyOrdersList';

/** `/portal/orders` — HO-ORD-002 §11.6: my orders (filter by status, paged, in the URL). */
export default function MyOrdersPage() {
  return (
    <Suspense fallback={null}>
      <MyOrdersList />
    </Suspense>
  );
}
