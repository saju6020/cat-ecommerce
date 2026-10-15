'use client';

import { Button } from '@/components/ui/button';
import { QueryGate } from '@/components/boarding/shared/QueryGate';
import { addDaysIso } from '@/lib/format/appointment';
import { formatShortDate } from '@/lib/format/boarding';
import { todayInZone } from '@/lib/format/time';
import { useBranchTimeZone } from '@/lib/hooks/useBranchTimeZone';
import { useFrozenWhile } from '@/lib/hooks/useFrozenWhile';
import { useMediaQuery } from '@/lib/hooks/useMediaQuery';
import { useBoardingOccupancy } from '@/lib/hooks/useStaffBoarding';
import { useUrlParams } from '@/lib/hooks/useUrlParams';
import { OccupancyDayList, OccupancyGrid } from './OccupancyGrid';

const DAYS = 14; // OQ-1

/** `components/boarding/board/OccupancyBoard.tsx` — HO-BRD-002 §11.10, US-BRD-02/15: 14 days from
 *  `?from=`, paged by week; the grid on wide screens and the day list below 768 px. Refreshes every
 *  30 s (ADR-FE-101). */
export function OccupancyBoard() {
  const url = useUrlParams();
  const zone = useBranchTimeZone();
  const from = url.get('from') || todayInZone(zone);
  const to = addDaysIso(from, DAYS - 1);
  const query = useBoardingOccupancy(from, to);
  const narrow = useMediaQuery('(max-width: 767px)');
  const shown = useFrozenWhile(query.data, false);

  return (
    <div className="flex flex-col gap-4">
      <div className="flex flex-wrap items-center gap-3">
        <Button variant="secondary" onClick={() => url.set({ from: addDaysIso(from, -7) })}>
          Previous week
        </Button>
        <p className="text-sm font-semibold text-ink">
          {formatShortDate(from)} – {formatShortDate(to)} · {zone}
        </p>
        <Button variant="secondary" onClick={() => url.set({ from: addDaysIso(from, 7) })}>
          Next week
        </Button>
        <Button variant="ghost" onClick={() => url.set({ from: undefined })}>
          Today
        </Button>
      </div>
      <QueryGate
        query={{ ...query, data: shown }}
        loadingLabel="Loading the board…"
        loadingRows={6}
      >
        {(data) => (narrow ? <OccupancyDayList data={data} /> : <OccupancyGrid data={data} />)}
      </QueryGate>
    </div>
  );
}
