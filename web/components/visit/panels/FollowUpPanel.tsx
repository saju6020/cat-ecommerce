'use client';

import { useState } from 'react';
import { Button } from '@/components/ui/button';
import { ConfirmDialog } from '@/components/common/ConfirmDialog';
import { toast } from '@/components/ui/use-toast';
import { FollowUpBookingDialog } from './FollowUpBookingDialog';
import { describeClinicalError } from '@/lib/api/errors';
import { useStaffAppointment } from '@/lib/hooks/useStaffAppointments';
import { useStaffCustomer } from '@/lib/hooks/useStaffCustomers';
import { useClearFollowUp, useSetFollowUp } from '@/lib/hooks/useVisit';
import { addDaysIso } from '@/lib/format/appointment';
import { formatDateOnly, todayInZone } from '@/lib/format/time';
import type { VisitDetail } from '@/lib/api/visits';

/**
 * ⭐ `FollowUpPanel` — HO-EMR-002 §11.3 (US-CLN-18). The current follow-up (kind, date), and — only with
 * `setFollowUp` in `allowedActions` — **Book an appointment** (HO-APT-002's staff slots in a dialog; it
 * books through HO-APT-001, then sets the follow-up with the new appointment's id) or **Send a
 * reminder** (a date after today, with the sentence saying who is reminded and when), and **Remove**
 * (confirmed). `FollowUpDateInPast` and `FollowUpAppointmentInvalid` are shown from the server.
 */
export function FollowUpPanel({ visit }: { visit: VisitDetail }) {
  const zone = visit.branchTimeZoneId;
  const canSet = visit.allowedActions.includes('setFollowUp');
  const set = useSetFollowUp(visit.id, visit.catId);
  const clear = useClearFollowUp(visit.id, visit.catId);
  const appointment = useStaffAppointment(canSet ? visit.appointment.id : '');
  const customerId = appointment.data?.customerId ?? null;
  const customer = useStaffCustomer(customerId ?? '');
  const owner = customer.data?.fullName ?? 'the owner';

  const [bookingOpen, setBookingOpen] = useState(false);
  const [reminderOpen, setReminderOpen] = useState(false);
  const [removeOpen, setRemoveOpen] = useState(false);
  const [date, setDate] = useState('');
  const [error, setError] = useState<string | null>(null);
  const minDate = addDaysIso(todayInZone(zone), 1);

  async function saveReminder() {
    setError(null);
    if (!date || date < minDate) {
      setError('Choose a follow-up date after today.');
      return;
    }
    try {
      await set.mutateAsync({ kind: 'Reminder', dueOn: date });
      setReminderOpen(false);
      setDate('');
      toast({ title: 'Reminder set' });
    } catch (caught) {
      setError(describeClinicalError(caught));
    }
  }

  const followUp = visit.followUp;
  return (
    <section
      aria-labelledby="followup-heading"
      className="rounded-[var(--radius-md)] border border-border bg-surface"
    >
      <div className="border-b border-border px-4 py-3">
        <h2 id="followup-heading" className="text-sm font-bold text-ink">
          Follow-up
        </h2>
      </div>
      <div className="flex flex-col gap-3 p-4 text-sm">
        {followUp ? (
          <p className="text-ink">
            {followUp.kind === 'Appointment' ? 'Appointment booked for ' : 'Reminder on '}
            <strong>{formatDateOnly(followUp.dueOn)}</strong>
            {followUp.kind === 'Reminder' && (
              <span className="block text-ink-muted">We’ll remind {owner} 3 days before.</span>
            )}
          </p>
        ) : (
          <p className="text-ink-muted">No follow-up set.</p>
        )}
        {canSet && (
          <div className="flex flex-wrap gap-2">
            <Button variant="secondary" size="sm" onClick={() => setBookingOpen(true)}>
              Book an appointment
            </Button>
            <Button
              variant="secondary"
              size="sm"
              onClick={() => setReminderOpen((o) => !o)}
              aria-expanded={reminderOpen}
            >
              Send a reminder
            </Button>
            {followUp && (
              <Button variant="ghost" size="sm" onClick={() => setRemoveOpen(true)}>
                Remove
              </Button>
            )}
          </div>
        )}
        {reminderOpen && canSet && (
          <form
            noValidate
            className="flex flex-col gap-2 rounded-[var(--radius-sm)] border border-border p-3"
            onSubmit={(event) => {
              event.preventDefault();
              void saveReminder();
            }}
          >
            <label htmlFor="reminder-date" className="text-sm font-semibold text-ink">
              Remind on
            </label>
            <input
              id="reminder-date"
              type="date"
              min={minDate}
              value={date}
              onChange={(event) => setDate(event.target.value)}
              aria-describedby="reminder-help"
              className="w-fit rounded-[var(--radius-sm)] border border-border-strong bg-surface px-3 py-2 text-sm text-ink focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-accent"
            />
            <p id="reminder-help" className="text-xs text-ink-muted">
              We’ll remind {owner} 3 days before.
            </p>
            {error && (
              <p role="alert" className="text-sm font-semibold text-danger-ink">
                {error}
              </p>
            )}
            <div>
              <Button type="submit" size="sm" disabled={set.isPending}>
                Set reminder
              </Button>
            </div>
          </form>
        )}
      </div>
      <FollowUpBookingDialog
        open={bookingOpen}
        onOpenChange={setBookingOpen}
        customerId={customerId}
        catId={visit.catId}
        catName={visit.catName}
        defaultServiceId={visit.service.id}
        zone={zone}
        onBooked={async (appointmentId, dateIso) => {
          await set.mutateAsync({ kind: 'Appointment', dueOn: dateIso, appointmentId });
          toast({ title: 'Follow-up booked' });
        }}
      />
      <ConfirmDialog
        open={removeOpen}
        onOpenChange={setRemoveOpen}
        title="Remove the follow-up?"
        consequence={
          followUp?.kind === 'Appointment'
            ? 'The link to the booked appointment is removed; the appointment itself stays booked.'
            : 'The reminder will not be sent.'
        }
        confirmLabel="Remove follow-up"
        variant="destructive"
        onConfirm={async () => {
          try {
            await clear.mutateAsync();
          } finally {
            setRemoveOpen(false);
          }
        }}
      />
    </section>
  );
}
