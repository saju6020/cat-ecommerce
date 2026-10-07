import { http, HttpResponse, type HttpHandler } from 'msw';
import { noContent, loadPersisted, savePersisted, clearPersisted } from './fixtures';

/** `mocks/handlers/admin/system.ts` — HO-ADM-002 §12, HO-ADM-001 §8.8/§12.4, ADR-053. */

const PERSIST_KEY = '__purrcare_admin_system_mock_db__';

interface Status {
  maintenanceMode: boolean;
  message: string | null;
}

function seed(): Status {
  return { maintenanceMode: false, message: null };
}

const status: Status = loadPersisted<Status>(PERSIST_KEY) ?? seed();

function persist(): void {
  savePersisted(PERSIST_KEY, status);
}

export function resetAdminSystemMockData(): void {
  status.maintenanceMode = false;
  status.message = null;
  clearPersisted(PERSIST_KEY);
}

export const adminSystemHandlers: HttpHandler[] = [
  http.get('*/api/system/status', () => HttpResponse.json(status)),

  http.put('*/api/admin/system/maintenance', async ({ request }) => {
    const body = (await request.json()) as { maintenanceMode: boolean; message?: string | null };
    status.maintenanceMode = body.maintenanceMode;
    status.message = body.message ?? null;
    persist();
    return noContent();
  }),
];
