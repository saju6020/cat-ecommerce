'use client';

import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { useMemo } from 'react';
import { Button } from '@/components/ui/button';
import { PasswordField } from '@/components/auth/PasswordField';
import { RetryNotice } from '@/components/auth/RetryNotice';
import { UnsavedChangesGuard } from './UnsavedChangesGuard';
import { AdditionalPhoneNumbersField } from './AdditionalPhoneNumbersField';
import {
  makeProfileSchema,
  PROFILE_FIELD_MAP,
  PROFILE_KNOWN_FIELDS,
  type ProfileFormValues,
} from './ProfileForm.schema';
import { applyValidationErrors } from '@/lib/api/errors';
import { ApiRateLimitedError, ApiValidationError } from '@/lib/api/client';
import type { CustomerProfile, UpdateCustomerProfileRequest } from '@/lib/api/account';

/**
 * `components/account/ProfileForm.tsx` — US-ACC-06, HO-ACC-002 §11.7 + the 2026-09-25 Amended
 * row: a `PasswordField purpose="current"` appears, and is required, only while the email field's
 * value differs from the profile it loaded with (the backend requires `currentPassword` in that
 * exact case — HO-ACC-001 §1 Amended (17)). `ReauthenticationFailed` maps to the same field the
 * deletion step-up uses it for. `429` (the `profile-update` policy) → `RetryNotice`.
 *
 * ARCH gate HIGH 3 (first pass) — takes `onSave`/`isPending`/`rateLimitError` as props; the page
 * (`app/(portal)/portal/account/profile/page.tsx`) wires `useUpdateCustomerProfile()`.
 */
export interface ProfileFormProps {
  profile: CustomerProfile;
  onSave: (request: UpdateCustomerProfileRequest) => Promise<void>;
  isPending: boolean;
  rateLimitError: ApiRateLimitedError | null;
}

export function ProfileForm({ profile, onSave, isPending, rateLimitError }: ProfileFormProps) {
  const schema = useMemo(() => makeProfileSchema(profile), [profile]);
  const {
    register,
    control,
    handleSubmit,
    watch,
    setError,
    setValue,
    formState: { errors, isDirty, isSubmitSuccessful },
  } = useForm<ProfileFormValues>({
    resolver: zodResolver(schema),
    defaultValues: {
      fullName: profile.fullName,
      emailAddress: profile.emailAddress ?? '',
      additionalPhoneNumbers: profile.additionalPhoneNumbers.map((value) => ({ value })),
      currentPassword: '',
    },
  });

  const emailValue = watch('emailAddress');
  const emailChanging = profile.canEditEmail && (emailValue || '') !== (profile.emailAddress ?? '');

  async function onSubmit(values: ProfileFormValues) {
    try {
      await onSave({
        fullName: values.fullName,
        emailAddress: profile.canEditEmail ? values.emailAddress || null : profile.emailAddress,
        additionalPhoneNumbers: values.additionalPhoneNumbers.map((f) => f.value).filter(Boolean),
        currentPassword: emailChanging ? values.currentPassword : undefined,
      });
    } catch (error) {
      if (error instanceof ApiValidationError) {
        applyValidationErrors(error, setError, {
          fieldMap: PROFILE_FIELD_MAP,
          knownFields: PROFILE_KNOWN_FIELDS,
        });
      } else if (!(error instanceof ApiRateLimitedError)) {
        setError('root', { type: 'server', message: 'Something went wrong. Please try again.' });
      }
      // CODE_REVIEW LOW (first pass) — every other password-bearing form in this slice
      // (RegisterForm, SignInForm, DeleteAccountForm) clears the password field on a failed
      // submit; this one didn't, including on ReauthenticationFailed itself.
      setValue('currentPassword', '');
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
      {rateLimitError && <RetryNotice retryAfterSeconds={rateLimitError.retryAfterSeconds} />}
      {isSubmitSuccessful && (
        <p role="status" aria-live="polite" className="text-sm font-semibold text-ink">
          Your profile has been saved.
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
          autoComplete="name"
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
        <label htmlFor="emailAddress" className="text-sm font-semibold text-ink">
          Email
        </label>
        {profile.canEditEmail ? (
          <input
            id="emailAddress"
            type="email"
            autoComplete="email"
            aria-invalid={!!errors.emailAddress}
            aria-describedby={errors.emailAddress ? 'emailAddress-error' : undefined}
            className="rounded-[var(--radius-sm)] border border-border-strong bg-surface px-3 py-2 text-sm text-ink focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-accent"
            {...register('emailAddress')}
          />
        ) : (
          <>
            <input
              id="emailAddress"
              readOnly
              value={profile.emailAddress ?? ''}
              aria-describedby="emailAddress-explanation"
              className="rounded-[var(--radius-sm)] border border-border bg-surface-sunken px-3 py-2 text-sm text-ink-muted"
            />
            <p id="emailAddress-explanation" className="text-xs text-ink-muted">
              Your email is how you sign in. Contact the clinic to change it.
            </p>
          </>
        )}
        {errors.emailAddress && (
          <p id="emailAddress-error" role="alert" className="text-xs font-semibold text-danger-ink">
            {errors.emailAddress.message}
          </p>
        )}
      </div>

      {emailChanging && (
        <PasswordField
          id="currentPassword"
          label="Current password"
          purpose="current"
          required
          error={errors.currentPassword?.message}
          {...register('currentPassword')}
        />
      )}

      <div className="flex flex-col gap-1.5">
        <label htmlFor="primaryMobileNumber" className="text-sm font-semibold text-ink">
          Primary mobile number
        </label>
        <input
          id="primaryMobileNumber"
          readOnly
          value={profile.mobileNumber ?? 'None on file'}
          aria-describedby="mobile-explanation"
          className="rounded-[var(--radius-sm)] border border-border bg-surface-sunken px-3 py-2 text-sm text-ink-muted"
        />
        <p id="mobile-explanation" className="text-xs text-ink-muted">
          Your mobile number is how you sign in. Contact the clinic to change it.
        </p>
      </div>

      <AdditionalPhoneNumbersField control={control} register={register} errors={errors} />

      <Button type="submit" disabled={isPending} aria-busy={isPending} className="w-fit">
        {isPending ? 'Saving…' : 'Save'}
      </Button>
    </form>
  );
}
