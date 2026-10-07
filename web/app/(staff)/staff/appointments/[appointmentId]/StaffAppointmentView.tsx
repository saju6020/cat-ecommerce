'use client';

import { useState } from 'react';
import { StaffAppointmentDetail } from '@/components/appointments/StaffAppointmentDetail';
import { MoveDialog } from '@/components/calendar/MoveDialog';
import { PermissionDeniedState } from '@/components/admin/PermissionDeniedState';
import { ErrorState } from '@/components/common/ErrorState';
import { LoadingState } from '@/components/common/LoadingState';
import { toast } from '@/components/ui/use-toast';
import { useMoveChoices } from '@/lib/hooks/useMoveChoices';
import {
  useCancelStaffAppointment,
  useCheckInAppointment,
  useCompleteAppointment,
  useCustomerAppointmentSummary,
  useMoveAppointment,
  useRecordNoShow,
  useStaffAppointment,
  useStartAppointment,
} from '@/lib/hooks/useStaffAppointments';
import { useStaffCustomer } from '@/lib/hooks/useStaffCustomers';
import { ApiForbiddenError, ApiNotFoundError } from '@/lib/api/client';
import { localDateOf } from '@/lib/format/time';
import { CatHeader } from '../CatHeader';

/** Container for a staff appointment detail (HO-APT-002 §11.4). */
export function StaffAppointmentView({ appointmentId }: { appointmentId: string }) {
  const query = useStaffAppointment(appointmentId);
  const detail = query.data;
  const customer = useStaffCustomer(detail?.customerId ?? '');
  const summary = useCustomerAppointmentSummary(detail?.customerId ?? null);
  const [moveOpen, setMoveOpen] = useState(false);
  const zone = detail?.branchTimeZoneId ?? 'UTC';
  const choices = useMoveChoices(
    detail?.startsOn ? localDateOf(detail.startsOn, zone) : '',
    moveOpen && !!detail?.startsOn,
  );

  const checkIn = useCheckInAppointment();
  const start = useStartAppointment();
  const complete = useCompleteAppointment();
  const noShow = useRecordNoShow();
  const cancel = useCancelStaffAppointment();
  const move = useMoveAppointment();

  if (query.isLoading) return <LoadingState variant="card" rows={6} label="Loading appointment…" />;
  if (query.error instanceof ApiForbiddenError) return <PermissionDeniedState />;
  if (query.error instanceof ApiNotFoundError) {
    return <ErrorState title="Appointment not found" description="It may have been removed." />;
  }
  if (query.isError || !detail) return <ErrorState onRetry={() => void query.refetch()} />;

  return (
    <>
      <StaffAppointmentDetail
        detail={detail}
        catHeader={<CatHeader catId={detail.catId} />}
        customerName={customer.data?.fullName ?? null}
        noShowCount={summary.data?.noShowCount ?? null}
        onAction={async (action) => {
          if (action === 'CheckIn') await checkIn.mutateAsync({ appointmentId });
          if (action === 'Start') await start.mutateAsync({ appointmentId });
          if (action === 'Complete') await complete.mutateAsync(appointmentId);
          if (action === 'RecordNoShow') await noShow.mutateAsync(appointmentId);
          toast({ title: 'Done' });
        }}
        onMove={() => setMoveOpen(true)}
        onCancel={async (reason) => {
          await cancel.mutateAsync({ appointmentId, reason });
          toast({ title: 'Appointment cancelled' });
        }}
      />
      <MoveDialog
        open={moveOpen}
        onOpenChange={setMoveOpen}
        subject={{
          id: detail.id,
          catName: detail.catName,
          serviceName: detail.service.name,
          startsOn: detail.startsOn,
          performerId: detail.performerId,
          roomId: detail.roomId,
        }}
        zone={zone}
        gridMinutes={choices.gridMinutes}
        performers={choices.performers}
        rooms={choices.rooms}
        onMove={async (target) => {
          await move.mutateAsync({
            appointmentId,
            request: { expectedVersion: detail.version, ...target },
          });
          toast({ title: 'Appointment moved' });
        }}
      />
    </>
  );
}
