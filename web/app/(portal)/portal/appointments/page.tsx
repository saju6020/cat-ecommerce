import { Suspense } from 'react';
import { LoadingState } from '@/components/common/LoadingState';
import { MyAppointmentsView } from './MyAppointmentsView';

/** `/portal/appointments` — HO-APT-002 §8.1/§11.2 (US-CLN-04, 09): upcoming, past and the waitlist. */
export default function MyAppointmentsPage() {
  return (
    <Suspense fallback={<LoadingState variant="card" rows={4} label="Loading appointments…" />}>
      <MyAppointmentsView />
    </Suspense>
  );
}
