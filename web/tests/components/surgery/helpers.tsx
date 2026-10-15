import type * as React from 'react';
import { QueryClientProvider } from '@tanstack/react-query';
import { http, HttpResponse } from 'msw';
import { getSurgery } from '@/lib/api/surgeries';
import { server } from '@/mocks/server';
import { resetSurgeryMockData, setSurgeryCallerRoles } from '@/mocks/handlers/surgeries';
import { resetNavigation } from '../common/pharmacyTestUtils';

export {
  renderWithClient,
  navigation,
  navigationMock,
  resetNavigation,
} from '../common/pharmacyTestUtils';

/** A clean mock database, roles back to the default (Vet) and an empty navigation, before each test. */
export function resetSurgeryTest(): void {
  window.sessionStorage.clear();
  window.localStorage.clear();
  resetSurgeryMockData();
  resetNavigation();
  setSurgeryCallerRoles(null);
}

const NAMES: Record<string, string> = {
  Vet: 'Dr Akter',
  Nurse: 'Nurse Tanvir',
  Reception: 'Reception Desk',
  Administrator: 'Ada Administrator',
};

/**
 * Signs the test in as `roles`: the surgery mock answers as them AND the staff session the components
 * read (`useStaffRoles`, `useStaffSession`) says the same. Presentation and server agree, as in life.
 */
export function signInAs(roles: string[]): void {
  setSurgeryCallerRoles(roles);
  server.use(
    http.get('*/api/staff/session', () =>
      HttpResponse.json({
        staffAccountId: 'a2000000-0000-0000-0000-000000000002',
        fullName: NAMES[roles[0] ?? 'Vet'] ?? 'Staff member',
        roleIds: roles,
        issuedOn: new Date().toISOString(),
        lastActivityOn: new Date().toISOString(),
        idleExpiresOn: new Date(Date.now() + 3_600_000).toISOString(),
        absoluteExpiresOn: new Date(Date.now() + 36_000_000).toISOString(),
        idleTimeoutMinutes: 30,
        serverTime: new Date().toISOString(),
      }),
    ),
  );
}

/** The surgery exactly as the (mock) server reports it to a caller with these roles. */
export async function surgeryAs(id: string, roles: string[] = ['Vet']) {
  signInAs(roles);
  return getSurgery(id);
}

/** `rerender` with the same QueryClient — what a poll that lands new data looks like to a component. */
export function rerenderWithClient(
  result: ReturnType<typeof import('../common/pharmacyTestUtils').renderWithClient>,
  ui: React.ReactElement,
): void {
  result.rerender(<QueryClientProvider client={result.queryClient}>{ui}</QueryClientProvider>);
}
