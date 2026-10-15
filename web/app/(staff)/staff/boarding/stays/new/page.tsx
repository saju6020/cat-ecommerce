import { Suspense } from 'react';
import { StaffStayBookingForm } from '@/components/boarding/stay/StaffStayBookingForm';

/** `/staff/boarding/stays/new` — HO-BRD-002 §11.4 (US-BRD-02…07): book a stay for a customer. */
export default function NewBoardingStayPage() {
  return (
    <div className="flex flex-col gap-4">
      <h1 className="font-display text-2xl font-semibold text-ink">New stay</h1>
      <Suspense fallback={null}>
        <StaffStayBookingForm />
      </Suspense>
    </div>
  );
}
