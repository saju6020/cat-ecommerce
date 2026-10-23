'use client';

import { useState } from 'react';
import { useForm, Controller } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { z } from 'zod';
import { Button } from '@/components/ui/button';
import { PasswordField } from '@/components/auth/PasswordField';
import { RoleAssignmentField } from './RoleAssignmentField';
import { CapabilityMatrixPreview } from './CapabilityMatrixPreview';
import { applyValidationErrors, describeUnexpectedError } from '@/lib/api/errors';
import { ApiValidationError } from '@/lib/api/client';
import type { StaffRoleCatalogue, CreateStaffAccountRequest } from '@/lib/api/staff-admin';
import { PASSWORD_MIN_LENGTH } from '@/lib/validation/password';

const schema = z
  .object({
    fullName: z.string().trim().min(1, 'Enter a name.'),
    mobileNumber: z.string().trim().optional(),
    emailAddress: z.string().trim().optional(),
    roleIds: z.array(z.string()).min(1, 'Select at least one role.'),
    password: z
      .string()
      .min(PASSWORD_MIN_LENGTH, `Use at least ${PASSWORD_MIN_LENGTH} characters.`),
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
  RoleIds: 'roleIds',
  Password: 'password',
} as const;
const KNOWN_FIELDS = Object.values(FIELD_MAP);

/**
 * `components/admin/StaffAccountForm.tsx` — ⭐ US-ACC-07, HO-ACC-002 §11.10. Owner, 2026-10-19: the
 * administrator sets the starting password here and shares it with the new member themselves (there is no
 * email delivery in production for the self-service reset). Success names the member and says so.
 *
 * ARCH gate HIGH 3 (first pass) — takes `catalogue`/`onCreate`/`isPending` as props; the page
 * (`app/(staff)/staff/admin/staff-accounts/new/page.tsx`) wires
 * `useStaffRoleCatalogue()`/`useCreateStaffAccount()`.
 */
export interface StaffAccountFormProps {
  catalogue: { data: StaffRoleCatalogue | undefined; isLoading: boolean };
  onCreate: (request: CreateStaffAccountRequest) => Promise<unknown>;
  isPending: boolean;
}

export function StaffAccountForm({ catalogue, onCreate, isPending }: StaffAccountFormProps) {
  const [created, setCreated] = useState<{
    name: string;
    mobileNumber: string | null;
    emailAddress: string | null;
  } | null>(null);

  const {
    register,
    control,
    handleSubmit,
    watch,
    setError,
    formState: { errors, isSubmitting },
  } = useForm<FormValues>({
    resolver: zodResolver(schema),
    defaultValues: { fullName: '', mobileNumber: '', emailAddress: '', roleIds: [], password: '' },
  });
  const selectedRoleIds = watch('roleIds');

  async function onSubmit(values: FormValues) {
    try {
      await onCreate({
        fullName: values.fullName,
        mobileNumber: values.mobileNumber || null,
        emailAddress: values.emailAddress || null,
        roleIds: values.roleIds,
        password: values.password,
      });
      setCreated({
        name: values.fullName,
        mobileNumber: values.mobileNumber || null,
        emailAddress: values.emailAddress || null,
      });
    } catch (error) {
      if (error instanceof ApiValidationError) {
        applyValidationErrors(error, setError, { fieldMap: FIELD_MAP, knownFields: KNOWN_FIELDS });
      } else {
        setError('root', { type: 'server', message: describeUnexpectedError(error) });
      }
    }
  }

  if (created) {
    // Owner, 2026-10-20: name the sign-in ID(s) the administrator shares along with the password.
    const ids = [created.mobileNumber, created.emailAddress].filter(Boolean).join(' or ');
    return (
      <p role="status" aria-live="polite" className="text-sm text-ink">
        Account created. Share the password with {created.name}; they sign in on the staff sign-in
        page with <span className="font-semibold">{ids}</span> and that password.
      </p>
    );
  }

  return (
    <form
      method="post"
      onSubmit={handleSubmit(onSubmit)}
      noValidate
      className="flex max-w-lg flex-col gap-5"
    >
      {errors.root && (
        <p role="alert" aria-live="polite" className="text-sm font-semibold text-danger-ink">
          {errors.root.message}
        </p>
      )}

      <div className="flex flex-col gap-1.5">
        <label htmlFor="fullName" className="text-sm font-semibold text-ink">
          Full name{' '}
          <span aria-hidden="true" className="text-danger-ink">
            *
          </span>
        </label>
        <input
          id="fullName"
          required
          aria-invalid={!!errors.fullName}
          aria-describedby={errors.fullName ? 'fullName-error' : undefined}
          className="rounded-[var(--radius-sm)] border border-border-strong bg-surface px-3 py-2 text-sm text-ink focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-accent"
          {...register('fullName')}
        />
        {errors.fullName && (
          <p id="fullName-error" role="alert" className="text-xs font-semibold text-danger-ink">
            {errors.fullName.message}
          </p>
        )}
      </div>

      <div className="flex flex-col gap-1.5">
        <label htmlFor="mobileNumber" className="text-sm font-semibold text-ink">
          Mobile number
        </label>
        <input
          id="mobileNumber"
          aria-invalid={!!errors.mobileNumber}
          aria-describedby={errors.mobileNumber ? 'mobileNumber-error' : undefined}
          className="rounded-[var(--radius-sm)] border border-border-strong bg-surface px-3 py-2 text-sm text-ink focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-accent"
          {...register('mobileNumber')}
        />
        {errors.mobileNumber && (
          <p id="mobileNumber-error" role="alert" className="text-xs font-semibold text-danger-ink">
            {errors.mobileNumber.message}
          </p>
        )}
      </div>

      <div className="flex flex-col gap-1.5">
        <label htmlFor="emailAddress" className="text-sm font-semibold text-ink">
          Email
        </label>
        <input
          id="emailAddress"
          type="email"
          className="rounded-[var(--radius-sm)] border border-border-strong bg-surface px-3 py-2 text-sm text-ink focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-accent"
          {...register('emailAddress')}
        />
      </div>

      <PasswordField
        id="staffPassword"
        label="Password"
        purpose="new"
        required
        error={errors.password?.message}
        {...register('password')}
      />

      <Controller
        control={control}
        name="roleIds"
        render={({ field }) => (
          <RoleAssignmentField
            roleIds={catalogue.data?.roleIds ?? []}
            isLoading={catalogue.isLoading}
            value={field.value}
            onChange={field.onChange}
            error={errors.roleIds?.message}
          />
        )}
      />

      {catalogue.data && (
        <CapabilityMatrixPreview catalogue={catalogue.data} selectedRoleIds={selectedRoleIds} />
      )}

      <Button
        type="submit"
        disabled={isSubmitting || isPending}
        aria-busy={isSubmitting || isPending}
        className="w-fit"
      >
        {isSubmitting || isPending ? 'Creating…' : 'Create account'}
      </Button>
    </form>
  );
}
