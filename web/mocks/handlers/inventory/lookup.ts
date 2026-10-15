import { http, HttpResponse, type HttpHandler } from 'msw';
import type { StockLocation, StorageProfile } from '@/lib/api/inventory-lookup';
import {
  ROLES,
  branchOf,
  guard,
  noContentResponse,
  omit,
  notFoundResponse,
  violation,
} from './fixtures';
import { db, newId, persist } from './store';

/** `mocks/handlers/inventory/lookup.ts` — SKU lookup, locations and storage profiles
 *  (`StaffSkuLookupController`, `StaffInventoryLocationsController`, `StaffStorageProfilesController`). */

export function locationsOf(branchId: string): StockLocation[] {
  return db.locations.filter((l) => l.branchId === branchId).map((l) => omit(l, 'branchId'));
}

export const lookupHandlers: HttpHandler[] = [
  http.get('*/api/staff/inventory/skus', ({ request }) => {
    const denied = guard(request, ROLES.stockView);
    if (denied) return denied;
    const url = new URL(request.url);
    const search = (url.searchParams.get('search') ?? '').trim().toLowerCase();
    const barcode = (url.searchParams.get('barcode') ?? '').trim();
    if (!search && !barcode) {
      return violation('search', 'InvalidQuery', 'Give a search text or a barcode.');
    }
    const found = barcode
      ? db.skus.filter((s) => s.barcode === barcode)
      : db.skus.filter(
          (s) =>
            s.displayName.toLowerCase().includes(search) ||
            s.skuCode.toLowerCase().includes(search),
        );
    return HttpResponse.json(found.slice(0, 20));
  }),

  http.get('*/api/staff/inventory/locations', ({ request }) => {
    const denied = guard(request, ROLES.stockView);
    if (denied) return denied;
    return HttpResponse.json(locationsOf(branchOf(request)));
  }),

  http.post('*/api/staff/inventory/locations', async ({ request }) => {
    const denied = guard(request, ROLES.locations);
    if (denied) return denied;
    const body = (await request.json()) as {
      code: string;
      name: string;
      kind: StockLocation['kind'];
    };
    const branchId = branchOf(request);
    if (
      db.locations.some(
        (l) => l.branchId === branchId && l.code.toLowerCase() === body.code.toLowerCase(),
      )
    ) {
      return violation(
        'Code',
        'StockLocationCodeAlreadyExists',
        'A location with this code already exists at this branch.',
      );
    }
    if (
      body.kind === 'Quarantine' &&
      db.locations.some((l) => l.branchId === branchId && l.kind === 'Quarantine' && l.isActive)
    ) {
      return violation(
        'Kind',
        'QuarantineLocationAlreadyExists',
        'This branch already has an active quarantine location.',
      );
    }
    const locationId = newId('e3');
    const movements = db.locations.find((l) => l.kind === body.kind)?.permittedMovements ?? [];
    db.locations.push({
      id: locationId,
      code: body.code,
      name: body.name,
      kind: body.kind,
      isActive: true,
      permittedMovements: movements,
      branchId,
    });
    persist();
    return HttpResponse.json({ locationId }, { status: 201 });
  }),

  http.put('*/api/staff/inventory/locations/:id', async ({ request, params }) => {
    const denied = guard(request, ROLES.locations);
    if (denied) return denied;
    const location = db.locations.find(
      (l) => l.id === params.id && l.branchId === branchOf(request),
    );
    if (!location) return notFoundResponse();
    location.name = ((await request.json()) as { name: string }).name;
    persist();
    return noContentResponse();
  }),

  http.put('*/api/staff/inventory/locations/:id/activation', async ({ request, params }) => {
    const denied = guard(request, ROLES.locations);
    if (denied) return denied;
    const location = db.locations.find(
      (l) => l.id === params.id && l.branchId === branchOf(request),
    );
    if (!location) return notFoundResponse();
    const { isActive } = (await request.json()) as { isActive: boolean };
    if (!isActive && db.positions.some((p) => p.locationId === location.id && p.onHand > 0)) {
      return violation(
        'LocationId',
        'StockLocationHasStock',
        'This location still holds stock. Move or write it off before deactivating it.',
      );
    }
    location.isActive = isActive;
    persist();
    return noContentResponse();
  }),

  http.get('*/api/staff/inventory/storage-profiles/:productId', ({ request, params }) => {
    const denied = guard(request, ROLES.stockView);
    if (denied) return denied;
    const profile = db.storageProfiles[String(params.productId)];
    return profile ? HttpResponse.json(profile) : notFoundResponse();
  }),

  http.put('*/api/staff/inventory/storage-profiles/:productId', async ({ request, params }) => {
    const denied = guard(request, ROLES.stockManage);
    if (denied) return denied;
    const productId = String(params.productId);
    const sku = db.skus.find((s) => s.productId === productId);
    if (!sku) return notFoundResponse();
    const body = (await request.json()) as {
      condition: StorageProfile['storage']['condition'];
      minTemperatureC: string | null;
      maxTemperatureC: string | null;
      handlingNotes: string | null;
    };
    const min =
      body.minTemperatureC === null || body.minTemperatureC === ''
        ? null
        : parseFloat(body.minTemperatureC);
    const max =
      body.maxTemperatureC === null || body.maxTemperatureC === ''
        ? null
        : parseFloat(body.maxTemperatureC);
    if (min !== null && max !== null && min >= max) {
      return violation(
        'MinTemperatureC',
        'InvalidTemperatureRange',
        'The minimum temperature must be lower than the maximum.',
      );
    }
    const existing = db.storageProfiles[productId];
    const storage = {
      condition: body.condition,
      minTemperatureC: min,
      maxTemperatureC: max,
      handlingNotes: body.handlingNotes,
    };
    db.storageProfiles[productId] = { productId, storage, version: (existing?.version ?? 0) + 1 };
    sku.storage = storage;
    persist();
    return noContentResponse();
  }),
];
