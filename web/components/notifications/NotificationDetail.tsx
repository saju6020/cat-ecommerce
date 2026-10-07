'use client';

import Link from 'next/link';
import { AttemptsTable } from './AttemptsTable';
import { NotificationChainLinks } from './NotificationChainLinks';
import { NotificationContent } from './NotificationContent';
import { ResendPanel } from './ResendPanel';
import { channelTitle, describeStatus, formatLogTime } from './statusText';
import type {
  NotificationDetail as NotificationDetailModel,
  ResendResult,
} from '@/lib/api/notifications-log';

function Section({ title, children }: { title: string; children: React.ReactNode }) {
  return (
    <section className="flex flex-col gap-3 rounded-[var(--radius-md)] border border-border bg-surface p-4">
      <h2 className="text-sm font-semibold text-ink">{title}</h2>
      {children}
    </section>
  );
}

/**
 * `components/notifications/NotificationDetail.tsx` — ⭐ HO-NTF-002 §11.5, US-NTF-06. One
 * notification: full destination (staff-only view), recipient (a link to the customer page when the
 * recipient is a customer), template, category and urgency, the content as plain text or the
 * redaction notice, EVERY delivery attempt, the request's chain of fallbacks and resends, and the
 * resend panel.
 */
export function NotificationDetail({
  detail,
  timeZoneId,
  onResend,
}: {
  detail: NotificationDetailModel;
  timeZoneId: string;
  onResend: (channel: string) => Promise<ResendResult>;
}) {
  return (
    <div className="flex flex-col gap-4">
      <header className="flex flex-col gap-1">
        <h1 className="font-display text-xl font-semibold text-ink">{detail.templateName}</h1>
        <p className="text-sm font-semibold text-ink">
          {describeStatus(detail, timeZoneId)}
          {detail.isResend && ' · Resend'}
          {detail.isFallback && ' · Fallback'}
        </p>
      </header>

      <Section title="Details">
        <dl className="m-0 grid grid-cols-[auto_1fr] gap-x-4 gap-y-1.5 text-sm">
          <dt className="font-semibold text-ink">Created</dt>
          <dd className="m-0 text-ink-muted">{formatLogTime(detail.createdOn, timeZoneId)}</dd>
          <dt className="font-semibold text-ink">Channel</dt>
          <dd className="m-0 text-ink-muted">{channelTitle(detail.channel)}</dd>
          <dt className="font-semibold text-ink">Destination</dt>
          <dd className="m-0 break-all text-ink-muted">{detail.destination ?? '—'}</dd>
          <dt className="font-semibold text-ink">Recipient</dt>
          <dd className="m-0 text-ink-muted">
            {detail.recipientKind === 'Customer' ? (
              <Link
                href={`/staff/customers/${detail.recipientId}`}
                className="font-semibold text-accent-ink underline underline-offset-2"
              >
                {detail.recipientName}
              </Link>
            ) : (
              detail.recipientName
            )}
          </dd>
          <dt className="font-semibold text-ink">Template</dt>
          <dd className="m-0 break-all text-ink-muted">{detail.templateKey}</dd>
          <dt className="font-semibold text-ink">Category</dt>
          <dd className="m-0 text-ink-muted">{detail.category}</dd>
          <dt className="font-semibold text-ink">Sent</dt>
          <dd className="m-0 text-ink-muted">
            {detail.sentOn ? formatLogTime(detail.sentOn, timeZoneId) : 'Not sent'}
          </dd>
        </dl>
      </Section>

      <Section title="Content">
        <NotificationContent detail={detail} />
      </Section>
      <Section title={`Delivery attempts (${detail.attempts.length})`}>
        <AttemptsTable attempts={detail.attempts} timeZoneId={timeZoneId} />
      </Section>
      <Section title="Fallbacks and resends">
        <NotificationChainLinks chain={detail.chain} />
      </Section>
      <div className="rounded-[var(--radius-md)] border border-border bg-surface p-4">
        <ResendPanel detail={detail} timeZoneId={timeZoneId} onResend={onResend} />
      </div>
    </div>
  );
}
