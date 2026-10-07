'use client';

import { useState } from 'react';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import Link from 'next/link';
import { Button } from '@/components/ui/button';
import { PasswordField } from './PasswordField';
import { CaptchaField } from './CaptchaField';
import { RetryNotice } from './RetryNotice';
import { RegisterIdentifierFields } from './RegisterIdentifierFields';
import {
  registerSchema,
  REGISTER_FIELD_MAP,
  REGISTER_KNOWN_FIELDS,
  type RegisterFormValues,
} from './RegisterForm.schema';
import { applyValidationErrors } from '@/lib/api/errors';
import { ApiRateLimitedError, ApiValidationError } from '@/lib/api/client';
import type { CaptchaChallenge } from '@/lib/api/auth';
import type { RegisterCustomerRequest } from '@/lib/api/auth';

/**
 * `components/auth/RegisterForm.tsx` — US-ACC-01, HO-ACC-002 §11.4. Mobile/email are each
 * labelled optional; a form-level message (linked to both fields) requires at least one. No
 * mobile regex (OQ-1 unconfirmed — shape only, the server normalises). Password ≥ 10 chars,
 * message names the minimum and the actual length. Validation rules themselves live in the
 * sibling `RegisterForm.schema.ts` (MEDIUM/LOW gate item 18, first pass — a component-size split).
 *
 * ADR-FE-006/NFR-USA-5's one exception: after ANY failed submit (validation, `429`, `5xx` or
 * network) the password and CAPTCHA answer are cleared and a fresh challenge is fetched — a
 * client-side-only validation failure (caught before `onSubmit` even runs) does NOT spend the
 * challenge, since nothing was submitted to the server.
 */

/**
 * ARCH gate HIGH 3 (first pass), HO-CAT-002 §8.2 rule 4 — takes `captcha`/`onRegister`/`isPending`/
 * `rateLimitError` as props rather than calling `useCaptcha()`/`useRegisterCustomer()` itself; the
 * page (`app/(auth)/register/page.tsx`) wires those hooks and passes their results down, the same
 * pattern `components/cat/CatForm.tsx`'s own `onSubmit` prop already establishes.
 */
export interface RegisterFormProps {
  captcha: Pick<
    { data: CaptchaChallenge | undefined; isLoading: boolean; isError: boolean },
    'data' | 'isLoading' | 'isError'
  > & { refetch: () => unknown };
  onRegister: (request: RegisterCustomerRequest) => Promise<unknown>;
  isPending: boolean;
  rateLimitError: ApiRateLimitedError | null;
  onSuccess: () => void;
}

export function RegisterForm({
  captcha,
  onRegister,
  isPending,
  rateLimitError,
  onSuccess,
}: RegisterFormProps) {
  const [duplicateField, setDuplicateField] = useState<'mobileNumber' | 'emailAddress' | null>(
    null,
  );

  const {
    register,
    handleSubmit,
    setValue,
    setError,
    formState: { errors, isSubmitting },
  } = useForm<RegisterFormValues>({
    resolver: zodResolver(registerSchema),
    defaultValues: {
      fullName: '',
      mobileNumber: '',
      emailAddress: '',
      password: '',
      captchaAnswer: '',
    },
  });

  async function onSubmit(values: RegisterFormValues) {
    setDuplicateField(null);
    try {
      await onRegister({
        fullName: values.fullName,
        mobileNumber: values.mobileNumber || null,
        emailAddress: values.emailAddress || null,
        password: values.password,
        captchaId: captcha.data?.captchaId,
        captchaAnswer: values.captchaAnswer,
      });
      onSuccess();
    } catch (error) {
      if (error instanceof ApiValidationError) {
        applyValidationErrors(error, setError, {
          fieldMap: REGISTER_FIELD_MAP,
          knownFields: REGISTER_KNOWN_FIELDS,
        });
        const code = error.response.errors[0]?.errorCode;
        if (code === 'MobileNumberAlreadyRegistered') setDuplicateField('mobileNumber');
        else if (code === 'EmailAddressAlreadyRegistered') setDuplicateField('emailAddress');
      } else if (!(error instanceof ApiRateLimitedError)) {
        setError('root', { type: 'server', message: 'Something went wrong. Please try again.' });
      }
      // ADR-FE-006: every failed submit that reached the server clears the password and CAPTCHA
      // answer, then fetches a fresh challenge.
      setValue('password', '');
      setValue('captchaAnswer', '');
      void captcha.refetch();
    }
  }

  const mobileError = errors.mobileNumber?.message;
  const emailErrorMessage = errors.emailAddress?.message;
  // A shared form-level "give at least one" message is linked to BOTH fields via aria-describedby
  // — react-hook-form sets the same error on both paths, so either one carries it.
  const sharedAtLeastOneMessage =
    mobileError && mobileError === emailErrorMessage ? mobileError : undefined;

  return (
    <form
      method="post"
      onSubmit={handleSubmit(onSubmit)}
      noValidate
      className="flex flex-col gap-5"
    >
      {errors.root && (
        <p role="alert" aria-live="polite" className="text-sm font-semibold text-danger-ink">
          {errors.root.message}
        </p>
      )}
      {rateLimitError && <RetryNotice retryAfterSeconds={rateLimitError.retryAfterSeconds} />}

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

      <RegisterIdentifierFields
        register={register}
        mobileError={mobileError}
        emailErrorMessage={emailErrorMessage}
        sharedAtLeastOneMessage={sharedAtLeastOneMessage}
        duplicateField={duplicateField}
      />

      <PasswordField
        id="password"
        label="Password"
        purpose="new"
        required
        error={errors.password?.message}
        {...register('password')}
      />

      <CaptchaField
        id="captchaAnswer"
        imageDataUrl={captcha.data?.imageDataUrl}
        isLoading={captcha.isLoading}
        isError={captcha.isError}
        onRetry={() => captcha.refetch()}
        error={errors.captchaAnswer?.message}
        inputProps={register('captchaAnswer')}
      />

      <Button
        type="submit"
        disabled={isSubmitting || isPending}
        aria-busy={isSubmitting || isPending}
      >
        {isSubmitting || isPending ? 'Creating your account…' : 'Create account'}
      </Button>

      <p className="text-sm text-ink-muted">
        Already have an account?{' '}
        <Link
          href="/sign-in"
          className="font-semibold text-accent-ink no-underline hover:underline"
        >
          Sign in
        </Link>
      </p>
    </form>
  );
}
