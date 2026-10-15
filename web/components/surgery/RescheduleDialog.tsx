'use client';

import { useEffect, useRef, useState } from 'react';
import { Button } from '@/components/ui/button';
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog';
import { toast } from '@/components/ui/use-toast';
import { ApiConflictError, ApiValidationError } from '@/lib/api/client';
import { describeSurgeryError, surgeryCode } from '@/lib/api/errors';
import { formatAppointmentWhen, formatSlot } from '@/lib/format/appointment';
import { localDateOf, timeOnlyInZone } from '@/lib/format/time';
import { useSurgeryBookingData } from '@/lib/hooks/useSurgeryBookingData';
import { useRescheduleSurgery, useSurgeryServices } from '@/lib/hooks/useSurgeries';
import { mapBookingViolations, type MappedBookingErrors } from '@/lib/validation/surgery';
import type { SurgeryDetail } from '@/lib/api/surgeries';
import { BookingClashSummary } from './BookingClashSummary';
import {
  SurgeryResourcePicker,
  type ResourceChoices,
  type ResourceErrors,
} from './SurgeryResourcePicker';

const EMPTY: MappedBookingErrors = { summary: [], byField: {}, alreadyNeutered: null };

/**
 * `RescheduleDialog` — HO-SRG-002 §11.3, US-SRG-01. The same `SurgeryResourcePicker` as booking, prefilled
 * with the current date and team, asking the server for availability with `excludeSurgeryId` so the
 * surgery's own slot is not reported as busy. The confirm states old → new. A stale version or a clash is
 * shown, and what was chosen is kept (NFR-USA-5).
 */
export function RescheduleDialog({
  surgery,
  onClose,
}: {
  surgery: SurgeryDetail;
  onClose: () => void;
}) {
  const zone = surgery.branchTimeZoneId;
  const [choices, setChoices] = useState<ResourceChoices>({
    date: localDateOf(surgery.startsOn, zone),
    surgeonId: surgery.surgeon.id,
    assistantId: surgery.assistant.id,
    theatreId: surgery.theatre.id,
    start: '',
  });
  const [errors, setErrors] = useState<MappedBookingErrors>(EMPTY);
  const [step, setStep] = useState<'pick' | 'confirm'>('pick');
  const [localError, setLocalError] = useState('');
  const summaryRef = useRef<HTMLDivElement>(null);
  const reschedule = useRescheduleSurgery(surgery.id);
  // The detail view carries the procedure NAME but not its service id (a contract gap, recorded in
  // HO-SRG-002 section 1 Amended): the id is found in the server's own services list by that name.
  const services = useSurgeryServices();
  const serviceId = services.data?.find((s) => s.name === surgery.procedure.name)?.id ?? '';
  const serviceMissing = services.isSuccess && !serviceId;
  const serviceError = services.isError;
  const data = useSurgeryBookingData(
    {
      customerId: '',
      catId: surgery.catId,
      serviceId,
      date: choices.date,
      surgeonId: choices.surgeonId,
      assistantId: choices.assistantId,
      theatreId: choices.theatreId,
    },
    { excludeSurgeryId: surgery.id, withCustomer: false },
  );
  const chosenStart = data.availability.data?.starts.find((s) => s.startsOn === choices.start);

  useEffect(() => {
    if (errors.summary.length === 0) return;
    // After the confirm dialog has closed and handed focus back to its button, take it for the summary.
    const timer = setTimeout(() => summaryRef.current?.focus(), 150);
    return () => clearTimeout(timer);
  }, [errors]);

  async function confirm() {
    setErrors(EMPTY);
    try {
      await reschedule.mutateAsync({
        expectedVersion: surgery.version,
        startsOn: choices.start,
        theatreId: choices.theatreId,
        surgeonId: choices.surgeonId,
        assistantId: choices.assistantId,
      });
      toast({ title: 'Surgery rescheduled' });
      onClose();
    } catch (error) {
      setStep('pick');
      if (error instanceof ApiValidationError) setErrors(mapBookingViolations(error));
      else if (error instanceof ApiConflictError) {
        setChoices((c) => ({ ...c, start: '' }));
        setErrors({
          ...EMPTY,
          summary: [
            surgeryCode(error, 'StaleVersion')
              ? 'This surgery changed while you were working. Close this and reload the page.'
              : 'Someone has just booked that time. Availability has been refreshed.',
          ],
        });
      } else setErrors({ ...EMPTY, summary: [describeSurgeryError(error)] });
      void data.availability.refetch();
    }
  }

  const resourceErrors: ResourceErrors = {
    theatreId: errors.byField.theatreId,
    surgeonId: errors.byField.surgeonId,
    assistantId: errors.byField.assistantId,
    start: [...(localError ? [localError] : []), ...(errors.byField.start ?? [])],
  };

  return (
    <Dialog open onOpenChange={(open) => !open && !reschedule.isPending && onClose()}>
      <DialogContent className="max-h-[90vh] max-w-2xl overflow-y-auto">
        <DialogHeader>
          <DialogTitle>
            Reschedule {surgery.catName}’s {surgery.procedure.name.toLowerCase()}
          </DialogTitle>
          <DialogDescription>
            Now {formatAppointmentWhen(surgery.startsOn, zone)} (
            {formatSlot(surgery.startsOn, surgery.endsOn, zone)}), {surgery.theatre.name},{' '}
            {surgery.surgeon.name} with {surgery.assistant.name}. The owner will be told of any
            change.
          </DialogDescription>
        </DialogHeader>
        <BookingClashSummary ref={summaryRef} messages={errors.summary} />
        {serviceError ? (
          <p
            role="alert"
            className="rounded-[var(--radius-sm)] bg-danger-bg p-3 text-sm text-danger-ink"
          >
            We couldn’t load the list of theatre procedures, so times cannot be offered yet.{' '}
            <button
              type="button"
              className="font-semibold underline"
              onClick={() => void services.refetch()}
            >
              Try again
            </button>
          </p>
        ) : serviceMissing ? (
          <p
            role="alert"
            className="rounded-[var(--radius-sm)] bg-warning-bg p-3 text-sm text-warning-ink"
          >
            This procedure can no longer be found in the list of theatre procedures (it may have
            been renamed or switched off), so new times cannot be offered here. Close this, reload
            the page and try again, or cancel the surgery and book it again.
          </p>
        ) : step === 'pick' ? (
          <SurgeryResourcePicker
            choices={choices}
            onChange={(changes) => {
              setLocalError('');
              setChoices((c) => ({ ...c, ...changes }));
            }}
            staff={data.staff.data}
            theatres={data.theatres}
            availability={data.availability.data}
            availabilityLoading={data.availability.isFetching}
            availabilityError={data.availability.isError}
            onRetry={() => void data.availability.refetch()}
            zone={zone}
            errors={resourceErrors}
            nextFreeDay={data.nextFreeDay}
            onUseDay={(date) => setChoices((c) => ({ ...c, date, start: '' }))}
          />
        ) : (
          <p
            className="rounded-[var(--radius-sm)] bg-surface-sunken p-3 text-sm text-ink"
            role="status"
          >
            Move {surgery.catName}’s {surgery.procedure.name.toLowerCase()} from{' '}
            <strong>{formatAppointmentWhen(surgery.startsOn, zone)}</strong> to{' '}
            <strong>
              {chosenStart
                ? `${formatAppointmentWhen(chosenStart.startsOn, zone)}–${timeOnlyInZone(chosenStart.endsOn, zone)}`
                : ''}
            </strong>
            ?
          </p>
        )}
        <DialogFooter>
          <Button variant="secondary" onClick={onClose} disabled={reschedule.isPending}>
            Close
          </Button>
          {step === 'pick' ? (
            <Button
              disabled={serviceMissing || serviceError}
              onClick={() => {
                if (!choices.start) {
                  setLocalError('Choose a start time.');
                  return;
                }
                setStep('confirm');
              }}
            >
              Review
            </Button>
          ) : (
            <>
              <Button
                variant="secondary"
                onClick={() => setStep('pick')}
                disabled={reschedule.isPending}
              >
                Back
              </Button>
              <Button onClick={() => void confirm()} disabled={reschedule.isPending}>
                {reschedule.isPending ? 'Rescheduling…' : 'Reschedule'}
              </Button>
            </>
          )}
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
