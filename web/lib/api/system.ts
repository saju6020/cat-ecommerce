import { apiFetch } from './client';

/** `lib/api/system.ts` — HO-ADM-002 §8.2. Maintenance mode (HO-ADM-001 §8.8/§12.4, ADR-053). */

export interface SystemStatus {
  maintenanceMode: boolean;
  message: string | null;
}

export async function getSystemStatus(): Promise<SystemStatus> {
  return apiFetch<SystemStatus>('/api/system/status');
}

export interface SetMaintenanceModeRequest {
  maintenanceMode: boolean;
  message?: string | null;
}

export async function setMaintenanceMode(request: SetMaintenanceModeRequest): Promise<void> {
  await apiFetch<void>('/api/admin/system/maintenance', { method: 'PUT', body: request });
}
