import type { HttpHandler } from 'msw';
import { dashboardHandlers, resetDashboardMock } from './dashboard';
import { setMockReportRoles } from './fixtures';
import { reportsHandlers } from './reports';
import { resetSchedulesMock, scheduleHandlers } from './schedules';

export { reportFixtureIds, setMockReportRoles } from './fixtures';
export { dashboardControl } from './dashboard';

/** `mocks/handlers/reports/index.ts` — HO-RPT-002 §12/§13. Every Reporting MSW handler. */
export const reportsAreaHandlers: HttpHandler[] = [
  ...dashboardHandlers,
  ...reportsHandlers,
  ...scheduleHandlers,
];

export function resetReportsMockData(): void {
  setMockReportRoles(null);
  resetDashboardMock();
  resetSchedulesMock();
}
