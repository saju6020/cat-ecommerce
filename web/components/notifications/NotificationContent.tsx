import type { NotificationDetail } from '@/lib/api/notifications-log';

export const REDACTED_BODY = '[redacted]';

/** *"it contained a password-reset link"* — the reason a stored message body was removed. */
export function redactionReason(reason: string | null): string {
  switch (reason) {
    case 'Secret':
      return 'it contained a password-reset link';
    case 'Anonymisation':
      return "the customer's account was anonymised";
    default:
      return 'it is no longer kept';
  }
}

export function isRedacted(detail: Pick<NotificationDetail, 'body' | 'redactedReason'>): boolean {
  return detail.body === REDACTED_BODY || detail.redactedReason !== null;
}

/**
 * `components/notifications/NotificationContent.tsx` — HO-NTF-002 §8.4, §11.5, ADR-FE-162. The
 * message as it was sent: subject, title and body as **plain text** (`whitespace-pre-wrap`, React
 * text children — never markup, never HTML, even for an email), or, when the server redacted it,
 * *"Content removed — {reason}"*. An email's HTML is never rendered in the log.
 */
export function NotificationContent({ detail }: { detail: NotificationDetail }) {
  if (isRedacted(detail)) {
    return (
      <p className="rounded-[var(--radius-sm)] bg-surface-sunken p-3 text-sm font-semibold text-ink">
        Content removed — {redactionReason(detail.redactedReason)}
      </p>
    );
  }
  return (
    <div className="flex flex-col gap-3">
      {detail.subject && (
        <div>
          <h3 className="text-xs font-semibold text-ink-muted">Subject</h3>
          <p className="whitespace-pre-wrap break-words text-sm text-ink">{detail.subject}</p>
        </div>
      )}
      {detail.title && (
        <div>
          <h3 className="text-xs font-semibold text-ink-muted">Title</h3>
          <p className="whitespace-pre-wrap break-words text-sm text-ink">{detail.title}</p>
        </div>
      )}
      <div>
        <h3 className="text-xs font-semibold text-ink-muted">Message</h3>
        <p
          data-testid="notification-body"
          className="whitespace-pre-wrap break-words rounded-[var(--radius-sm)] bg-surface-sunken p-3 text-sm text-ink"
        >
          {detail.body}
        </p>
      </div>
    </div>
  );
}
