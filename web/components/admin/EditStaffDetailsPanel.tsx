'use client';

import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { z } from 'zod';
import { Button } from '@/components/ui/button';
import { applyValidationErrors, describeUnexpectedError } from '@/lib/api/errors';
import { ApiValidationError } from '@/lib/api/client';
import type { StaffAccount, UpdateStaffAccountDetailsRequest } from '@/lib/api/staff-admin';

const schema = z
  .object({
    fullName: z.string().trim().min(1, 'Enter a name.').max(200, 'Use at most 200 characters.'),
    mobileNumber: z.string().trim().optional(),
    emailAddress: z.string().trim().optional(),
  })
  .refine((data) => data.mobileNumber || data.emailAddress, {
    message: 'Give a mobile number, an email, or both.',
    path: ['mobileNumber'],
  });
type FormValues = z.infer<typeof schema>;

const FIELD_MAP = {
  FullName: 'fullName',
  MobileNumber: 'mobileNumber',
  EmailAddress: 'emailAddress',
} as const;
const KNOWN_FIELDS = Object.values(FIELD_MAP);

const INPUT_CLASS =
  'rounded-[var(--radius-sm)] border border-border-strong bg-surface px-3 py-2 text-sm text-ink focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-accent';

/**
 * `components/admin/EditStaffDetailsPanel.tsx` — owner, 2026-10-20. An administrator edits a staff member's
 * name, mobile number and email address (the same rules as creating the account: at least one way to sign
 * in). Open sessions stay; the next sign-in uses the new details. The page wires
 * `useUpdateStaffAccountDetails(staffAccountId)`.
 */
export interface EditStaffDetailsPanelProps {
  account: StaffAccount;
  onSave: (request: UpdateStaffAccountDetailsRequest) => Promise<unknown>;
  onDone: () => void;
  isPending: boolean;
}

export function EditStaffDetailsPanel({
  account,
  onSave,
  onDone,
  isPending,
}: EditStaffDetailsPanelProps) {
  const {
    register,
    handleSubmit,
    setError,
    formState: { errors, isSubmitting },
  } = useForm<FormValues>({
    resolver: zodResolver(schema),
    defaultValues: {
      fullName: account.fullName,
      mobileNumber: account.mobileNumber ?? '',
      emailAddress: account.emailAddress ?? '',
    },
  });

  async function onSubmit(values: FormValues) {
    try {
      await onSave({
        fullName: values.fullName,
        mobileNumber: values.mobileNumber || null,
        emailAddress: values.emailAddress || null,
      });
      onDone();
    } catch (error) {
      if (error instanceof ApiValidationError) {
        applyValidationErrors(error, setError, { fieldMap: FIELD_MAP, knownFields: KNOWN_FIELDS });
      } else {
        setError('root', { type: 'server', message: describeUnexpectedError(error) });
      }
    }
  }

  const busy = isSubmitting || isPending;

  return (
    <form
      method="post"
      aria-label="Edit details"
      onSubmit={handleSubmit(onSubmit)}
      noValidate
      className="flex flex-col gap-4 rounded-[var(--radius-md)] border border-border bg-surface p-4"
    >
      {errors.root && (
        <p role="alert" aria-live="polite" className="text-sm font-semibold text-danger-ink">
          {errors.root.message}
        </p>
      )}

      <div className="flex flex-col gap-1.5">
        <label htmlFor="editFullName" className="text-sm font-semibold text-ink">
          Full name{' '}
          <span aria-hidden="true" className="text-danger-ink">
            *
          </span>
        </label>
        <input
          id="editFullName"
          required
          aria-invalid={!!errors.fullName}
          aria-describedby={errors.fullName ? 'editFullName-error' : undefined}
          className={INPUT_CLASS}
          {...register('fullName')}
        />
        {errors.fullName && (
          <p id="editFullName-error" role="alert" className="text-xs font-semibold text-danger-ink">
            {errors.fullName.message}
          </p>
        )}
      </div>

      <div className="flex flex-col gap-1.5">
        <label htmlFor="editMobileNumber" className="text-sm font-semibold text-ink">
          Mobile number
        </label>
        <input
          id="editMobileNumber"
          inputMode="tel"
          aria-invalid={!!errors.mobileNumber}
          aria-describedby={errors.mobileNumber ? 'editMobileNumber-error' : undefined}
          className={INPUT_CLASS}
          {...register('mobileNumber')}
        />
        {errors.mobileNumber && (
          <p
            id="editMobileNumber-error"
            role="alert"
            className="text-xs font-semibold text-danger-ink"
          >
            {errors.mobileNumber.message}
          </p>
        )}
      </div>

      <div className="flex flex-col gap-1.5">
        <label htmlFor="editEmailAddress" className="text-sm font-semibold text-ink">
          Email
        </label>
        <input
          id="editEmailAddress"
          type="email"
          aria-invalid={!!errors.emailAddress}
          aria-describedby={errors.emailAddress ? 'editEmailAddress-error' : undefined}
          className={INPUT_CLASS}
          {...register('emailAddress')}
        />
        {errors.emailAddress && (
          <p
            id="editEmailAddress-error"
            role="alert"
            className="text-xs font-semibold text-danger-ink"
          >
            {errors.emailAddress.message}
          </p>
        )}
      </div>

      <div className="flex gap-2">
        <Button type="submit" disabled={busy} aria-busy={busy}>
          {busy ? 'Saving…' : 'Save details'}
        </Button>
        <Button type="button" variant="secondary" onClick={onDone} disabled={busy}>
          Cancel
        </Button>
      </div>
    </form>
  );
}
