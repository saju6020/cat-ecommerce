'use client';

import * as React from 'react';
import { RefreshCw } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { LoadingState } from '@/components/common/LoadingState';

/**
 * `components/auth/CaptchaField.tsx` — HO-ACC-002 §11.3/§8.6, ADR-FE-006. The client never judges
 * an answer; it only shows the challenge and lets the caller `refetch()` a new one. Callers
 * (`RegisterForm`) own the `useCaptcha()` query and pass its state down as props — components
 * never fetch (HO-CAT-002 §8.2).
 *
 *  - `alt` states purpose, never the answer (WCAG SC 1.1.1, SRS Q-17).
 *  - "New code" is a real text button, not an icon.
 *  - The reception alternative is always visible help text, linked by `aria-describedby`.
 *  - A new challenge is announced once, politely, without re-announcing on every keystroke.
 *  - `isError` renders a distinct "couldn't load" state with its own retry (U-B13).
 */
export interface CaptchaFieldProps {
  id: string;
  imageDataUrl?: string;
  isLoading: boolean;
  isError: boolean;
  onRetry: () => void;
  error?: string;
  inputProps: React.ComponentProps<'input'>;
}

export function CaptchaField({
  id,
  imageDataUrl,
  isLoading,
  isError,
  onRetry,
  error,
  inputProps,
}: CaptchaFieldProps) {
  const helpId = `${id}-help`;
  const errorId = error ? `${id}-error` : undefined;
  const [announcement, setAnnouncement] = React.useState('');
  const previousImageRef = React.useRef<string | undefined>(undefined);

  React.useEffect(() => {
    if (imageDataUrl && imageDataUrl !== previousImageRef.current) {
      if (previousImageRef.current !== undefined) {
        setAnnouncement('A new security check code is shown.');
      }
      previousImageRef.current = imageDataUrl;
    }
  }, [imageDataUrl]);

  return (
    // Hearth register (owner-approved 2026-10-16): the check sits in a rounded white box; "New code" is a pill.
    <div className="flex flex-col gap-2.5 rounded-card border border-border bg-surface p-4">
      <div className="flex items-center justify-between gap-2">
        <label htmlFor={id} className="text-sm font-semibold text-ink">
          Security check{' '}
          <span aria-hidden="true" className="text-danger-ink">
            *
          </span>
        </label>
        <button
          type="button"
          onClick={onRetry}
          className="inline-flex min-h-11 items-center gap-1.5 rounded-pill border border-border-strong bg-surface px-4 text-[13px] font-semibold text-accent-ink hover:bg-surface-sunken focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-accent"
        >
          <RefreshCw size={14} aria-hidden="true" />
          New code
        </button>
      </div>

      <div aria-live="polite" className="sr-only">
        {announcement}
      </div>

      {isLoading ? (
        <div className="flex h-[70px] w-[200px] items-center justify-center rounded-[14px] border border-border bg-surface-sunken">
          <LoadingState rows={1} variant="table" label="Loading security check…" className="w-24" />
        </div>
      ) : isError ? (
        <div className="flex h-[70px] w-[200px] flex-col items-center justify-center gap-1 rounded-[14px] border border-border bg-surface-sunken px-2 text-center">
          <p className="text-xs text-ink-muted">Couldn&rsquo;t load the security check.</p>
          <Button type="button" variant="ghost" size="sm" onClick={onRetry}>
            Try again
          </Button>
        </div>
      ) : (
        // Purpose stated, never the answer (WCAG SC 1.1.1).
        // eslint-disable-next-line @next/next/no-img-element -- a data: URL, not an optimisable asset
        <img
          src={imageDataUrl}
          alt="Security check: an image of 5 letters and numbers"
          width={200}
          height={70}
          className="h-[70px] w-[200px] rounded-[14px] border border-border-strong"
        />
      )}

      <input
        id={id}
        type="text"
        required
        autoComplete="off"
        autoCapitalize="characters"
        spellCheck={false}
        aria-invalid={!!error}
        aria-describedby={[helpId, errorId].filter(Boolean).join(' ')}
        className="min-h-12 w-full rounded-pill border border-border-strong bg-surface px-5 text-[15px] uppercase tracking-widest text-ink focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-accent"
        {...inputProps}
      />
      <p id={helpId} className="text-xs text-ink-muted">
        Type the letters and numbers shown above. Or register in person at the clinic reception.
      </p>
      {error && (
        <p id={errorId} role="alert" className="text-xs font-semibold text-danger-ink">
          {error}
        </p>
      )}
    </div>
  );
}
