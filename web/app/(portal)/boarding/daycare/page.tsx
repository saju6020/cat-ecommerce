import { DaycareBookingForm } from '@/components/boarding/daycare/DaycareBookingForm';

/** `/boarding/daycare` — HO-BRD-002 §11.12 (US-BRD-21): book daycare. */
export default function DaycareBookingPage() {
  return (
    <div className="flex flex-col gap-4">
      <h1 className="font-display text-2xl font-semibold text-ink">Book daycare</h1>
      <DaycareBookingForm />
    </div>
  );
}
