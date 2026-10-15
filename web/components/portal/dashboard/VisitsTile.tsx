import Link from 'next/link';
import { buttonVariants } from '@/components/ui/button';
import type { PortalVisits } from '@/lib/api/portal';
import { formatDateOnly } from '@/lib/format/time';
import { portalCatVisitsHref, portalVisitHref } from '@/lib/routing/portal-links';
import { TileEmpty, TileRow } from './DashboardSection';

/** ADR-262: identifying details only — date, service, vet, whether a summary exists, the
 *  follow-up date. No diagnosis, no instructions. */
export function VisitsTile({ data }: { data: PortalVisits }) {
  if (data.items.length === 0) return <TileEmpty>You have no visits on record yet.</TileEmpty>;
  return (
    <ul>
      {data.items.map((visit) => (
        <TileRow
          key={visit.visitId}
          main={
            <>
              <p className="text-[14.5px] font-bold text-ink">
                {visit.serviceName} · {visit.catName}
              </p>
              <p className="mt-0.5 text-[13px] leading-snug text-ink-muted">
                {formatDateOnly(visit.visitedOn)} · {visit.vetName}
                {visit.followUpOn && (
                  <>
                    <br />
                    Follow-up due by {formatDateOnly(visit.followUpOn)}
                  </>
                )}
                {!visit.hasDischargeSummary && (
                  <>
                    <br />
                    No discharge summary was written for this visit.
                  </>
                )}
              </p>
            </>
          }
          side={
            visit.hasDischargeSummary ? (
              <Link
                href={portalVisitHref(visit.visitId)}
                className={buttonVariants({ variant: 'secondary', size: 'sm' })}
                aria-label={`View discharge summary, ${visit.serviceName} for ${visit.catName}`}
              >
                View discharge summary
              </Link>
            ) : (
              <Link
                href={portalCatVisitsHref(visit.catId)}
                className="text-[13px] font-semibold text-accent-ink"
              >
                {visit.catName}’s visits
              </Link>
            )
          }
        />
      ))}
    </ul>
  );
}
