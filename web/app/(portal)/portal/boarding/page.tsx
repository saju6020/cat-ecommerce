import { MyStaysList } from '@/components/boarding/stay/MyStaysList';

/** `/portal/boarding` — HO-BRD-002 §11.2 (US-BRD-04 AC2, 06): my stays, enquiries to resume, waitlist. */
export default function MyBoardingPage() {
  return (
    <div className="flex flex-col gap-6">
      <h1 className="font-display text-2xl font-semibold text-ink">Boarding</h1>
      <MyStaysList />
    </div>
  );
}
