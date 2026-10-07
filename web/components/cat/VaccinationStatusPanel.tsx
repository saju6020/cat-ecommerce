import Link from 'next/link';
import { OctagonAlert } from 'lucide-react';
import type { CatVaccinationStatus } from '@/lib/api/cats';
import { VaccinationStatusBadge } from './VaccinationStatusBadge';
import { formatDateOnly } from '@/lib/dateOnly';

/**
 * `components/cat/VaccinationStatusPanel.tsx` — US-CAT-07, all three criteria (HO-CAT-002
 * §11.5).
 *
 * Lists every active core vaccine with its own status, including one never administered (U13 —
 * "listed as overdue, not omitted"). Never recomputes status client-side — `status` is rendered
 * exactly as `GET .../vaccination-status` (Q4) returned it.
 *
 * `bookingHref` — HO-CAT-002 §1's 2026-09-22 amendment names `/staff/appointments/new?
 * customerId=…&catId=…` as the intended **staff-side** booking route (added by HO-APT-002,
 * before this slice's implementation). §20.5's choice between "render the link to the intended
 * future route" and "render it disabled with a tooltip" is resolved as the former on staff
 * screens, now that a concrete target exists there — the route itself is Appointments' own slice
 * and may not exist in this branch yet, so the link can 404 until that lands; that is expected,
 * not a bug in this component. The amendment names only the staff page, not a portal
 * equivalent, so the portal profile page (`app/(portal)/portal/cats/[catId]/page.tsx`) passes
 * `bookingDisabled` instead and gets §20.5's other option — inert, with a tooltip explaining why.
 */
export interface VaccinationStatusPanelProps {
  status: CatVaccinationStatus;
  bookingHref: string;
  bookingDisabled?: boolean;
}

export function VaccinationStatusPanel({
  status,
  bookingHref,
  bookingDisabled,
}: VaccinationStatusPanelProps) {
  return (
    <div className="flex flex-col gap-4">
      {!status.bookingEligible && (
        <div
          role="alert"
          className="flex items-start gap-3 rounded-[var(--radius-md)] border border-danger-ink/20 bg-danger-bg p-4 text-danger-ink"
        >
          <OctagonAlert size={20} className="mt-0.5 flex-none" aria-hidden="true" />
          <p className="text-sm font-semibold">
            Boarding will be blocked until this cat&rsquo;s vaccinations are brought up to date.
          </p>
        </div>
      )}

      <ul className="flex flex-col divide-y divide-border rounded-[var(--radius-md)] border border-border bg-surface">
        {status.vaccines.map((vaccine) => (
          <li
            key={vaccine.vaccineId}
            className="flex flex-wrap items-center justify-between gap-3 p-4"
          >
            <div>
              <p className="text-sm font-semibold text-ink">{vaccine.vaccineName}</p>
              <p className="text-xs text-ink-muted">
                {vaccine.nextDueOn
                  ? `Next due ${formatDateOnly(vaccine.nextDueOn)}`
                  : 'Never administered'}
              </p>
            </div>
            <div className="flex flex-wrap items-center gap-3">
              <VaccinationStatusBadge status={vaccine.status} />
              {vaccine.status === 'DueSoon' &&
                (bookingDisabled ? (
                  <span
                    title="Booking isn't available here yet."
                    className="cursor-not-allowed text-sm font-semibold text-ink-muted"
                  >
                    Book a vaccination
                  </span>
                ) : (
                  <Link
                    href={bookingHref}
                    className="text-sm font-semibold text-accent-ink underline"
                  >
                    Book a vaccination
                  </Link>
                ))}
            </div>
          </li>
        ))}
      </ul>
    </div>
  );
}
