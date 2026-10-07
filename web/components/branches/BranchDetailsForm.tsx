'use client';

import { useState } from 'react';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { z } from 'zod';
import { Button } from '@/components/ui/button';
import { Badge } from '@/components/ui/badge';
import { ConfirmDialog } from '@/components/common/ConfirmDialog';
import { UnsavedChangesGuard } from '@/components/account/UnsavedChangesGuard';
import { applyValidationErrors, describeApiError, describeUnexpectedError } from '@/lib/api/errors';
import { ApiValidationError } from '@/lib/api/client';
import { timeZoneOptions } from '@/lib/format/time';
import type { BranchDetail } from '@/lib/api/admin-branches';

const schema = z.object({
  code: z.string().trim().min(1, 'Enter a code.').max(20),
  name: z.string().trim().min(1, 'Enter a name.').max(200),
  timeZoneId: z.string().trim().min(1, 'Choose a time zone.'),
  addressLine: z.string().trim().optional(),
  phoneNumber: z.string().trim().optional(),
  email: z.string().trim().optional(),
});
type FormValues = z.infer<typeof schema>;

const FIELD_MAP = {
  Code: 'code',
  Name: 'name',
  TimeZoneId: 'timeZoneId',
  AddressLine: 'addressLine',
  PhoneNumber: 'phoneNumber',
  Email: 'email',
} as const;
const KNOWN_FIELDS = Object.values(FIELD_MAP);

/** `components/branches/BranchDetailsForm.tsx` — HO-ADM-002 §11.2, US-ADM-01. Create when
 *  `branch` is omitted (code editable); edit when supplied (code read-only). Deactivate/reactivate
 *  is only offered in edit mode, confirmed, with the server's `LastActiveBranchCannotBeDeactivated`
 *  shown as its own plain-language message. */
export interface BranchDetailsFormProps {
  branch?: BranchDetail;
  onSave: (values: {
    code?: string;
    name: string;
    timeZoneId: string;
    addressLine: string | null;
    phoneNumber: string | null;
    email: string | null;
  }) => Promise<unknown>;
  isPending: boolean;
  onSetActivation?: (isActive: boolean) => Promise<unknown>;
}

export function BranchDetailsForm({
  branch,
  onSave,
  isPending,
  onSetActivation,
}: BranchDetailsFormProps) {
  const isCreate = !branch;
  const [activationError, setActivationError] = useState<string | null>(null);
  const [confirmOpen, setConfirmOpen] = useState(false);
  const [activationPending, setActivationPending] = useState(false);

  const {
    register,
    handleSubmit,
    setError,
    formState: { errors, isDirty, isSubmitSuccessful, isSubmitting },
  } = useForm<FormValues>({
    resolver: zodResolver(schema),
    defaultValues: {
      code: branch?.code ?? '',
      name: branch?.name ?? '',
      timeZoneId: branch?.timeZoneId ?? 'Asia/Dhaka',
      addressLine: branch?.addressLine ?? '',
      phoneNumber: branch?.phoneNumber ?? '',
      email: branch?.email ?? '',
    },
  });

  async function onSubmit(values: FormValues) {
    try {
      await onSave({
        code: isCreate ? values.code : undefined,
        name: values.name,
        timeZoneId: values.timeZoneId,
        addressLine: values.addressLine || null,
        phoneNumber: values.phoneNumber || null,
        email: values.email || null,
      });
    } catch (error) {
      if (error instanceof ApiValidationError) {
        applyValidationErrors(error, setError, { fieldMap: FIELD_MAP, knownFields: KNOWN_FIELDS });
      } else {
        setError('root', { type: 'server', message: describeUnexpectedError(error) });
      }
    }
  }

  async function handleActivationConfirm() {
    if (!onSetActivation || !branch) return;
    setActivationPending(true);
    setActivationError(null);
    try {
      await onSetActivation(!branch.isActive);
      setConfirmOpen(false);
    } catch (error) {
      setActivationError(describeApiError(error));
    } finally {
      setActivationPending(false);
    }
  }

  return (
    <form
      method="post"
      onSubmit={handleSubmit(onSubmit)}
      noValidate
      className="flex max-w-lg flex-col gap-5"
    >
      <UnsavedChangesGuard when={isDirty && !isSubmitSuccessful} />

      {errors.root && (
        <p role="alert" aria-live="polite" className="text-sm font-semibold text-danger-ink">
          {errors.root.message}
        </p>
      )}
      {isSubmitSuccessful && (
        <p role="status" aria-live="polite" className="text-sm font-semibold text-ink">
          Saved.
        </p>
      )}

      {!isCreate && (
        <Badge variant={branch.isActive ? 'success' : 'neutral'} className="w-fit">
          {branch.isActive ? 'Active' : 'Inactive'}
        </Badge>
      )}

      <div className="flex flex-col gap-1.5">
        <label htmlFor="code" className="text-sm font-semibold text-ink">
          Code
        </label>
        <input
          id="code"
          readOnly={!isCreate}
          aria-invalid={!!errors.code}
          aria-describedby={errors.code ? 'code-error' : undefined}
          className="rounded-[var(--radius-sm)] border border-border-strong bg-surface px-3 py-2 text-sm text-ink read-only:bg-surface-sunken read-only:text-ink-muted focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-accent"
          {...register('code')}
        />
        {errors.code && (
          <p id="code-error" role="alert" className="text-xs font-semibold text-danger-ink">
            {errors.code.message}
          </p>
        )}
      </div>

      <div className="flex flex-col gap-1.5">
        <label htmlFor="name" className="text-sm font-semibold text-ink">
          Name
        </label>
        <input
          id="name"
          aria-invalid={!!errors.name}
          aria-describedby={errors.name ? 'name-error' : undefined}
          className="rounded-[var(--radius-sm)] border border-border-strong bg-surface px-3 py-2 text-sm text-ink focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-accent"
          {...register('name')}
        />
        {errors.name && (
          <p id="name-error" role="alert" className="text-xs font-semibold text-danger-ink">
            {errors.name.message}
          </p>
        )}
      </div>

      <div className="flex flex-col gap-1.5">
        <label htmlFor="timeZoneId" className="text-sm font-semibold text-ink">
          Time zone
        </label>
        <select
          id="timeZoneId"
          aria-invalid={!!errors.timeZoneId}
          className="rounded-[var(--radius-sm)] border border-border-strong bg-surface px-3 py-2 text-sm text-ink focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-accent"
          {...register('timeZoneId')}
        >
          {timeZoneOptions().map((zone) => (
            <option key={zone} value={zone}>
              {zone}
            </option>
          ))}
        </select>
        <p className="text-xs text-ink-muted">
          Every hour, shift and closure for this branch is entered and shown in this zone.
        </p>
      </div>

      <div className="flex flex-col gap-1.5">
        <label htmlFor="addressLine" className="text-sm font-semibold text-ink">
          Address
        </label>
        <input
          id="addressLine"
          className="rounded-[var(--radius-sm)] border border-border-strong bg-surface px-3 py-2 text-sm text-ink focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-accent"
          {...register('addressLine')}
        />
      </div>

      <div className="flex flex-col gap-1.5">
        <label htmlFor="phoneNumber" className="text-sm font-semibold text-ink">
          Phone
        </label>
        <input
          id="phoneNumber"
          className="rounded-[var(--radius-sm)] border border-border-strong bg-surface px-3 py-2 text-sm text-ink focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-accent"
          {...register('phoneNumber')}
        />
      </div>

      <div className="flex flex-col gap-1.5">
        <label htmlFor="email" className="text-sm font-semibold text-ink">
          Email
        </label>
        <input
          id="email"
          type="email"
          className="rounded-[var(--radius-sm)] border border-border-strong bg-surface px-3 py-2 text-sm text-ink focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-accent"
          {...register('email')}
        />
      </div>

      <div className="flex flex-wrap items-center gap-3">
        <Button
          type="submit"
          disabled={isSubmitting || isPending}
          aria-busy={isSubmitting || isPending}
        >
          {isSubmitting || isPending ? 'Saving…' : isCreate ? 'Create branch' : 'Save'}
        </Button>

        {!isCreate && onSetActivation && (
          <Button type="button" variant="secondary" onClick={() => setConfirmOpen(true)}>
            {branch.isActive ? 'Deactivate' : 'Reactivate'}
          </Button>
        )}
      </div>

      {activationError && (
        <p role="alert" aria-live="polite" className="text-sm font-semibold text-danger-ink">
          {activationError}
        </p>
      )}

      {!isCreate && (
        <ConfirmDialog
          open={confirmOpen}
          onOpenChange={setConfirmOpen}
          title={branch.isActive ? `Deactivate ${branch.name}?` : `Reactivate ${branch.name}?`}
          consequence={
            branch.isActive
              ? `${branch.name} will stop appearing for new bookings and staff assignment. Existing records are kept.`
              : `${branch.name} will be available again for bookings and staff assignment.`
          }
          confirmLabel={branch.isActive ? 'Deactivate' : 'Reactivate'}
          variant={branch.isActive ? 'destructive' : 'primary'}
          onConfirm={handleActivationConfirm}
          pendingLabel={activationPending ? 'Working…' : undefined}
        />
      )}
    </form>
  );
}
