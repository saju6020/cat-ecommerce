'use client';

import { useEffect } from 'react';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { z } from 'zod';
import { Button } from '@/components/ui/button';
import { UnsavedChangesGuard } from '@/components/account/UnsavedChangesGuard';
import { applyValidationErrors, describeUnexpectedError } from '@/lib/api/errors';
import { ApiValidationError } from '@/lib/api/client';
import { todayInZone } from '@/lib/format/time';
import type { CreateBranchClosureRequest } from '@/lib/api/admin-branches';

const schema = z
  .object({
    kind: z.enum(['PublicHoliday', 'Closure']),
    startsOn: z.string().min(1, 'Choose a start date.'),
    endsOn: z.string().min(1, 'Choose an end date.'),
    partialFrom: z.string().optional(),
    partialTo: z.string().optional(),
    reason: z.string().trim().min(1, 'Enter a reason.').max(200),
  })
  .refine((data) => data.endsOn >= data.startsOn, {
    message: 'End date must be on or after the start date.',
    path: ['endsOn'],
  });
type FormValues = z.infer<typeof schema>;

const FIELD_MAP = {
  Kind: 'kind',
  StartsOn: 'startsOn',
  EndsOn: 'endsOn',
  PartialFrom: 'partialFrom',
  PartialTo: 'partialTo',
  Reason: 'reason',
} as const;
const KNOWN_FIELDS = Object.values(FIELD_MAP);

/** `components/branches/ClosureForm.tsx` — HO-ADM-002 §11.2, U-B3. Half-day times only apply to a
 *  single-day closure — a multi-day range disables and clears them, with an explanatory note. */
export interface ClosureFormProps {
  timeZoneId: string;
  onCreate: (request: CreateBranchClosureRequest) => Promise<unknown>;
  isPending: boolean;
}

export function ClosureForm({ timeZoneId, onCreate, isPending }: ClosureFormProps) {
  const today = todayInZone(timeZoneId);
  const {
    register,
    handleSubmit,
    watch,
    setValue,
    setError,
    reset,
    formState: { errors, isDirty, isSubmitting, isSubmitSuccessful },
  } = useForm<FormValues>({
    resolver: zodResolver(schema),
    defaultValues: { kind: 'Closure', startsOn: today, endsOn: today, reason: '' },
  });

  const startsOn = watch('startsOn');
  const endsOn = watch('endsOn');
  const isSingleDay = !!startsOn && startsOn === endsOn;

  useEffect(() => {
    if (!isSingleDay) {
      setValue('partialFrom', '');
      setValue('partialTo', '');
    }
  }, [isSingleDay, setValue]);

  async function onSubmit(values: FormValues) {
    try {
      await onCreate({
        kind: values.kind,
        startsOn: values.startsOn,
        endsOn: values.endsOn,
        reason: values.reason,
        partialFrom: isSingleDay && values.partialFrom ? values.partialFrom : null,
        partialTo: isSingleDay && values.partialTo ? values.partialTo : null,
      });
      reset({
        kind: 'Closure',
        startsOn: today,
        endsOn: today,
        reason: '',
        partialFrom: '',
        partialTo: '',
      });
    } catch (error) {
      if (error instanceof ApiValidationError) {
        applyValidationErrors(error, setError, { fieldMap: FIELD_MAP, knownFields: KNOWN_FIELDS });
      } else {
        setError('root', { type: 'server', message: describeUnexpectedError(error) });
      }
    }
  }

  return (
    <form onSubmit={handleSubmit(onSubmit)} noValidate className="flex max-w-lg flex-col gap-4">
      <UnsavedChangesGuard when={isDirty && !isSubmitSuccessful} />
      <h2 className="text-sm font-semibold text-ink">Add a closure or holiday</h2>

      {errors.root && (
        <p role="alert" aria-live="polite" className="text-sm font-semibold text-danger-ink">
          {errors.root.message}
        </p>
      )}
      {isSubmitSuccessful && (
        <p role="status" aria-live="polite" className="text-sm font-semibold text-ink">
          Added.
        </p>
      )}

      <div className="flex flex-col gap-1.5">
        <label htmlFor="kind" className="text-sm font-semibold text-ink">
          Kind
        </label>
        <select
          id="kind"
          {...register('kind')}
          className="rounded-[var(--radius-sm)] border border-border-strong bg-surface px-3 py-2 text-sm text-ink"
        >
          <option value="Closure">Closure</option>
          <option value="PublicHoliday">Public holiday</option>
        </select>
      </div>

      <div className="flex flex-wrap gap-3">
        <div className="flex flex-col gap-1.5">
          <label htmlFor="startsOn" className="text-sm font-semibold text-ink">
            Start date
          </label>
          <input
            id="startsOn"
            type="date"
            min={today}
            {...register('startsOn')}
            className="rounded-[var(--radius-sm)] border border-border-strong bg-surface px-3 py-2 text-sm text-ink"
          />
          {errors.startsOn && (
            <p role="alert" className="text-xs font-semibold text-danger-ink">
              {errors.startsOn.message}
            </p>
          )}
        </div>
        <div className="flex flex-col gap-1.5">
          <label htmlFor="endsOn" className="text-sm font-semibold text-ink">
            End date
          </label>
          <input
            id="endsOn"
            type="date"
            min={startsOn || today}
            {...register('endsOn')}
            className="rounded-[var(--radius-sm)] border border-border-strong bg-surface px-3 py-2 text-sm text-ink"
          />
          {errors.endsOn && (
            <p role="alert" className="text-xs font-semibold text-danger-ink">
              {errors.endsOn.message}
            </p>
          )}
        </div>
      </div>

      <div className="flex flex-wrap gap-3">
        <div className="flex flex-col gap-1.5">
          <label htmlFor="partialFrom" className="text-sm font-semibold text-ink">
            Half-day from ({timeZoneId})
          </label>
          <input
            id="partialFrom"
            type="time"
            disabled={!isSingleDay}
            {...register('partialFrom')}
            className="rounded-[var(--radius-sm)] border border-border-strong bg-surface px-3 py-2 text-sm text-ink disabled:bg-surface-sunken disabled:text-ink-faint"
          />
        </div>
        <div className="flex flex-col gap-1.5">
          <label htmlFor="partialTo" className="text-sm font-semibold text-ink">
            Half-day to ({timeZoneId})
          </label>
          <input
            id="partialTo"
            type="time"
            disabled={!isSingleDay}
            {...register('partialTo')}
            className="rounded-[var(--radius-sm)] border border-border-strong bg-surface px-3 py-2 text-sm text-ink disabled:bg-surface-sunken disabled:text-ink-faint"
          />
        </div>
      </div>
      {!isSingleDay && (
        <p className="text-xs text-ink-muted">
          Half-day times only apply to a single-day closure — choose the same start and end date to
          set one.
        </p>
      )}

      <div className="flex flex-col gap-1.5">
        <label htmlFor="reason" className="text-sm font-semibold text-ink">
          Reason
        </label>
        <input
          id="reason"
          {...register('reason')}
          className="rounded-[var(--radius-sm)] border border-border-strong bg-surface px-3 py-2 text-sm text-ink"
        />
        {errors.reason && (
          <p role="alert" className="text-xs font-semibold text-danger-ink">
            {errors.reason.message}
          </p>
        )}
      </div>

      <Button
        type="submit"
        disabled={isSubmitting || isPending}
        aria-busy={isSubmitting || isPending}
        className="w-fit"
      >
        {isSubmitting || isPending ? 'Adding…' : 'Add'}
      </Button>
    </form>
  );
}
