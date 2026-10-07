import { http, HttpResponse, type HttpHandler } from 'msw';
import {
  administrationFixtureIds,
  validationError,
  noContent,
  loadPersisted,
  savePersisted,
  clearPersisted,
} from './fixtures';

const PERSIST_KEY = '__purrcare_admin_settings_mock_db__';

/** `mocks/handlers/admin/settings.ts` — HO-ADM-002 §12, HO-ADM-001 §10.2/§12.4. The full settings
 *  registry, with one branch override (§12's own fixture requirement). */

interface Definition {
  key: string;
  group: string;
  type: 'Integer' | 'Decimal' | 'Boolean' | 'TimeRange' | 'CurrencyCode';
  unit: string | null;
  min: number | null;
  max: number | null;
  default: string;
  branchOverridable: boolean;
}

const REGISTRY: Definition[] = [
  {
    key: 'booking.changeCutoffHours',
    group: 'Booking',
    type: 'Integer',
    unit: 'hours',
    min: 0,
    max: 72,
    default: '4',
    branchOverridable: true,
  },
  {
    key: 'booking.slotHoldMinutes',
    group: 'Booking',
    type: 'Integer',
    unit: 'minutes',
    min: 2,
    max: 30,
    default: '10',
    branchOverridable: true,
  },
  {
    key: 'booking.waitlistClaimMinutes',
    group: 'Booking',
    type: 'Integer',
    unit: 'minutes',
    min: 5,
    max: 240,
    default: '30',
    branchOverridable: true,
  },
  {
    key: 'boarding.depositPercent',
    group: 'Boarding',
    type: 'Decimal',
    unit: '%',
    min: 0,
    max: 100,
    default: '30.00',
    branchOverridable: true,
  },
  {
    key: 'inventory.expiryAlertDays',
    group: 'Inventory',
    type: 'Integer',
    unit: 'days',
    min: 1,
    max: 365,
    default: '60',
    branchOverridable: true,
  },
  {
    key: 'inventory.defaultReorderPoint',
    group: 'Inventory',
    type: 'Integer',
    unit: null,
    min: 0,
    max: 100_000,
    default: '10',
    branchOverridable: true,
  },
  {
    key: 'clinical.vaccinationDueSoonDays',
    group: 'Clinical',
    type: 'Integer',
    unit: 'days',
    min: 1,
    max: 180,
    default: '30',
    branchOverridable: false,
  },
  {
    key: 'clinical.defaultParasiteIntervalDays',
    group: 'Clinical',
    type: 'Integer',
    unit: 'days',
    min: 7,
    max: 365,
    default: '90',
    branchOverridable: false,
  },
  {
    key: 'notifications.quietHours',
    group: 'Notifications',
    type: 'TimeRange',
    unit: null,
    min: null,
    max: null,
    default: '21:00-08:00',
    branchOverridable: true,
  },
  {
    key: 'security.staffIdleTimeoutMinutes',
    group: 'Security',
    type: 'Integer',
    unit: 'minutes',
    min: 5,
    max: 240,
    default: '30',
    branchOverridable: false,
  },
  {
    key: 'commerce.currencyCode',
    group: 'Commerce',
    type: 'CurrencyCode',
    unit: null,
    min: null,
    max: null,
    default: 'BDT',
    branchOverridable: false,
  },
];

type ScopeId = 'global' | string; // string = branchId

function seed(): Map<string, Map<string, string>> {
  const global = new Map(REGISTRY.map((d) => [d.key, d.default]));
  const branchOverrides = new Map<string, string>([['booking.slotHoldMinutes', '15']]);
  return new Map<ScopeId, Map<string, string>>([
    ['global', global],
    [administrationFixtureIds.mainBranchId, branchOverrides],
  ]);
}

const persisted = loadPersisted<Array<[ScopeId, Array<[string, string]>]>>(PERSIST_KEY);
let scopes: Map<ScopeId, Map<string, string>> = persisted
  ? new Map(persisted.map(([scopeId, entries]) => [scopeId, new Map(entries)]))
  : seed();

function persist(): void {
  savePersisted(
    PERSIST_KEY,
    [...scopes.entries()].map(([scopeId, values]) => [scopeId, [...values.entries()]]),
  );
}

export function resetAdminSettingsMockData(): void {
  scopes = seed();
  clearPersisted(PERSIST_KEY);
}

function globalValues(): Map<string, string> {
  return scopes.get('global')!;
}

export const adminSettingsHandlers: HttpHandler[] = [
  http.get('*/api/admin/settings', ({ request }) => {
    const url = new URL(request.url);
    const branchId = url.searchParams.get('branchId');
    const branchValues = branchId ? scopes.get(branchId) : undefined;
    const view = REGISTRY.map((d) => {
      const global = globalValues().get(d.key) ?? d.default;
      const branchValue =
        branchId && d.branchOverridable ? (branchValues?.get(d.key) ?? null) : null;
      return {
        ...d,
        globalValue: global,
        branchValue,
        effectiveValue: branchValue ?? global,
      };
    });
    return HttpResponse.json(view);
  }),

  http.put('*/api/admin/settings', async ({ request }) => {
    const url = new URL(request.url);
    const branchId = url.searchParams.get('branchId');
    const body = (await request.json()) as { changes: Array<{ key: string; value: string }> };
    const target = branchId ? (scopes.get(branchId) ?? new Map<string, string>()) : globalValues();

    for (const change of body.changes) {
      const definition = REGISTRY.find((d) => d.key === change.key);
      if (!definition)
        return validationError(
          `Changes[${body.changes.indexOf(change)}].Key`,
          'UnknownSetting',
          'Unknown setting key.',
        );
      if (branchId && !definition.branchOverridable) {
        return validationError(
          `Changes[${body.changes.indexOf(change)}].Value`,
          'SettingNotBranchOverridable',
          'This setting cannot be overridden per branch.',
        );
      }
      if (
        (definition.type === 'Integer' || definition.type === 'Decimal') &&
        Number.isNaN(Number(change.value))
      ) {
        return validationError(
          `Changes[${body.changes.indexOf(change)}].Value`,
          'InvalidSettingValue',
          'Enter a valid number.',
        );
      }
      if (definition.min !== null && Number(change.value) < definition.min) {
        return validationError(
          `Changes[${body.changes.indexOf(change)}].Value`,
          'InvalidSettingValue',
          `Must be at least ${definition.min}.`,
        );
      }
      if (definition.max !== null && Number(change.value) > definition.max) {
        return validationError(
          `Changes[${body.changes.indexOf(change)}].Value`,
          'InvalidSettingValue',
          `Must be at most ${definition.max}.`,
        );
      }
    }
    for (const change of body.changes) target.set(change.key, change.value);
    if (branchId) scopes.set(branchId, target);
    persist();
    return noContent();
  }),

  http.delete('*/api/admin/settings/:key', ({ params, request }) => {
    const url = new URL(request.url);
    const branchId = url.searchParams.get('branchId');
    if (branchId) scopes.get(branchId)?.delete(params.key as string);
    persist();
    return noContent();
  }),
];
