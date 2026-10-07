'use client';

import { useEffect, useState } from 'react';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { z } from 'zod';
import Link from 'next/link';
import { Button } from '@/components/ui/button';
import { PasswordField } from './PasswordField';
import { ApiValidationError } from '@/lib/api/client';
import type { ConfirmPasswordResetRequest } from '@/lib/api/auth';

const schema = z.object({
  newPassword: z.string().min(1, 'Enter a new password.'),
});
type FormValues = z.infer<typeof schema>;

/**
 * `components/auth/ResetPasswordForm.tsx` — US-ACC-05, ADR-FE-007. The reset token is read ONCE
 * from `location.hash` on mount, kept only in component state, then stripped from the address bar
 * — it never reaches a URL a browser might log, sync or restore. No pre-check: validity is
 * decided entirely by the `confirm` submission. One endpoint serves both customer and staff
 * tokens (HO-ACC-001 §12.4), so the success screen links to both sign-in pages without hinting
 * which kind of account it was for.
 *
 * ARCH gate HIGH 3 (first pass) — takes `onConfirm`/`isPending` as props; the page
 * (`app/(auth)/reset-password/page.tsx`) wires `useConfirmPasswordReset()`.
 */
export interface ResetPasswordFormProps {
  onConfirm: (request: ConfirmPasswordResetRequest) => Promise<unknown>;
  isPending: boolean;
}

export function ResetPasswordForm({ onConfirm, isPending }: ResetPasswordFormProps) {
  const [token, setToken] = useState<string | null>(null);
  const [tokenRead, setTokenRead] = useState(false);
  const [succeeded, setSucceeded] = useState(false);
  const [invalidToken, setInvalidToken] = useState(false);

  useEffect(() => {
    const hash = window.location.hash; // e.g. "#token=abc123"
    const match = /token=([^&]+)/.exec(hash);
    setToken(match ? decodeURIComponent(match[1]!) : null);
    setTokenRead(true);
    if (hash) window.history.replaceState(null, '', window.location.pathname);
  }, []);

  const {
    register,
    handleSubmit,
    setError,
    formState: { errors, isSubmitting },
  } = useForm<FormValues>({ resolver: zodResolver(schema), defaultValues: { newPassword: '' } });

  async function onSubmit(values: FormValues) {
    if (!token) return;
    try {
      await onConfirm({ token, newPassword: values.newPassword });
      setSucceeded(true);
    } catch (error) {
      if (error instanceof ApiValidationError) {
        const code = error.response.errors[0]?.errorCode;
        if (code === 'InvalidPasswordResetToken') {
          setInvalidToken(true);
          return;
        }
        // BreachedPassword / PasswordTooShort — field error; the token stays in state so the
        // user can try another password without re-opening the link.
        for (const validationError of error.response.errors) {
          setError('newPassword', { type: 'server', message: validationError.errorMessage });
        }
      } else {
        setError('root', { type: 'server', message: 'Something went wrong. Please try again.' });
      }
    }
  }

  if (!tokenRead) return null;

  if (!token || invalidToken) {
    return (
      <div className="flex flex-col gap-4">
        <p role="alert" className="text-sm text-ink">
          This link is invalid or has expired.
        </p>
        <Link
          href="/forgot-password"
          className="text-sm font-semibold text-accent-ink no-underline hover:underline"
        >
          Request another link
        </Link>
      </div>
    );
  }

  if (succeeded) {
    return (
      <div className="flex flex-col gap-4" role="status" aria-live="polite">
        <p className="text-sm text-ink">Your password has been set. Sign in with it now.</p>
        <div className="flex flex-col gap-2">
          <Link
            href="/sign-in"
            className="text-sm font-semibold text-accent-ink no-underline hover:underline"
          >
            Sign in as a customer
          </Link>
          <Link
            href="/staff/sign-in"
            className="text-sm font-semibold text-accent-ink no-underline hover:underline"
          >
            Sign in as staff
          </Link>
        </div>
      </div>
    );
  }

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
      <PasswordField
        id="newPassword"
        label="New password"
        purpose="new"
        required
        error={errors.newPassword?.message}
        {...register('newPassword')}
      />
      <Button
        type="submit"
        disabled={isSubmitting || isPending}
        aria-busy={isSubmitting || isPending}
      >
        {isSubmitting || isPending ? 'Setting password…' : 'Set new password'}
      </Button>
    </form>
  );
}
