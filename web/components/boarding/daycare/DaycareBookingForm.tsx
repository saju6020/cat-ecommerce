'use client';

import { useState } from 'react';
import Link from 'next/link';
import { Button } from '@/components/ui/button';
import { FormFieldRow } from '@/components/cat/FormFieldRow';
import { EmptyState } from '@/components/common/EmptyState';
import { FormErrorAlert, SelectRow } from '@/components/boarding/shared/FormBits';
import { QueryGate } from '@/components/boarding/shared/QueryGate';
import { describeGateReason } from '@/components/boarding/shared/labels';
import { ApiValidationError } from '@/lib/api/client';
import { normaliseGate } from '@/lib/api/my-boarding';
import { describeBoardingError, getViolationCode } from '@/lib/api/errors';
import type { DaycareBasis, VaccinationGate } from '@/lib/api/types';
import { formatFullDate } from '@/lib/format/boarding';
import { formatMoney } from '@/lib/format/money';
import { todayInZone } from '@/lib/format/time';
import { useCats } from '@/lib/hooks/useCats';
import { usePublicBranches } from '@/lib/hooks/useMyOrders';
import {
  useBoardingUnitTypes,
  useBookDaycare,
  useCancelDaycare,
  useMyDaycareVisits,
} from '@/lib/hooks/useMyBoarding';
import { PORTAL_BOARDING } from '@/lib/routing/boarding-links';
import { isSafeInternalPath } from '@/lib/routing/isSafeInternalPath';

/**
 * `components/boarding/daycare/DaycareBookingForm.tsx` — HO-BRD-002 §11.12, US-BRD-21: cat, date, the
 * basis (hourly or whole day — **both prices shown**) and the hours expected. A branch that does not
 * offer daycare says *“Daycare isn’t offered at this branch.”*; a full day and a vaccination block
 * are said in words, the latter with the link to book the vaccination. The charge appears after
 * check-out, from the server. No payment control.
 */
export function DaycareBookingForm() {
  const branches = usePublicBranches();
  const cats = useCats({ pageNumber: 1, pageSize: 100 });
  const offer = useBoardingUnitTypes();
  const visits = useMyDaycareVisits();
  const book = useBookDaycare();
  const cancel = useCancelDaycare();
  const branch = branches.data?.[0];
  const [catId, setCatId] = useState('');
  const [date, setDate] = useState('');
  const [basis, setBasis] = useState<DaycareBasis>('Day');
  const [hours, setHours] = useState('4');
  const [error, setError] = useState<string | null>(null);
  const [gate, setGate] = useState<VaccinationGate | null>(null);
  const [done, setDone] = useState(false);
  const daycare = offer.data?.daycare;
  const notOffered = daycare && !daycare.offered;

  async function submit(e: React.FormEvent) {
    e.preventDefault();
    setError(null);
    setGate(null);
    setDone(false);
    if (!catId || !date || !branch) {
      setError('Choose a cat and a date.');
      return;
    }
    try {
      await book.mutateAsync({
        branchId: branch.id,
        catId,
        localDate: date,
        basis,
        expectedHours: basis === 'Hourly' ? Number(hours) : undefined,
      });
      setDone(true);
    } catch (err) {
      if (err instanceof ApiValidationError && getViolationCode(err) === 'VaccinationGateBlocked') {
        const sent = (err.response as unknown as { vaccinationGate?: VaccinationGate })
          .vaccinationGate;
        setGate(sent ? normaliseGate(sent) : null);
      }
      setError(describeBoardingError(err));
    }
  }

  return (
    <div className="flex flex-col gap-8">
      {notOffered ? (
        <EmptyState
          title="Daycare isn’t offered at this branch."
          description="You can still book a boarding stay."
          action={
            <Link href="/boarding" className="font-semibold text-accent-ink underline">
              Book a stay
            </Link>
          }
        />
      ) : (
        <form
          onSubmit={submit}
          noValidate
          aria-label="Book daycare"
          className="flex max-w-xl flex-col gap-4 rounded-[var(--radius-md)] border border-border bg-surface p-5"
        >
          <SelectRow
            id="dc-cat"
            label="Cat"
            required
            value={catId}
            onChange={(e) => setCatId(e.target.value)}
          >
            <option value="">Choose a cat…</option>
            {(cats.data?.items ?? [])
              .filter((c) => c.lifeStatus === 'Alive')
              .map((c) => (
                <option key={c.id} value={c.id}>
                  {c.name}
                </option>
              ))}
          </SelectRow>
          <FormFieldRow
            id="dc-date"
            label="Date"
            type="date"
            required
            min={todayInZone(branch?.timeZoneId ?? 'Asia/Dhaka')}
            value={date}
            onChange={(e) => setDate(e.target.value)}
          />
          <fieldset className="flex flex-col gap-2">
            <legend className="text-sm font-semibold text-ink">How long?</legend>
            <label className="flex items-center gap-2 text-sm text-ink">
              <input
                type="radio"
                checked={basis === 'Hourly'}
                onChange={() => setBasis('Hourly')}
              />
              By the hour
              {daycare?.hourlyRate ? ` — ${formatMoney(daycare.hourlyRate)} an hour` : ''}
            </label>
            <label className="flex items-center gap-2 text-sm text-ink">
              <input type="radio" checked={basis === 'Day'} onChange={() => setBasis('Day')} />
              The whole day{daycare?.dayRate ? ` — ${formatMoney(daycare.dayRate)}` : ''}
            </label>
            {!daycare?.hourlyRate && (
              <p className="text-xs text-ink-muted">
                The price is shown on your booking and charged after the visit.
              </p>
            )}
          </fieldset>
          {basis === 'Hourly' && (
            <FormFieldRow
              id="dc-hours"
              label="Hours expected (1–12)"
              type="number"
              min={1}
              max={12}
              value={hours}
              onChange={(e) => setHours(e.target.value)}
              className="max-w-40"
            />
          )}
          <FormErrorAlert message={error} />
          {gate && (
            <ul className="text-sm text-danger-ink">
              {gate.blockedCats.map((c) => (
                <li key={c.catId}>
                  {c.reasons.map((r) => describeGateReason(c.catName, r)).join(' ')}{' '}
                  {isSafeInternalPath(c.bookingLink) && (
                    <Link href={c.bookingLink} className="font-semibold underline">
                      Book a vaccination
                    </Link>
                  )}
                </li>
              ))}
            </ul>
          )}
          {done && (
            <p role="status" className="text-sm font-semibold text-accent-ink">
              Booked. You’ll see it below.
            </p>
          )}
          <div>
            <Button type="submit" disabled={book.isPending}>
              {book.isPending ? 'Booking…' : 'Book daycare'}
            </Button>
          </div>
        </form>
      )}

      <section aria-labelledby="dc-mine" className="flex flex-col gap-3">
        <h2 id="dc-mine" className="font-display text-lg font-semibold text-ink">
          Your daycare visits
        </h2>
        <QueryGate query={visits} loadingLabel="Loading…">
          {(list) =>
            list.length === 0 ? (
              <p className="text-sm text-ink-muted">No daycare visits yet.</p>
            ) : (
              <ul className="flex flex-col gap-2">
                {list.map((v) => (
                  <li
                    key={v.id}
                    className="flex flex-wrap items-center justify-between gap-2 rounded-[var(--radius-sm)] border border-border bg-surface p-3 text-sm"
                  >
                    <span>
                      <strong>{v.catName}</strong> · {formatFullDate(v.localDate)} ·{' '}
                      {v.basis === 'Day' ? 'whole day' : `${v.expectedHours ?? '?'} h`} · {v.status}
                      {v.chargeAmount ? ` · charge ${formatMoney(v.chargeAmount)}` : ''}
                    </span>
                    {v.status === 'Booked' && (
                      <Button
                        size="sm"
                        variant="secondary"
                        onClick={() => void cancel.mutateAsync(v.id)}
                      >
                        Cancel
                      </Button>
                    )}
                  </li>
                ))}
              </ul>
            )
          }
        </QueryGate>
        <Link href={PORTAL_BOARDING} className="text-sm font-semibold text-accent-ink underline">
          ← My stays
        </Link>
      </section>
    </div>
  );
}
