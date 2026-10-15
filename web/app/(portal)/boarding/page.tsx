import { Suspense } from 'react';
import { LoadingState } from '@/components/common/LoadingState';
import { BookingFlow } from '@/components/boarding/booking/BookingFlow';

/**
 * `/boarding` — HO-BRD-002 §8.1/§11.1 (US-BRD-02…07): book a stay for one or several cats. Inside
 * `(portal)`, so a visitor signs in first and comes back with the search (`?next=`). The search is
 * in the URL, the enquiry on the server (ADR-FE-099). There is no payment step anywhere
 * (ADR-FE-106): the deposit is paid at the clinic or by bank transfer and recorded by staff.
 */
export default function BoardingBookingPage() {
  return (
    <div className="flex flex-col gap-4">
      <h1 className="font-display text-2xl font-semibold text-ink">Book a stay</h1>
      <Suspense fallback={<LoadingState variant="card" rows={5} label="Loading…" />}>
        <BookingFlow />
      </Suspense>
    </div>
  );
}
