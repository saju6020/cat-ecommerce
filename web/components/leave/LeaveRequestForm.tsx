'use client';

import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { z } from 'zod';
import { Button } from '@/components/ui/button';
import { UnsavedChangesGuard } from '@/components/account/UnsavedChangesGuard';
import { applyValidationErrors, describeUnexpectedError } from '@/lib/api/errors';
import { ApiValidationError } from '@/lib/api/client';
import { LEAVE_KINDS, type RequestLeaveRequest } from '@/lib/api/leave';

const schema = z
  .object({
    startsOn: z.string().min(1, 'Choose a start date.'),
    endsOn: z.string().min(1, 'Choose an end date.'),
    kind: z.enum(['Annual', 'Sick', 'Training', 'Other']),
    note: z.string().trim().max(500).optional(),
  })
  .refine((data) => data.endsOn >= data.startsOn, {
    message: 'End date must be on or after the start date.',
    path: ['endsOn'],
  });
type FormValues = z.infer<typeof schema>;

const FIELD_MAP = { StartsOn: 'startsOn', EndsOn: 'endsOn', Kind: 'kind', Note: 'note' } as const;
const KNOWN_FIELDS = Object.values(FIELD_MAP);

/** `components/leave/LeaveRequestForm.tsx` — HO-ADM-002 §11.4, US-ADM-03. Used both by "my leave"
 *  (every staff member) and, with `staffAccountId` supplied, by an administrator requesting on
 *  someone else's behalf. */
export interface LeaveRequestFormProps {
  onSubmitRequest: (request: RequestLeaveRequest) => Promise<unknown>;
  isPending: boolean;
}

export function LeaveRequestForm({ onSubmitRequest, isPending }: LeaveRequestFormProps) {
  const {
    register,
    handleSubmit,
    setError,
    reset,
    formState: { errors, isDirty, isSubmitting, isSubmitSuccessful },
  } = useForm<FormValues>({
    resolver: zodResolver(schema),
    defaultValues: { startsOn: '', endsOn: '', kind: 'Annual', note: '' },
  });

  async function onSubmit(values: FormValues) {
    try {
      await onSubmitRequest({
        startsOn: values.startsOn,
        endsOn: values.endsOn,
        kind: values.kind,
        note: values.note || null,
      });
      reset({ startsOn: '', endsOn: '', kind: 'Annual', note: '' });
    } catch (error) {
      if (error instanceof ApiValidationError) {
        applyValidationErrors(error, setError, { fieldMap: FIELD_MAP, knownFields: KNOWN_FIELDS });
      } else {
        setError('root', { type: 'server', message: describeUnexpectedError(error) });
      }
    }
  }

  return (
    <form onSubmit={handleSubmit(onSubmit)} noValidate className="flex max-w-lg flex-col gap-3">
      <UnsavedChangesGuard when={isDirty && !isSubmitSuccessful} />
      {errors.root && (
        <p role="alert" aria-live="polite" className="text-sm font-semibold text-danger-ink">
          {errors.root.message}
        </p>
      )}
      {isSubmitSuccessful && (
        <p role="status" aria-live="polite" className="text-sm font-semibold text-ink">
          Leave requested.
        </p>
      )}

      <div className="flex flex-wrap gap-3">
        <div className="flex flex-col gap-1.5">
          <label htmlFor="leave-starts" className="text-sm font-semibold text-ink">
            Start date
          </label>
          <input
            id="leave-starts"
            type="date"
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
          <label htmlFor="leave-ends" className="text-sm font-semibold text-ink">
            End date
          </label>
          <input
            id="leave-ends"
            type="date"
            {...register('endsOn')}
            className="rounded-[var(--radius-sm)] border border-border-strong bg-surface px-3 py-2 text-sm text-ink"
          />
          {errors.endsOn && (
            <p role="alert" className="text-xs font-semibold text-danger-ink">
              {errors.endsOn.message}
            </p>
          )}
        </div>
        <div className="flex flex-col gap-1.5">
          <label htmlFor="leave-kind" className="text-sm font-semibold text-ink">
            Kind
          </label>
          <select
            id="leave-kind"
            {...register('kind')}
            className="rounded-[var(--radius-sm)] border border-border-strong bg-surface px-3 py-2 text-sm text-ink"
          >
            {LEAVE_KINDS.map((k) => (
              <option key={k} value={k}>
                {k}
              </option>
            ))}
          </select>
        </div>
      </div>

      <div className="flex flex-col gap-1.5">
        <label htmlFor="leave-note" className="text-sm font-semibold text-ink">
          Note
        </label>
        <textarea
          id="leave-note"
          rows={2}
          {...register('note')}
          className="rounded-[var(--radius-sm)] border border-border-strong bg-surface px-3 py-2 text-sm text-ink"
        />
      </div>

      <Button
        type="submit"
        disabled={isSubmitting || isPending}
        aria-busy={isSubmitting || isPending}
        className="w-fit"
      >
        {isSubmitting || isPending ? 'Requesting…' : 'Request leave'}
      </Button>
    </form>
  );
}
