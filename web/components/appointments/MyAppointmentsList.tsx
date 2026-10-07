'use client';

import Link from 'next/link';
import { buttonVariants } from '@/components/ui/button';
import { EmptyState, EmptyCatIllustration } from '@/components/common/EmptyState';
import { ErrorState } from '@/components/common/ErrorState';
import { LoadingState } from '@/components/common/LoadingState';
import { formatAppointmentWhen } from '@/lib/format/appointment';
import type { AppointmentScope } from '@/lib/api/my-appointments';
import type { AppointmentSummary } from '@/lib/api/types';
import { AppointmentStatusBadge } from './AppointmentStatusBadge';
import { cn } from '@/lib/utils';

const TABS: Array<{ scope: AppointmentScope; label: string }> = [
  { scope: 'upcoming', label: 'Upcoming' },
  { scope: 'past', label: 'Past' },
];

/**
 * `components/appointments/MyAppointmentsList.tsx` — HO-APT-002 §11.2 (US-CLN-04). Tabs
 * *Upcoming* / *Past*; each row names the cat, service, time (in the branch's zone, labelled),
 * vet, clinic and status as text, and links to the detail. Distinct loading / empty / error states.
 */
export function MyAppointmentsList({
  scope,
  onScopeChange,
  appointments,
  isLoading,
  isError,
  onRetry,
}: {
  scope: AppointmentScope;
  onScopeChange: (scope: AppointmentScope) => void;
  appointments: AppointmentSummary[] | undefined;
  isLoading: boolean;
  isError: boolean;
  onRetry: () => void;
}) {
  function onKeyDown(event: React.KeyboardEvent, index: number) {
    if (event.key !== 'ArrowRight' && event.key !== 'ArrowLeft') return;
    const next = TABS[(index + (event.key === 'ArrowRight' ? 1 : TABS.length - 1)) % TABS.length]!;
    onScopeChange(next.scope);
    requestAnimationFrame(() => document.getElementById(`appointments-tab-${next.scope}`)?.focus());
  }

  return (
    <section aria-labelledby="my-appointments-heading" className="flex flex-col gap-4">
      <div className="flex flex-wrap items-center justify-between gap-3">
        <h2 id="my-appointments-heading" className="font-display text-xl font-semibold text-ink">
          Appointments
        </h2>
        <Link href="/book" className={buttonVariants({ size: 'sm' })}>
          Book another
        </Link>
      </div>

      <div role="tablist" aria-label="Appointments" className="flex gap-1 border-b border-border">
        {TABS.map((tab, index) => (
          <button
            key={tab.scope}
            id={`appointments-tab-${tab.scope}`}
            type="button"
            role="tab"
            aria-selected={scope === tab.scope}
            aria-controls="appointments-panel"
            tabIndex={scope === tab.scope ? 0 : -1}
            onClick={() => onScopeChange(tab.scope)}
            onKeyDown={(event) => onKeyDown(event, index)}
            className={cn(
              '-mb-px border-b-2 px-4 py-2 text-sm font-semibold focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-accent',
              scope === tab.scope
                ? 'border-accent text-accent-ink'
                : 'border-transparent text-ink-muted hover:text-ink',
            )}
          >
            {tab.label}
          </button>
        ))}
      </div>

      <div
        id="appointments-panel"
        role="tabpanel"
        aria-labelledby={`appointments-tab-${scope}`}
        tabIndex={0}
        className="outline-none focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-accent"
      >
        {isLoading ? (
          <LoadingState variant="card" rows={4} label="Loading appointments…" />
        ) : isError ? (
          <ErrorState title="We couldn’t load your appointments just now." onRetry={onRetry} />
        ) : !appointments || appointments.length === 0 ? (
          <EmptyState
            icon={<EmptyCatIllustration />}
            title={scope === 'upcoming' ? 'No upcoming appointments.' : 'No past appointments yet.'}
            description={scope === 'upcoming' ? 'Book a visit and it will appear here.' : undefined}
          />
        ) : (
          <ul className="m-0 flex list-none flex-col gap-3 p-0">
            {appointments.map((appointment) => (
              <li key={appointment.id}>
                <Link
                  href={`/portal/appointments/${appointment.id}`}
                  className="flex flex-col gap-1 rounded-[var(--radius-md)] border border-border bg-surface p-4 no-underline hover:bg-surface-sunken focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-accent"
                >
                  <span className="flex flex-wrap items-center justify-between gap-2">
                    <span className="text-[15px] font-semibold text-ink">
                      {appointment.service.name} · {appointment.catName}
                    </span>
                    <AppointmentStatusBadge status={appointment.status} />
                  </span>
                  <span className="text-sm text-ink">
                    {appointment.startsOn
                      ? `${formatAppointmentWhen(appointment.startsOn, appointment.branchTimeZoneId)} (${appointment.branchTimeZoneId})`
                      : 'Time to be confirmed'}
                  </span>
                  <span className="text-sm text-ink-muted">
                    {appointment.performerName ?? 'Vet to be assigned'} · {appointment.branch.name}
                  </span>
                </Link>
              </li>
            ))}
          </ul>
        )}
      </div>
    </section>
  );
}
