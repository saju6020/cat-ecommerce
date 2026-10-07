/**
 * `components/auth/RetryNotice.tsx` — HO-ACC-002 §11.3. Renders a `429` (`ApiRateLimitedError`):
 * "Too many attempts. Please wait {n} minutes and try again." with `n = ceil(retryAfterSeconds /
 * 60)`, minimum 1 — never the raw seconds, never the `Retry-After` header name. The submit button
 * stays enabled (§11.3) — this is informational, not a client-enforced lockout.
 */
export function formatRetryAfterMinutes(retryAfterSeconds: number): number {
  return Math.max(1, Math.ceil(retryAfterSeconds / 60));
}

export function RetryNotice({ retryAfterSeconds }: { retryAfterSeconds: number }) {
  const minutes = formatRetryAfterMinutes(retryAfterSeconds);
  return (
    <p role="alert" aria-live="polite" className="text-sm font-semibold text-danger-ink">
      Too many attempts. Please wait {minutes} minute{minutes === 1 ? '' : 's'} and try again.
    </p>
  );
}
