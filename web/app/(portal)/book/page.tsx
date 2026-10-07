import { Suspense } from 'react';
import { LoadingState } from '@/components/common/LoadingState';
import { BookingFlow } from './BookingFlow';

/**
 * `/book` — HO-APT-002 §8.1/§11.1 (US-CLN-02, 03, 09): book an appointment online. Inside
 * `(portal)`, so a visitor from a reminder link signs in first and returns here with the booking
 * parameters (`?next=` — `/book` is on the customer sign-in allow-list). There is no payment step
 * anywhere (ADR-073): a booking is paid at the clinic.
 */
export default function BookPage() {
  return (
    <div className="flex flex-col gap-2">
      <h1 className="font-display text-2xl font-semibold text-ink">Book an appointment</h1>
      <Suspense fallback={<LoadingState variant="card" rows={5} label="Loading…" />}>
        <BookingFlow />
      </Suspense>
    </div>
  );
}
