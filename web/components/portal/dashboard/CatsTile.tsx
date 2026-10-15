import Link from 'next/link';
import { Cat, TriangleAlert } from 'lucide-react';
import { buttonVariants } from '@/components/ui/button';
import { EmptyCatIllustration } from '@/components/common/EmptyState';
import { cn } from '@/lib/utils';
import type { PortalCatItem, PortalCats } from '@/lib/api/portal';
import { formatDateOnly } from '@/lib/format/time';
import {
  PORTAL_ADD_CAT,
  PORTAL_CATS,
  bookVaccinationHref,
  portalCatHref,
} from '@/lib/routing/portal-links';
import { vaccinationLabel } from './labels';
import { StatusBadge } from './StatusBadge';
import { TileRow } from './DashboardSection';

/** U-A1: the invitation to add a cat — rendered ONLY for a `Ready` cats section with no cats. */
export function NoCatsEmptyState() {
  return (
    <div className="flex flex-wrap items-center gap-5 py-5" data-testid="no-cats">
      <EmptyCatIllustration />
      <div className="min-w-[220px] flex-1">
        <p className="text-[15px] font-semibold text-ink">Add your first cat</p>
        <p className="mt-1 text-sm text-ink-muted">
          Once a cat is on your account you can book visits, see vaccinations and keep their records
          together.
        </p>
        <Link href={PORTAL_ADD_CAT} className={cn(buttonVariants({ size: 'md' }), 'mt-3')}>
          Add a cat
        </Link>
      </div>
    </div>
  );
}

function VaccinationLine({ cat }: { cat: PortalCatItem }) {
  if (cat.vaccinationState === 'Unavailable' || cat.vaccination === null) {
    return <p className="mt-0.5 text-[13px] text-ink-muted">Vaccination status unavailable.</p>;
  }
  const { overallStatus, earliestNextDueOn } = cat.vaccination;
  const due = earliestNextDueOn ? formatDateOnly(earliestNextDueOn) : null;
  const text =
    overallStatus === 'Overdue'
      ? due
        ? `Vaccination was due ${due}.`
        : 'Vaccination is overdue.'
      : overallStatus === 'DueSoon'
        ? due
          ? `Next vaccination due ${due}.`
          : 'A vaccination is due soon.'
        : due
          ? `Vaccinations are up to date. Next due ${due}.`
          : 'Vaccinations are up to date.';
  return <p className="mt-0.5 text-[13px] text-ink-muted">{text}</p>;
}

function CatRow({ cat }: { cat: PortalCatItem }) {
  const vaccination = cat.vaccinationState === 'Ready' ? cat.vaccination : null;
  const needsBooking =
    vaccination !== null &&
    (vaccination.overallStatus === 'DueSoon' || vaccination.overallStatus === 'Overdue');
  return (
    <TileRow
      main={
        <div className="flex items-start gap-3">
          <span
            aria-hidden="true"
            className="flex h-11 w-11 flex-none items-center justify-center rounded-full bg-warm-soft text-warm-ink"
          >
            <Cat size={24} />
          </span>
          <div className="min-w-0">
            <Link
              href={portalCatHref(cat.catId)}
              className="text-[14.5px] font-bold text-ink no-underline hover:underline"
            >
              {cat.name}
            </Link>
            <VaccinationLine cat={cat} />
            {cat.isHighRisk && (
              <p className="mt-2 flex items-start gap-2 rounded-[var(--radius-sm)] bg-danger-bg px-2.5 py-1.5 text-[12.5px] leading-snug text-danger-ink">
                <TriangleAlert size={13} className="mt-0.5 flex-none" aria-hidden="true" />
                <span>
                  <b>High risk</b> — the clinic has a handling note for {cat.name}. Allergies and
                  care notes are in{' '}
                  <Link href={portalCatHref(cat.catId)} className="font-semibold text-danger-ink">
                    {cat.name}’s record
                  </Link>
                  .
                </span>
              </p>
            )}
          </div>
        </div>
      }
      side={
        vaccination ? (
          <>
            <StatusBadge status={vaccinationLabel(vaccination.overallStatus)} />
            {needsBooking && vaccination.bookingEligible && (
              <Link
                href={bookVaccinationHref(cat.catId)}
                className={buttonVariants({ size: 'sm' })}
                aria-label={`Book a vaccination for ${cat.name}`}
              >
                Book now
              </Link>
            )}
            {needsBooking && !vaccination.bookingEligible && (
              <span className="max-w-[230px] text-xs leading-snug text-ink-muted">
                Online booking isn’t available for this vaccination. Please contact the clinic.
              </span>
            )}
          </>
        ) : undefined
      }
    />
  );
}

export function CatsTile({ data }: { data: PortalCats }) {
  if (!data.hasCats) return <NoCatsEmptyState />;
  return (
    <>
      <ul>
        {data.items.map((cat) => (
          <CatRow key={cat.catId} cat={cat} />
        ))}
      </ul>
      {data.hasMore && (
        <p className="py-3 text-[13px] text-ink-muted">
          You have more cats than fit here.{' '}
          <Link href={PORTAL_CATS} className="font-semibold text-accent-ink">
            See all cats
          </Link>
        </p>
      )}
    </>
  );
}
