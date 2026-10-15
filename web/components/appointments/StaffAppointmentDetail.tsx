'use client';

import { useState, type ReactNode } from 'react';
import Link from 'next/link';
import { AlertTriangle } from 'lucide-react';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { ConfirmDialog } from '@/components/common/ConfirmDialog';
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
  useDialogReturnFocus,
} from '@/components/ui/dialog';
import { staffCancelSchema, type StaffCancelValues } from '@/lib/validation/appointments';
import { describeAppointmentError, submitWithServerErrors } from '@/lib/api/errors';
import { describeRescheduleFlag, formatAppointmentWhen } from '@/lib/format/appointment';
import { formatInZone } from '@/lib/format/time';
import { formatMoney } from '@/lib/format/money';
import type { AppointmentAction, AppointmentDetail } from '@/lib/api/types';
import { AppointmentClinicalLinks } from '@/components/visit/AppointmentClinicalLinks';
import { AppointmentStatusBadge } from './AppointmentStatusBadge';

/**
 * ⭐ `components/appointments/StaffAppointmentDetail.tsx` — HO-APT-002 §11.4 (US-CLN-07, 10). The
 * full cat header (UI-7, passed in), the customer with their no-show count, the service snapshot
 * with its price, when/vet/room, the status and its history, a *needs rescheduling* banner with the
 * reason, and ONLY the actions the server lists in `allowedTransitions` (ADR-FE-028): check in,
 * start, complete, no-show (naming the customer), cancel (reason required) — plus **Move…**, which
 * is the server's `Reschedule`. The reason for visit and the complaint are rendered as text.
 */
export function StaffAppointmentDetail({
  detail,
  catHeader,
  customerName,
  noShowCount,
  onAction,
  onMove,
  onCancel,
}: {
  detail: AppointmentDetail;
  catHeader: ReactNode;
  customerName: string | null;
  noShowCount: number | null;
  /** Check in / start / complete / no-show — throws on a server refusal. */
  onAction: (
    action: Extract<AppointmentAction, 'CheckIn' | 'Start' | 'Complete' | 'RecordNoShow'>,
  ) => Promise<void>;
  onMove: () => void;
  onCancel: (reason: string) => Promise<void>;
}) {
  const [noShowOpen, setNoShowOpen] = useState(false);
  const [cancelOpen, setCancelOpen] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const zone = detail.branchTimeZoneId;
  const can = (action: AppointmentAction) => detail.allowedTransitions.includes(action);

  async function run(action: Parameters<typeof onAction>[0]) {
    setError(null);
    try {
      await onAction(action);
    } catch (caught) {
      setError(describeAppointmentError(caught));
    }
  }

  return (
    <article aria-labelledby="staff-appt-heading" className="flex flex-col gap-5">
      {catHeader}
      <header className="flex flex-wrap items-center gap-3">
        <h1 id="staff-appt-heading" className="font-display text-2xl font-semibold text-ink">
          {detail.service.name}
        </h1>
        <AppointmentStatusBadge status={detail.status} />
        {detail.isWalkIn && <Badge variant="neutral">Walk-in</Badge>}
      </header>

      {detail.needsRescheduling && (
        <div
          role="status"
          className="flex items-start gap-2 rounded-[var(--radius-md)] border border-border bg-warning-bg p-4 text-sm text-warning-ink"
        >
          <AlertTriangle size={18} className="mt-0.5 flex-none" aria-hidden="true" />
          <div>
            <p className="font-semibold">This appointment needs a new time</p>
            <p>{describeRescheduleFlag(detail.rescheduleFlagReason, detail.performerName)}</p>
          </div>
        </div>
      )}

      <dl className="grid grid-cols-[auto_1fr] gap-x-4 gap-y-2 rounded-[var(--radius-md)] border border-border bg-surface p-4 text-sm">
        <dt className="font-semibold text-ink">Customer</dt>
        <dd className="flex flex-wrap items-center gap-2 text-ink-muted">
          <Link
            href={`/staff/customers/${detail.customerId}`}
            className="font-semibold text-accent-ink no-underline hover:underline"
          >
            {customerName ?? 'Open customer'}
          </Link>
          {noShowCount != null && noShowCount > 0 && (
            <Badge variant="danger">
              {noShowCount} {noShowCount === 1 ? 'no-show' : 'no-shows'}
            </Badge>
          )}
        </dd>
        <dt className="font-semibold text-ink">When</dt>
        <dd className="text-ink-muted">
          {detail.startsOn
            ? `${formatAppointmentWhen(detail.startsOn, zone)} (${zone})`
            : 'Walk-in — not scheduled'}
        </dd>
        <dt className="font-semibold text-ink">With</dt>
        <dd className="text-ink-muted">{detail.performerName ?? 'Not yet assigned'}</dd>
        <dt className="font-semibold text-ink">Room</dt>
        <dd className="text-ink-muted">{detail.roomName ?? 'Not yet assigned'}</dd>
        <dt className="font-semibold text-ink">Service</dt>
        <dd className="text-ink-muted">
          {detail.service.name} · {detail.service.durationMinutes} min ·{' '}
          {formatMoney({
            amount: String(detail.service.priceAmount),
            currencyCode: detail.service.priceCurrencyCode,
          })}
        </dd>
        <dt className="font-semibold text-ink">Booked via</dt>
        <dd className="text-ink-muted">{detail.channel}</dd>
        {detail.reasonForVisit && (
          <>
            <dt className="font-semibold text-ink">Reason for visit</dt>
            <dd className="whitespace-pre-wrap text-ink-muted">{detail.reasonForVisit}</dd>
          </>
        )}
        {detail.presentingComplaint && (
          <>
            <dt className="font-semibold text-ink">Presenting complaint</dt>
            <dd className="whitespace-pre-wrap text-ink-muted">{detail.presentingComplaint}</dd>
          </>
        )}
      </dl>

      {error && (
        <p role="alert" className="text-sm font-semibold text-danger-ink">
          {error}
        </p>
      )}

      <div className="flex flex-wrap gap-3">
        {can('CheckIn') && <Button onClick={() => void run('CheckIn')}>Check in</Button>}
        {can('Start') && <Button onClick={() => void run('Start')}>Start</Button>}
        {can('Complete') && <Button onClick={() => void run('Complete')}>Complete</Button>}
        {can('Reschedule') && (
          <Button variant="secondary" onClick={onMove}>
            Move…
          </Button>
        )}
        {can('RecordNoShow') && (
          <Button variant="secondary" onClick={() => setNoShowOpen(true)}>
            No-show
          </Button>
        )}
        {can('Cancel') && (
          <Button variant="destructive" onClick={() => setCancelOpen(true)}>
            Cancel appointment
          </Button>
        )}
      </div>

      {/* HO-EMR-002 §11.9/§13 — the two links only: Visit (when one exists) and Record consent. */}
      <AppointmentClinicalLinks
        appointmentId={detail.id}
        catId={detail.catId}
        status={detail.status}
      />

      <section aria-labelledby="history-heading">
        <h2 id="history-heading" className="mb-2 text-[15px] font-semibold text-ink">
          Status history
        </h2>
        {detail.statusHistory.length === 0 ? (
          <p className="text-sm text-ink-muted">No changes recorded yet.</p>
        ) : (
          <ol className="m-0 flex list-none flex-col gap-1 p-0 text-sm text-ink-muted">
            {detail.statusHistory.map((change, index) => (
              <li key={index}>
                {change.from} → <strong className="text-ink">{change.to}</strong> ·{' '}
                {formatInZone(change.changedOn, zone)}
              </li>
            ))}
          </ol>
        )}
      </section>

      <ConfirmDialog
        open={noShowOpen}
        onOpenChange={setNoShowOpen}
        title={`Record ${detail.catName} as a no-show?`}
        consequence={`This counts against ${customerName ?? 'the customer'}.`}
        confirmLabel="Record no-show"
        variant="destructive"
        onConfirm={async () => {
          await run('RecordNoShow');
          setNoShowOpen(false);
        }}
      />
      <CancelDialog
        open={cancelOpen}
        onOpenChange={setCancelOpen}
        catName={detail.catName}
        serviceName={detail.service.name}
        when={detail.startsOn ? formatAppointmentWhen(detail.startsOn, zone) : null}
        onCancel={onCancel}
      />
    </article>
  );
}

/** Staff cancellation: a reason is required (the server enforces it too). */
export function CancelDialog({
  open,
  onOpenChange,
  catName,
  serviceName,
  when,
  onCancel,
}: {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  catName: string;
  serviceName: string;
  when: string | null;
  onCancel: (reason: string) => Promise<void>;
}) {
  const returnFocusRef = useDialogReturnFocus(open);
  const {
    register,
    handleSubmit,
    setError,
    reset,
    formState: { errors, isSubmitting },
  } = useForm<StaffCancelValues>({
    resolver: zodResolver(staffCancelSchema),
    defaultValues: { reason: '' },
  });

  async function submit(values: StaffCancelValues) {
    const done = await submitWithServerErrors(
      async () => {
        await onCancel(values.reason);
        return true;
      },
      setError,
      { fieldMap: { Reason: 'reason' }, knownFields: ['reason'] },
    );
    if (done) {
      reset();
      onOpenChange(false);
    }
  }

  return (
    <Dialog open={open} onOpenChange={(next) => !isSubmitting && onOpenChange(next)}>
      <DialogContent returnFocusRef={returnFocusRef}>
        <form onSubmit={handleSubmit(submit)} noValidate>
          <DialogHeader>
            <DialogTitle>
              Cancel {catName}’s {serviceName.toLowerCase()}?
            </DialogTitle>
            <DialogDescription>
              {when ? `The ${when} appointment` : 'The appointment'} is cancelled, the time is
              released and the customer is told.
            </DialogDescription>
          </DialogHeader>
          <div className="flex flex-col gap-1.5">
            <label htmlFor="cancel-reason" className="text-sm font-semibold text-ink">
              Reason (required)
            </label>
            <textarea
              id="cancel-reason"
              rows={3}
              aria-invalid={!!errors.reason}
              aria-describedby={errors.reason ? 'cancel-reason-error' : undefined}
              className="rounded-[var(--radius-sm)] border border-border-strong bg-surface px-3 py-2 text-sm text-ink focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-accent"
              {...register('reason')}
            />
            {errors.reason && (
              <p
                id="cancel-reason-error"
                role="alert"
                className="text-xs font-semibold text-danger-ink"
              >
                {errors.reason.message}
              </p>
            )}
            {errors.root && (
              <p role="alert" className="text-xs font-semibold text-danger-ink">
                {errors.root.message}
              </p>
            )}
          </div>
          <DialogFooter>
            <Button
              type="button"
              variant="secondary"
              onClick={() => onOpenChange(false)}
              disabled={isSubmitting}
            >
              Keep appointment
            </Button>
            <Button type="submit" variant="destructive" disabled={isSubmitting}>
              {isSubmitting ? 'Cancelling…' : 'Cancel appointment'}
            </Button>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  );
}
