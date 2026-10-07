'use client';

import Link from 'next/link';
import { useId } from 'react';

/**
 * `components/account/PreferredChannelField.tsx` — HO-NTF-002 §11.2, US-NTF-01. A radio group
 * "Send important messages by" — **Text message (SMS)** or **Email** — rendered above HO-ACC-002's
 * consent matrix, inside the SAME form and the same explicit Save (ADR-FE-008): it holds no state
 * of its own and sends nothing; the page owns the value and the single `PUT`.
 *
 * A channel the account has no identifier for (`availablePreferredChannels` omits it) is shown
 * disabled with a sentence saying what to add, and a link to the profile tab. `value` is the
 * current effective choice (selected even when the saved `preferredChannel` is null). A server
 * `PreferredChannelHasNoDestination` arrives as `error` and is shown on this field.
 */
export interface PreferredChannelFieldProps {
  value: string | null;
  available: readonly string[];
  onChange: (channel: string) => void;
  error?: string | null;
  disabled?: boolean;
  profileHref?: string;
}

const OPTIONS = [
  {
    id: 'Sms',
    label: 'Text message (SMS)',
    missingHint: 'Add a mobile number to your profile to choose text messages',
  },
  {
    id: 'Email',
    label: 'Email',
    missingHint: 'Add an email address to your profile to choose email',
  },
] as const;

export function PreferredChannelField({
  value,
  available,
  onChange,
  error,
  disabled,
  profileHref = '/portal/account/profile',
}: PreferredChannelFieldProps) {
  const baseId = useId();
  const helpId = `${baseId}-help`;
  const errorId = `${baseId}-error`;

  return (
    <fieldset
      className="flex flex-col gap-3 rounded-[var(--radius-md)] border border-border bg-surface p-4"
      aria-describedby={error ? `${helpId} ${errorId}` : helpId}
    >
      <legend className="px-1 text-sm font-semibold text-ink">Send important messages by</legend>
      <div className="flex flex-col gap-3">
        {OPTIONS.map((option) => {
          const isAvailable = available.includes(option.id);
          const inputId = `${baseId}-${option.id}`;
          return (
            <div key={option.id} className="flex flex-col gap-1">
              <label htmlFor={inputId} className="flex items-center gap-2 text-sm text-ink">
                <input
                  id={inputId}
                  type="radio"
                  name={`${baseId}-preferred-channel`}
                  value={option.id}
                  checked={value === option.id}
                  disabled={disabled || !isAvailable}
                  aria-describedby={isAvailable ? undefined : `${inputId}-hint`}
                  onChange={() => onChange(option.id)}
                />
                {option.label}
              </label>
              {!isAvailable && (
                <p id={`${inputId}-hint`} className="ml-6 text-xs text-ink-muted">
                  {option.missingHint}.{' '}
                  <Link
                    href={profileHref}
                    className="font-semibold text-accent-ink underline underline-offset-2"
                  >
                    Go to your profile
                  </Link>
                </p>
              )}
            </div>
          );
        })}
      </div>
      <p id={helpId} className="text-xs text-ink-muted">
        Password resets, reminders and updates are sent this way, and a copy always appears in your
        inbox.
      </p>
      {error && (
        <p id={errorId} role="alert" className="text-sm font-semibold text-danger-ink">
          {error}
        </p>
      )}
    </fieldset>
  );
}
