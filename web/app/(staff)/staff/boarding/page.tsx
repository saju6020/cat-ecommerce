import { BoardingDashboard } from '@/components/boarding/stay/BoardingDashboard';

/** `/staff/boarding` — HO-BRD-002 §11.5 (US-BRD-19): the boarding dashboard. */
export default function BoardingDashboardPage() {
  return (
    <div className="flex flex-col gap-4">
      <h1 className="font-display text-2xl font-semibold text-ink">Boarding</h1>
      <BoardingDashboard />
    </div>
  );
}
