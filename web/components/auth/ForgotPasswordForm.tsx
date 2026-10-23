'use client';

import { useState } from 'react';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { z } from 'zod';
import Link from 'next/link';
import { Button } from '@/components/ui/button';
import { RetryNotice } from './RetryNotice';
import { ApiRateLimitedError } from '@/lib/api/client';
import { cn } from '@/lib/utils';
import { pillInputClass } from '@/components/hearth/fields';

const schema = z.object({
  identifier: z.string().trim().min(1, 'Enter your mobile number or email.'),
});
type FormValues = z.infer<typeof schema>;

/**
 * `components/auth/ForgotPasswordForm.tsx` — US-ACC-05/14, ADR-FE-007. On ANY `200` — whatever
 * the response body actually contains — renders the same client-owned confirmation. The body is
 * never rendered (§20.3: varying the screen by account state defeats the server's non-disclosure).
 *
 * ARCH gate HIGH 3 (first pass) — takes `onRequestReset`/`isPending`/`error` as props; the
 * customer and staff forgot-password pages each wire their own single mutation
 * (`useRequestCustomerPasswordReset`/`useRequestStaffPasswordReset`) rather than this component
 * importing both and picking one from its own `audience` prop.
 */
export interface ForgotPasswordFormProps {
  audience: 'customer' | 'staff';
  onRequestReset: (identifier: string) => Promise<unknown>;
  isPending: boolean;
  error: unknown;
  /** `hearth` = the customer "Welcome home" look (owner, 2026-10-16); staff keep `plain`. */
  appearance?: 'plain' | 'hearth';
  /** Hearth page: told when the request went through, so it can show its own "Check your messages" screen. */
  onSent?: () => void;
}

export function ForgotPasswordForm({
  audience,
  onRequestReset,
  isPending,
  error,
  appearance = 'plain',
  onSent,
}: ForgotPasswordFormProps) {
  const hearth = appearance === 'hearth';
  const [submitted, setSubmitted] = useState(false);

  const {
    register,
    handleSubmit,
    formState: { errors, isSubmitting },
  } = useForm<FormValues>({ resolver: zodResolver(schema), defaultValues: { identifier: '' } });

  async function onSubmit(values: FormValues) {
    try {
      await onRequestReset(values.identifier);
      setSubmitted(true);
      onSent?.();
    } catch {
      // A 429 is the only failure this endpoint can report to the client (R3 — every other
      // outcome is folded into the same 200); rendered below via the `error` prop.
    }
  }

  if (submitted) {
    return (
      <p role="status" aria-live="polite" className="text-sm text-ink">
        If an account matches, we&rsquo;ve sent a link to reset the password. It expires in 15
        minutes.
      </p>
    );
  }

  const rateLimitError = error instanceof ApiRateLimitedError ? error : null;

  return (
    <form
      method="post"
      onSubmit={handleSubmit(onSubmit)}
      noValidate
      className="flex flex-col gap-5"
    >
      {rateLimitError && <RetryNotice retryAfterSeconds={rateLimitError.retryAfterSeconds} />}

      <div className={cn('flex flex-col', hearth ? 'gap-2' : 'gap-1.5')}>
        <label htmlFor="identifier" className="text-sm font-semibold text-ink">
          Mobile number or email{' '}
          <span aria-hidden="true" className="text-danger-ink">
            *
          </span>
        </label>
        <input
          id="identifier"
          required
          autoComplete="username"
          aria-invalid={!!errors.identifier}
          aria-describedby={errors.identifier ? 'identifier-error' : undefined}
          className={cn(
            hearth
              ? cn(pillInputClass, 'min-h-12 px-5')
              : 'rounded-[var(--radius-sm)] border border-border-strong bg-surface px-3 py-2 text-sm text-ink',
            'focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-accent',
          )}
          {...register('identifier')}
        />
        {errors.identifier && (
          <p id="identifier-error" role="alert" className="text-xs font-semibold text-danger-ink">
            {errors.identifier.message}
          </p>
        )}
      </div>

      <Button
        type="submit"
        disabled={isSubmitting || isPending}
        aria-busy={isSubmitting || isPending}
        {...(hearth
          ? { variant: 'pill' as const, size: 'pillLg' as const, className: 'w-full' }
          : {})}
      >
        {isSubmitting || isPending ? 'Sending…' : 'Send reset link'}
      </Button>

      {hearth ? (
        <p className="text-[13px] leading-relaxed text-ink-muted">
          Can&rsquo;t get to your phone or email? Ask at the clinic reception and we&rsquo;ll help
          you in person.
        </p>
      ) : (
        <Link
          href={audience === 'customer' ? '/sign-in' : '/staff/sign-in'}
          className="text-sm font-semibold text-accent-ink no-underline hover:underline"
        >
          Back to sign in
        </Link>
      )}
    </form>
  );
}
