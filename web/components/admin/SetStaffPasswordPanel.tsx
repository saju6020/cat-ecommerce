'use client';

import { useState } from 'react';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { z } from 'zod';
import { Button } from '@/components/ui/button';
import { PasswordField } from '@/components/auth/PasswordField';
import { applyValidationErrors, describeUnexpectedError } from '@/lib/api/errors';
import { ApiValidationError } from '@/lib/api/client';
import { PASSWORD_MIN_LENGTH } from '@/lib/validation/password';

const schema = z.object({
  password: z.string().min(PASSWORD_MIN_LENGTH, `Use at least ${PASSWORD_MIN_LENGTH} characters.`),
});
type FormValues = z.infer<typeof schema>;

/**
 * `components/admin/SetStaffPasswordPanel.tsx` — owner, 2026-10-19. An administrator sets a staff member's
 * password and shares it with them (there is no email delivery in production for the self-service reset). The
 * same rules as every password; setting it signs the member out of any open session. The page wires
 * `useSetStaffPassword(staffAccountId)`.
 */
export interface SetStaffPasswordPanelProps {
  staffName: string;
  hasPassword: boolean;
  onSetPassword: (password: string) => Promise<unknown>;
  isPending: boolean;
}

export function SetStaffPasswordPanel({
  staffName,
  hasPassword,
  onSetPassword,
  isPending,
}: SetStaffPasswordPanelProps) {
  const [done, setDone] = useState(false);
  const {
    register,
    handleSubmit,
    setError,
    reset,
    formState: { errors, isSubmitting },
  } = useForm<FormValues>({ resolver: zodResolver(schema), defaultValues: { password: '' } });

  async function onSubmit(values: FormValues) {
    setDone(false);
    try {
      await onSetPassword(values.password);
      reset({ password: '' });
      setDone(true);
    } catch (error) {
      if (error instanceof ApiValidationError) {
        applyValidationErrors(error, setError, {
          fieldMap: { Password: 'password', NewPassword: 'password' },
          knownFields: ['password'],
        });
      } else {
        setError('root', { type: 'server', message: describeUnexpectedError(error) });
      }
    }
  }

  return (
    <section aria-labelledby="set-password-heading" className="flex max-w-lg flex-col gap-3">
      <h2 id="set-password-heading" className="m-0 text-lg font-semibold text-ink">
        {hasPassword ? 'Set a new password' : 'Set a password'}
      </h2>
      <p className="m-0 text-sm text-ink-muted">
        Share it with {staffName} yourself.{' '}
        {hasPassword ? 'They will be signed out of any open session.' : ''}
      </p>
      <form
        method="post"
        onSubmit={handleSubmit(onSubmit)}
        noValidate
        className="flex flex-col gap-4"
      >
        {errors.root && (
          <p role="alert" className="text-sm font-semibold text-danger-ink">
            {errors.root.message}
          </p>
        )}
        <PasswordField
          id="newStaffPassword"
          label="New password"
          purpose="new"
          required
          error={errors.password?.message}
          {...register('password')}
        />
        <Button
          type="submit"
          variant="secondary"
          disabled={isSubmitting || isPending}
          aria-busy={isSubmitting || isPending}
          className="w-fit"
        >
          {isSubmitting || isPending ? 'Saving…' : 'Set password'}
        </Button>
        {done && (
          <p role="status" aria-live="polite" className="m-0 text-sm text-ink">
            Password set. Share it with {staffName}.
          </p>
        )}
      </form>
    </section>
  );
}
