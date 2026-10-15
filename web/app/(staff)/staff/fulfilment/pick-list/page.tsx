import { Suspense } from 'react';
import { PickListPage } from '@/components/fulfilment/PickListPage';

/** `/staff/fulfilment/pick-list` — HO-ORD-002 §11.10 (US-ORD-10 AC1): the print route. */
export default function PickListRoute() {
  return (
    <Suspense fallback={null}>
      <PickListPage />
    </Suspense>
  );
}
