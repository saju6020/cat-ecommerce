'use client';

import Link from 'next/link';
import { CalendarPlus } from 'lucide-react';
import { buttonVariants } from '@/components/ui/button';
import { ErrorState } from '@/components/common/ErrorState';
import { usePortalDashboard } from '@/lib/hooks/usePortalDashboard';
import type { PortalDashboard as PortalDashboardData } from '@/lib/api/portal';
import {
  BOOK_APPOINTMENT,
  PORTAL_APPOINTMENTS,
  PORTAL_BILLING,
  PORTAL_CATS,
  PORTAL_ORDERS,
  PORTAL_PRESCRIPTIONS,
} from '@/lib/routing/portal-links';
import { AppointmentsTile } from './AppointmentsTile';
import { BalancesTile } from './BalancesTile';
import { CatsTile } from './CatsTile';
import { DashboardSection } from './DashboardSection';
import { DashboardSkeleton } from './DashboardSkeleton';
import { OrdersTile } from './OrdersTile';
import { PrescriptionsTile } from './PrescriptionsTile';
import { VisitsTile } from './VisitsTile';

/**
 * `components/portal/dashboard/PortalDashboard.tsx` — HO-POR-002 §2, US-POR-01. One request, six
 * tiles. There is NO stays tile and NO subscriptions tile (owner skipped BRD and SUB, 2026-10-10 —
 * not even a placeholder). Each tile goes through `DashboardSection`, so an absent key, an
 * `Unavailable` section and a `Ready` one can never be confused.
 */
function greetingFor(data: PortalDashboardData): string {
  const fullName = data.customer?.state === 'Ready' ? data.customer.data?.fullName : undefined;
  const first = fullName?.trim().split(/\s+/)[0];
  return first ? `Welcome back, ${first}` : 'Welcome back';
}

function nextStepFor(data: PortalDashboardData): string {
  const cats = data.cats?.state === 'Ready' ? data.cats.data : null;
  const needing =
    cats?.items.filter(
      (cat) =>
        cat.vaccinationState === 'Ready' &&
        (cat.vaccination?.overallStatus === 'DueSoon' ||
          cat.vaccination?.overallStatus === 'Overdue'),
    ).length ?? 0;
  if (needing > 0) {
    return `${needing} ${needing === 1 ? 'cat is' : 'cats are'} due or overdue for a vaccination.`;
  }
  return 'Everything we hold for you, on one page.';
}

export function PortalDashboard() {
  const query = usePortalDashboard();

  if (query.isPending) return <DashboardSkeleton />;

  if (query.isError || !query.data) {
    return (
      <ErrorState
        headingLevel={1}
        title="We couldn’t load your home page just now."
        description="This does not mean there is nothing here — please try again."
        onRetry={() => void query.refetch()}
      />
    );
  }

  const data = query.data;
  const retry = () => void query.refetch();
  const retrying = query.isFetching;

  return (
    <div className="flex flex-col gap-5">
      <div className="flex flex-wrap items-center justify-between gap-x-6 gap-y-4 rounded-[var(--radius-md)] bg-warm-soft px-6 py-5">
        <div className="min-w-0 flex-[1_1_320px]">
          <h1 className="font-display text-[26px] font-semibold text-ink">{greetingFor(data)}</h1>
          <p className="mt-1 text-sm text-ink">{nextStepFor(data)}</p>
        </div>
        <Link href={BOOK_APPOINTMENT} className={buttonVariants({ variant: 'secondary' })}>
          <CalendarPlus size={15} aria-hidden="true" />
          Book an appointment
        </Link>
      </div>

      <div className="grid items-start gap-4 [grid-template-columns:repeat(auto-fit,minmax(min(100%,420px),1fr))]">
        <DashboardSection
          id="cats"
          title="My cats"
          section={data.cats}
          link={{ href: PORTAL_CATS, label: 'All cats' }}
          onRetry={retry}
          retrying={retrying}
          unavailableSubject="your cats"
          className="col-span-full"
        >
          {(cats) => <CatsTile data={cats} />}
        </DashboardSection>

        <DashboardSection
          id="appointments"
          title="Upcoming appointments"
          section={data.appointments}
          link={{ href: PORTAL_APPOINTMENTS, label: 'All appointments' }}
          onRetry={retry}
          retrying={retrying}
          unavailableSubject="your appointments"
        >
          {(appointments) => <AppointmentsTile data={appointments} />}
        </DashboardSection>

        <DashboardSection
          id="orders"
          title="Recent orders"
          section={data.orders}
          link={{ href: PORTAL_ORDERS, label: 'All orders' }}
          onRetry={retry}
          retrying={retrying}
          unavailableSubject="your orders"
        >
          {(orders) => <OrdersTile data={orders} />}
        </DashboardSection>

        <DashboardSection
          id="balances"
          title="Balances"
          section={data.balances}
          link={{ href: PORTAL_BILLING, label: 'All invoices' }}
          onRetry={retry}
          retrying={retrying}
          unavailableSubject="your balances"
          className="col-span-full"
        >
          {(balances) => <BalancesTile data={balances} />}
        </DashboardSection>

        <DashboardSection
          id="visits"
          title="Recent visits"
          section={data.visits}
          onRetry={retry}
          retrying={retrying}
          unavailableSubject="your visits"
        >
          {(visits) => <VisitsTile data={visits} />}
        </DashboardSection>

        <DashboardSection
          id="prescriptions"
          title="Prescriptions"
          section={data.prescriptions}
          link={{ href: PORTAL_PRESCRIPTIONS, label: 'All prescriptions' }}
          onRetry={retry}
          retrying={retrying}
          unavailableSubject="your prescriptions"
        >
          {(prescriptions) => <PrescriptionsTile data={prescriptions} />}
        </DashboardSection>
      </div>
    </div>
  );
}
