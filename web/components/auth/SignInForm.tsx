'use client';

import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { z } from 'zod';
import Link from 'next/link';
import { Button } from '@/components/ui/button';
import { PasswordField } from './PasswordField';
import { RetryNotice } from './RetryNotice';
import { ApiRateLimitedError, ApiUnauthorizedError } from '@/lib/api/client';
import type { CustomerSignInResult, SignInRequest } from '@/lib/api/auth';
import type { StaffSignInResult } from '@/lib/api/staff-auth';

const signInSchema = z.object({
  identifier: z.string().trim().min(1, 'Enter your mobile number or email.'),
  password: z.string().min(1, 'Enter your password.'),
});

export type SignInFormValues = z.infer<typeof signInSchema>;

/**
 * `components/auth/SignInForm.tsx` — HO-ACC-002 §11.3, US-ACC-04/14. One generic failure message
 * for every kind of sign-in refusal (unknown identifier, wrong password, locked, no password yet)
 * — ADR-FE-007 — never the server's own `message` (which happens to already be the same text
 * today, but this component does not rely on that; ADR-FE-007 requires the client to own it).
 * Password is cleared on failure; the identifier is kept.
 *
 * Navigation and session-cache invalidation are the CALLER's job (`onSuccess`) — this component
 * only submits credentials, so the same form works standalone (`/sign-in`, `/staff/sign-in`) and
 * embedded inside `IdleLock`'s re-authentication dialog, which has different post-success rules
 * (§11.9) than a fresh sign-in.
 *
 * ARCH gate HIGH 3 (first pass) — `audience` now only decides copy/hrefs/id prefixes; which
 * mutation hook to call is entirely the caller's decision, passed in as `onSignIn` — the
 * customer and staff sign-in pages each wire their own single hook
 * (`useSignInCustomer`/`useSignInStaff`), and `IdleLock` (only ever `audience="staff"`) wires
 * its own already-in-scope staff mutation, rather than this shared, reused-everywhere component
 * importing both hooks itself.
 */
export interface SignInFormProps {
  audience: 'customer' | 'staff';
  onSignIn: (values: SignInRequest) => Promise<CustomerSignInResult | StaffSignInResult>;
  isPending: boolean;
  error: unknown;
  onSuccess: (result: CustomerSignInResult | StaffSignInResult) => void;
  submitLabel?: string;
  showForgotPasswordLink?: boolean;
}

const SIGN_IN_FAILED_MESSAGE =
  "That mobile number or email and password don't match an account. Check them and try again, or reset your password.";

export function SignInForm({
  audience,
  onSignIn,
  isPending,
  error,
  onSuccess,
  submitLabel = 'Sign in',
  showForgotPasswordLink = true,
}: SignInFormProps) {
  const {
    register,
    handleSubmit,
    setValue,
    formState: { errors, isSubmitting },
  } = useForm<SignInFormValues>({
    resolver: zodResolver(signInSchema),
    defaultValues: { identifier: '', password: '' },
  });

  async function onSubmit(values: SignInFormValues) {
    try {
      const result = await onSignIn(values);
      onSuccess(result);
    } catch {
      // Password is cleared on ANY failure — identifier survives (NFR-USA-5's one exception).
      setValue('password', '');
    }
  }

  const forgotPasswordHref =
    audience === 'customer' ? '/forgot-password' : '/staff/forgot-password';

  return (
    <form
      method="post"
      onSubmit={handleSubmit(onSubmit)}
      noValidate
      className="flex flex-col gap-5"
    >
      {error instanceof ApiRateLimitedError && (
        <RetryNotice retryAfterSeconds={error.retryAfterSeconds} />
      )}
      {error instanceof ApiUnauthorizedError && (
        <p role="alert" aria-live="polite" className="text-sm font-semibold text-danger-ink">
          {SIGN_IN_FAILED_MESSAGE}
        </p>
      )}
      {!!error &&
        !(error instanceof ApiRateLimitedError) &&
        !(error instanceof ApiUnauthorizedError) && (
          <p role="alert" aria-live="polite" className="text-sm font-semibold text-danger-ink">
            Something went wrong. Please try again.
          </p>
        )}

      <div className="flex flex-col gap-1.5">
        <label htmlFor={`${audience}-identifier`} className="text-sm font-semibold text-ink">
          Mobile number or email{' '}
          <span aria-hidden="true" className="text-danger-ink">
            *
          </span>
        </label>
        <input
          id={`${audience}-identifier`}
          required
          autoComplete="username"
          aria-invalid={!!errors.identifier}
          aria-describedby={errors.identifier ? `${audience}-identifier-error` : undefined}
          className="rounded-[var(--radius-sm)] border border-border-strong bg-surface px-3 py-2 text-sm text-ink focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-accent"
          {...register('identifier')}
        />
        {errors.identifier && (
          <p
            id={`${audience}-identifier-error`}
            role="alert"
            className="text-xs font-semibold text-danger-ink"
          >
            {errors.identifier.message}
          </p>
        )}
      </div>

      <PasswordField
        id={`${audience}-password`}
        label="Password"
        purpose="current"
        required
        error={errors.password?.message}
        {...register('password')}
      />

      <div className="flex flex-col gap-3">
        <Button
          type="submit"
          disabled={isSubmitting || isPending}
          aria-busy={isSubmitting || isPending}
        >
          {isSubmitting || isPending ? 'Signing in…' : submitLabel}
        </Button>
        {showForgotPasswordLink && (
          <Link
            href={forgotPasswordHref}
            className="text-sm font-semibold text-accent-ink no-underline hover:underline"
          >
            {audience === 'staff' ? 'Set or reset password' : 'Forgot your password?'}
          </Link>
        )}
      </div>
    </form>
  );
}
