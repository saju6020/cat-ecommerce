import { http, type HttpHandler } from 'msw';
import type { FormularyMedicineInput, FormularySkuSearchResult } from '@/lib/api/formulary';
import { conflictViolation, noContent, notFound, ok, requireAction, violation } from './fixtures';
import { db, findMedicine, newId, persist, type MMedicine } from './store';
import { medicineDetail, medicineSummary } from './views';

/**
 * `mocks/handlers/pharmacy/formulary.ts` — `FormularyController` + `AdminFormularyController`
 * (HO-PHM-001 §12.4). The list has no `isStocked` filter and no strength object (only `strengthText`);
 * the SKU search lists clinic-only SKUs and says which medicine already owns one.
 */

/** SKUs that exist besides the ones the seed medicines already link. */
const EXTRA_SKUS = [
  {
    skuId: 'e7000000-0000-4000-8000-000000000101',
    productName: 'Meloxicam 1.5 mg/ml oral suspension',
    variantLabel: '30 ml bottle',
    barcode: '8901000000101',
    isClinicOnly: true,
  },
  {
    skuId: 'e7000000-0000-4000-8000-000000000102',
    productName: 'Tramadol 50 mg tablets',
    variantLabel: 'Strip of 10',
    barcode: '8901000000102',
    isClinicOnly: true,
  },
  {
    skuId: 'e7000000-0000-4000-8000-000000000103',
    productName: 'Clinic gloves',
    variantLabel: 'Box of 100',
    barcode: null,
    isClinicOnly: true,
  },
];

function skuUniverse(): FormularySkuSearchResult[] {
  const linked = db.medicines.flatMap((m) =>
    m.linkedSkus.map((s) => ({
      skuId: s.skuId,
      productName: s.productName,
      variantLabel: s.variantLabel,
      barcode: null,
      isClinicOnly: true,
      linkedMedicineId: m.id,
    })),
  );
  const extra = EXTRA_SKUS.map((s) => ({
    ...s,
    linkedMedicineId:
      db.medicines.find((m) => m.linkedSkus.some((l) => l.skuId === s.skuId))?.id ?? null,
  }));
  return [...linked, ...extra];
}

function applyInput(target: MMedicine, body: FormularyMedicineInput): void {
  target.name = body.name;
  target.ingredients = body.activeIngredients;
  target.classes = body.drugClasses;
  target.form = body.form;
  target.strengthAmount = body.strength.amount;
  target.strengthUnit = body.strength.unit;
  target.perQuantity = body.strength.perQuantity;
  target.perUnit = body.strength.perUnit;
  target.defaultRoute = body.defaultRoute;
  target.allowedRoutes = body.allowedRoutes;
  target.dosingBasis = body.dosingBasis as MMedicine['dosingBasis'];
  target.range = body.doseRange
    ? { min: body.doseRange.minMgPerKg, max: body.doseRange.maxMgPerKg }
    : null;
  target.contraindications = body.contraindications;
  target.dispensingUnit = body.dispensingUnit;
  target.interactions = body.interactions.map((i) => ({
    id: newId('e8'),
    medicineId: i.interactsWithMedicineId,
    term: i.interactsWithTerm,
    severity: i.severity as MMedicine['interactions'][number]['severity'],
    note: i.note,
  }));
  const universe = skuUniverse();
  target.linkedSkus = body.linkedSkuIds
    .map((id) => universe.find((s) => s.skuId === id))
    .filter((s): s is FormularySkuSearchResult => Boolean(s))
    .map((s) => ({ skuId: s.skuId, productName: s.productName, variantLabel: s.variantLabel }));
}

/** The seed's rule for a body the real validators would refuse (shape only). */
function shapeErrors(body: FormularyMedicineInput, selfId?: string) {
  const errors = [];
  if (body.dosingBasis === 'PerKg' && !body.doseRange)
    errors.push({
      propertyName: 'DoseRange',
      errorCode: 'DoseRangeRequired',
      errorMessage: 'A per-kg medicine needs a minimum and maximum dose per kg.',
    });
  if (body.doseRange && body.doseRange.minMgPerKg >= body.doseRange.maxMgPerKg)
    errors.push({
      propertyName: 'DoseRange',
      errorCode: 'InvalidDoseRange',
      errorMessage: 'The minimum must be lower than the maximum.',
    });
  for (const id of body.linkedSkuIds) {
    const owner = skuUniverse().find((s) => s.skuId === id)?.linkedMedicineId;
    if (owner && owner !== selfId)
      errors.push({
        propertyName: 'LinkedSkuIds',
        errorCode: 'SkuAlreadyLinkedToMedicine',
        errorMessage: 'One of these SKUs is already linked to another medicine.',
      });
  }
  return errors;
}

export const formularyHandlers: HttpHandler[] = [
  http.get('*/api/staff/formulary', ({ request }) => {
    const denied = requireAction(request, 'Formulary.View');
    if (denied) return denied;
    const url = new URL(request.url);
    const search = (url.searchParams.get('search') ?? '').toLowerCase();
    const controlled = url.searchParams.get('isControlled');
    const active = url.searchParams.get('isActive');
    const pageNumber = Number(url.searchParams.get('pageNumber') ?? 1);
    const pageSize = Number(url.searchParams.get('pageSize') ?? 20);
    const rows = db.medicines
      .filter(
        (m) =>
          !search || m.name.toLowerCase().includes(search) || m.code.toLowerCase().includes(search),
      )
      .filter((m) => controlled === null || String(m.isControlled) === controlled)
      .filter((m) => active === null || String(m.isActive) === active)
      .sort((a, b) => a.name.localeCompare(b.name));
    const start = (pageNumber - 1) * pageSize;
    return ok({
      items: rows.slice(start, start + pageSize).map(medicineSummary),
      pageNumber,
      pageSize,
      totalCount: rows.length,
    });
  }),

  http.get('*/api/staff/formulary/:medicineId', ({ request, params }) => {
    const denied = requireAction(request, 'Formulary.View');
    if (denied) return denied;
    const m = findMedicine(params.medicineId as string);
    return m ? ok(medicineDetail(m)) : notFound();
  }),

  http.get('*/api/admin/formulary/sku-search', ({ request }) => {
    const denied = requireAction(request, 'Formulary.Manage');
    if (denied) return denied;
    const term = (new URL(request.url).searchParams.get('term') ?? '').toLowerCase();
    return ok(
      skuUniverse().filter(
        (s) =>
          !term || s.productName.toLowerCase().includes(term) || (s.barcode ?? '').includes(term),
      ),
    );
  }),

  http.post('*/api/admin/formulary', async ({ request }) => {
    const denied = requireAction(request, 'Formulary.Manage');
    if (denied) return denied;
    const body = (await request.json()) as FormularyMedicineInput;
    if (db.medicines.some((m) => m.code === body.code))
      return violation(null, {
        propertyName: 'Code',
        errorCode: 'FormularyCodeAlreadyExists',
        errorMessage: 'A medicine with this code already exists.',
      });
    const errors = shapeErrors(body);
    if (errors.length > 0) return violation(null, ...errors);
    const created: MMedicine = {
      id: newId('e1'),
      code: body.code,
      name: body.name,
      ingredients: [],
      classes: [],
      form: body.form,
      strengthAmount: 0,
      strengthUnit: 'Mg',
      perQuantity: 1,
      perUnit: 'Tablet',
      defaultRoute: body.defaultRoute,
      allowedRoutes: [],
      dosingBasis: 'PerKg',
      range: null,
      contraindications: '',
      interactions: [],
      dispensingUnit: body.dispensingUnit,
      isControlled: false,
      schedule: null,
      isActive: true,
      linkedSkus: [],
      version: 1,
    };
    applyInput(created, body);
    db.medicines.push(created);
    persist();
    return ok({ medicineId: created.id }, 201);
  }),

  http.put('*/api/admin/formulary/:medicineId', async ({ request, params }) => {
    const denied = requireAction(request, 'Formulary.Manage');
    if (denied) return denied;
    const m = findMedicine(params.medicineId as string);
    if (!m) return notFound();
    const body = (await request.json()) as FormularyMedicineInput;
    if (body.expectedVersion !== m.version)
      return conflictViolation(null, {
        propertyName: 'ExpectedVersion',
        errorCode: 'StaleVersion',
        errorMessage: 'Someone else changed this medicine. Reload to see their changes.',
      });
    const errors = shapeErrors(body, m.id);
    if (errors.length > 0) return violation(null, ...errors);
    applyInput(m, body);
    m.version += 1;
    persist();
    return noContent();
  }),

  http.put('*/api/admin/formulary/:medicineId/controlled', async ({ request, params }) => {
    const denied = requireAction(request, 'Formulary.Manage');
    if (denied) return denied;
    const m = findMedicine(params.medicineId as string);
    if (!m) return notFound();
    const body = (await request.json()) as {
      isControlled: boolean;
      controlledSchedule?: string | null;
      reason?: string | null;
    };
    if (body.isControlled && !body.controlledSchedule?.trim())
      return violation(null, {
        propertyName: 'ControlledSchedule',
        errorCode: 'ControlledScheduleRequired',
        errorMessage: 'Enter the controlled schedule.',
      });
    // Clearing the flag on a medicine that already has register entries needs a reason.
    if (!body.isControlled && db.entries.some((e) => e.medicineId === m.id) && !body.reason?.trim())
      return violation(null, {
        propertyName: 'Reason',
        errorCode: 'ControlledFlagChangeReasonRequired',
        errorMessage: 'This medicine has register entries — say why it is no longer controlled.',
      });
    m.isControlled = body.isControlled;
    m.schedule = body.isControlled ? (body.controlledSchedule ?? null) : null;
    persist();
    return noContent();
  }),

  http.put('*/api/admin/formulary/:medicineId/active', async ({ request, params }) => {
    const denied = requireAction(request, 'Formulary.Manage');
    if (denied) return denied;
    const m = findMedicine(params.medicineId as string);
    if (!m) return notFound();
    m.isActive = ((await request.json()) as { isActive: boolean }).isActive;
    persist();
    return noContent();
  }),
];
