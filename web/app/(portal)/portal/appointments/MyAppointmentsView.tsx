'use client';

import { useMemo, useState } from 'react';
import { useSearchParams } from 'next/navigation';
import { MyAppointmentsList } from '@/components/appointments/MyAppointmentsList';
import { WaitlistSection } from '@/components/appointments/WaitlistSection';
import { toast } from '@/components/ui/use-toast';
import {
  useMyAppointments,
  useMyWaitlist,
  useWithdrawFromWaitlist,
} from '@/lib/hooks/useMyAppointments';
import { useClinicBranches } from '@/lib/hooks/useBooking';
import { useFilterUrlUpdate } from '@/lib/hooks/useFilterUrlUpdate';
import { describeAppointmentError } from '@/lib/api/errors';
import type { AppointmentScope } from '@/lib/api/my-appointments';

/** Container for `/portal/appointments`: the active tab is a URL param (`?scope=past`). */
export function MyAppointmentsView() {
  const searchParams = useSearchParams();
  const setParam = useFilterUrlUpdate();
  const scope: AppointmentScope = searchParams.get('scope') === 'past' ? 'past' : 'upcoming';
  const appointments = useMyAppointments(scope);
  const waitlist = useMyWaitlist();
  const branches = useClinicBranches();
  const withdraw = useWithdrawFromWaitlist();
  const [withdrawingId, setWithdrawingId] = useState<string | null>(null);

  const zoneByBranchId = useMemo(
    () => Object.fromEntries((branches.data ?? []).map((branch) => [branch.id, branch.timeZoneId])),
    [branches.data],
  );

  return (
    <div className="flex flex-col gap-8">
      <h1 className="sr-only">My appointments</h1>
      <MyAppointmentsList
        scope={scope}
        onScopeChange={(next) => setParam('scope', next === 'upcoming' ? '' : next)}
        appointments={appointments.data?.items}
        isLoading={appointments.isLoading}
        isError={appointments.isError}
        onRetry={() => void appointments.refetch()}
      />
      <WaitlistSection
        entries={waitlist.data ?? []}
        zoneByBranchId={zoneByBranchId}
        withdrawingId={withdrawingId}
        onWithdraw={async (entry) => {
          setWithdrawingId(entry.id);
          try {
            await withdraw.mutateAsync(entry.id);
            toast({ title: 'Removed from the waitlist' });
          } catch (error) {
            toast({ title: 'Could not withdraw', description: describeAppointmentError(error) });
          } finally {
            setWithdrawingId(null);
          }
        }}
      />
    </div>
  );
}
