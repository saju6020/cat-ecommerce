'use client';

import { useCallback, useMemo, useRef, useState } from 'react';
import Link from 'next/link';
import { AppointmentDetail, type ClinicContact } from '@/components/appointments/AppointmentDetail';
import { ClaimOfferedSlot } from '@/components/appointments/ClaimOfferedSlot';
import { RescheduleSlotPicker } from '@/components/appointments/RescheduleSlotPicker';
import { buttonVariants } from '@/components/ui/button';
import { toast } from '@/components/ui/use-toast';
import { LoadingState } from '@/components/common/LoadingState';
import { ErrorState } from '@/components/common/ErrorState';
import {
  useMyAppointment,
  useCancelMyAppointment,
  useRescheduleMyAppointment,
} from '@/lib/hooks/useMyAppointments';
import {
  useBookableServices,
  useBookingPerformers,
  useBookingSlots,
  useClinicBranches,
  useConfirmAppointment,
} from '@/lib/hooks/useBooking';
import { ApiNotFoundError } from '@/lib/api/client';
import { describeAppointmentError, getViolationCode } from '@/lib/api/errors';
import { findServiceIdForSnapshot } from '@/lib/api/booking';
import { formatAppointmentWhen } from '@/lib/format/appointment';
import { localDateOf, todayInZone } from '@/lib/format/time';

const RACE_MESSAGE =
  'Someone has just taken that time. Please choose another — your current appointment is unchanged.';

/** Container for a customer's appointment: detail, reschedule, cancel, claim (HO-APT-002 §11.2). */
export function MyAppointmentView({ appointmentId }: { appointmentId: string }) {
  const query = useMyAppointment(appointmentId);
  const branches = useClinicBranches();
  const services = useBookableServices();
  const reschedule = useRescheduleMyAppointment(appointmentId);
  const cancel = useCancelMyAppointment(appointmentId);
  const confirm = useConfirmAppointment();
  const [rescheduling, setRescheduling] = useState(false);
  const [actionError, setActionError] = useState<string | null>(null);
  const [notice, setNotice] = useState<string | null>(null);
  const [date, setDate] = useState<string | null>(null);
  const [performerId, setPerformerId] = useState<string | null>(null);
  const listRef = useRef<HTMLDivElement>(null);

  const detail = query.data;
  const branch = branches.data?.find((b) => b.id === detail?.branchId) ?? null;
  const serviceId = useMemo(
    () =>
      detail && services.data ? findServiceIdForSnapshot(services.data, detail.service) : null,
    [detail, services.data],
  );
  // No UTC stand-in while the appointment loads (W16): until its zone is known, nothing below
  // that needs a date is computed and the view shows a loading state.
  const zone = detail?.branchTimeZoneId ?? null;
  const todayIso = zone ? todayInZone(zone) : '';
  // The branch-local day of the appointment, not the UTC one (W5).
  const effectiveDate =
    date ?? (detail?.startsOn && zone ? localDateOf(detail.startsOn, zone) : todayIso);
  const effectiveVet = performerId ?? detail?.performerId ?? '';
  const slotsQuery = useBookingSlots(
    rescheduling && detail && serviceId
      ? {
          branchId: detail.branchId,
          serviceId,
          date: effectiveDate >= todayIso ? effectiveDate : todayIso,
          performerId: effectiveVet || undefined,
          catId: detail.catId,
        }
      : {},
  );
  const performers = useBookingPerformers(
    rescheduling ? (detail?.branchId ?? null) : null,
    serviceId,
  );

  const focusList = useCallback(() => requestAnimationFrame(() => listRef.current?.focus()), []);

  if (query.isLoading) return <LoadingState variant="card" rows={5} label="Loading appointment…" />;
  if (query.error instanceof ApiNotFoundError) {
    return (
      <ErrorState
        title="We couldn’t find that appointment."
        description="It may have been removed, or it belongs to another account."
      />
    );
  }
  if (query.isError || !detail) {
    return <ErrorState onRetry={() => void query.refetch()} />;
  }
  if (!zone) return <LoadingState variant="card" rows={5} label="Loading appointment…" />;

  const contact: ClinicContact = {
    name: branch?.name ?? 'the clinic',
    phone: branch?.phone ?? null,
    email: branch?.email ?? null,
    address: branch?.address ?? null,
  };

  const back = (
    <Link href="/portal/appointments" className={buttonVariants({ variant: 'ghost', size: 'sm' })}>
      ← All appointments
    </Link>
  );

  if (detail.status === 'Held' && detail.isWaitlistOffer) {
    return (
      <div className="flex flex-col gap-4">
        {back}
        <ClaimOfferedSlot
          detail={detail}
          onConfirm={async () => {
            await confirm.mutateAsync({ appointmentId: detail.id, reasonForVisit: null });
            toast({ title: 'Booked', description: 'Your appointment is confirmed.' });
            await query.refetch();
          }}
        />
      </div>
    );
  }
  if (detail.status === 'HoldExpired' && detail.isWaitlistOffer) {
    return (
      <div className="flex flex-col gap-4">
        {back}
        <ClaimOfferedSlot detail={detail} onConfirm={async () => undefined} />
      </div>
    );
  }

  if (rescheduling) {
    const currentWhen = detail.startsOn
      ? `${formatAppointmentWhen(detail.startsOn, zone)} (${zone})`
      : '—';
    return (
      <div className="flex flex-col gap-4">
        {back}
        <RescheduleSlotPicker
          ref={listRef}
          zone={zone}
          todayIso={todayIso}
          date={effectiveDate >= todayIso ? effectiveDate : todayIso}
          onDateChange={(next) => {
            setNotice(null);
            setDate(next);
          }}
          openWeekdays={branch?.openWeekdays}
          performers={performers.data ?? []}
          performerId={effectiveVet}
          onPerformerChange={(id) => {
            setNotice(null);
            setPerformerId(id);
          }}
          slots={slotsQuery.data}
          isLoading={slotsQuery.isLoading}
          isError={slotsQuery.isError}
          onRetry={() => void slotsQuery.refetch()}
          notice={notice}
          currentWhen={currentWhen}
          onCancel={() => {
            setRescheduling(false);
            setNotice(null);
          }}
          onMove={async (slot, performer) => {
            setNotice(null);
            try {
              await reschedule.mutateAsync({
                startsOn: slot.startsOn,
                performerId: performer?.id,
              });
              toast({ title: 'Appointment moved' });
              setRescheduling(false);
              await query.refetch();
            } catch (error) {
              setNotice(
                getViolationCode(error) === 'SlotNoLongerAvailable'
                  ? RACE_MESSAGE
                  : describeAppointmentError(error),
              );
              void slotsQuery.refetch();
              focusList();
            }
          }}
        />
      </div>
    );
  }

  return (
    <div className="flex flex-col gap-4">
      {back}
      <AppointmentDetail
        detail={detail}
        contact={contact}
        actionError={actionError}
        rescheduleLoading={services.isLoading}
        onReschedule={() => {
          setActionError(null);
          if (services.isError) {
            setActionError(
              'We couldn’t check whether this can be rescheduled online. Please try again.',
            );
            void services.refetch();
            return;
          }
          if (!serviceId) {
            setActionError(
              'This service can’t be rescheduled online. Please call the clinic to move it.',
            );
            return;
          }
          setRescheduling(true);
        }}
        onCancel={async () => {
          setActionError(null);
          try {
            await cancel.mutateAsync(null);
            toast({ title: 'Appointment cancelled' });
            await query.refetch();
          } catch (error) {
            setActionError(describeAppointmentError(error));
          }
        }}
      />
    </div>
  );
}
