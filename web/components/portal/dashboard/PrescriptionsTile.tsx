import Link from 'next/link';
import type { PortalPrescriptions } from '@/lib/api/portal';
import { formatDateOnly } from '@/lib/format/time';
import { PORTAL_PRESCRIPTIONS } from '@/lib/routing/portal-links';
import { TileEmpty, TileRow } from './DashboardSection';

/** ADR-262: a count and an end date per cat — never a medicine or a dose. */
export function PrescriptionsTile({ data }: { data: PortalPrescriptions }) {
  if (data.byCat.length === 0) return <TileEmpty>No active prescriptions.</TileEmpty>;
  return (
    <>
      <ul>
        {data.byCat.map((row) => (
          <TileRow
            key={row.catId}
            main={
              <>
                <p className="text-[14.5px] font-bold text-ink">{row.catName}</p>
                <p className="mt-0.5 text-[13px] leading-snug text-ink-muted">
                  {row.activeCount} active{' '}
                  {row.activeCount === 1 ? 'prescription' : 'prescriptions'}
                  {' · '}
                  {row.earliestEndsOn
                    ? `the first ends ${formatDateOnly(row.earliestEndsOn)}`
                    : 'no end date recorded'}
                </p>
              </>
            }
            side={
              <Link
                href={PORTAL_PRESCRIPTIONS}
                className="text-[13px] font-semibold text-accent-ink"
                aria-label={`View ${row.catName}’s prescriptions`}
              >
                View
              </Link>
            }
          />
        ))}
      </ul>
      <p className="pb-2 text-[13px] text-ink-muted">
        Medicines and doses are on the prescriptions page, not here.
      </p>
    </>
  );
}
