import { Suspense } from 'react';
import { StaffOrderQueue } from '@/components/orders/StaffOrderQueue';

/** `/staff/orders` — HO-ORD-002 §11.8 (US-ORD-09): the order queue; filters live in the URL. */
export default function StaffOrdersPage() {
  return (
    <Suspense fallback={null}>
      <StaffOrderQueue />
    </Suspense>
  );
}
