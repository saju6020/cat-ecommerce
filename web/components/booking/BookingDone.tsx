'use client';

import Link from 'next/link';
import { CalendarPlus, CheckCircle2 } from 'lucide-react';
import { Button, buttonVariants } from '@/components/ui/button';
import { buildIcs, icsDownloadUrl } from '@/lib/calendar/ics';
import { formatMoney } from '@/lib/format/money';
import type { MyAppointmentDetail } from '@/lib/api/types';
import type { BookingSummary } from './ConfirmStep';

/**
 * `components/booking/BookingDone.tsx` — HO-APT-002 §11.1 step 6. "Booked.", the details, where
 * the confirmation went, a link to the appointment and **Add to calendar** — a `.ics` generated
 * here from the confirmed data (text only, escaped; `lib/calendar/ics.ts`).
 */
export function BookingDone({
  appointment,
  summary,
  branchAddress,
  cutoffHours,
}: {
  appointment: MyAppointmentDetail;
  summary: BookingSummary;
  branchAddress: string | null;
  cutoffHours: number | null;
}) {
  function addToCalendar() {
    if (!appointment.startsOn || !appointment.endsOn) return;
    const ics = buildIcs({
      uid: appointment.id,
      summary: `${summary.service.name} for ${appointment.catName}`,
      startsOn: appointment.startsOn,
      endsOn: appointment.endsOn,
      location: [summary.branchName, branchAddress].filter(Boolean).join(', '),
      description: `${summary.service.name} with ${summary.performerName ?? 'the clinic'}. Pay at the clinic.`,
    });
    const url = icsDownloadUrl(ics);
    const anchor = document.createElement('a');
    anchor.href = url;
    anchor.download = 'purrcare-appointment.ics';
    document.body.append(anchor);
    anchor.click();
    anchor.remove();
    setTimeout(() => URL.revokeObjectURL(url), 1000);
  }

  return (
    <section aria-labelledby="done-heading" className="flex flex-col gap-5">
      <div className="flex items-center gap-3">
        <CheckCircle2 size={28} className="flex-none text-accent" aria-hidden="true" />
        <h2 id="done-heading" className="font-display text-2xl font-semibold text-ink">
          Booked.
        </h2>
      </div>
      <dl className="grid grid-cols-[auto_1fr] gap-x-4 gap-y-2 rounded-[var(--radius-md)] border border-border bg-surface p-4 text-sm">
        <dt className="font-semibold text-ink">Cat</dt>
        <dd className="text-ink-muted">{summary.catName}</dd>
        <dt className="font-semibold text-ink">Service</dt>
        <dd className="text-ink-muted">{summary.service.name}</dd>
        <dt className="font-semibold text-ink">When</dt>
        <dd className="text-ink-muted">
          {summary.whenLabel} ({summary.zone})
        </dd>
        <dt className="font-semibold text-ink">With</dt>
        <dd className="text-ink-muted">{summary.performerName ?? 'To be assigned'}</dd>
        <dt className="font-semibold text-ink">Clinic</dt>
        <dd className="text-ink-muted">
          {summary.branchName}
          {branchAddress ? `, ${branchAddress}` : ''}
        </dd>
        <dt className="font-semibold text-ink">Price</dt>
        <dd className="text-ink-muted">
          {formatMoney({
            amount: String(summary.service.priceAmount),
            currencyCode: summary.service.priceCurrencyCode,
          })}{' '}
          · <strong className="text-ink">Pay at the clinic</strong>
        </dd>
      </dl>
      <p className="text-sm text-ink-muted">
        We&rsquo;ve sent a confirmation to your preferred contact channel and your inbox.
        {cutoffHours !== null &&
          ` You can change or cancel online until ${cutoffHours} hours before; after that, please call the clinic.`}
      </p>
      <div className="flex flex-wrap gap-3">
        <Link href={`/portal/appointments/${appointment.id}`} className={buttonVariants()}>
          View appointment
        </Link>
        <Button variant="secondary" onClick={addToCalendar}>
          <CalendarPlus size={16} aria-hidden="true" />
          Add to calendar
        </Button>
        <Link href="/book" className={buttonVariants({ variant: 'ghost' })}>
          Book another
        </Link>
      </div>
    </section>
  );
}
