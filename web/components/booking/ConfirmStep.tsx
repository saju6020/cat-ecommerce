'use client';

import { useEffect } from 'react';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { Button } from '@/components/ui/button';
import {
  reasonForVisitSchema,
  MAX_APPOINTMENT_TEXT,
  type ReasonForVisitValues,
} from '@/lib/validation/appointments';
import { getViolationCode, submitWithServerErrors } from '@/lib/api/errors';
import { formatMoney } from '@/lib/format/money';
import type { ServiceSnapshot } from '@/lib/api/types';
import { HoldCountdown } from './HoldCountdown';

export interface BookingSummary {
  catName: string;
  service: ServiceSnapshot;
  whenLabel: string;
  performerName: string | null;
  roomName: string | null;
  branchName: string;
  zone: string;
}

/**
 * ⭐ `components/booking/ConfirmStep.tsx` — HO-APT-002 §11.1 step 5. A summary, an optional reason
 * for visit (500 characters, plain text — rendered as text everywhere), the `HoldCountdown` and
 * **Confirm booking**. There is deliberately NO payment control (ADR-073, U-A10): the price is
 * information, "Pay at the clinic". `SlotHoldExpired` from the server takes the same path as the
 * countdown reaching zero (`onHoldEnded`). The typed reason survives any failed submit (NFR-USA-5),
 * and — because this component unmounts when the hold ends — it is also reported up through
 * `onReasonChange` and handed back through `initialReason` when the customer holds a new time.
 */
export function ConfirmStep({
  summary,
  holdExpiresOn,
  onHoldEnded,
  onConfirm,
  onBack,
  initialReason = '',
  onReasonChange,
}: {
  summary: BookingSummary;
  holdExpiresOn: string;
  onHoldEnded: () => void;
  onConfirm: (reasonForVisit: string | null) => Promise<void>;
  onBack: () => void;
  /** The reason typed before an earlier hold ended; the textarea starts with it. */
  initialReason?: string;
  onReasonChange?: (reason: string) => void;
}) {
  const {
    register,
    handleSubmit,
    setError,
    watch,
    formState: { errors, isSubmitting },
  } = useForm<ReasonForVisitValues>({
    resolver: zodResolver(reasonForVisitSchema),
    defaultValues: { reasonForVisit: initialReason },
  });
  const typed = watch('reasonForVisit') ?? '';
  useEffect(() => {
    onReasonChange?.(typed);
  }, [typed, onReasonChange]);

  async function submit(values: ReasonForVisitValues) {
    await submitWithServerErrors(
      async () => {
        try {
          await onConfirm(values.reasonForVisit.trim() || null);
        } catch (error) {
          if (getViolationCode(error) === 'SlotHoldExpired') {
            onHoldEnded();
            return;
          }
          throw error;
        }
      },
      setError,
      { fieldMap: { ReasonForVisit: 'reasonForVisit' }, knownFields: ['reasonForVisit'] },
    );
  }

  return (
    <form
      method="post"
      noValidate
      onSubmit={handleSubmit(submit)}
      className="flex flex-col gap-5"
      aria-labelledby="confirm-heading"
    >
      <h2 id="confirm-heading" className="text-[15px] font-semibold text-ink">
        Check your booking
      </h2>

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
        {summary.roomName && (
          <>
            <dt className="font-semibold text-ink">Room</dt>
            <dd className="text-ink-muted">{summary.roomName}</dd>
          </>
        )}
        <dt className="font-semibold text-ink">Clinic</dt>
        <dd className="text-ink-muted">{summary.branchName}</dd>
        <dt className="font-semibold text-ink">Price</dt>
        <dd className="text-ink-muted">
          {formatMoney({
            amount: String(summary.service.priceAmount),
            currencyCode: summary.service.priceCurrencyCode,
          })}{' '}
          · <strong className="text-ink">Pay at the clinic</strong>
        </dd>
      </dl>

      <div className="flex flex-col gap-1.5">
        <label htmlFor="reasonForVisit" className="text-sm font-semibold text-ink">
          Reason for visit <span className="font-normal text-ink-muted">(optional)</span>
        </label>
        <textarea
          id="reasonForVisit"
          rows={3}
          maxLength={MAX_APPOINTMENT_TEXT + 50}
          aria-invalid={!!errors.reasonForVisit}
          aria-describedby="reasonForVisit-hint reasonForVisit-error"
          className="rounded-[var(--radius-sm)] border border-border-strong bg-surface px-3 py-2 text-sm text-ink focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-accent"
          {...register('reasonForVisit')}
        />
        <p id="reasonForVisit-hint" className="text-xs text-ink-muted">
          Plain text, up to {MAX_APPOINTMENT_TEXT} characters ({typed.length} used).
        </p>
        {errors.reasonForVisit && (
          <p
            id="reasonForVisit-error"
            role="alert"
            className="text-xs font-semibold text-danger-ink"
          >
            {errors.reasonForVisit.message}
          </p>
        )}
      </div>

      <HoldCountdown holdExpiresOn={holdExpiresOn} onExpire={onHoldEnded} />

      {errors.root && (
        <p role="alert" className="text-sm font-semibold text-danger-ink">
          {errors.root.message}
        </p>
      )}

      <div className="flex flex-wrap items-center gap-3">
        <Button type="submit" disabled={isSubmitting} aria-busy={isSubmitting}>
          {isSubmitting ? 'Confirming…' : 'Confirm booking'}
        </Button>
        <Button type="button" variant="secondary" onClick={onBack} disabled={isSubmitting}>
          Back
        </Button>
        <span className="text-xs text-ink-muted">Going back releases this time.</span>
      </div>
    </form>
  );
}
