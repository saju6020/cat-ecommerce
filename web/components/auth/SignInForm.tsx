'use client';

import { useState } from 'react';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { z } from 'zod';
import Link from 'next/link';
import { LoaderCircle, TriangleAlert } from 'lucide-react';
import { cn } from '@/lib/utils';
import { pillInputClass } from '@/components/hearth/fields';
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
  /** `hearth` = the customer "Welcome home" sign-in look (owner, 2026-10-16). Staff keep `plain`. */
  appearance?: 'plain' | 'hearth';
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
  appearance = 'plain',
}: SignInFormProps) {
  const hearth = appearance === 'hearth';
  // Signed in, next page still loading: the button stays busy so it never looks idle (or re-clickable) meanwhile.
  const [leaving, setLeaving] = useState(false);
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
      setLeaving(true);
      onSuccess(result);
    } catch {
      // Password is cleared on ANY failure — identifier survives (NFR-USA-5's one exception).
      setValue('password', '');
    }
  }

  const busy = isSubmitting || isPending || leaving;

  const forgotPasswordHref =
    audience === 'customer' ? '/forgot-password' : '/staff/forgot-password';
  const forgotPasswordLink = showForgotPasswordLink && (
    <Link
      href={forgotPasswordHref}
      className={cn(
        'font-semibold text-accent-ink no-underline hover:underline',
        hearth ? 'text-[13px]' : 'text-sm',
      )}
    >
      {audience === 'staff' ? 'Set or reset password' : 'Forgot your password?'}
    </Link>
  );
  // The Hearth look boxes the (unchanged) messages: a rounded danger-bg panel with a warning mark.
  const alertClass = hearth
    ? 'flex gap-2.5 rounded-2xl bg-danger-bg px-4 py-3.5 text-sm leading-snug text-danger-ink'
    : 'text-sm font-semibold text-danger-ink';
  const alertMark = hearth && (
    <TriangleAlert size={18} aria-hidden="true" className="mt-px flex-none" />
  );

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
        <p role="alert" aria-live="polite" className={alertClass}>
          {alertMark}
          <span>{SIGN_IN_FAILED_MESSAGE}</span>
        </p>
      )}
      {!!error &&
        !(error instanceof ApiRateLimitedError) &&
        !(error instanceof ApiUnauthorizedError) && (
          <p role="alert" aria-live="polite" className={alertClass}>
            {alertMark}
            <span>Something went wrong. Please try again.</span>
          </p>
        )}

      <div className={cn('flex flex-col', hearth ? 'gap-2' : 'gap-1.5')}>
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
          className={cn(
            hearth
              ? cn(pillInputClass, 'min-h-12 px-5')
              : 'rounded-[var(--radius-sm)] border border-border-strong bg-surface px-3 py-2 text-sm text-ink',
            'focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-accent',
          )}
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
        appearance={appearance}
        labelAside={hearth ? forgotPasswordLink : undefined}
        {...register('password')}
      />

      <div className="flex flex-col gap-3">
        <Button
          type="submit"
          disabled={busy}
          aria-busy={busy}
          {...(hearth
            ? { variant: 'pill' as const, size: 'pillLg' as const, className: 'w-full' }
            : {})}
        >
          {busy ? (
            <>
              <LoaderCircle
                size={18}
                aria-hidden="true"
                className="animate-spin motion-reduce:animate-none"
              />
              Signing in…
            </>
          ) : (
            submitLabel
          )}
        </Button>
        {!hearth && forgotPasswordLink}
      </div>
    </form>
  );
}
