'use client';

import { useState } from 'react';
import Link from 'next/link';
import { Button, buttonVariants } from '@/components/ui/button';
import { EmptyState } from '@/components/common/EmptyState';
import { QueryGate } from '@/components/boarding/shared/QueryGate';
import { DepositStateBadge, StayStatusBadge } from '@/components/boarding/shared/StatusBadges';
import type { BoardingStayStatus } from '@/lib/api/types';
import { formatStayDates } from '@/lib/format/boarding';
import { formatMoney } from '@/lib/format/money';
import { useStaffStays } from '@/lib/hooks/useStaffBoarding';
import { STAFF_BOARDING_NEW_STAY, staffStayHref } from '@/lib/routing/boarding-links';
import { cn } from '@/lib/utils';

const FILTERS: Array<{ status: BoardingStayStatus | ''; label: string }> = [
  { status: '', label: 'All' },
  { status: 'Reserved', label: 'Reserved' },
  { status: 'Confirmed', label: 'Confirmed' },
  { status: 'CheckedIn', label: 'In house' },
  { status: 'CheckedOut', label: 'Checked out' },
  { status: 'Cancelled', label: 'Cancelled' },
];

/** `components/boarding/stay/StayList.tsx` — HO-BRD-002 §11.8: stays by status; every flag (gate
 *  blocked, needs attention, isolation, balance) is **written out**, not just coloured. */
export function StayList() {
  const [status, setStatus] = useState<BoardingStayStatus | ''>('');
  const [page, setPage] = useState(1);
  const query = useStaffStays({ status: status || undefined, pageNumber: page, pageSize: 20 });
  return (
    <div className="flex flex-col gap-4">
      <div className="flex flex-wrap items-center justify-between gap-3">
        <div role="tablist" aria-label="Stay status" className="flex flex-wrap gap-2">
          {FILTERS.map((f) => (
            <button
              key={f.label}
              role="tab"
              type="button"
              aria-selected={status === f.status}
              onClick={() => {
                setStatus(f.status);
                setPage(1);
              }}
              className={cn(
                'min-h-11 rounded-[var(--radius-sm)] border px-3 text-sm font-semibold',
                status === f.status
                  ? 'border-accent bg-accent-soft text-accent-ink'
                  : 'border-border bg-surface text-ink',
              )}
            >
              {f.label}
            </button>
          ))}
        </div>
        <Link href={STAFF_BOARDING_NEW_STAY} className={cn(buttonVariants({ variant: 'primary' }))}>
          New stay
        </Link>
      </div>
      <QueryGate query={query} loadingLabel="Loading stays…">
        {(result) =>
          result.items.length === 0 ? (
            <EmptyState
              title="No stays match."
              description="Try another status, or book a new stay."
            />
          ) : (
            <>
              <ul aria-label="Stays" className="flex flex-col gap-3">
                {result.items.map((s) => (
                  <li
                    key={s.id}
                    className="flex flex-col gap-2 rounded-[var(--radius-md)] border border-border bg-surface p-4"
                  >
                    <div className="flex flex-wrap items-center justify-between gap-2">
                      <Link
                        href={staffStayHref(s.id)}
                        className="text-[15px] font-semibold text-accent-ink underline"
                      >
                        {s.catNames.join(' and ')}
                        {s.anyHighRisk ? ' (high risk)' : ''}
                      </Link>
                      <StayStatusBadge status={s.status} />
                    </div>
                    <p className="text-sm text-ink">
                      {s.customerName} · {formatStayDates(s.arrivalOn, s.departureOn)} ·{' '}
                      {s.unitName ?? 'No unit yet'}
                    </p>
                    <div className="flex flex-wrap items-center gap-3 text-sm">
                      <DepositStateBadge state={s.depositState} stayStatus={s.status} />
                      {s.balanceDue && (
                        <span className="text-ink-muted">Balance {formatMoney(s.balanceDue)}</span>
                      )}
                      {s.gateBlocked && (
                        <span className="font-semibold text-danger-ink">Vaccination overdue</span>
                      )}
                      {s.requiresIsolation && (
                        <span className="font-semibold text-warning-ink">Needs isolation</span>
                      )}
                      {s.needsAttention && (
                        <span className="font-semibold text-warning-ink">Needs attention</span>
                      )}
                    </div>
                  </li>
                ))}
              </ul>
              <div className="flex items-center gap-3 text-sm text-ink-muted">
                <Button
                  variant="secondary"
                  size="sm"
                  disabled={page <= 1}
                  onClick={() => setPage(page - 1)}
                >
                  Previous
                </Button>
                Page {result.pageNumber} of{' '}
                {Math.max(1, Math.ceil(result.totalCount / result.pageSize))}
                <Button
                  variant="secondary"
                  size="sm"
                  disabled={page * result.pageSize >= result.totalCount}
                  onClick={() => setPage(page + 1)}
                >
                  Next
                </Button>
              </div>
            </>
          )
        }
      </QueryGate>
    </div>
  );
}
