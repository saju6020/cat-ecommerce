'use client';

import Link from 'next/link';
import { Button } from '@/components/ui/button';
import { EmptyState } from '@/components/common/EmptyState';
import { ErrorState } from '@/components/common/ErrorState';
import { LoadingState } from '@/components/common/LoadingState';
import { describeRescheduleFlag, formatAppointmentWhen } from '@/lib/format/appointment';

export interface FlaggedRow {
  id: string;
  catName: string;
  startsOn: string;
  /** The branch's zone, from the appointment's detail; `null` while that is loading. */
  zone: string | null;
  /** The appointment's detail failed to load, so the zone (and the rest) will not arrive on its own. */
  detailFailed?: boolean;
  reason: string;
  performerName: string | null;
  customerId: string | null;
  customerName: string | null;
  phone: string | null;
}

/** The "When" cell while the row's detail is missing: loading, or a failure with its own Retry. */
function DetailPending({ row, onRetry }: { row: FlaggedRow; onRetry?: (row: FlaggedRow) => void }) {
  if (!row.detailFailed) return <>Loading…</>;
  return (
    <span role="alert" className="flex flex-wrap items-center gap-2 text-danger-ink">
      We couldn’t load this appointment’s details.
      {onRetry && (
        <Button
          size="sm"
          variant="secondary"
          aria-label={`Retry loading details for ${row.catName}`}
          onClick={() => onRetry(row)}
        >
          Retry
        </Button>
      )}
    </span>
  );
}

/**
 * `components/appointments/FlaggedAppointmentsTable.tsx` — HO-APT-002 §11.4 (ADM-03 AC2). Future
 * appointments flagged for rescheduling — when, cat, customer, phone, and the reason in words —
 * each with **Reschedule** (opens the Move… dialog) and **Cancel**. The flagged payload carries the
 * cat and time only, so the container joins the customer and phone from the detail and customer
 * records; until they load the cells say so rather than staying blank.
 */
export function FlaggedAppointmentsTable({
  rows,
  isLoading,
  isError,
  onRetry,
  onReschedule,
  onRetryDetail,
  onCancel,
}: {
  rows: FlaggedRow[] | undefined;
  isLoading: boolean;
  isError: boolean;
  onRetry: () => void;
  onReschedule: (row: FlaggedRow) => void;
  onRetryDetail?: (row: FlaggedRow) => void;
  onCancel: (row: FlaggedRow) => void;
}) {
  if (isLoading)
    return <LoadingState variant="table" rows={4} label="Loading flagged appointments…" />;
  if (isError && !rows)
    return <ErrorState title="We couldn’t load the flagged list." onRetry={onRetry} />;
  if (!rows || rows.length === 0) {
    return (
      <EmptyState
        title="Nothing needs rescheduling."
        description="Appointments affected by leave or a closure appear here."
      />
    );
  }
  return (
    <>
      <ul className="m-0 flex list-none flex-col gap-3 p-0 md:hidden">
        {rows.map((row) => (
          <li
            key={row.id}
            className="flex flex-col gap-1 rounded-[var(--radius-md)] border border-border bg-surface p-3 text-sm"
          >
            <p className="font-semibold text-ink">
              {row.zone ? (
                <>
                  {formatAppointmentWhen(row.startsOn, row.zone)}{' '}
                  <span className="font-normal text-ink-muted">({row.zone})</span>
                </>
              ) : (
                <DetailPending row={row} onRetry={onRetryDetail} />
              )}
            </p>
            <Link
              href={`/staff/appointments/${row.id}`}
              className="font-semibold text-accent-ink no-underline hover:underline"
            >
              {row.catName}
            </Link>
            <p className="text-ink-muted">
              {row.customerName ?? (row.detailFailed ? 'Details unavailable' : 'Loading…')}
              {row.phone ? ` · ${row.phone}` : ''}
            </p>
            <p className="text-ink">{describeRescheduleFlag(row.reason, row.performerName)}</p>
            <div className="flex flex-wrap gap-2">
              <Button
                size="sm"
                aria-label={`Reschedule ${row.catName}`}
                disabled={!row.zone}
                onClick={() => onReschedule(row)}
              >
                Reschedule
              </Button>
              <Button
                size="sm"
                variant="secondary"
                aria-label={`Cancel ${row.catName}`}
                onClick={() => onCancel(row)}
              >
                Cancel
              </Button>
            </div>
          </li>
        ))}
      </ul>
      <div className="hidden overflow-x-auto md:block">
        <table className="w-full min-w-[40rem] border-collapse text-left text-sm">
          <caption className="sr-only">Appointments flagged for rescheduling</caption>
          <thead>
            <tr className="border-b border-border text-xs uppercase tracking-wide text-ink-muted">
              <th scope="col" className="py-2 pr-3">
                When
              </th>
              <th scope="col" className="py-2 pr-3">
                Cat
              </th>
              <th scope="col" className="py-2 pr-3">
                Customer
              </th>
              <th scope="col" className="py-2 pr-3">
                Reason
              </th>
              <th scope="col" className="py-2">
                <span className="sr-only">Actions</span>
              </th>
            </tr>
          </thead>
          <tbody>
            {rows.map((row) => (
              <tr key={row.id} className="border-b border-border align-top">
                <td className="py-3 pr-3 text-ink">
                  {row.zone ? (
                    <>
                      {formatAppointmentWhen(row.startsOn, row.zone)}
                      <span className="block text-xs text-ink-muted">{row.zone}</span>
                    </>
                  ) : (
                    <DetailPending row={row} onRetry={onRetryDetail} />
                  )}
                </td>
                <td className="py-3 pr-3">
                  <Link
                    href={`/staff/appointments/${row.id}`}
                    className="font-semibold text-accent-ink no-underline hover:underline"
                  >
                    {row.catName}
                  </Link>
                </td>
                <td className="py-3 pr-3 text-ink-muted">
                  {row.customerName ?? (row.detailFailed ? 'Details unavailable' : 'Loading…')}
                  {row.phone && <span className="block text-xs">{row.phone}</span>}
                </td>
                <td className="py-3 pr-3 text-ink">
                  {describeRescheduleFlag(row.reason, row.performerName)}
                </td>
                <td className="py-3">
                  <div className="flex flex-wrap gap-2">
                    <Button
                      size="sm"
                      aria-label={`Reschedule ${row.catName}`}
                      disabled={!row.zone}
                      onClick={() => onReschedule(row)}
                    >
                      Reschedule
                    </Button>
                    <Button
                      size="sm"
                      variant="secondary"
                      aria-label={`Cancel ${row.catName}`}
                      onClick={() => onCancel(row)}
                    >
                      Cancel
                    </Button>
                  </div>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </>
  );
}
