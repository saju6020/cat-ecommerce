import { StayList } from '@/components/boarding/stay/StayList';

/** `/staff/boarding/stays` — HO-BRD-002 §11.8: every stay, by status. */
export default function BoardingStaysPage() {
  return (
    <div className="flex flex-col gap-4">
      <h1 className="font-display text-2xl font-semibold text-ink">Stays</h1>
      <StayList />
    </div>
  );
}
