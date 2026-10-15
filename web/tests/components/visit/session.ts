import { http, HttpResponse } from 'msw';
import { server } from '../../../mocks/server';

/** A staff session with these roles, for components that read `useStaffSession()` (convenience gating). */
export function signedInAs(roleIds: string[]): void {
  server.use(
    http.get('*/api/staff/session', () =>
      HttpResponse.json({
        staffAccountId: 's0000000-0000-0000-0000-000000000009',
        fullName: 'Test Staff',
        roleIds,
        issuedOn: new Date().toISOString(),
        lastActivityOn: new Date().toISOString(),
        idleExpiresOn: new Date(Date.now() + 30 * 60_000).toISOString(),
        absoluteExpiresOn: new Date(Date.now() + 8 * 3_600_000).toISOString(),
        idleTimeoutMinutes: 30,
        serverTime: new Date().toISOString(),
      }),
    ),
  );
}
