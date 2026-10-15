'use client';

import { useState } from 'react';
import { Button } from '@/components/ui/button';
import { EmptyState } from '@/components/common/EmptyState';
import { FormFieldRow } from '@/components/cat/FormFieldRow';
import { CatHeaderCompact } from '@/components/boarding/shared/CatHeaderCompact';
import { FormErrorAlert } from '@/components/boarding/shared/FormBits';
import { QueryGate } from '@/components/boarding/shared/QueryGate';
import { describeBoardingError } from '@/lib/api/errors';
import type { DaycareAction } from '@/lib/api/staff-boarding';
import { formatMoney } from '@/lib/format/money';
import { todayInZone } from '@/lib/format/time';
import { useBranchTimeZone } from '@/lib/hooks/useBranchTimeZone';
import { useDaycareAction, useDaycareDayList } from '@/lib/hooks/useStaffBoarding';

/** `DaycareDayList` — HO-BRD-002 §11.12, US-BRD-21: the day's visits with check in, check out, cancel
 *  and no-show; the charge is shown after check-out (from the server). A daycare visit occupies no
 *  unit, so it never appears on the occupancy board. */
export function DaycareDayList() {
  const zone = useBranchTimeZone();
  const [date, setDate] = useState('');
  const day = date || todayInZone(zone);
  const query = useDaycareDayList(day);
  const act = useDaycareAction();
  const [error, setError] = useState<string | null>(null);

  async function run(visitId: string, action: DaycareAction) {
    setError(null);
    try {
      await act.mutateAsync({ visitId, action });
    } catch (e) {
      setError(describeBoardingError(e));
    }
  }

  return (
    <div className="flex flex-col gap-4">
      <FormFieldRow
        id="dc-day"
        label="Date"
        type="date"
        value={day}
        onChange={(e) => setDate(e.target.value)}
        className="max-w-48"
      />
      <FormErrorAlert message={error} />
      <QueryGate query={query} loadingLabel="Loading the day…">
        {(data) =>
          !data.offered ? (
            <EmptyState title="Daycare isn’t offered at this branch." />
          ) : data.visits.length === 0 ? (
            <EmptyState
              title="No daycare visits that day."
              description={`Capacity is ${data.capacity} a day.`}
            />
          ) : (
            <ul aria-label="Daycare visits" className="flex flex-col gap-3">
              {data.visits.map((v) => (
                <li
                  key={v.id}
                  className="flex flex-col gap-2 rounded-[var(--radius-md)] border border-border bg-surface p-4"
                >
                  <div className="flex flex-wrap items-center justify-between gap-2">
                    <CatHeaderCompact
                      name={v.catName}
                      isHighRisk={v.catIsHighRisk}
                      handlingNote={v.handlingNote}
                    />
                    <span className="text-sm font-semibold text-ink">{v.status}</span>
                  </div>
                  <p className="text-sm text-ink-muted">
                    {v.customerName} ·{' '}
                    {v.basis === 'Day' ? 'whole day' : `${v.expectedHours ?? '?'} h expected`}
                    {v.chargeAmount ? ` · charge ${formatMoney(v.chargeAmount)}` : ''}
                  </p>
                  <div className="flex flex-wrap gap-2">
                    {v.status === 'Booked' && (
                      <>
                        <Button size="sm" onClick={() => void run(v.id, 'check-in')}>
                          Check in
                        </Button>
                        <Button
                          size="sm"
                          variant="secondary"
                          onClick={() => void run(v.id, 'cancel')}
                        >
                          Cancel
                        </Button>
                        <Button
                          size="sm"
                          variant="secondary"
                          onClick={() => void run(v.id, 'no-show')}
                        >
                          No-show
                        </Button>
                      </>
                    )}
                    {v.status === 'CheckedIn' && (
                      <Button size="sm" onClick={() => void run(v.id, 'check-out')}>
                        Check out
                      </Button>
                    )}
                  </div>
                </li>
              ))}
            </ul>
          )
        }
      </QueryGate>
    </div>
  );
}
