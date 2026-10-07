import type { HttpHandler } from 'msw';
import { appointmentsCustomerHandlers } from './customer';
import { appointmentsStaffHandlers } from './staff';

export { appointmentsFixtureIds, RACE_LOCAL_TIME, MOCK_HOLD_SECONDS } from './fixtures';
export { resetAppointmentsMockData, specialDays } from './store';

/** HO-APT-002 §12 — every Appointments handler, generated from HO-APT-001 §12.4. */
export const appointmentsHandlers: HttpHandler[] = [
  ...appointmentsCustomerHandlers,
  ...appointmentsStaffHandlers,
];
