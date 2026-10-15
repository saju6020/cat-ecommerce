'use client';

import { Suspense } from 'react';
import Link from 'next/link';
import { useSearchParams } from 'next/navigation';
import { ChevronLeft, ChevronRight, Plus } from 'lucide-react';
import { ActiveBranchZoneGate } from '@/components/appointments/ActiveBranchZoneGate';
import { AccessRefusedState } from '@/components/admin/AccessRefusedState';
import { LoadingState } from '@/components/common/LoadingState';
import { Button, buttonVariants } from '@/components/ui/button';
import { PageHeading } from '@/components/inventory/fields';
import { SurgeryDayList } from '@/components/surgery/SurgeryDayList';
import { ApiForbiddenError } from '@/lib/api/client';
import { addDaysIso } from '@/lib/format/appointment';
import { timeOnlyInZone, todayInZone } from '@/lib/format/time';
import { useFilterUrlUpdate } from '@/lib/hooks/useFilterUrlUpdate';
import { useStaffRoles } from '@/lib/hooks/useStaffRoles';
import { useSurgeryDay } from '@/lib/hooks/useSurgeries';

/** `/staff/surgeries` — HO-SRG-002 §11.2 (US-SRG-02): the theatre day list. Polls every 30 s (UI-5). */
export default function SurgeriesPage() {
  return (
    <Suspense fallback={<LoadingState variant="card" rows={4} label="Loading the day list…" />}>
      <ActiveBranchZoneGate>{(zone) => <DayView zone={zone} />}</ActiveBranchZoneGate>
    </Suspense>
  );
}

function DayView({ zone }: { zone: string }) {
  const searchParams = useSearchParams();
  const setParam = useFilterUrlUpdate();
  const date = searchParams.get('date') || todayInZone(zone);
  const roles = useStaffRoles();
  const day = useSurgeryDay(date);

  if (day.error instanceof ApiForbiddenError) return <AccessRefusedState error={day.error} />;
  const canBook = roles.has('Reception', 'Vet', 'Administrator');
  const canConfigure = roles.has('Vet', 'Administrator');
  return (
    <div className="flex flex-col gap-5">
      <PageHeading
        title="Theatre day list"
        description={`Times in ${zone}. ${
          day.dataUpdatedAt
            ? `Updated ${timeOnlyInZone(new Date(day.dataUpdatedAt).toISOString(), zone)}, refreshes every 30 seconds.`
            : ''
        }`}
        actions={
          <>
            <div className="flex items-center gap-1" role="group" aria-label="Choose the day">
              <Button
                variant="secondary"
                size="icon"
                aria-label="Previous day"
                onClick={() => setParam('date', addDaysIso(date, -1))}
              >
                <ChevronLeft size={16} aria-hidden="true" />
              </Button>
              <label className="sr-only" htmlFor="surgery-day">
                Day
              </label>
              <input
                id="surgery-day"
                type="date"
                value={date}
                onChange={(event) => event.target.value && setParam('date', event.target.value)}
                className="h-8 rounded-[var(--radius-sm)] border border-border-strong bg-surface px-2 text-sm text-ink focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-accent"
              />
              <Button
                variant="secondary"
                size="icon"
                aria-label="Next day"
                onClick={() => setParam('date', addDaysIso(date, 1))}
              >
                <ChevronRight size={16} aria-hidden="true" />
              </Button>
            </div>
            {canBook && (
              <Link href="/staff/surgeries/new" className={buttonVariants({ variant: 'primary' })}>
                <Plus size={16} aria-hidden="true" />
                Book a surgery
              </Link>
            )}
            {canConfigure && (
              <Link
                href="/staff/surgeries/protocols"
                className={buttonVariants({ variant: 'secondary' })}
              >
                Protocols
              </Link>
            )}
            <Link
              href="/staff/surgeries/campaigns"
              className={buttonVariants({ variant: 'secondary' })}
            >
              Campaigns
            </Link>
          </>
        }
      />
      <SurgeryDayList
        surgeries={day.data?.surgeries}
        zone={day.data?.branchTimeZoneId ?? zone}
        isLoading={day.isPending}
        isError={day.isError}
        onRetry={() => void day.refetch()}
      />
    </div>
  );
}
