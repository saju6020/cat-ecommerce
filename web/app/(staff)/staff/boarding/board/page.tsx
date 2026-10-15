import { Suspense } from 'react';
import { OccupancyBoard } from '@/components/boarding/board/OccupancyBoard';

/** `/staff/boarding/board` — HO-BRD-002 §11.10 (US-BRD-02, 15): units × days. */
export default function OccupancyBoardPage() {
  return (
    <div className="flex flex-col gap-4">
      <h1 className="font-display text-2xl font-semibold text-ink">Occupancy</h1>
      <Suspense fallback={null}>
        <OccupancyBoard />
      </Suspense>
    </div>
  );
}
