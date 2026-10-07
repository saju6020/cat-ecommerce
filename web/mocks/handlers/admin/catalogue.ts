import { http, HttpResponse, type HttpHandler } from 'msw';
import {
  administrationFixtureIds,
  validationError,
  notFound,
  noContent,
  loadPersisted,
  savePersisted,
  clearPersisted,
} from './fixtures';

const PERSIST_KEY = '__purrcare_admin_catalogue_mock_db__';

/** `mocks/handlers/admin/catalogue.ts` — HO-ADM-002 §12, HO-ADM-001 §8.6/§12.4. Services with
 *  effective-dated prices, tax classes with effective-dated rates. §12's own fixture requirement:
 *  a priced service, an unpriced one, and one with a scheduled future price. */

interface Money {
  amount: string;
  currencyCode: string;
}
interface ServicePrice {
  id: string;
  serviceId: string;
  amount: Money;
  effectiveFrom: string;
  setBy: string;
}
interface ClinicService {
  id: string;
  code: string;
  name: string;
  description: string;
  category: string;
  durationMinutes: number;
  qualifiedRoleIds: string[];
  requiredRoomKind: string;
  taxClassId: string;
  isBookableOnline: boolean;
  isActive: boolean;
}
interface TaxClass {
  id: string;
  code: string;
  name: string;
  isActive: boolean;
}
interface TaxRate {
  id: string;
  taxClassId: string;
  ratePercent: string;
  effectiveFrom: string;
}

function seedServices(): Map<string, ClinicService> {
  const priced: ClinicService = {
    id: administrationFixtureIds.serviceConsultationId,
    code: 'CONSULT',
    name: 'General consultation',
    description: 'A routine health check.',
    category: 'Consultation',
    durationMinutes: 20,
    qualifiedRoleIds: ['Vet', 'Nurse'],
    requiredRoomKind: 'Consultation',
    taxClassId: administrationFixtureIds.taxClassStandardId,
    isBookableOnline: true,
    isActive: true,
  };
  const unpriced: ClinicService = {
    id: administrationFixtureIds.serviceUnpricedId,
    code: 'NEWSVC',
    name: 'New procedure (pricing pending)',
    description: 'Not yet priced.',
    category: 'Emergency',
    durationMinutes: 30,
    qualifiedRoleIds: ['Vet'],
    requiredRoomKind: 'Theatre',
    taxClassId: administrationFixtureIds.taxClassStandardId,
    isBookableOnline: false,
    isActive: true,
  };
  return new Map([
    [priced.id, priced],
    [unpriced.id, unpriced],
  ]);
}

function seedPrices(): Map<string, ServicePrice> {
  const current: ServicePrice = {
    id: crypto.randomUUID(),
    serviceId: administrationFixtureIds.serviceConsultationId,
    amount: { amount: '1500.00', currencyCode: 'BDT' },
    effectiveFrom: '2026-01-01T00:00:00Z',
    setBy: administrationFixtureIds.adminStaffId,
  };
  const scheduled: ServicePrice = {
    id: crypto.randomUUID(),
    serviceId: administrationFixtureIds.serviceConsultationId,
    amount: { amount: '1800.00', currencyCode: 'BDT' },
    effectiveFrom: '2028-06-01T00:00:00Z',
    setBy: administrationFixtureIds.adminStaffId,
  };
  return new Map([
    [current.id, current],
    [scheduled.id, scheduled],
  ]);
}

function seedTaxClasses(): Map<string, TaxClass> {
  const standard: TaxClass = {
    id: administrationFixtureIds.taxClassStandardId,
    code: 'STD',
    name: 'Standard',
    isActive: true,
  };
  return new Map([[standard.id, standard]]);
}

function seedTaxRates(): Map<string, TaxRate> {
  const current: TaxRate = {
    id: crypto.randomUUID(),
    taxClassId: administrationFixtureIds.taxClassStandardId,
    ratePercent: '15.00',
    effectiveFrom: '2026-01-01T00:00:00Z',
  };
  return new Map([[current.id, current]]);
}

interface PersistedShape {
  services: Array<[string, ClinicService]>;
  prices: Array<[string, ServicePrice]>;
  taxClasses: Array<[string, TaxClass]>;
  taxRates: Array<[string, TaxRate]>;
}
const persisted = loadPersisted<PersistedShape>(PERSIST_KEY);

let services = persisted ? new Map(persisted.services) : seedServices();
let prices = persisted ? new Map(persisted.prices) : seedPrices();
let taxClasses = persisted ? new Map(persisted.taxClasses) : seedTaxClasses();
let taxRates = persisted ? new Map(persisted.taxRates) : seedTaxRates();

function persist(): void {
  savePersisted(PERSIST_KEY, {
    services: [...services.entries()],
    prices: [...prices.entries()],
    taxClasses: [...taxClasses.entries()],
    taxRates: [...taxRates.entries()],
  });
}

export function resetAdminCatalogueMockData(): void {
  services = seedServices();
  prices = seedPrices();
  taxClasses = seedTaxClasses();
  taxRates = seedTaxRates();
  clearPersisted(PERSIST_KEY);
}

function currentPrice(serviceId: string): Money | null {
  const now = new Date().toISOString();
  const candidates = [...prices.values()].filter(
    (p) => p.serviceId === serviceId && p.effectiveFrom <= now,
  );
  candidates.sort((a, b) => b.effectiveFrom.localeCompare(a.effectiveFrom));
  return candidates[0]?.amount ?? null;
}

function serviceView(s: ClinicService) {
  const price = currentPrice(s.id);
  return { ...s, isPriced: price !== null, currentPrice: price };
}

export const adminCatalogueHandlers: HttpHandler[] = [
  http.get('*/api/services', () => {
    const list = [...services.values()]
      .filter((s) => s.isActive && s.isBookableOnline)
      .map((s) => ({
        id: s.id,
        name: s.name,
        description: s.description,
        category: s.category,
        durationMinutes: s.durationMinutes,
        isBookableOnline: s.isBookableOnline,
        currentPrice: currentPrice(s.id),
      }))
      .filter((s) => s.currentPrice !== null);
    return HttpResponse.json(list);
  }),

  http.get('*/api/admin/services', () =>
    HttpResponse.json([...services.values()].map(serviceView)),
  ),

  http.get('*/api/admin/services/:serviceId', ({ params }) => {
    const service = services.get(params.serviceId as string);
    if (!service) return notFound();
    return HttpResponse.json(serviceView(service));
  }),

  http.post('*/api/admin/services', async ({ request }) => {
    const body = (await request.json()) as Omit<ClinicService, 'id' | 'isActive'>;
    if (!body.code?.trim()) return validationError('Code', 'RequiredField', 'Enter a code.');
    if (body.qualifiedRoleIds.length === 0)
      return validationError(
        'QualifiedRoleIds',
        'ServiceRolesRequired',
        'Select at least one role.',
      );
    if ([...services.values()].some((s) => s.code === body.code))
      return validationError(
        'Code',
        'ServiceCodeAlreadyExists',
        'A service with this code already exists.',
      );
    const id = crypto.randomUUID();
    services.set(id, { ...body, id, isActive: true });
    persist();
    return HttpResponse.json({ serviceId: id }, { status: 201 });
  }),

  http.put('*/api/admin/services/:serviceId', async ({ request, params }) => {
    const service = services.get(params.serviceId as string);
    if (!service) return notFound();
    const body = (await request.json()) as Partial<ClinicService>;
    Object.assign(service, body);
    persist();
    return noContent();
  }),

  http.put('*/api/admin/services/:serviceId/activation', async ({ request, params }) => {
    const service = services.get(params.serviceId as string);
    if (!service) return notFound();
    const body = (await request.json()) as { isActive: boolean };
    service.isActive = body.isActive;
    persist();
    return noContent();
  }),

  http.get('*/api/admin/services/:serviceId/prices', ({ params }) => {
    const list = [...prices.values()].filter((p) => p.serviceId === params.serviceId);
    return HttpResponse.json(list);
  }),

  http.post('*/api/admin/services/:serviceId/prices', async ({ request, params }) => {
    const body = (await request.json()) as {
      amount: string;
      currencyCode: string;
      effectiveFrom: string;
    };
    if (new Date(body.effectiveFrom).getTime() < Date.now() - 60_000) {
      return validationError(
        'EffectiveFrom',
        'PriceCannotBeBackdated',
        'The effective date cannot be in the past.',
      );
    }
    const serviceId = params.serviceId as string;
    if (
      [...prices.values()].some(
        (p) => p.serviceId === serviceId && p.effectiveFrom === body.effectiveFrom,
      )
    ) {
      return validationError(
        'EffectiveFrom',
        'DuplicatePriceEffectiveDate',
        'A price already has this effective date.',
      );
    }
    const id = crypto.randomUUID();
    prices.set(id, {
      id,
      serviceId,
      amount: { amount: body.amount, currencyCode: body.currencyCode },
      effectiveFrom: body.effectiveFrom,
      setBy: administrationFixtureIds.adminStaffId,
    });
    persist();
    return HttpResponse.json({ servicePriceId: id }, { status: 201 });
  }),

  http.get('*/api/admin/tax-classes', () => HttpResponse.json([...taxClasses.values()])),

  http.get('*/api/admin/tax-classes/:taxClassId', ({ params }) => {
    const taxClass = taxClasses.get(params.taxClassId as string);
    if (!taxClass) return notFound();
    return HttpResponse.json(taxClass);
  }),

  http.post('*/api/admin/tax-classes', async ({ request }) => {
    const body = (await request.json()) as { code: string; name: string };
    if ([...taxClasses.values()].some((t) => t.code === body.code))
      return validationError(
        'Code',
        'RoomNameAlreadyExists',
        'A tax class with this code already exists.',
      );
    const id = crypto.randomUUID();
    taxClasses.set(id, { id, code: body.code, name: body.name, isActive: true });
    persist();
    return HttpResponse.json({ taxClassId: id }, { status: 201 });
  }),

  http.put('*/api/admin/tax-classes/:taxClassId', async ({ request, params }) => {
    const taxClass = taxClasses.get(params.taxClassId as string);
    if (!taxClass) return notFound();
    const body = (await request.json()) as { name: string; isActive: boolean };
    Object.assign(taxClass, body);
    persist();
    return noContent();
  }),

  http.get('*/api/admin/tax-classes/:taxClassId/rates', ({ params }) => {
    const list = [...taxRates.values()].filter((r) => r.taxClassId === params.taxClassId);
    return HttpResponse.json(list);
  }),

  http.post('*/api/admin/tax-classes/:taxClassId/rates', async ({ request, params }) => {
    const body = (await request.json()) as { ratePercent: string; effectiveFrom: string };
    if (new Date(body.effectiveFrom).getTime() < Date.now() - 60_000) {
      return validationError(
        'EffectiveFrom',
        'PriceCannotBeBackdated',
        'The effective date cannot be in the past.',
      );
    }
    const id = crypto.randomUUID();
    taxRates.set(id, {
      id,
      taxClassId: params.taxClassId as string,
      ratePercent: body.ratePercent,
      effectiveFrom: body.effectiveFrom,
    });
    persist();
    return HttpResponse.json({ taxRateId: id }, { status: 201 });
  }),
];
