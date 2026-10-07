'use client';

import { useState } from 'react';
import { Button } from '@/components/ui/button';
import { HoldCountdown } from '@/components/booking/HoldCountdown';
import { formatAppointmentWhen } from '@/lib/format/appointment';
import { describeAppointmentError, getViolationCode } from '@/lib/api/errors';
import type { MyAppointmentDetail } from '@/lib/api/types';

/**
 * `components/appointments/ClaimOfferedSlot.tsx` — HO-APT-002 §11.2 (US-CLN-09). When the detail
 * is a `Held` waitlist offer: the slot, the `HoldCountdown` to `holdExpiresOn` (server clock), and
 * **Confirm booking** (the normal confirm endpoint). An offer that has ended — the countdown
 * reached zero, the server says the hold expired, or the status is no longer `Held` — reads
 * *"This offer has ended."* and offers nothing to confirm.
 */
export function ClaimOfferedSlot({
  detail,
  onConfirm,
}: {
  detail: MyAppointmentDetail;
  onConfirm: () => Promise<void>;
}) {
  const [ended, setEnded] = useState(detail.status !== 'Held' || !detail.holdExpiresOn);
  const [pending, setPending] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const zone = detail.branchTimeZoneId;

  async function confirm() {
    setPending(true);
    setError(null);
    try {
      await onConfirm();
    } catch (caught) {
      if (getViolationCode(caught) === 'SlotHoldExpired') setEnded(true);
      else setError(describeAppointmentError(caught));
    } finally {
      setPending(false);
    }
  }

  return (
    <section aria-labelledby="offer-heading" className="flex flex-col gap-4">
      <h1 id="offer-heading" className="font-display text-2xl font-semibold text-ink">
        A time has become available
      </h1>
      <dl className="grid grid-cols-[auto_1fr] gap-x-4 gap-y-2 rounded-[var(--radius-md)] border border-border bg-surface p-4 text-sm">
        <dt className="font-semibold text-ink">Cat</dt>
        <dd className="text-ink-muted">{detail.catName}</dd>
        <dt className="font-semibold text-ink">Service</dt>
        <dd className="text-ink-muted">{detail.service.name}</dd>
        <dt className="font-semibold text-ink">When</dt>
        <dd className="text-ink-muted">
          {detail.startsOn ? `${formatAppointmentWhen(detail.startsOn, zone)} (${zone})` : '—'}
        </dd>
        <dt className="font-semibold text-ink">With</dt>
        <dd className="text-ink-muted">{detail.performerName ?? 'To be assigned'}</dd>
      </dl>

      {ended ? (
        <p role="status" className="text-sm font-semibold text-ink">
          This offer has ended.
        </p>
      ) : (
        <>
          <HoldCountdown
            holdExpiresOn={detail.holdExpiresOn!}
            onExpire={() => setEnded(true)}
            label="Time left to claim this offer"
          />
          {error && (
            <p role="alert" className="text-sm font-semibold text-danger-ink">
              {error}
            </p>
          )}
          <div>
            <Button onClick={confirm} disabled={pending} aria-busy={pending}>
              {pending ? 'Confirming…' : 'Confirm booking'}
            </Button>
          </div>
        </>
      )}
    </section>
  );
}
