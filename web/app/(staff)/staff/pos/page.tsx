import { Suspense } from 'react';
import { PosScreen } from '@/components/pos/PosScreen';

/** `/staff/pos` — HO-ORD-002 §11.11 (US-ORD-16): the counter sale. `?customerId=` pre-attaches a customer. */
export default function PosPage() {
  return (
    <Suspense fallback={null}>
      <PosScreen />
    </Suspense>
  );
}
