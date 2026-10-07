'use client';

import { useMemo, useState } from 'react';
import Link from 'next/link';
import {
  FlaggedAppointmentsTable,
  type FlaggedRow,
} from '@/components/appointments/FlaggedAppointmentsTable';
import { CancelDialog } from '@/components/appointments/StaffAppointmentDetail';
import { MoveDialog } from '@/components/calendar/MoveDialog';
import { PermissionDeniedState } from '@/components/admin/PermissionDeniedState';
import { toast } from '@/components/ui/use-toast';
import { useMoveChoices } from '@/lib/hooks/useMoveChoices';
import {
  useCancelStaffAppointment,
  useFlaggedAppointments,
  useMoveAppointment,
  useStaffAppointmentDetails,
} from '@/lib/hooks/useStaffAppointments';
import { useStaffCustomersByIds } from '@/lib/hooks/useStaffCustomers';
import { ApiForbiddenError } from '@/lib/api/client';
import { getViolationCode } from '@/lib/api/errors';
import { localDateOf } from '@/lib/format/time';

/** Container for the flagged list (HO-APT-002 §11.4): joins each row's customer and phone. */
export function FlaggedView() {
  const flagged = useFlaggedAppointments();
  const items = useMemo(() => flagged.data ?? [], [flagged.data]);
  const details = useStaffAppointmentDetails(items.map((item) => item.id));
  const customerIds = [
    ...new Set(details.map((d) => d.data?.customerId).filter((id): id is string => !!id)),
  ];
  const customers = useStaffCustomersByIds(customerIds);
  const [moveTarget, setMoveTarget] = useState<FlaggedRow | null>(null);
  const [cancelTarget, setCancelTarget] = useState<FlaggedRow | null>(null);
  // As in the calendar: the move sends the version the dialog opened with; only after a
  // StaleCalendarVersion 409 does the retry adopt the refetched one.
  const [openedVersion, setOpenedVersion] = useState<number | null>(null);
  const [adoptLive, setAdoptLive] = useState(false);
  const move = useMoveAppointment();
  const cancel = useCancelStaffAppointment();

  const rows: FlaggedRow[] = items.map((item, index) => {
    const detail = details[index]?.data;
    const customer = customers.find((c) => c.data?.id === detail?.customerId)?.data;
    return {
      id: item.id,
      catName: item.catName,
      startsOn: item.startsOn,
      zone: detail?.branchTimeZoneId ?? null,
      detailFailed: !detail && !!details[index]?.isError,
      reason: item.rescheduleFlagReason,
      performerName: detail?.performerName ?? null,
      customerId: detail?.customerId ?? null,
      customerName: customer?.fullName ?? null,
      phone: customer?.mobileNumber ?? null,
    };
  });
  const moveDetail = details[items.findIndex((i) => i.id === moveTarget?.id)]?.data;
  const moveZone = moveTarget?.zone ?? null;
  const choices = useMoveChoices(
    moveTarget && moveZone ? localDateOf(moveTarget.startsOn, moveZone) : '',
    moveTarget !== null && moveZone !== null,
  );

  if (flagged.error instanceof ApiForbiddenError) return <PermissionDeniedState />;

  return (
    <div className="flex flex-col gap-4">
      <div className="flex flex-wrap items-center justify-between gap-3">
        <h1 className="font-display text-2xl font-semibold text-ink">Flagged for rescheduling</h1>
        <Link
          href="/staff/appointments"
          className="text-sm font-semibold text-accent-ink underline underline-offset-2"
        >
          Back to the calendar
        </Link>
      </div>
      <FlaggedAppointmentsTable
        rows={flagged.isLoading ? undefined : rows}
        isLoading={flagged.isLoading}
        isError={flagged.isError}
        onRetry={() => void flagged.refetch()}
        onReschedule={(row) => {
          const index = items.findIndex((i) => i.id === row.id);
          setOpenedVersion(details[index]?.data?.version ?? null);
          setAdoptLive(false);
          setMoveTarget(row);
        }}
        onRetryDetail={(row) => {
          const index = items.findIndex((i) => i.id === row.id);
          void details[index]?.refetch();
        }}
        onCancel={setCancelTarget}
      />
      <MoveDialog
        open={moveTarget !== null && !!moveDetail && moveZone !== null}
        onOpenChange={(open) => !open && setMoveTarget(null)}
        subject={
          moveTarget && moveDetail && moveZone
            ? {
                id: moveTarget.id,
                catName: moveTarget.catName,
                serviceName: moveDetail.service.name,
                startsOn: moveTarget.startsOn,
                performerId: moveDetail.performerId,
                roomId: moveDetail.roomId,
              }
            : null
        }
        zone={moveZone ?? ''}
        gridMinutes={choices.gridMinutes}
        performers={choices.performers}
        rooms={choices.rooms}
        onMove={async (target) => {
          if (!moveTarget || !moveDetail) return;
          const expectedVersion = adoptLive
            ? moveDetail.version
            : (openedVersion ?? moveDetail.version);
          try {
            await move.mutateAsync({
              appointmentId: moveTarget.id,
              request: { expectedVersion, ...target },
            });
          } catch (error) {
            if (getViolationCode(error) === 'StaleCalendarVersion') setAdoptLive(true);
            throw error;
          }
          toast({ title: 'Appointment moved' });
        }}
      />
      <CancelDialog
        open={cancelTarget !== null}
        onOpenChange={(open) => !open && setCancelTarget(null)}
        catName={cancelTarget?.catName ?? ''}
        serviceName="appointment"
        when={null}
        onCancel={async (reason) => {
          if (!cancelTarget) return;
          await cancel.mutateAsync({ appointmentId: cancelTarget.id, reason });
          toast({ title: 'Appointment cancelled' });
        }}
      />
    </div>
  );
}
