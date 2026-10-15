import { http, HttpResponse, type HttpHandler } from 'msw';
import { noContent, notFound, orderingIds as ids, violation, violations } from './fixtures';
import {
  db,
  money,
  persist,
  toCents,
  uuid,
  type DbBank,
  type DbCode,
  type DbCourier,
  type DbZone,
  type DbZoneService,
} from './store';

/**
 * `mocks/handlers/ordering/admin.ts` — HO-ORD-002 §12.1. Delivery zones, couriers, discount codes and
 * the bank-transfer account as `AdminDeliveryZonesController`, `AdminCouriersController`,
 * `AdminDiscountCodesController` and `AdminBankTransferAccountController` send and take them. Rules
 * the real aggregates enforce (unique names, tracking-link template, discount definition) are mirrored
 * with the real messages so a server rejection renders on its field.
 */
const ADMIN = '*/api/admin';

interface ZoneBody {
  branchId: string;
  name: string;
  priority: number;
  isActive: boolean;
  codEnabled: boolean;
  codMaxOrderValue: unknown;
  coverage: Array<{ district: string; postalCodes?: string[] | null }>;
  services: Array<{
    serviceLevel: 'Standard' | 'Express';
    isActive: boolean;
    baseCharge: unknown;
    freeOverOrderValue: unknown;
    leadTimeDays: number;
    weightBands?: Array<{ upToGrams: number | null; surcharge: unknown }> | null;
    slotWindows?: Array<{ dayOfWeek: string; start: string; end: string }> | null;
  }>;
}

function zoneSummary(z: DbZone) {
  return {
    id: z.id,
    branchId: z.branchId,
    name: z.name,
    priority: z.priority,
    isActive: z.isActive,
    codEnabled: z.codEnabled,
    coverageCount: z.coverage.length,
    serviceLevels: z.services.filter((s) => s.isActive).map((s) => s.serviceLevel),
  };
}

function zoneDetail(z: DbZone) {
  return {
    id: z.id,
    branchId: z.branchId,
    name: z.name,
    priority: z.priority,
    isActive: z.isActive,
    codEnabled: z.codEnabled,
    codMaxOrderValue: z.codMaxCents !== null ? money(z.codMaxCents) : null,
    currencyCode: 'BDT',
    version: z.version,
    coverage: z.coverage,
    services: z.services.map((s) => ({
      serviceLevel: s.serviceLevel,
      isActive: s.isActive,
      baseCharge: money(s.baseChargeCents),
      freeOverOrderValue: s.freeOverCents !== null ? money(s.freeOverCents) : null,
      leadTimeDays: s.leadTimeDays,
      weightBands: s.weightBands.map((b) => ({ upToGrams: b.upToGrams, surcharge: money(b.surchargeCents) })),
      slotWindows: s.slotWindows.map((w) => ({ ...w, label: `${w.start} to ${w.end}` })),
    })),
  };
}

function checkZone(body: ZoneBody, selfId?: string) {
  if (!body.name?.trim()) return violation('Name', 'InvalidDeliveryZone', 'Enter a name for the zone.');
  if (db.zones.some((z) => z.id !== selfId && z.name.toLowerCase() === body.name.trim().toLowerCase())) {
    return violation('Name', 'DeliveryZoneNameAlreadyExists', 'A delivery zone with this name already exists.');
  }
  if (!body.coverage?.length) return violation('Coverage', 'InvalidDeliveryZone', 'Cover at least one district.');
  if (!body.services?.length) return violation('Services', 'InvalidDeliveryZone', 'Offer at least one service level.');
  return null;
}

function applyZone(target: DbZone, body: ZoneBody): void {
  target.branchId = body.branchId;
  target.name = body.name.trim();
  target.priority = body.priority;
  target.isActive = body.isActive;
  target.codEnabled = body.codEnabled;
  target.codMaxCents = body.codMaxOrderValue === null || body.codMaxOrderValue === undefined ? null : toCents(body.codMaxOrderValue);
  target.coverage = body.coverage.map((c) => ({ district: c.district.trim(), postalCodes: c.postalCodes ?? [] }));
  target.services = body.services.map(
    (s): DbZoneService => ({
      serviceLevel: s.serviceLevel,
      isActive: s.isActive,
      baseChargeCents: toCents(s.baseCharge),
      freeOverCents: s.freeOverOrderValue === null || s.freeOverOrderValue === undefined ? null : toCents(s.freeOverOrderValue),
      leadTimeDays: s.leadTimeDays,
      weightBands: (s.weightBands ?? []).map((b) => ({ upToGrams: b.upToGrams, surchargeCents: toCents(b.surcharge) })),
      slotWindows: s.slotWindows ?? [],
    }),
  );
  target.version += 1;
}

interface CourierBody {
  name: string;
  trackingUrlTemplate?: string | null;
  supportsCod: boolean;
  isActive: boolean;
}

function checkCourier(body: CourierBody, selfId?: string) {
  if (!body.name?.trim()) return violation('Name', 'NotEmptyValidator', 'Enter the courier name.');
  if (db.couriers.some((c) => c.id !== selfId && c.name.toLowerCase() === body.name.trim().toLowerCase())) {
    return violation('Name', 'CourierNameAlreadyExists', 'A courier with this name already exists.');
  }
  const template = body.trackingUrlTemplate?.trim();
  if (template) {
    if (!template.startsWith('https://')) {
      return violation('TrackingUrlTemplate', 'InvalidCourier', 'The tracking link template must start with https://.');
    }
    if (template.split('{tracking}').length !== 2) {
      return violation('TrackingUrlTemplate', 'InvalidCourier', 'The tracking link template must contain {tracking} exactly once.');
    }
  }
  return null;
}

interface CodeBody {
  code?: string;
  description?: string | null;
  kind: 'Percentage' | 'FixedAmount';
  percent?: unknown;
  amount?: unknown;
  minimumSpend?: unknown;
  scope: 'Order' | 'Products' | 'Categories';
  productIds?: string[];
  categoryIds?: number[];
  firstOrderOnly: boolean;
  usageLimit?: number | null;
  perCustomerLimit?: number | null;
  validFrom?: string | null;
  validTo?: string | null;
  isActive: boolean;
}

function codeView(c: DbCode) {
  return {
    id: c.id,
    code: c.code,
    description: c.description,
    kind: c.kind,
    percent: c.percent,
    amount: c.amountCents !== null ? money(c.amountCents) : null,
    minimumSpend: c.minimumSpendCents !== null ? money(c.minimumSpendCents) : null,
    scope: c.scope,
    productIds: c.productIds,
    categoryIds: c.categoryIds,
    firstOrderOnly: c.firstOrderOnly,
    usageLimit: c.usageLimit,
    perCustomerLimit: c.perCustomerLimit,
    validFrom: c.validFrom,
    validTo: c.validTo,
    isActive: c.isActive,
    timesRedeemed: c.timesRedeemed,
    version: c.version,
  };
}

function checkCode(body: CodeBody) {
  const problems: Array<{ propertyName: string; errorCode: string; errorMessage: string }> = [];
  if (body.kind === 'Percentage') {
    const percent = Number(body.percent);
    if (!(percent > 0 && percent <= 100)) {
      problems.push({ propertyName: 'Percent', errorCode: 'InvalidDiscountDefinition', errorMessage: 'A percentage discount must be above 0 and at most 100.' });
    }
  } else if (!(toCents(body.amount) > 0)) {
    problems.push({ propertyName: 'Amount', errorCode: 'InvalidDiscountDefinition', errorMessage: 'A fixed discount must be above zero.' });
  }
  if (body.validFrom && body.validTo && Date.parse(body.validTo) < Date.parse(body.validFrom)) {
    problems.push({ propertyName: 'ValidTo', errorCode: 'InvalidDiscountDefinition', errorMessage: 'The end of the validity window must be after its start.' });
  }
  return problems;
}

function applyCode(target: DbCode, body: CodeBody): void {
  target.description = body.description ?? null;
  target.kind = body.kind;
  target.percent = body.kind === 'Percentage' ? Number(body.percent) : null;
  target.amountCents = body.kind === 'FixedAmount' ? toCents(body.amount) : null;
  target.minimumSpendCents = body.minimumSpend === null || body.minimumSpend === undefined ? null : toCents(body.minimumSpend);
  target.scope = body.scope;
  target.productIds = body.productIds ?? [];
  target.categoryIds = body.categoryIds ?? [];
  target.firstOrderOnly = body.firstOrderOnly;
  target.usageLimit = body.usageLimit ?? null;
  target.perCustomerLimit = body.perCustomerLimit ?? null;
  target.validFrom = body.validFrom ?? null;
  target.validTo = body.validTo ?? null;
  target.isActive = body.isActive;
  target.version += 1;
}

export const adminOrderingHandlers: HttpHandler[] = [
  // ---- delivery zones ------------------------------------------------------------------------------
  http.get(`${ADMIN}/delivery-zones/resolve`, ({ request }) => {
    const url = new URL(request.url);
    const district = (url.searchParams.get('district') ?? '').trim().toLowerCase();
    const postalCode = url.searchParams.get('postalCode')?.trim() || null;
    const zone = db.zones
      .filter((z) => z.isActive)
      .sort((a, b) => a.priority - b.priority)
      .find((z) =>
        z.coverage.some(
          (c) =>
            c.district.toLowerCase() === district &&
            (c.postalCodes.length === 0 || (postalCode !== null && c.postalCodes.includes(postalCode))),
        ),
      );
    return HttpResponse.json(
      zone
        ? { matched: true, zoneId: zone.id, zoneName: zone.name, branchId: zone.branchId, reasonCode: null }
        : { matched: false, zoneId: null, zoneName: null, branchId: null, reasonCode: 'AddressOutsideServedZones' },
    );
  }),
  http.get(`${ADMIN}/delivery-zones`, () => HttpResponse.json(db.zones.map(zoneSummary))),
  http.post(`${ADMIN}/delivery-zones`, async ({ request }) => {
    const body = (await request.json()) as ZoneBody;
    const refusal = checkZone(body);
    if (refusal) return refusal;
    const zone: DbZone = {
      id: uuid(),
      branchId: body.branchId,
      name: '',
      priority: 0,
      isActive: true,
      codEnabled: false,
      codMaxCents: null,
      version: 0,
      coverage: [],
      services: [],
    };
    applyZone(zone, body);
    db.zones.push(zone);
    persist();
    return HttpResponse.json({ zoneId: zone.id }, { status: 201 });
  }),
  http.get(`${ADMIN}/delivery-zones/:zoneId`, ({ params }) => {
    const zone = db.zones.find((z) => z.id === params.zoneId);
    return zone ? HttpResponse.json(zoneDetail(zone)) : notFound();
  }),
  http.put(`${ADMIN}/delivery-zones/:zoneId`, async ({ request, params }) => {
    const zone = db.zones.find((z) => z.id === params.zoneId);
    if (!zone) return notFound();
    const body = (await request.json()) as ZoneBody;
    const refusal = checkZone(body, zone.id);
    if (refusal) return refusal;
    applyZone(zone, body);
    persist();
    return noContent();
  }),
  http.put(`${ADMIN}/delivery-zones/:zoneId/activation`, async ({ request, params }) => {
    const zone = db.zones.find((z) => z.id === params.zoneId);
    if (!zone) return notFound();
    zone.isActive = ((await request.json()) as { isActive: boolean }).isActive;
    zone.version += 1;
    persist();
    return noContent();
  }),

  // ---- couriers ------------------------------------------------------------------------------------------
  http.get(`${ADMIN}/couriers`, () => HttpResponse.json(db.couriers)),
  http.post(`${ADMIN}/couriers`, async ({ request }) => {
    const body = (await request.json()) as CourierBody;
    const refusal = checkCourier(body);
    if (refusal) return refusal;
    const courier: DbCourier = {
      id: uuid(),
      name: body.name.trim(),
      trackingUrlTemplate: body.trackingUrlTemplate?.trim() || null,
      supportsCod: body.supportsCod,
      isActive: body.isActive,
    };
    db.couriers.push(courier);
    persist();
    return HttpResponse.json({ courierId: courier.id }, { status: 201 });
  }),
  http.put(`${ADMIN}/couriers/:courierId`, async ({ request, params }) => {
    const courier = db.couriers.find((c) => c.id === params.courierId);
    if (!courier) return notFound();
    const body = (await request.json()) as CourierBody;
    const refusal = checkCourier(body, courier.id);
    if (refusal) return refusal;
    courier.name = body.name.trim();
    courier.trackingUrlTemplate = body.trackingUrlTemplate?.trim() || null;
    courier.supportsCod = body.supportsCod;
    courier.isActive = body.isActive;
    persist();
    return noContent();
  }),

  // ---- discount codes ------------------------------------------------------------------------------------
  http.get(`${ADMIN}/discount-codes`, () => HttpResponse.json(db.codes.map(codeView))),
  http.post(`${ADMIN}/discount-codes`, async ({ request }) => {
    const body = (await request.json()) as CodeBody;
    const code = (body.code ?? '').trim().toUpperCase();
    if (!/^[A-Z0-9_-]{3,40}$/.test(code)) {
      return violation('Code', 'InvalidDiscountDefinition', 'A code is 3 to 40 letters, digits, dashes or underscores.');
    }
    if (db.codes.some((c) => c.code === code)) {
      return violation('Code', 'DiscountCodeAlreadyExists', 'A code with this name already exists.');
    }
    const problems = checkCode(body);
    if (problems.length > 0) return violations(problems);
    const created: DbCode = {
      id: uuid(),
      code,
      description: null,
      kind: body.kind,
      percent: null,
      amountCents: null,
      minimumSpendCents: null,
      scope: 'Order',
      productIds: [],
      categoryIds: [],
      firstOrderOnly: false,
      usageLimit: null,
      perCustomerLimit: null,
      validFrom: null,
      validTo: null,
      isActive: true,
      timesRedeemed: 0,
      version: 0,
    };
    applyCode(created, body);
    db.codes.push(created);
    persist();
    return HttpResponse.json({ discountCodeId: created.id }, { status: 201 });
  }),
  http.put(`${ADMIN}/discount-codes/:codeId/activation`, async ({ request, params }) => {
    const code = db.codes.find((c) => c.id === params.codeId);
    if (!code) return notFound();
    code.isActive = ((await request.json()) as { isActive: boolean }).isActive;
    code.version += 1;
    persist();
    return noContent();
  }),
  http.put(`${ADMIN}/discount-codes/:codeId`, async ({ request, params }) => {
    const code = db.codes.find((c) => c.id === params.codeId);
    if (!code) return notFound();
    const body = (await request.json()) as CodeBody;
    const problems = checkCode(body);
    if (problems.length > 0) return violations(problems);
    applyCode(code, body);
    persist();
    return noContent();
  }),

  // ---- bank-transfer account ---------------------------------------------------------------------------
  http.get(`${ADMIN}/bank-transfer-account`, () => HttpResponse.json(db.bank)),
  http.put(`${ADMIN}/bank-transfer-account`, async ({ request }) => {
    const body = (await request.json()) as Omit<DbBank, 'configured'>;
    const problems: Array<{ propertyName: string; errorCode: string; errorMessage: string }> = [];
    for (const [field, label] of [
      ['accountName', 'the account name'],
      ['accountNumber', 'the account number'],
      ['bankName', 'the bank'],
      ['branchName', 'the branch'],
    ] as const) {
      if (!String(body[field] ?? '').trim()) {
        problems.push({
          propertyName: field.charAt(0).toUpperCase() + field.slice(1),
          errorCode: 'InvalidBankTransferAccount',
          errorMessage: `Enter ${label}.`,
        });
      }
    }
    if (problems.length > 0) return violations(problems);
    Object.assign(db.bank, body, { configured: true });
    persist();
    return noContent();
  }),
];

export { ids };
