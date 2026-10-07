import type { HttpHandler } from 'msw';
import { notificationsInboxHandlers, resetNotificationsInboxMockData } from './inbox';
import { notificationsUnsubscribeHandlers } from './unsubscribe';
import { notificationsTemplatesHandlers, resetNotificationsTemplatesMockData } from './templates';
import { notificationsLogHandlers, resetNotificationsLogMockData } from './log';

export { notificationFixtureIds } from './fixtures';
export { setMockInbox, type MockInboxItem } from './inbox';
export { setNotificationSummaryScenario } from './log';

/** `mocks/handlers/notifications/index.ts` — HO-NTF-002 §12/§13. Every notifications MSW handler. */
export const notificationsAreaHandlers: HttpHandler[] = [
  ...notificationsInboxHandlers,
  ...notificationsUnsubscribeHandlers,
  ...notificationsTemplatesHandlers,
  ...notificationsLogHandlers,
];

export function resetNotificationsMockData(): void {
  resetNotificationsInboxMockData();
  resetNotificationsTemplatesMockData();
  resetNotificationsLogMockData();
}
