import { http, HttpResponse, type HttpHandler } from 'msw';
import {
  notificationFixtureIds as ids,
  checkNotificationAction,
  pagedResponse,
  minutesAgo,
  hoursAgo,
  nextEightAmDhaka,
  validationError,
  notFound,
  loadPersisted,
  savePersisted,
  clearPersisted,
} from './fixtures';

const PERSIST_KEY = '__purrcare_notifications_log_mock_db__';

/**
 * `mocks/handlers/notifications/log.ts` — HO-NTF-002 §12, HO-NTF-001 §12.4 (`AdminNotificationsController`).
 * Log pages covering every status, a fallback chain (undeliverable SMS -> email), a resend chain, a
 * held-for-quiet-hours row, a redacted password-reset message, a body with markup characters and a
 * provider response containing a `<script>`; summaries with both channels configured and with SMS
 * unconfigured.
 */

export interface MockAttempt {
  attemptedOn: string;
  outcome: string;
  providerName: string;
  providerMessageId: string | null;
  providerResponse: string | null;
}
export interface MockNotification {
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
  status: string;
  attemptCount: number;
  sentOn: string | null;
  deferredUntil: string | null;
  isResend: boolean;
  isFallback: boolean;
  subject: string | null;
  title: string | null;
  body: string;
  redactedReason: string | null;
  attempts: MockAttempt[];
  requestId: string;
  fallbackFromId: string | null;
  fallbacks: string[];
  resendOfId: string | null;
  resends: string[];
  canResend: boolean;
  cannotResendReason: string | null;
  resendableChannels: string[];
}

const CUSTOMER_MOBILE = '+8801799999999';
const CUSTOMER_EMAIL = 'signed-in-customer@example.com';

export function maskDestination(destination: string | null): string | null {
  if (!destination) return null;
  if (destination.includes('@')) {
    const [local, domain] = destination.split('@');
    return `${local![0]}•••@${domain}`;
  }
  return `${destination.slice(0, 6)}•••${destination.slice(-3)}`;
}

function base(
  id: string,
  over: Partial<MockNotification> & Pick<MockNotification, 'channel' | 'status'>,
): MockNotification {
  const isSms = over.channel === 'Sms';
  return {
    id,
    createdOn: hoursAgo(2),
    templateKey: ids.vaccinationKey,
    templateName: 'Vaccination due',
    category: 'Transactional',
    destination: isSms ? CUSTOMER_MOBILE : over.channel === 'InApp' ? null : CUSTOMER_EMAIL,
    recipientKind: 'Customer',
    recipientId: ids.customerId,
    recipientName: ids.customerName,
    attemptCount: 1,
    sentOn: null,
    deferredUntil: null,
    isResend: false,
    isFallback: false,
    subject: over.channel === 'Email' ? 'Momo is due a vaccination' : null,
    title: over.channel === 'InApp' ? 'Momo is due a vaccination' : null,
    body: "PurrCare: Momo's Feline Core vaccine is due on 12 Oct 2026. Book: https://purrcare.example/book",
    redactedReason: null,
    attempts: [],
    requestId: `r-${id}`,
    fallbackFromId: null,
    fallbacks: [],
    resendOfId: null,
    resends: [],
    canResend: true,
    cannotResendReason: null,
    resendableChannels: ['Sms', 'Email'],
    ...over,
  };
}

function attempt(
  minutes: number,
  outcome: string,
  providerResponse: string | null,
  id: string | null,
) {
  return {
    attemptedOn: minutesAgo(minutes),
    outcome,
    providerName: outcome === 'Rejected' ? 'Unconfigured' : 'HttpSmsGateway',
    providerMessageId: id,
    providerResponse,
  };
}

function seedNotifications(): MockNotification[] {
  return [
    base(ids.sentSmsId, {
      channel: 'Sms',
      status: 'Sent',
      createdOn: hoursAgo(1),
      sentOn: minutesAgo(58),
      attemptCount: 1,
      attempts: [attempt(58, 'Accepted', 'queued id=a1', 'msg-1001')],
    }),
    base(ids.failedSmsId, {
      channel: 'Sms',
      status: 'Undeliverable',
      createdOn: hoursAgo(4),
      attemptCount: 3,
      attempts: [
        attempt(235, 'Rejected', 'SMS is not connected to a provider.', null),
        attempt(225, 'Rejected', 'SMS is not connected to a provider.', null),
        attempt(205, 'Rejected', 'SMS is not connected to a provider.', null),
      ],
      fallbacks: [ids.fallbackEmailId],
      requestId: 'r-fallback',
    }),
    base(ids.fallbackEmailId, {
      channel: 'Email',
      status: 'Sent',
      createdOn: hoursAgo(3.9),
      sentOn: minutesAgo(233),
      isFallback: true,
      fallbackFromId: ids.failedSmsId,
      requestId: 'r-fallback',
      attempts: [attempt(233, 'Accepted', '250 OK', 'smtp-77')],
      resendableChannels: ['Sms', 'Email'],
    }),
    base(ids.heldId, {
      channel: 'Sms',
      status: 'Queued',
      templateKey: ids.dewormingKey,
      templateName: 'Deworming due',
      createdOn: minutesAgo(30),
      deferredUntil: nextEightAmDhaka(),
      attemptCount: 0,
      body: 'PurrCare: Momo is due Broad-spectrum dewormer on 12 Oct 2026. Book: https://purrcare.example/book',
    }),
    base(ids.retryId, {
      channel: 'Email',
      status: 'RetryPending',
      createdOn: minutesAgo(50),
      attemptCount: 1,
      attempts: [attempt(45, 'TemporaryFailure', '451 Temporary local problem', null)],
    }),
    base(ids.redactedResetId, {
      channel: 'Sms',
      status: 'Sent',
      templateKey: ids.passwordResetKey,
      templateName: 'Password reset',
      createdOn: hoursAgo(6),
      sentOn: minutesAgo(358),
      body: '[redacted]',
      subject: null,
      redactedReason: 'Secret',
      canResend: false,
      cannotResendReason:
        "This message contained a password-reset link and can't be resent. Ask the customer to request a new one.",
      resendableChannels: [],
      attempts: [attempt(358, 'Accepted', 'queued id=a2', 'msg-1002')],
    }),
    base(ids.resendOriginalId, {
      channel: 'Sms',
      status: 'Sent',
      createdOn: hoursAgo(8),
      sentOn: minutesAgo(478),
      resends: [ids.resendCopyId],
      requestId: 'r-resend',
      attempts: [attempt(478, 'Accepted', 'queued id=a3', 'msg-1003')],
    }),
    base(ids.resendCopyId, {
      channel: 'Email',
      status: 'Sent',
      createdOn: hoursAgo(7),
      sentOn: minutesAgo(418),
      isResend: true,
      resendOfId: ids.resendOriginalId,
      requestId: 'r-resend',
      attempts: [attempt(418, 'Accepted', '250 OK', 'smtp-78')],
    }),
    base(ids.expiredId, {
      channel: 'Sms',
      status: 'Expired',
      createdOn: hoursAgo(20),
      attemptCount: 0,
      body: 'PurrCare: this message was not delivered in time.',
    }),
    base(ids.cancelledId, {
      channel: 'Email',
      status: 'Cancelled',
      createdOn: hoursAgo(22),
      attemptCount: 0,
    }),
    base(ids.sendingId, {
      channel: 'Sms',
      status: 'Sending',
      createdOn: minutesAgo(2),
      attemptCount: 1,
      attempts: [],
    }),
    base(ids.inboxDeliveredId, {
      channel: 'InApp',
      status: 'InboxDelivered',
      createdOn: hoursAgo(1.5),
      attemptCount: 0,
      destination: null,
      canResend: false,
      cannotResendReason: 'This is the in-app copy. Resend the text or email message instead.',
      resendableChannels: [],
    }),
    base(ids.markupId, {
      channel: 'Email',
      status: 'Undeliverable',
      createdOn: hoursAgo(9),
      subject: '<b>Subject</b> & "quotes"',
      body: 'Hello <i>there</i>, **not bold** [not a link](https://evil.example)\n{{notAPlaceholder}}',
      attemptCount: 1,
      attempts: [
        attempt(530, 'Rejected', '550 <script>alert(1)</script> mailbox unavailable', null),
      ],
    }),
    base(ids.limitReachedId, {
      channel: 'Sms',
      status: 'Sent',
      createdOn: hoursAgo(12),
      sentOn: minutesAgo(718),
      resends: ['x1', 'x2', 'x3'].map((s) => `n0000000-0000-0000-0000-0000000000${s}`),
      attempts: [attempt(718, 'Accepted', 'queued id=a9', 'msg-1009')],
    }),
  ];
}

let notifications: MockNotification[] =
  loadPersisted<MockNotification[]>(PERSIST_KEY) ?? seedNotifications();

function persist() {
  savePersisted(PERSIST_KEY, notifications);
}

export type SummaryScenario = 'bothConfigured' | 'smsUnconfigured';
let summaryScenario: SummaryScenario = 'smsUnconfigured';

/** Test/e2e hook: choose which `channelsConfigured` the summary reports. */
export function setNotificationSummaryScenario(scenario: SummaryScenario): void {
  summaryScenario = scenario;
}

export function resetNotificationsLogMockData(): void {
  notifications = seedNotifications();
  summaryScenario = 'smsUnconfigured';
  clearPersisted(PERSIST_KEY);
}

function logItem(n: MockNotification) {
  return {
    id: n.id,
    createdOn: n.createdOn,
    templateKey: n.templateKey,
    templateName: n.templateName,
    category: n.category,
    channel: n.channel,
    destinationMasked: maskDestination(n.destination),
    recipientKind: n.recipientKind,
    recipientId: n.recipientId,
    recipientName: n.recipientName,
    status: n.status,
    attemptCount: n.attemptCount,
    sentOn: n.sentOn,
    lastProviderResponse: n.attempts.at(-1)?.providerResponse?.slice(0, 200) ?? null,
    deferredUntil: n.deferredUntil,
    isResend: n.isResend,
    isFallback: n.isFallback,
  };
}

function detail(n: MockNotification) {
  return {
    id: n.id,
    createdOn: n.createdOn,
    templateKey: n.templateKey,
    templateName: n.templateName,
    category: n.category,
    channel: n.channel,
    destination: n.destination,
    recipientKind: n.recipientKind,
    recipientId: n.recipientId,
    recipientName: n.recipientName,
    status: n.status,
    attemptCount: n.attemptCount,
    sentOn: n.sentOn,
    deferredUntil: n.deferredUntil,
    isResend: n.isResend,
    isFallback: n.isFallback,
    subject: n.subject,
    title: n.title,
    body: n.body,
    redactedReason: n.redactedReason,
    attempts: n.attempts,
    chain: {
      requestId: n.requestId,
      fallbackFromId: n.fallbackFromId,
      fallbacks: n.fallbacks,
      resendOfId: n.resendOfId,
      resends: n.resends,
    },
    canResend: n.canResend,
    cannotResendReason: n.cannotResendReason,
    resendableChannels: n.resendableChannels,
  };
}

function normaliseDestination(raw: string): string {
  const trimmed = raw.trim();
  if (trimmed.includes('@')) return trimmed.toLowerCase();
  const digits = trimmed.replace(/[^\d]/g, '');
  if (digits.startsWith('880')) return `+${digits}`;
  if (digits.startsWith('0')) return `+88${digits}`;
  return trimmed;
}

export const notificationsLogHandlers: HttpHandler[] = [
  // Declared before `/:id` so "summary" is never read as an id.
  http.get('*/api/admin/notifications/summary', ({ request }) => {
    const denied = checkNotificationAction(request, 'NotificationLog.View');
    if (denied) return denied;
    const queued = notifications.filter((n) => n.status === 'Queued');
    return HttpResponse.json({
      queued: queued.length,
      retryPending: notifications.filter((n) => n.status === 'RetryPending').length,
      sending: notifications.filter((n) => n.status === 'Sending').length,
      oldestQueuedOn: queued.map((n) => n.createdOn).sort()[0] ?? null,
      last24h: { sent: 5, undeliverable: 2, expired: 1, cancelled: 1, suppressed: 0 },
      channelsConfigured: {
        sms: summaryScenario === 'bothConfigured',
        email: true,
      },
    });
  }),

  http.get('*/api/admin/notifications', ({ request }) => {
    const denied = checkNotificationAction(request, 'NotificationLog.View');
    if (denied) return denied;
    const url = new URL(request.url);
    const from = url.searchParams.get('from');
    const to = url.searchParams.get('to');
    if (!from || !to) {
      return HttpResponse.json({ error: 'A date range (from, to) is required.' }, { status: 400 });
    }
    const days = (new Date(to).getTime() - new Date(from).getTime()) / 86_400_000;
    if (days > 93) {
      return HttpResponse.json({ error: 'The date range cannot exceed 93 days.' }, { status: 400 });
    }
    const recipientId = url.searchParams.get('recipientId');
    const destination = url.searchParams.get('destination');
    const channel = url.searchParams.get('channel');
    const templateKey = url.searchParams.get('templateKey');
    const status = url.searchParams.get('status');
    const pageNumber = Number(url.searchParams.get('pageNumber') ?? '1') || 1;
    const pageSize = Number(url.searchParams.get('pageSize') ?? '25') || 25;

    const normalised = destination ? normaliseDestination(destination) : null;
    const rows = notifications
      .filter((n) => n.createdOn >= from && n.createdOn <= to)
      .filter((n) => !recipientId || n.recipientId === recipientId)
      .filter((n) => !normalised || n.destination === normalised)
      .filter((n) => !channel || n.channel === channel)
      .filter((n) => !templateKey || n.templateKey === templateKey)
      .filter((n) => !status || n.status === status)
      .sort((a, b) => b.createdOn.localeCompare(a.createdOn))
      .map(logItem);
    return pagedResponse(rows, pageNumber, pageSize);
  }),

  http.get('*/api/admin/notifications/:id', ({ request, params }) => {
    const denied = checkNotificationAction(request, 'NotificationLog.View');
    if (denied) return denied;
    const found = notifications.find((n) => n.id === params.id);
    return found ? HttpResponse.json(detail(found)) : notFound();
  }),

  http.post('*/api/admin/notifications/:id/resend', async ({ request, params }) => {
    const denied = checkNotificationAction(request, 'NotificationLog.Resend');
    if (denied) return denied;
    const original = notifications.find((n) => n.id === params.id);
    if (!original) return notFound();
    const body = (await request.json().catch(() => ({}))) as { channel?: string };

    if (original.id === ids.limitReachedId) {
      return validationError(
        'NotificationId',
        'ResendLimitReached',
        'This notification has reached the resend limit.',
        'Notification',
      );
    }
    if (!original.canResend) {
      return validationError(
        'NotificationId',
        'NotificationCannotBeResent',
        original.cannotResendReason ?? "This message can't be resent.",
        'Notification',
      );
    }
    const channel = body.channel ?? original.channel;
    if (!original.resendableChannels.includes(channel)) {
      return validationError(
        'Channel',
        'ResendChannelNotAvailable',
        'That channel is not available for this message.',
        'Notification',
      );
    }

    const held = original.templateKey === ids.dewormingKey;
    const newId = crypto.randomUUID();
    const copy: MockNotification = {
      ...original,
      id: newId,
      channel,
      destination: channel === 'Sms' ? CUSTOMER_MOBILE : CUSTOMER_EMAIL,
      createdOn: new Date().toISOString(),
      status: 'Queued',
      attemptCount: 0,
      sentOn: null,
      deferredUntil: held ? nextEightAmDhaka() : null,
      isResend: true,
      isFallback: false,
      resendOfId: original.id,
      resends: [],
      fallbackFromId: null,
      fallbacks: [],
      attempts: [],
    };
    original.resends = [...original.resends, newId];
    notifications = [copy, ...notifications];
    persist();
    return HttpResponse.json(
      { notificationId: newId, status: 'Queued', notBefore: copy.deferredUntil },
      { status: 201 },
    );
  }),
];
