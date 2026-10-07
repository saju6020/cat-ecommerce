import { HttpResponse } from 'msw';
import { accountsFixtureIds, getStaffRoleIdsForRequest } from '../areas/accounts';
import { forbidden } from '../admin/fixtures';

export {
  validationError,
  notFound,
  noContent,
  loadPersisted,
  savePersisted,
  clearPersisted,
} from '../admin/fixtures';

/**
 * `mocks/handlers/notifications/fixtures.ts` — shared ids and helpers for every
 * `mocks/handlers/notifications/*.ts` file (HO-NTF-002 §12). Shapes are generated from
 * HO-NTF-001 §12.4 as verified against the real controllers and view models, never invented
 * (HO-NTF-002 §20.5).
 *
 * Role enforcement follows `mocks/handlers/admin/fixtures.ts`'s precedent: when the request
 * carries a staff session cookie that the accounts mock resolves, its roles are checked against
 * the presentation map of `ActionRoles.Notifications.cs`; a request with no session (a Vitest
 * request — undici has no cookie jar) is allowed through, and a test that needs a `403` overrides
 * the one endpoint with `server.use(...)`.
 */
export const notificationFixtureIds = {
  customerId: accountsFixtureIds.customerId,
  customerName: 'Sam Rahman',

  // Template keys (HO-NTF-001 §8.2's shipped catalogue + the test marketing template of §12).
  passwordResetKey: 'account.password-reset',
  vaccinationKey: 'care.vaccination-due',
  dewormingKey: 'care.deworming-due',
  marketingKey: 'marketing.test-newsletter',

  // Inbox items.
  inboxLinkedUnreadId: 'i0000000-0000-0000-0000-000000000001',
  inboxReadId: 'i0000000-0000-0000-0000-000000000002',
  inboxUnsafeLinkId: 'i0000000-0000-0000-0000-000000000003',
  inboxMarkupId: 'i0000000-0000-0000-0000-000000000004',

  // Log notifications.
  sentSmsId: 'n0000000-0000-0000-0000-000000000001',
  failedSmsId: 'n0000000-0000-0000-0000-000000000002',
  fallbackEmailId: 'n0000000-0000-0000-0000-000000000003',
  heldId: 'n0000000-0000-0000-0000-000000000004',
  retryId: 'n0000000-0000-0000-0000-000000000005',
  redactedResetId: 'n0000000-0000-0000-0000-000000000006',
  resendOriginalId: 'n0000000-0000-0000-0000-000000000007',
  resendCopyId: 'n0000000-0000-0000-0000-000000000008',
  expiredId: 'n0000000-0000-0000-0000-000000000009',
  cancelledId: 'n0000000-0000-0000-0000-00000000000a',
  sendingId: 'n0000000-0000-0000-0000-00000000000b',
  inboxDeliveredId: 'n0000000-0000-0000-0000-00000000000c',
  markupId: 'n0000000-0000-0000-0000-00000000000d',
  limitReachedId: 'n0000000-0000-0000-0000-00000000000e',

  // Unsubscribe tokens (the real ones are opaque; these are reserved trigger values).
  tokenEmailSubscribed: 'valid-email-token',
  tokenAlreadyUnsubscribed: 'already-unsubscribed-token',
  tokenSmsSubscribed: 'valid-sms-token',
  tokenInvalid: 'invalid-token',
  tokenRateLimited: 'rate-limited-token',
} as const;

/** `ActionRoles.Notifications.cs`, by name. */
const ACTION_ROLES = {
  'NotificationTemplates.View': ['Administrator', 'ShopManager'],
  'NotificationTemplates.Manage': ['Administrator'],
  'NotificationLog.View': ['Administrator', 'Reception'],
  'NotificationLog.Resend': ['Administrator', 'Reception'],
} as const;

/** `null` when allowed, otherwise the `403` the real `RequireActionFilter` sends. */
export function checkNotificationAction(
  request: Request,
  action: keyof typeof ACTION_ROLES,
): Response | null {
  const roles = getStaffRoleIdsForRequest(request);
  if (roles === null) return null; // no resolvable staff session — not modelled (see above)
  const permitted = ACTION_ROLES[action].some((role) => roles.includes(role));
  return permitted ? null : forbidden('ActionNotPermitted', 'You are not permitted to do this.');
}

export function pagedResponse<T>(all: T[], pageNumber: number, pageSize: number) {
  const start = (pageNumber - 1) * pageSize;
  return HttpResponse.json({
    items: all.slice(start, start + pageSize),
    pageNumber,
    pageSize,
    totalCount: all.length,
  });
}

export function minutesAgo(minutes: number): string {
  return new Date(Date.now() - minutes * 60_000).toISOString();
}

export function hoursAgo(hours: number): string {
  return minutesAgo(hours * 60);
}

/** The next 08:00 in `Asia/Dhaka` (UTC+6, no DST) as an instant — the quiet-hours release time. */
export function nextEightAmDhaka(): string {
  const now = new Date();
  const candidate = new Date(
    Date.UTC(now.getUTCFullYear(), now.getUTCMonth(), now.getUTCDate(), 2, 0, 0),
  ); // 08:00 +06:00 == 02:00Z
  if (candidate.getTime() <= now.getTime()) candidate.setUTCDate(candidate.getUTCDate() + 1);
  return candidate.toISOString();
}
