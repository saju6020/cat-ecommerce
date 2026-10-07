import { http, HttpResponse, type HttpHandler } from 'msw';
import { notificationFixtureIds, validationError } from './fixtures';
import { revokeMarketingConsent } from '../areas/accounts';

/** `mocks/handlers/notifications/unsubscribe.ts` — HO-NTF-002 §12, HO-NTF-001 §12.4/§8.11. Public;
 *  no session. Reserved trigger tokens stand in for the real opaque ones: 200 true (and the seeded
 *  customer's marketing preference for that channel is turned off, so E-D1 can see it in the
 *  matrix), 200 false, 400 `InvalidUnsubscribeLink` for anything else, and 429. */
export const notificationsUnsubscribeHandlers: HttpHandler[] = [
  http.post('*/api/notifications/unsubscribe', async ({ request }) => {
    const body = (await request.json().catch(() => ({}))) as { token?: string };
    const token = body.token ?? '';

    if (token === notificationFixtureIds.tokenRateLimited) {
      return HttpResponse.json(
        {
          errors: [
            {
              propertyName: '',
              errorCode: 'TooManyRequests',
              errorMessage: 'Too many requests.',
              resourceName: 'Notification',
            },
          ],
          isValid: false,
        },
        { status: 429, headers: { 'Retry-After': '60' } },
      );
    }
    if (token === notificationFixtureIds.tokenAlreadyUnsubscribed) {
      return HttpResponse.json({ channel: 'Email', wasSubscribed: false });
    }
    if (token === notificationFixtureIds.tokenEmailSubscribed) {
      revokeMarketingConsent(notificationFixtureIds.customerId, 'Email');
      return HttpResponse.json({ channel: 'Email', wasSubscribed: true });
    }
    if (token === notificationFixtureIds.tokenSmsSubscribed) {
      revokeMarketingConsent(notificationFixtureIds.customerId, 'Sms');
      return HttpResponse.json({ channel: 'Sms', wasSubscribed: true });
    }
    return validationError(
      'Token',
      'InvalidUnsubscribeLink',
      'This unsubscribe link is not valid.',
      'Notification',
    );
  }),
];
