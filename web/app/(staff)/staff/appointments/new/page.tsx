import { Suspense } from 'react';
import { LoadingState } from '@/components/common/LoadingState';
import { StaffBookingView } from './StaffBookingView';

/** `/staff/appointments/new` — HO-APT-002 §11.3 (US-CLN-07, 08): book by phone. Prefilled from
 *  `?customerId&catId` (the cat page and customer page links). */
export default function StaffBookingPage() {
  return (
    <Suspense fallback={<LoadingState variant="card" rows={5} label="Loading…" />}>
      <StaffBookingView />
    </Suspense>
  );
}
