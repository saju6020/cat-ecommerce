'use client';

import Link from 'next/link';
import type { UseFormRegister } from 'react-hook-form';
import type { RegisterFormValues } from './RegisterForm.schema';
import { cn } from '@/lib/utils';
import { pillInputClass } from '@/components/hearth/fields';

/**
 * `components/auth/RegisterIdentifierFields.tsx` — MEDIUM/LOW gate item 18 (first pass): split out
 * of `RegisterForm.tsx` once it was still over §11's ~200-line guideline after the props/
 * `method=post` gate fixes (items 3/4) put lines back on top of the original hook-removal shrink.
 * The "give a mobile, an email, or both" pair stays tightly coupled to the parent's
 * `register`/error state, passed straight through as props rather than re-derived here.
 */

/** U-B6/U-B7: which duplicate-identifier field to show the Sign in/Reset password links under —
 *  driven by the server's `errorCode`, which is read by the caller for branching only and never
 *  rendered (U-X2). Both `MobileNumberAlreadyRegistered` and `EmailAddressAlreadyRegistered`
 *  render the same fixed pair of links; the message text itself already comes from the one server
 *  constant (`AccountMessages.MobileInUse`/`EmailInUse`) via `applyValidationErrors`. */
function DuplicateIdentifierLinks() {
  return (
    <p className="text-xs text-ink-muted">
      <Link href="/sign-in" className="font-semibold text-accent-ink no-underline hover:underline">
        Sign in
      </Link>{' '}
      or{' '}
      <Link
        href="/forgot-password"
        className="font-semibold text-accent-ink no-underline hover:underline"
      >
        reset your password
      </Link>
      .
    </p>
  );
}

// Hearth "Welcome home" register (owner-approved 2026-10-16): pill fields at least 48px high.
const HEARTH_INPUT = cn(
  pillInputClass,
  'min-h-12 px-5 focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-accent',
);

export function RegisterIdentifierFields({
  register,
  mobileError,
  emailErrorMessage,
  sharedAtLeastOneMessage,
  duplicateField,
}: {
  register: UseFormRegister<RegisterFormValues>;
  mobileError: string | undefined;
  emailErrorMessage: string | undefined;
  sharedAtLeastOneMessage: string | undefined;
  duplicateField: 'mobileNumber' | 'emailAddress' | null;
}) {
  return (
    <>
      {/* The "give a mobile number, an email, or both" line is now the page's description, under the heading. */}
      <div className="grid gap-4 md:grid-cols-2 md:gap-3">
        <div className="flex min-w-0 flex-col gap-2">
          <label htmlFor="mobileNumber" className="text-sm font-semibold text-ink">
            Mobile number <span className="font-normal text-ink-muted">(optional)</span>
          </label>
          <input
            id="mobileNumber"
            autoComplete="tel"
            placeholder="01712 345678"
            aria-invalid={!!mobileError}
            aria-describedby={
              sharedAtLeastOneMessage
                ? 'identifiers-error'
                : mobileError
                  ? 'mobileNumber-error'
                  : undefined
            }
            className={HEARTH_INPUT}
            {...register('mobileNumber')}
          />
          {mobileError && !sharedAtLeastOneMessage && (
            <p
              id="mobileNumber-error"
              role="alert"
              className="text-xs font-semibold text-danger-ink"
            >
              {mobileError}
            </p>
          )}
          {duplicateField === 'mobileNumber' && <DuplicateIdentifierLinks />}
        </div>

        <div className="flex min-w-0 flex-col gap-2">
          <label htmlFor="emailAddress" className="text-sm font-semibold text-ink">
            Email <span className="font-normal text-ink-muted">(optional)</span>
          </label>
          <input
            id="emailAddress"
            type="email"
            autoComplete="email"
            aria-invalid={!!emailErrorMessage}
            aria-describedby={
              sharedAtLeastOneMessage
                ? 'identifiers-error'
                : emailErrorMessage
                  ? 'emailAddress-error'
                  : undefined
            }
            className={HEARTH_INPUT}
            {...register('emailAddress')}
          />
          {emailErrorMessage && emailErrorMessage !== sharedAtLeastOneMessage && (
            <p
              id="emailAddress-error"
              role="alert"
              className="text-xs font-semibold text-danger-ink"
            >
              {emailErrorMessage}
            </p>
          )}
          {duplicateField === 'emailAddress' && <DuplicateIdentifierLinks />}
        </div>
        {/* Side by side, the shared "give at least one" message shows once, under both fields, linked to each. */}
        {sharedAtLeastOneMessage && (
          <p
            id="identifiers-error"
            role="alert"
            className="text-xs font-semibold text-danger-ink md:col-span-2"
          >
            {sharedAtLeastOneMessage}
          </p>
        )}
      </div>
    </>
  );
}
