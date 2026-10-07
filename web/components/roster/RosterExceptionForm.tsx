'use client';

import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { z } from 'zod';
import { Button } from '@/components/ui/button';
import { UnsavedChangesGuard } from '@/components/account/UnsavedChangesGuard';
import { applyValidationErrors, describeUnexpectedError } from '@/lib/api/errors';
import { ApiValidationError } from '@/lib/api/client';
import { todayInZone } from '@/lib/format/time';
import type { CreateRosterExceptionRequest } from '@/lib/api/admin-rosters';

const schema = z.object({
  staffAccountId: z.string().min(1, 'Choose a staff member.'),
  date: z.string().min(1, 'Choose a date.'),
  kind: z.enum(['ExtraShift', 'ShiftCancelled']),
  starts: z.string().min(1, 'Enter a start time.'),
  ends: z.string().min(1, 'Enter an end time.'),
  reason: z.string().trim().min(1, 'Enter a reason.').max(200),
});
type FormValues = z.infer<typeof schema>;

const FIELD_MAP = {
  StaffAccountId: 'staffAccountId',
  Date: 'date',
  Kind: 'kind',
  Starts: 'starts',
  Ends: 'ends',
  Reason: 'reason',
} as const;
const KNOWN_FIELDS = Object.values(FIELD_MAP);

/** `components/roster/RosterExceptionForm.tsx` — HO-ADM-002 §11.4, US-ADM-03. */
export interface RosterExceptionFormProps {
  timeZoneId: string;
  staffOptions: Array<{ staffAccountId: string; staffFullName: string }>;
  onCreate: (request: CreateRosterExceptionRequest) => Promise<unknown>;
  isPending: boolean;
}

export function RosterExceptionForm({
  timeZoneId,
  staffOptions,
  onCreate,
  isPending,
}: RosterExceptionFormProps) {
  const today = todayInZone(timeZoneId);
  const {
    register,
    handleSubmit,
    setError,
    reset,
    formState: { errors, isDirty, isSubmitting, isSubmitSuccessful },
  } = useForm<FormValues>({
    resolver: zodResolver(schema),
    defaultValues: {
      staffAccountId: '',
      date: today,
      kind: 'ExtraShift',
      starts: '09:00',
      ends: '17:00',
      reason: '',
    },
  });

  async function onSubmit(values: FormValues) {
    try {
      await onCreate(values);
      reset({
        staffAccountId: '',
        date: today,
        kind: 'ExtraShift',
        starts: '09:00',
        ends: '17:00',
        reason: '',
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
    <form onSubmit={handleSubmit(onSubmit)} noValidate className="flex max-w-lg flex-col gap-3">
      <UnsavedChangesGuard when={isDirty && !isSubmitSuccessful} />
      <h2 className="text-sm font-semibold text-ink">Add a roster exception</h2>

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
        <label htmlFor="exception-staff" className="text-sm font-semibold text-ink">
          Staff member
        </label>
        <select
          id="exception-staff"
          {...register('staffAccountId')}
          className="rounded-[var(--radius-sm)] border border-border-strong bg-surface px-3 py-2 text-sm text-ink"
        >
          <option value="">Choose…</option>
          {staffOptions.map((s) => (
            <option key={s.staffAccountId} value={s.staffAccountId}>
              {s.staffFullName}
            </option>
          ))}
        </select>
        {errors.staffAccountId && (
          <p role="alert" className="text-xs font-semibold text-danger-ink">
            {errors.staffAccountId.message}
          </p>
        )}
      </div>

      <div className="flex flex-wrap gap-3">
        <div className="flex flex-col gap-1.5">
          <label htmlFor="exception-date" className="text-sm font-semibold text-ink">
            Date
          </label>
          <input
            id="exception-date"
            type="date"
            min={today}
            {...register('date')}
            className="rounded-[var(--radius-sm)] border border-border-strong bg-surface px-3 py-2 text-sm text-ink"
          />
        </div>
        <div className="flex flex-col gap-1.5">
          <label htmlFor="exception-kind" className="text-sm font-semibold text-ink">
            Kind
          </label>
          <select
            id="exception-kind"
            {...register('kind')}
            className="rounded-[var(--radius-sm)] border border-border-strong bg-surface px-3 py-2 text-sm text-ink"
          >
            <option value="ExtraShift">Extra shift</option>
            <option value="ShiftCancelled">Shift cancelled</option>
          </select>
        </div>
        <div className="flex flex-col gap-1.5">
          <label htmlFor="exception-starts" className="text-sm font-semibold text-ink">
            Starts ({timeZoneId})
          </label>
          <input
            id="exception-starts"
            type="time"
            {...register('starts')}
            className="rounded-[var(--radius-sm)] border border-border-strong bg-surface px-3 py-2 text-sm text-ink"
          />
        </div>
        <div className="flex flex-col gap-1.5">
          <label htmlFor="exception-ends" className="text-sm font-semibold text-ink">
            Ends ({timeZoneId})
          </label>
          <input
            id="exception-ends"
            type="time"
            {...register('ends')}
            className="rounded-[var(--radius-sm)] border border-border-strong bg-surface px-3 py-2 text-sm text-ink"
          />
        </div>
      </div>

      <div className="flex flex-col gap-1.5">
        <label htmlFor="exception-reason" className="text-sm font-semibold text-ink">
          Reason
        </label>
        <input
          id="exception-reason"
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
        {isSubmitting || isPending ? 'Adding…' : 'Add exception'}
      </Button>
    </form>
  );
}
