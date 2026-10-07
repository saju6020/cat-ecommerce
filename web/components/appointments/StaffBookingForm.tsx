'use client';

import { useEffect, useMemo, useState, type ReactNode } from 'react';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { Button } from '@/components/ui/button';
import { ConfirmDialog } from '@/components/common/ConfirmDialog';
import { UnsavedChangesGuard } from '@/components/account/UnsavedChangesGuard';
import {
  staffBookingSchema,
  MAX_APPOINTMENT_TEXT,
  type StaffBookingValues,
} from '@/lib/validation/appointments';
import { getViolationCode, submitWithServerErrors } from '@/lib/api/errors';
import { formatAppointmentWhen, formatSlot } from '@/lib/format/appointment';
import { timeOnlyInZone } from '@/lib/format/time';
import type { StaffSlot } from '@/lib/api/staff-appointments';
import type { StaffCustomerSummary } from '@/lib/api/staff-customers';
import { CustomerPicker, type PickedCustomer } from './CustomerPicker';

export interface BookingQuery {
  catId: string;
  serviceId: string;
  date: string;
  performerId: string;
}

const FIELD_MAP = {
  CustomerId: 'customerId',
  CatId: 'catId',
  ServiceId: 'serviceId',
  StartsOn: 'slotStart',
  PerformerId: 'performerId',
  ReasonForVisit: 'reasonForVisit',
} as const;

const SLOT_TAKEN_MESSAGE =
  'Someone else has just booked that time. The times have been refreshed — pick another.';

/** One offered slot: the start, the room AND the vet (two vets can offer the same start and room). */
function slotKey(slot: StaffSlot): string {
  return `${slot.startsOn}|${slot.roomId}|${slot.performerId}`;
}

const cls =
  'rounded-[var(--radius-sm)] border border-border-strong bg-surface px-3 py-2 text-sm text-ink focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-accent disabled:opacity-50';

/**
 * ⭐ `components/appointments/StaffBookingForm.tsx` — HO-APT-002 §11.3 (US-CLN-07, 08). Customer
 * (with the inline no-show badge — *visible at booking*), cat (with its full UI-7 header, passed in
 * as `catHeader`), service, date, vet (required for staff), then the staff slots the SERVER offers,
 * with out-of-hours times marked *"Outside opening hours"* in text. **Book** asks for confirmation.
 * A lost race (`409`) refreshes the times and says so; every other refusal lands on its field or at
 * the top, and the form keeps what was typed (NFR-USA-5). Room override is not offered — no
 * free-rooms endpoint exists, so the slot's own room is used (HO-APT-002 §1 Amended).
 */
export function StaffBookingForm({
  customer,
  onSelectCustomer,
  onClearCustomer,
  onSearch,
  results,
  isSearching,
  searchedTerm,
  cats,
  catHeader,
  services,
  performers,
  slots,
  slotsLoading,
  slotsError,
  zone,
  initialCatId,
  initialDate,
  initialPerformerId,
  initialTime,
  onQueryChange,
  onSlotTaken,
  onBook,
}: {
  customer: PickedCustomer | null;
  onSelectCustomer: (customer: StaffCustomerSummary) => void;
  onClearCustomer: () => void;
  onSearch: (term: string) => void;
  results: StaffCustomerSummary[] | undefined;
  isSearching: boolean;
  searchedTerm: string | null;
  cats: Array<{ id: string; name: string }>;
  catHeader: ReactNode;
  services: Array<{ id: string; name: string }>;
  performers: Array<{ id: string; name: string }>;
  slots: StaffSlot[] | undefined;
  slotsLoading: boolean;
  slotsError: boolean;
  zone: string;
  initialCatId?: string;
  /** Prefill from a calendar cell: the day, the vet and the local `HH:mm` start. */
  initialDate?: string;
  initialPerformerId?: string;
  initialTime?: string;
  onQueryChange: (query: BookingQuery) => void;
  onSlotTaken: () => void;
  onBook: (values: StaffBookingValues & { roomId: string; startsOn: string }) => Promise<void>;
}) {
  const [confirming, setConfirming] = useState<StaffBookingValues | null>(null);
  const {
    register,
    handleSubmit,
    setError,
    setValue,
    watch,
    formState: { errors, isDirty },
  } = useForm<StaffBookingValues>({
    resolver: zodResolver(staffBookingSchema),
    defaultValues: {
      customerId: '',
      catId: initialCatId ?? '',
      serviceId: '',
      date: initialDate ?? '',
      performerId: initialPerformerId ?? '',
      slotStart: '',
      reasonForVisit: '',
    },
  });
  const { catId, serviceId, date, performerId, slotStart } = watch();
  const customerId = customer?.id ?? '';

  useEffect(() => {
    setValue('customerId', customerId);
    if (cats.length === 1) setValue('catId', cats[0]!.id);
  }, [customerId, cats, setValue]);

  useEffect(() => {
    onQueryChange({ catId, serviceId, date, performerId });
    setValue('slotStart', '');
  }, [catId, serviceId, date, performerId, onQueryChange, setValue]);

  const shownSlots = useMemo(() => slots ?? [], [slots]);

  // A time carried in from a calendar cell selects the matching offered slot once, when it appears.
  const [timeApplied, setTimeApplied] = useState(false);
  useEffect(() => {
    if (timeApplied || !initialTime || shownSlots.length === 0) return;
    const match = shownSlots.find((slot) => timeOnlyInZone(slot.startsOn, zone) === initialTime);
    if (match) setValue('slotStart', slotKey(match));
    setTimeApplied(true);
  }, [shownSlots, initialTime, timeApplied, zone, setValue]);
  const chosen = shownSlots.find((slot) => slotKey(slot) === slotStart);
  const disabled = !customer;
  const label = (id: string, list: Array<{ id: string; name: string }>) =>
    list.find((item) => item.id === id)?.name ?? '';

  async function book() {
    if (!confirming || !chosen) return;
    const values = confirming;
    await submitWithServerErrors(
      async () => {
        try {
          // The chosen slot's own vet: the form's vet filter may be empty, and two vets can offer the
          // same start and room.
          await onBook({
            ...values,
            performerId: chosen.performerId,
            roomId: chosen.roomId,
            startsOn: chosen.startsOn,
          });
        } catch (error) {
          if (getViolationCode(error) === 'SlotNoLongerAvailable') {
            // The taken time must not stay selected: the next Book would open an empty confirm.
            setValue('slotStart', '');
            onSlotTaken();
            setError('root', { type: 'server', message: SLOT_TAKEN_MESSAGE });
            return;
          }
          throw error;
        }
      },
      setError,
      { fieldMap: FIELD_MAP, knownFields: Object.values(FIELD_MAP) },
    );
    setConfirming(null);
  }

  return (
    <form
      method="post"
      noValidate
      onSubmit={handleSubmit((values) => setConfirming(values))}
      className="flex max-w-2xl flex-col gap-5"
    >
      <UnsavedChangesGuard when={isDirty} />

      <CustomerPicker
        selected={customer}
        onSelect={onSelectCustomer}
        onClear={onClearCustomer}
        onSearch={onSearch}
        results={results}
        isSearching={isSearching}
        searchedTerm={searchedTerm}
      />

      <div className="flex flex-col gap-1.5">
        <label htmlFor="booking-cat" className="text-sm font-semibold text-ink">
          Cat
        </label>
        <select
          id="booking-cat"
          disabled={disabled}
          className={cls}
          aria-invalid={!!errors.catId}
          {...register('catId')}
        >
          <option value="">Choose…</option>
          {cats.map((cat) => (
            <option key={cat.id} value={cat.id}>
              {cat.name}
            </option>
          ))}
        </select>
        {errors.catId && (
          <p role="alert" className="text-xs font-semibold text-danger-ink">
            {errors.catId.message}
          </p>
        )}
      </div>
      {catId && <div>{catHeader}</div>}

      <div className="flex flex-col gap-1.5">
        <label htmlFor="booking-service" className="text-sm font-semibold text-ink">
          Service
        </label>
        <select
          id="booking-service"
          disabled={disabled}
          className={cls}
          aria-invalid={!!errors.serviceId}
          {...register('serviceId')}
        >
          <option value="">Choose…</option>
          {services.map((service) => (
            <option key={service.id} value={service.id}>
              {service.name}
            </option>
          ))}
        </select>
        {errors.serviceId && (
          <p role="alert" className="text-xs font-semibold text-danger-ink">
            {errors.serviceId.message}
          </p>
        )}
      </div>

      <div className="grid gap-4 sm:grid-cols-2">
        <div className="flex flex-col gap-1.5">
          <label htmlFor="booking-date" className="text-sm font-semibold text-ink">
            Date ({zone})
          </label>
          <input
            id="booking-date"
            type="date"
            disabled={disabled}
            className={cls}
            aria-invalid={!!errors.date}
            {...register('date')}
          />
          {errors.date && (
            <p role="alert" className="text-xs font-semibold text-danger-ink">
              {errors.date.message}
            </p>
          )}
        </div>
        <div className="flex flex-col gap-1.5">
          <label htmlFor="booking-performer" className="text-sm font-semibold text-ink">
            Vet or nurse
          </label>
          <select
            id="booking-performer"
            disabled={disabled}
            className={cls}
            aria-invalid={!!errors.performerId}
            {...register('performerId')}
          >
            <option value="">Choose…</option>
            {performers.map((p) => (
              <option key={p.id} value={p.id}>
                {p.name}
              </option>
            ))}
          </select>
          {errors.performerId && (
            <p role="alert" className="text-xs font-semibold text-danger-ink">
              {errors.performerId.message}
            </p>
          )}
        </div>
      </div>

      <div className="flex flex-col gap-1.5">
        <label htmlFor="booking-slot" className="text-sm font-semibold text-ink">
          Time ({zone})
        </label>
        <select
          id="booking-slot"
          disabled={disabled || !serviceId || !date || !performerId}
          className={cls}
          aria-invalid={!!errors.slotStart}
          {...register('slotStart')}
        >
          <option value="">
            {slotsLoading
              ? 'Loading times…'
              : shownSlots.length === 0 && serviceId && date && performerId
                ? 'No times'
                : 'Choose…'}
          </option>
          {shownSlots.map((slot) => (
            <option key={slotKey(slot)} value={slotKey(slot)}>
              {formatSlot(slot.startsOn, slot.endsOn, zone)} · {slot.roomName}
              {slot.outsideOpeningHours ? ' · Outside opening hours' : ''}
            </option>
          ))}
        </select>
        {slotsError && (
          <p role="alert" className="text-xs font-semibold text-danger-ink">
            We couldn’t load the times just now.
          </p>
        )}
        {errors.slotStart && (
          <p role="alert" className="text-xs font-semibold text-danger-ink">
            {errors.slotStart.message}
          </p>
        )}
      </div>

      <div className="flex flex-col gap-1.5">
        <label htmlFor="booking-reason" className="text-sm font-semibold text-ink">
          Reason for visit <span className="font-normal text-ink-muted">(optional)</span>
        </label>
        <textarea
          id="booking-reason"
          rows={2}
          disabled={disabled}
          className={cls}
          {...register('reasonForVisit')}
        />
        <p className="text-xs text-ink-muted">
          Plain text, up to {MAX_APPOINTMENT_TEXT} characters.
        </p>
        {errors.reasonForVisit && (
          <p role="alert" className="text-xs font-semibold text-danger-ink">
            {errors.reasonForVisit.message}
          </p>
        )}
      </div>

      {errors.root && (
        <p role="alert" className="text-sm font-semibold text-danger-ink">
          {errors.root.message}
        </p>
      )}

      <div>
        <Button type="submit" disabled={disabled}>
          Book
        </Button>
      </div>

      <ConfirmDialog
        open={confirming !== null}
        onOpenChange={(open) => !open && setConfirming(null)}
        title="Book this appointment?"
        consequence={
          chosen && confirming
            ? `${label(confirming.catId, cats)}’s ${label(confirming.serviceId, services).toLowerCase()} with ${label(confirming.performerId, performers)} on ${formatAppointmentWhen(chosen.startsOn, zone)} (${zone})${chosen.outsideOpeningHours ? ', outside opening hours' : ''}. The customer is sent a confirmation.`
            : ''
        }
        confirmLabel="Book"
        pendingLabel="Booking…"
        onConfirm={book}
      />
    </form>
  );
}
