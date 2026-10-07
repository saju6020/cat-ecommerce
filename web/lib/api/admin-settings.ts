import { apiFetch } from './client';

/** `lib/api/admin-settings.ts` — HO-ADM-002 §8.2/§11.5. The business-settings registry
 *  (HO-ADM-001 §8.7/§10.2/§12.4). Rendered entirely from this payload — no hardcoded list. */

export type SettingType = 'Integer' | 'Decimal' | 'Boolean' | 'TimeRange' | 'CurrencyCode';

export interface SettingDefinitionView {
  key: string;
  group: string;
  type: SettingType;
  unit: string | null;
  min: number | null;
  max: number | null;
  default: string;
  branchOverridable: boolean;
  globalValue: string;
  branchValue: string | null;
  effectiveValue: string;
  /** Not named in HO-ADM-001 §12.4's own field list for this endpoint — optional so an absent
   *  value degrades gracefully to a formatted `key` instead of failing a strict shape check
   *  (§20.4: never invent a required field the contract doesn't promise). */
  displayName?: string;
  description?: string;
}

export async function getSettings(branchId?: string): Promise<SettingDefinitionView[]> {
  const query = branchId ? `?branchId=${encodeURIComponent(branchId)}` : '';
  return apiFetch<SettingDefinitionView[]>(`/api/admin/settings${query}`);
}

export interface SettingChange {
  key: string;
  value: string;
}

export async function changeSettings(changes: SettingChange[], branchId?: string): Promise<void> {
  const query = branchId ? `?branchId=${encodeURIComponent(branchId)}` : '';
  await apiFetch<void>(`/api/admin/settings${query}`, { method: 'PUT', body: { changes } });
}

export async function clearSettingOverride(key: string, branchId: string): Promise<void> {
  const query = `?branchId=${encodeURIComponent(branchId)}`;
  await apiFetch<void>(`/api/admin/settings/${encodeURIComponent(key)}${query}`, {
    method: 'DELETE',
  });
}

/** Falls back to a title-cased last segment of the dotted key (`booking.slotHoldMinutes` ->
 *  "Slot hold minutes") when the payload carries no `displayName` — a presentation nicety, never
 *  a substitute for a real label the server should eventually send. */
export function settingLabel(definition: SettingDefinitionView): string {
  if (definition.displayName) return definition.displayName;
  const lastSegment = definition.key.split('.').pop() ?? definition.key;
  const spaced = lastSegment.replace(/([a-z])([A-Z])/g, '$1 $2').toLowerCase();
  return spaced.charAt(0).toUpperCase() + spaced.slice(1);
}
