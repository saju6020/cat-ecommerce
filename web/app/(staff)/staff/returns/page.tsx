import { Suspense } from 'react';
import { ReturnsList } from '@/components/returns/ReturnsList';

/** `/staff/returns` — HO-ORD-002 §11.10 (US-ORD-13): returns at the active branch. */
export default function ReturnsPage() {
  return (
    <Suspense fallback={null}>
      <ReturnsList />
    </Suspense>
  );
}
