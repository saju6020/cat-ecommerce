import { apiFetch } from './client';
import type { PagedResult } from './types';

/**
 * `lib/api/notifications-log.ts` — HO-NTF-002 §8.2/§11.5, US-NTF-06. Shapes verified against
 * `AdminNotificationsController.cs` and `NotificationLogViewModels.cs`. Destinations are masked in
 * lists and full only on the detail view. `body` is `"[redacted]"` (with `redactedReason`) for a
 * secret-bearing message; every text field here is shown as plain text only (ADR-FE-022 -> 162).
 */
export type NotificationStatus =
  | 'Queued'
  | 'Sending'
  | 'Sent'
  | 'RetryPending'
  | 'Undeliverable'
  | 'Expired'
  | 'Cancelled'
  | 'InboxDelivered';

export const NOTIFICATION_STATUSES: readonly NotificationStatus[] = [
  'Queued',
  'Sending',
  'Sent',
  'RetryPending',
  'Undeliverable',
  'Expired',
  'Cancelled',
  'InboxDelivered',
];

export interface NotificationLogItem {
  id: string;
  createdOn: string;
  templateKey: string;
  templateName: string;
  category: string;
  channel: string;
  destinationMasked: string | null;
  recipientKind: string;
  recipientId: string;
  recipientName: string;
  status: NotificationStatus;
  attemptCount: number;
  sentOn: string | null;
  lastProviderResponse: string | null;
  deferredUntil: string | null;
  isResend: boolean;
  isFallback: boolean;
}

export interface NotificationAttempt {
  attemptedOn: string;
  outcome: string;
  providerName: string;
  providerMessageId: string | null;
  providerResponse: string | null;
}

export interface NotificationChain {
  requestId: string;
  fallbackFromId: string | null;
  fallbacks: string[];
  resendOfId: string | null;
  resends: string[];
}

export interface NotificationDetail {
  id: string;
  createdOn: string;
  templateKey: string;
  templateName: string;
  category: string;
  channel: string;
  destination: string | null;
  recipientKind: string;
  recipientId: string;
  recipientName: string;
  status: NotificationStatus;
  attemptCount: number;
  sentOn: string | null;
  deferredUntil: string | null;
  isResend: boolean;
  isFallback: boolean;
  subject: string | null;
  title: string | null;
  body: string;
  redactedReason: string | null;
  attempts: NotificationAttempt[];
  chain: NotificationChain;
  canResend: boolean;
  cannotResendReason: string | null;
  resendableChannels: string[];
}

export interface NotificationSummary {
  queued: number;
  retryPending: number;
  sending: number;
  oldestQueuedOn: string | null;
  last24h: {
    sent: number;
    undeliverable: number;
    expired: number;
    cancelled: number;
    suppressed: number;
  };
  channelsConfigured: { sms: boolean; email: boolean };
}

export interface NotificationLogParams {
  /** `yyyy-MM-dd` — required; widened to day-boundary instants before it is sent. */
  from: string;
  to: string;
  recipientId?: string;
  destination?: string;
  channel?: string;
  templateKey?: string;
  status?: string;
  pageNumber?: number;
  pageSize?: number;
}

export const NOTIFICATION_LOG_MAX_RANGE_DAYS = 93;
export const NOTIFICATION_LOG_DEFAULT_RANGE_DAYS = 7; // OQ-4
export const NOTIFICATION_LOG_PAGE_SIZE = 25;

/** `from`/`to` are calendar days in the UI; the API takes instants. `to` is the END of its day so a
 *  one-day range still covers the whole day. The server caps the span at 93 days; the UI blocks a
 *  longer range before submit (`isNotificationRangeTooLong`). */
function startOfDayIso(day: string): string {
  return new Date(`${day}T00:00:00.000Z`).toISOString();
}
function endOfDayIso(day: string): string {
  return new Date(`${day}T23:59:59.999Z`).toISOString();
}

/**
 * The default range (OQ-4): the last 7 calendar days, ending today. The API takes instants and the
 * day boundaries are UTC, so `to` is the LATER of the browser's local date and the UTC date — a
 * message created a moment ago is therefore always inside the default range, whatever zone the
 * browser is in.
 */
export function defaultNotificationRange(now: Date = new Date()): { from: string; to: string } {
  const pad = (n: number) => String(n).padStart(2, '0');
  const local = `${now.getFullYear()}-${pad(now.getMonth() + 1)}-${pad(now.getDate())}`;
  const utc = now.toISOString().slice(0, 10);
  const to = local > utc ? local : utc;
  const end = new Date(`${to}T00:00:00Z`);
  end.setUTCDate(end.getUTCDate() - (NOTIFICATION_LOG_DEFAULT_RANGE_DAYS - 1));
  return { from: end.toISOString().slice(0, 10), to };
}

/** Whole days from `from` to `to`, inclusive of both. */
export function notificationRangeDays(from: string, to: string): number {
  const span = new Date(`${to}T00:00:00Z`).getTime() - new Date(`${from}T00:00:00Z`).getTime();
  return Math.floor(span / 86_400_000) + 1;
}

export function isNotificationRangeTooLong(from: string, to: string): boolean {
  return notificationRangeDays(from, to) > NOTIFICATION_LOG_MAX_RANGE_DAYS;
}

export function buildNotificationLogQuery(params: NotificationLogParams): URLSearchParams {
  const query = new URLSearchParams();
  query.set('from', startOfDayIso(params.from));
  query.set('to', endOfDayIso(params.to));
  if (params.recipientId) query.set('recipientId', params.recipientId);
  if (params.destination) query.set('destination', params.destination);
  if (params.channel) query.set('channel', params.channel);
  if (params.templateKey) query.set('templateKey', params.templateKey);
  if (params.status) query.set('status', params.status);
  if (params.pageNumber) query.set('pageNumber', String(params.pageNumber));
  query.set('pageSize', String(params.pageSize ?? NOTIFICATION_LOG_PAGE_SIZE));
  return query;
}

export async function getNotificationLog(
  params: NotificationLogParams,
): Promise<PagedResult<NotificationLogItem>> {
  return apiFetch<PagedResult<NotificationLogItem>>(
    `/api/admin/notifications?${buildNotificationLogQuery(params).toString()}`,
  );
}

export async function getNotificationDetail(id: string): Promise<NotificationDetail> {
  return apiFetch<NotificationDetail>(`/api/admin/notifications/${encodeURIComponent(id)}`);
}

export async function getNotificationSummary(): Promise<NotificationSummary> {
  return apiFetch<NotificationSummary>('/api/admin/notifications/summary');
}

export interface ResendResult {
  notificationId: string;
  status: NotificationStatus;
  /** Set when the new notification is held for quiet hours. */
  notBefore: string | null;
}

export async function resendNotification(id: string, channel?: string): Promise<ResendResult> {
  return apiFetch<ResendResult>(`/api/admin/notifications/${encodeURIComponent(id)}/resend`, {
    method: 'POST',
    body: channel ? { channel } : {},
  });
}

/** Staff roles `NotificationLog.View` / `.Resend` are granted to (`ActionRoles.Notifications.cs`)
 *  — presentation only; the server still decides every request. */
export const NOTIFICATION_LOG_VIEW_ROLES = ['Administrator', 'Reception'] as const;
