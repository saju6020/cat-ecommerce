'use client';

import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { z } from 'zod';
import Link from 'next/link';
import { Button } from '@/components/ui/button';
import { PasswordField } from '@/components/auth/PasswordField';
import { RetryNotice } from '@/components/auth/RetryNotice';
import { ApiRateLimitedError, ApiValidationError } from '@/lib/api/client';

const schema = z.object({
  currentPassword: z.string().min(1, 'Enter your current password.'),
  understood: z.boolean().refine((v) => v, 'Confirm you understand this cannot be undone.'),
});
type FormValues = z.infer<typeof schema>;

/**
 * `components/account/DeleteAccountForm.tsx` — US-ACC-13, UI-3. States exactly what happens,
 * offers the download first, requires the current password and an explicit checkbox.
 * `ReauthenticationFailed` → field error (the same pattern the email step-up uses); `429` →
 * `RetryNotice`.
 *
 * ARCH gate HIGH 3 (first pass) — takes `onDelete`/`isPending`/`error` as props; the page
 * (`app/(portal)/portal/account/delete/page.tsx`) wires `useDeleteAccount()` and owns the
 * success behaviour (clear the whole cache, route to `/account-deleted`) — this component only
 * submits the current password and interprets whatever `onDelete` throws.
 */
export interface DeleteAccountFormProps {
  onDelete: (currentPassword: string) => Promise<void>;
  isPending: boolean;
  error: unknown;
}

export function DeleteAccountForm({ onDelete, isPending, error }: DeleteAccountFormProps) {
  const {
    register,
    handleSubmit,
    setValue,
    setError,
    formState: { errors, isSubmitting },
  } = useForm<FormValues>({
    resolver: zodResolver(schema),
    defaultValues: { currentPassword: '', understood: false },
  });

  async function onSubmit(values: FormValues) {
    try {
      await onDelete(values.currentPassword);
    } catch (submitError) {
      if (submitError instanceof ApiValidationError) {
        const code = submitError.response.errors[0]?.errorCode;
        if (code === 'ReauthenticationFailed') {
          setError('currentPassword', {
            type: 'server',
            message: 'That current password is incorrect.',
          });
          setValue('currentPassword', '');
          return;
        }
        setError('root', {
          type: 'server',
          message: submitError.response.errors[0]?.errorMessage ?? 'Something went wrong.',
        });
      } else if (!(submitError instanceof ApiRateLimitedError)) {
        setError('root', { type: 'server', message: 'Something went wrong. Please try again.' });
      }
    }
  }

  const rateLimitError = error instanceof ApiRateLimitedError ? error : null;

  return (
    <div className="flex max-w-lg flex-col gap-5">
      <div className="rounded-[var(--radius-md)] border border-danger-ink/30 bg-danger-bg p-4 text-sm text-danger-ink">
        <p className="font-semibold">Deleting your account will:</p>
        <ul className="mt-2 list-disc pl-5">
          <li>Remove your personal details, contact details and addresses.</li>
          <li>Sign you out — you will not be able to sign in again.</li>
          <li>
            Keep clinical and billing records for the legally required period, with your name
            replaced by &ldquo;Anonymised customer&rdquo;.
          </li>
          <li>This cannot be undone.</li>
        </ul>
      </div>

      <Link
        href="/portal/account/data"
        className="text-sm font-semibold text-accent-ink no-underline hover:underline"
      >
        Download my data first
      </Link>

      <form
        method="post"
        onSubmit={handleSubmit(onSubmit)}
        noValidate
        className="flex flex-col gap-4"
      >
        {errors.root && (
          <p role="alert" aria-live="polite" className="text-sm font-semibold text-danger-ink">
            {errors.root.message}
          </p>
        )}
        {rateLimitError && <RetryNotice retryAfterSeconds={rateLimitError.retryAfterSeconds} />}

        <PasswordField
          id="currentPassword"
          label="Current password"
          purpose="current"
          required
          error={errors.currentPassword?.message}
          {...register('currentPassword')}
        />

        <label className="flex items-start gap-2 text-sm text-ink">
          <input type="checkbox" className="mt-1" {...register('understood')} />I understand this
          cannot be undone.
        </label>
        {errors.understood && (
          <p role="alert" className="text-xs font-semibold text-danger-ink">
            {errors.understood.message}
          </p>
        )}

        <Button
          type="submit"
          variant="destructive"
          disabled={isSubmitting || isPending}
          aria-busy={isSubmitting || isPending}
          className="w-fit"
        >
          {isSubmitting || isPending ? 'Deleting…' : 'Delete my account'}
        </Button>
      </form>
    </div>
  );
}
