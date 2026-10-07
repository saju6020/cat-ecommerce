'use client';

import Link from 'next/link';
import { Button } from '@/components/ui/button';
import { Badge } from '@/components/ui/badge';
import { formatBranchDateLong } from '@/lib/format/appointment';
import { timeOnlyInZone } from '@/lib/format/time';
import type { WaitlistEntry } from '@/lib/api/my-appointments';

const SHOWN_STATUSES = ['Waiting', 'Offered', 'Lapsed'];

/**
 * `components/appointments/WaitlistSection.tsx` — HO-APT-002 §11.2 (US-CLN-09). The caller's
 * waitlist entries in `Waiting`, `Offered` (with the claim-by time in the clinic's zone and a link
 * to the held appointment) and `Lapsed`; **Withdraw** for the two live ones. Renders nothing when
 * there is nothing to show.
 */
export function WaitlistSection({
  entries,
  zoneByBranchId,
  onWithdraw,
  withdrawingId,
}: {
  entries: WaitlistEntry[];
  zoneByBranchId: Record<string, string>;
  onWithdraw: (entry: WaitlistEntry) => void;
  withdrawingId: string | null;
}) {
  const shown = entries.filter((entry) => SHOWN_STATUSES.includes(entry.status));
  if (shown.length === 0) return null;

  return (
    <section aria-labelledby="waitlist-heading" className="flex flex-col gap-3">
      <h2 id="waitlist-heading" className="font-display text-xl font-semibold text-ink">
        Waitlist
      </h2>
      <ul className="m-0 flex list-none flex-col gap-3 p-0">
        {shown.map((entry) => {
          // `undefined` until the branch list loads: no UTC stand-in for a claim-by time (W16).
          const zone: string | undefined = zoneByBranchId[entry.branchId];
          const live = entry.status === 'Waiting' || entry.status === 'Offered';
          return (
            <li
              key={entry.id}
              className="flex flex-wrap items-center justify-between gap-3 rounded-[var(--radius-md)] border border-border bg-surface p-4"
            >
              <div className="min-w-0 flex-1 text-sm">
                <p className="flex flex-wrap items-center gap-2 text-[15px] font-semibold text-ink">
                  {entry.serviceName} · {entry.catName}
                  <Badge
                    variant={
                      entry.status === 'Offered'
                        ? 'warning'
                        : entry.status === 'Waiting'
                          ? 'neutral'
                          : 'neutral'
                    }
                  >
                    {entry.status === 'Lapsed' ? 'Offer lapsed' : entry.status}
                  </Badge>
                </p>
                <p className="mt-0.5 text-ink-muted">{formatBranchDateLong(entry.localDate)}</p>
                {entry.status === 'Offered' && entry.offerExpiresOn && (
                  <p className="mt-1 font-semibold text-ink">
                    A time is available —{' '}
                    {zone
                      ? `claim by ${timeOnlyInZone(entry.offerExpiresOn, zone)} (${zone}).`
                      : 'checking the claim-by time…'}{' '}
                    {entry.offeredAppointmentId && (
                      <Link
                        href={`/portal/appointments/${entry.offeredAppointmentId}`}
                        className="text-accent-ink underline underline-offset-2"
                      >
                        View the offer
                      </Link>
                    )}
                  </p>
                )}
                {entry.status === 'Lapsed' && (
                  <p className="mt-1 text-ink-muted">
                    You didn’t claim in time. You can book again.
                  </p>
                )}
              </div>
              {live && (
                <Button
                  variant="secondary"
                  size="sm"
                  aria-label={`Withdraw from the waitlist for ${entry.serviceName}`}
                  onClick={() => onWithdraw(entry)}
                  disabled={withdrawingId === entry.id}
                >
                  Withdraw
                </Button>
              )}
            </li>
          );
        })}
      </ul>
    </section>
  );
}
