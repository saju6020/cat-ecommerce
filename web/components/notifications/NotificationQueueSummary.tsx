'use client';

import { AlertTriangle } from 'lucide-react';
import { ErrorState } from '@/components/common/ErrorState';
import { LoadingState } from '@/components/common/LoadingState';
import { formatRelative } from '@/components/inbox/relativeTime';
import type { NotificationSummary } from '@/lib/api/notifications-log';

export const SMS_NOT_CONNECTED =
  'SMS is not connected to a provider. Text messages — including password resets — are not being delivered. Customers still receive in-app copies where available.';
export const EMAIL_NOT_CONNECTED =
  'Email is not connected to a provider. Emails — including password resets — are not being delivered. Customers still receive in-app copies where available.';

function Stat({ label, value }: { label: string; value: React.ReactNode }) {
  return (
    <div className="flex flex-col gap-0.5">
      <dt className="text-xs font-semibold text-ink-muted">{label}</dt>
      <dd className="m-0 text-lg font-semibold text-ink">{value}</dd>
    </div>
  );
}

/**
 * `components/notifications/NotificationQueueSummary.tsx` — ⭐ HO-NTF-002 §11.5, US-NTF-06, HO-NTF-001
 * OQ-1. Top of `/staff/notifications`: queued, retrying, sending, the age of the oldest queued
 * message and last-24-hour counts (the page refetches every 30 s). When SMS or email has no
 * provider connected (`channelsConfigured`), a warning banner says so in plain words —
 * `role="status"`, not an alert that interrupts: outside Development, SMS and email end
 * `Undeliverable`, and that has to look like a known state, not a bug.
 */
export function NotificationQueueSummary({
  summary,
  isLoading,
  isError,
  onRetry,
}: {
  summary: NotificationSummary | undefined;
  isLoading: boolean;
  isError: boolean;
  onRetry: () => void;
}) {
  if (isLoading) return <LoadingState variant="card" rows={3} label="Loading queue summary…" />;
  if (isError || !summary) {
    return (
      <ErrorState
        title="We couldn't load the queue summary."
        description="The message log below may still work — try again."
        onRetry={onRetry}
      />
    );
  }

  const warnings = [
    summary.channelsConfigured.sms ? null : SMS_NOT_CONNECTED,
    summary.channelsConfigured.email ? null : EMAIL_NOT_CONNECTED,
  ].filter((w): w is string => w !== null);

  return (
    <section aria-label="Queue summary" className="flex flex-col gap-3">
      {warnings.length > 0 && (
        <div
          role="status"
          className="flex items-start gap-3 rounded-[var(--radius-md)] border border-warning-ink bg-warning-bg p-4 text-warning-ink"
        >
          <AlertTriangle size={20} aria-hidden="true" className="mt-0.5 flex-none" />
          <div className="flex flex-col gap-2">
            {warnings.map((warning) => (
              <p key={warning} className="text-sm font-semibold">
                {warning}
              </p>
            ))}
          </div>
        </div>
      )}
      <dl className="m-0 grid grid-cols-2 gap-4 rounded-[var(--radius-md)] border border-border bg-surface p-4 sm:grid-cols-3 lg:grid-cols-5">
        <Stat label="Queued" value={summary.queued} />
        <Stat label="Retrying" value={summary.retryPending} />
        <Stat label="Sending" value={summary.sending} />
        <Stat
          label="Oldest queued"
          value={summary.oldestQueuedOn ? formatRelative(summary.oldestQueuedOn) : '—'}
        />
        <Stat label="Sent, last 24 hours" value={summary.last24h.sent} />
        <Stat label="Undeliverable, last 24 hours" value={summary.last24h.undeliverable} />
        <Stat label="Expired, last 24 hours" value={summary.last24h.expired} />
        <Stat label="Cancelled, last 24 hours" value={summary.last24h.cancelled} />
        <Stat label="Suppressed, last 24 hours" value={summary.last24h.suppressed} />
      </dl>
    </section>
  );
}
