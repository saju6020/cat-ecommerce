'use client';

import { useState } from 'react';
import Link from 'next/link';
import { Button } from '@/components/ui/button';
import { ConfirmDialog } from '@/components/common/ConfirmDialog';
import { channelName, channelTitle } from './statusText';
import { timeOnlyInZone } from '@/lib/format/time';
import { describeApiError, describeResendRefusal } from '@/lib/api/errors';
import type { NotificationDetail, ResendResult } from '@/lib/api/notifications-log';

/** What the confirm dialog names as the destination. The detail only carries the ORIGINAL message's
 *  destination, so a different channel is described by whose contact detail it will use. */
export function resendDestination(detail: NotificationDetail, channel: string): string {
  if (channel === detail.channel && detail.destination) return detail.destination;
  const possessive = `${detail.recipientName}'s`;
  if (channel === 'Email') return `${possessive} email address on file`;
  if (channel === 'Sms') return `${possessive} mobile number on file`;
  return `${possessive} account`;
}

/**
 * `components/notifications/ResendPanel.tsx` — ⭐ HO-NTF-002 §11.5, US-NTF-06 AC2. When the server
 * says `canResend`: a channel select from `resendableChannels` (defaulting to the original), and a
 * confirm that NAMES the channel and the destination (UI-3 — *"Send this message again by email to
 * a@b.com?"*). On `201` it says *Queued* or *Held for quiet hours until {time}* (branch time) with
 * a link to the new notification. When not: `cannotResendReason` as a sentence. `ResendLimitReached`
 * and the other refusals are mapped to plain sentences; the error code is never shown.
 */
export function ResendPanel({
  detail,
  timeZoneId,
  onResend,
}: {
  detail: NotificationDetail;
  timeZoneId: string;
  onResend: (channel: string) => Promise<ResendResult>;
}) {
  const options = detail.resendableChannels;
  const [channel, setChannel] = useState(
    options.includes(detail.channel) ? detail.channel : (options[0] ?? detail.channel),
  );
  const [confirmOpen, setConfirmOpen] = useState(false);
  const [result, setResult] = useState<ResendResult | null>(null);
  const [error, setError] = useState<string | null>(null);

  if (!detail.canResend || options.length === 0) {
    return (
      <section aria-label="Resend" className="flex flex-col gap-2">
        <h2 className="text-sm font-semibold text-ink">Resend</h2>
        <p data-testid="cannot-resend" className="text-sm text-ink-muted">
          {detail.cannotResendReason ?? "This message can't be resent."}
        </p>
      </section>
    );
  }

  async function confirm() {
    setError(null);
    try {
      setResult(await onResend(channel));
    } catch (caught) {
      setError(describeResendRefusal(caught) ?? describeApiError(caught));
    }
    setConfirmOpen(false);
  }

  return (
    <section aria-label="Resend" className="flex flex-col gap-3">
      <h2 className="text-sm font-semibold text-ink">Resend</h2>
      <div className="flex flex-wrap items-end gap-3">
        <div className="flex flex-col gap-1.5">
          <label htmlFor="resend-channel" className="text-xs font-semibold text-ink-muted">
            Resend by
          </label>
          <select
            id="resend-channel"
            value={channel}
            onChange={(event) => setChannel(event.target.value)}
            className="rounded-[var(--radius-sm)] border border-border-strong bg-surface px-3 py-2 text-sm text-ink"
          >
            {options.map((option) => (
              <option key={option} value={option}>
                {channelTitle(option)}
              </option>
            ))}
          </select>
        </div>
        <Button type="button" onClick={() => setConfirmOpen(true)}>
          Resend
        </Button>
      </div>

      {error && (
        <p role="alert" className="text-sm font-semibold text-danger-ink">
          {error}
        </p>
      )}
      {result && (
        <p role="status" className="text-sm font-semibold text-accent-ink">
          {result.notBefore
            ? `Held for quiet hours until ${timeOnlyInZone(result.notBefore, timeZoneId)}`
            : 'Queued'}
          .{' '}
          <Link
            href={`/staff/notifications/${result.notificationId}`}
            className="underline underline-offset-2"
          >
            View the new notification
          </Link>
        </p>
      )}

      <ConfirmDialog
        open={confirmOpen}
        onOpenChange={setConfirmOpen}
        title="Resend this message?"
        consequence={`Send this message again by ${channelName(channel)} to ${resendDestination(detail, channel)}?`}
        confirmLabel="Resend"
        pendingLabel="Sending…"
        onConfirm={confirm}
      />
    </section>
  );
}
