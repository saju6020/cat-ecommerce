'use client';

import Link from 'next/link';
import type { UseFormRegister } from 'react-hook-form';
import type { RegisterFormValues } from './RegisterForm.schema';

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
      <p className="text-xs text-ink-muted">
        Give a mobile number, an email, or both — you&rsquo;ll use one to sign in.
      </p>

      <div className="flex flex-col gap-1.5">
        <label htmlFor="mobileNumber" className="text-sm font-semibold text-ink">
          Mobile number <span className="font-normal text-ink-muted">(optional)</span>
        </label>
        <input
          id="mobileNumber"
          autoComplete="tel"
          placeholder="Bangladesh mobile, e.g. 01712 345678"
          aria-invalid={!!mobileError}
          aria-describedby={mobileError ? 'mobileNumber-error' : undefined}
          className="rounded-[var(--radius-sm)] border border-border-strong bg-surface px-3 py-2 text-sm text-ink focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-accent"
          {...register('mobileNumber')}
        />
        {mobileError && (
          <p id="mobileNumber-error" role="alert" className="text-xs font-semibold text-danger-ink">
            {mobileError}
          </p>
        )}
        {duplicateField === 'mobileNumber' && <DuplicateIdentifierLinks />}
      </div>

      <div className="flex flex-col gap-1.5">
        <label htmlFor="emailAddress" className="text-sm font-semibold text-ink">
          Email <span className="font-normal text-ink-muted">(optional)</span>
        </label>
        <input
          id="emailAddress"
          type="email"
          autoComplete="email"
          aria-invalid={!!emailErrorMessage}
          aria-describedby={emailErrorMessage ? 'emailAddress-error' : undefined}
          className="rounded-[var(--radius-sm)] border border-border-strong bg-surface px-3 py-2 text-sm text-ink focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-accent"
          {...register('emailAddress')}
        />
        {emailErrorMessage && emailErrorMessage !== sharedAtLeastOneMessage && (
          <p id="emailAddress-error" role="alert" className="text-xs font-semibold text-danger-ink">
            {emailErrorMessage}
          </p>
        )}
        {sharedAtLeastOneMessage && (
          <p id="emailAddress-error" role="alert" className="text-xs font-semibold text-danger-ink">
            {sharedAtLeastOneMessage}
          </p>
        )}
        {duplicateField === 'emailAddress' && <DuplicateIdentifierLinks />}
      </div>
    </>
  );
}
