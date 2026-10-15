'use client';

import { useMemo, useState } from 'react';
import { Button } from '@/components/ui/button';
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
  useDialogReturnFocus,
} from '@/components/ui/dialog';
import { describeAppointmentError } from '@/lib/api/errors';
import { useAdminServices } from '@/lib/hooks/useAdminCatalogue';
import { useActiveBranchId } from '@/lib/hooks/useActiveBranch';
import { useBookingPerformers } from '@/lib/hooks/useBooking';
import { useBookStaffAppointment, useStaffSlots } from '@/lib/hooks/useStaffAppointments';
import { addDaysIso, formatSlot } from '@/lib/format/appointment';
import { localDateOf, todayInZone } from '@/lib/format/time';
import type { StaffSlot } from '@/lib/api/staff-appointments';

/**
 * `FollowUpBookingDialog` — HO-EMR-002 §11.3 (US-CLN-18, "Book an appointment"). HO-APT-002's staff slot
 * list in a dialog: service, date (after today), vet, then the staff slots the SERVER offers
 * (`/api/staff/appointments/slots`, out-of-hours marked in words). *Book* creates the appointment
 * through HO-APT-001 and hands back its id so the caller can set it as the follow-up. A lost race or
 * refusal is shown here and the choices are kept. No new slot logic lives in this file.
 */
export function FollowUpBookingDialog({
  open,
  onOpenChange,
  customerId,
  catId,
  catName,
  defaultServiceId,
  zone,
  onBooked,
}: {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  customerId: string | null;
  catId: string;
  catName: string;
  defaultServiceId: string;
  zone: string;
  onBooked: (appointmentId: string, dateIso: string) => Promise<void>;
}) {
  const returnFocusRef = useDialogReturnFocus(open);
  const branchId = useActiveBranchId();
  const services = useAdminServices();
  const bookable = useMemo(
    () => (services.data ?? []).filter((s) => s.isActive && s.isPriced),
    [services.data],
  );
  const [serviceId, setServiceId] = useState(defaultServiceId);
  const [date, setDate] = useState(() => addDaysIso(todayInZone(zone), 14));
  const [performerId, setPerformerId] = useState('');
  const [chosen, setChosen] = useState<StaffSlot | null>(null);
  const [error, setError] = useState<string | null>(null);
  const performers = useBookingPerformers(branchId, serviceId || null);
  const slots = useStaffSlots({
    serviceId: serviceId || undefined,
    date: date || undefined,
    performerId: performerId || undefined,
    catId,
  });
  const book = useBookStaffAppointment();
  const minDate = addDaysIso(todayInZone(zone), 1);
  const selectClass =
    'rounded-[var(--radius-sm)] border border-border-strong bg-surface px-3 py-2 text-sm text-ink focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-accent';

  async function submit() {
    if (!chosen || !customerId) return;
    setError(null);
    try {
      const result = await book.mutateAsync({
        customerId,
        catId,
        serviceId,
        startsOn: chosen.startsOn,
        performerId: chosen.performerId,
        roomId: chosen.roomId,
      });
      await onBooked(result.appointmentId, localDateOf(chosen.startsOn, zone));
      onOpenChange(false);
    } catch (caught) {
      setError(describeAppointmentError(caught));
      void slots.refetch();
    }
  }

  return (
    <Dialog open={open} onOpenChange={(next) => !book.isPending && onOpenChange(next)}>
      <DialogContent
        returnFocusRef={returnFocusRef}
        className="max-h-[92vh] max-w-xl overflow-y-auto"
      >
        <DialogHeader>
          <DialogTitle>Book a follow-up for {catName}</DialogTitle>
          <DialogDescription>
            Pick a time the clinic offers. The appointment is booked now and linked to this visit.
          </DialogDescription>
        </DialogHeader>
        <div className="grid gap-3 min-[460px]:grid-cols-2">
          <div className="flex flex-col gap-1.5">
            <label htmlFor="fu-service" className="text-sm font-semibold text-ink">
              Service
            </label>
            <select
              id="fu-service"
              value={serviceId}
              onChange={(e) => {
                setServiceId(e.target.value);
                setChosen(null);
              }}
              className={selectClass}
            >
              {!bookable.some((s) => s.id === serviceId) && (
                <option value={serviceId}>Choose a service…</option>
              )}
              {bookable.map((s) => (
                <option key={s.id} value={s.id}>
                  {s.name}
                </option>
              ))}
            </select>
          </div>
          <div className="flex flex-col gap-1.5">
            <label htmlFor="fu-date" className="text-sm font-semibold text-ink">
              Date ({zone})
            </label>
            <input
              id="fu-date"
              type="date"
              min={minDate}
              value={date}
              onChange={(e) => {
                setDate(e.target.value);
                setChosen(null);
              }}
              className={selectClass}
            />
          </div>
          <div className="flex flex-col gap-1.5">
            <label htmlFor="fu-vet" className="text-sm font-semibold text-ink">
              Vet or nurse
            </label>
            <select
              id="fu-vet"
              value={performerId}
              onChange={(e) => {
                setPerformerId(e.target.value);
                setChosen(null);
              }}
              className={selectClass}
            >
              <option value="">Any vet</option>
              {(performers.data ?? []).map((p) => (
                <option key={p.id} value={p.id}>
                  {p.name}
                </option>
              ))}
            </select>
          </div>
        </div>

        <fieldset className="m-0 mt-4 flex flex-col gap-2 border-0 p-0">
          <legend className="mb-1 text-sm font-semibold text-ink">Times offered</legend>
          {slots.isFetching && (
            <p role="status" className="text-sm text-ink-muted">
              Looking for times…
            </p>
          )}
          {slots.isError && (
            <p role="alert" className="text-sm font-semibold text-danger-ink">
              The times couldn’t be loaded. Try again.
            </p>
          )}
          {!slots.isFetching && slots.data && slots.data.length === 0 && (
            <p className="text-sm text-ink-muted">
              No times are offered on this date. Try another day.
            </p>
          )}
          <div className="grid gap-2 min-[420px]:grid-cols-2">
            {(slots.data ?? []).map((slot) => {
              const key = `${slot.startsOn}|${slot.roomId}|${slot.performerId}`;
              const selected =
                chosen && `${chosen.startsOn}|${chosen.roomId}|${chosen.performerId}` === key;
              return (
                <button
                  key={key}
                  type="button"
                  aria-pressed={!!selected}
                  onClick={() => setChosen(slot)}
                  className={`rounded-[var(--radius-sm)] border px-3 py-2 text-left text-sm focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-accent ${selected ? 'border-accent bg-accent-soft text-accent-ink' : 'border-border-strong text-ink hover:bg-surface-sunken'}`}
                >
                  <span className="block font-semibold">
                    {formatSlot(slot.startsOn, slot.endsOn, zone)}
                  </span>
                  <span className="block text-xs text-ink-muted">
                    {slot.performerName}
                    {slot.outsideOpeningHours ? ' · Outside opening hours' : ''}
                  </span>
                </button>
              );
            })}
          </div>
        </fieldset>
        {!customerId && (
          <p role="status" className="mt-3 text-sm text-ink-muted">
            Finding the owner for this appointment…
          </p>
        )}
        {error && (
          <p role="alert" className="mt-3 text-sm font-semibold text-danger-ink">
            {error}
          </p>
        )}
        <DialogFooter className="mt-4">
          <Button variant="secondary" onClick={() => onOpenChange(false)} disabled={book.isPending}>
            Cancel
          </Button>
          <Button
            onClick={() => void submit()}
            disabled={!chosen || !customerId || book.isPending}
            aria-busy={book.isPending}
          >
            {book.isPending ? 'Booking…' : 'Book and set as follow-up'}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
