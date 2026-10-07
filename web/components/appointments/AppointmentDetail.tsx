'use client';

import { useState } from 'react';
import { Button } from '@/components/ui/button';
import { ConfirmDialog } from '@/components/common/ConfirmDialog';
import { formatAppointmentWhen } from '@/lib/format/appointment';
import { formatMoney } from '@/lib/format/money';
import type { MyAppointmentDetail as AppointmentDetailData } from '@/lib/api/types';
import { AppointmentStatusBadge } from './AppointmentStatusBadge';
import { ClinicContactCard } from './ClinicContactCard';

export interface ClinicContact {
  name: string;
  phone: string | null;
  email: string | null;
  address: string | null;
}

/** Whole hours between the cut-off and the start — the "less than n hours" the contact card names. */
export function cutoffHoursOf(detail: AppointmentDetailData): number | null {
  if (!detail.startsOn || !detail.changeCutoffOn) return null;
  return Math.round(
    (new Date(detail.startsOn).getTime() - new Date(detail.changeCutoffOn).getTime()) / 3_600_000,
  );
}

/**
 * ⭐ `components/appointments/AppointmentDetail.tsx` — HO-APT-002 §11.2 (US-CLN-04). Details and
 * status. Change actions are rendered only from the server's `canChange` (ADR-FE-028): **Reschedule**
 * and **Cancel** (the latter behind a confirmation that states what happens) while it is true; once
 * it is false for a booked appointment, the `ClinicContactCard` instead (AC2). The reason for visit
 * is the customer's own text and is rendered as plain text.
 */
export function AppointmentDetail({
  detail,
  contact,
  onReschedule,
  onCancel,
  actionError,
  rescheduleLoading = false,
}: {
  detail: AppointmentDetailData;
  contact: ClinicContact;
  onReschedule: () => void;
  onCancel: () => Promise<void>;
  actionError?: string | null;
  /** The bookable-services list that decides whether Reschedule works is still loading. */
  rescheduleLoading?: boolean;
}) {
  const [cancelOpen, setCancelOpen] = useState(false);
  const zone = detail.branchTimeZoneId;
  const when = detail.startsOn && `${formatAppointmentWhen(detail.startsOn, zone)} (${zone})`;

  return (
    <article aria-labelledby="appointment-heading" className="flex flex-col gap-5">
      <header className="flex flex-wrap items-center gap-3">
        <h1 id="appointment-heading" className="font-display text-2xl font-semibold text-ink">
          {detail.service.name} · {detail.catName}
        </h1>
        <AppointmentStatusBadge status={detail.status} />
      </header>

      <dl className="grid grid-cols-[auto_1fr] gap-x-4 gap-y-2 rounded-[var(--radius-md)] border border-border bg-surface p-4 text-sm">
        <dt className="font-semibold text-ink">When</dt>
        <dd className="text-ink-muted">{when ?? 'To be confirmed'}</dd>
        <dt className="font-semibold text-ink">With</dt>
        <dd className="text-ink-muted">{detail.performerName ?? 'To be assigned'}</dd>
        <dt className="font-semibold text-ink">Clinic</dt>
        <dd className="text-ink-muted">
          {contact.name}
          {contact.address ? `, ${contact.address}` : ''}
        </dd>
        <dt className="font-semibold text-ink">Price</dt>
        <dd className="text-ink-muted">
          {formatMoney({
            amount: String(detail.service.priceAmount),
            currencyCode: detail.service.priceCurrencyCode,
          })}{' '}
          · <strong className="text-ink">Pay at the clinic</strong>
        </dd>
        {detail.reasonForVisit && (
          <>
            <dt className="font-semibold text-ink">Reason for visit</dt>
            <dd className="whitespace-pre-wrap text-ink-muted">{detail.reasonForVisit}</dd>
          </>
        )}
      </dl>

      {actionError && (
        <p role="alert" className="text-sm font-semibold text-danger-ink">
          {actionError}
        </p>
      )}

      {detail.canChange && (
        <div className="flex flex-wrap gap-3">
          <Button onClick={onReschedule} disabled={rescheduleLoading} aria-busy={rescheduleLoading}>
            Reschedule
          </Button>
          <Button variant="destructive" onClick={() => setCancelOpen(true)}>
            Cancel appointment
          </Button>
        </div>
      )}

      {!detail.canChange && detail.status === 'Booked' && (
        <ClinicContactCard
          branchName={contact.name}
          phone={contact.phone}
          email={contact.email}
          cutoffHours={cutoffHoursOf(detail)}
        />
      )}

      <ConfirmDialog
        open={cancelOpen}
        onOpenChange={setCancelOpen}
        title={`Cancel ${detail.catName}’s ${detail.service.name.toLowerCase()}?`}
        consequence={`Your appointment${when ? ` on ${when}` : ''} will be cancelled and the time released. You can book again at any time.`}
        confirmLabel="Cancel appointment"
        cancelLabel="Keep appointment"
        pendingLabel="Cancelling…"
        variant="destructive"
        onConfirm={async () => {
          await onCancel();
          setCancelOpen(false);
        }}
      />
    </article>
  );
}
