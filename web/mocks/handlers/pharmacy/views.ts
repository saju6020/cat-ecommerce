import type { FormularyMedicineDetail, FormularyMedicineSummary } from '@/lib/api/formulary';
import type {
  Dispensing,
  DispensePlan,
  MedicineLabel,
  PlanBatch,
  SuggestedLine,
} from '@/lib/api/pharmacy';
import type {
  PrescriptionAction,
  PrescriptionDetail,
  PrescriptionStatus,
  PrescriptionSummary,
  SafetyAssessment,
  SafetyWarning,
} from '@/lib/api/prescriptions';
import type { RegisterEntry } from '@/lib/api/controlled-drugs';
import type { MyPrescription } from '@/lib/api/my-prescriptions';
import { dateOffset } from '../inventory/fixtures';
import { pharmacyFixtureIds as ids, amountText, apiUrl, quantity } from './fixtures';
import {
  CLINIC,
  FREQUENCIES,
  db,
  findMedicine,
  localDay,
  today,
  type MBatch,
  type MDispensing,
  type MMedicine,
  type MPrescription,
  type MRegisterEntry,
} from './store';

/**
 * `mocks/handlers/pharmacy/views.ts` — the mock plays the SERVER: the rules the real API applies
 * (`PrescriptionActivityRules`, `PrescriptionActionRules`, the safety assessment, the FEFO plan) live
 * here so the app never has to. Nothing in this file is imported by app code.
 */

// ── names ───────────────────────────────────────────────────────────────────────────────────────
const CAT_NAMES: Record<string, string> = {
  [ids.catWhiskersId]: 'Whiskers',
  [ids.catTigerId]: 'Tiger',
  [ids.catMittensId]: 'Mittens',
  [ids.catMomoId]: 'Momo',
};
export function catNameOf(catId: string): string {
  return CAT_NAMES[catId] ?? 'Cat';
}
export function isHighRisk(catId: string): boolean {
  return catId === ids.catTigerId;
}
export function customerNameOf(catId: string): string {
  return catId === ids.catMittensId || catId === ids.catMomoId ? 'Sam Rahman' : 'Rahim Uddin';
}
/** Cats the signed-in portal customer owns. */
export const OWNED_CAT_IDS = new Set<string>([ids.catMittensId, ids.catMomoId]);

// ── dates ───────────────────────────────────────────────────────────────────────────────────────
function dayNumber(date: string): number {
  return Math.round(new Date(`${date}T00:00:00Z`).getTime() / 86_400_000);
}
function addDays(date: string, days: number): string {
  const d = new Date(`${date}T00:00:00Z`);
  d.setUTCDate(d.getUTCDate() + days);
  return d.toISOString().slice(0, 10);
}
function longDate(date: string): string {
  return new Date(`${date}T00:00:00Z`).toLocaleDateString('en-GB', {
    day: 'numeric',
    month: 'short',
    year: 'numeric',
    timeZone: 'UTC',
  });
}

// ── PrescriptionActivityRules / PrescriptionActionRules ────────────────────────────────────────
export function suppliesLeft(p: MPrescription): number {
  return 1 + p.repeatsAuthorised - p.suppliesDispensed;
}
export function courseEndsOn(p: MPrescription): string {
  return addDays(p.lastDispensedOn ?? p.startsOn, p.durationDays - 1);
}
export function remainingDays(p: MPrescription): number {
  return Math.max(0, dayNumber(courseEndsOn(p)) - dayNumber(today()) + 1);
}
export function effectiveStatus(p: MPrescription): PrescriptionStatus {
  return (p.status === 'Issued' || p.status === 'Dispensed') &&
    today() > p.validUntil &&
    suppliesLeft(p) > 0
    ? 'Expired'
    : p.status;
}
export function isActive(p: MPrescription): boolean {
  if (p.status === 'Cancelled') return false;
  if (courseEndsOn(p) >= today()) return true;
  return suppliesLeft(p) > 0 && p.validUntil >= today() && effectiveStatus(p) !== 'Expired';
}
/** Inside `ValidUntil` with supplies left, and not cancelled/completed. */
export function dispensableToday(p: MPrescription): boolean {
  const status = effectiveStatus(p);
  return (
    (status === 'Issued' || status === 'Dispensed') &&
    suppliesLeft(p) > 0 &&
    today() <= p.validUntil
  );
}

export function evaluateRepeat(p: MPrescription): {
  ok: boolean;
  code: string | null;
  reason: string | null;
} {
  const open = p.repeatRequests.some((r) => r.status === 'Requested');
  if (!p.isRepeatable)
    return { ok: false, code: 'NotRepeatable', reason: 'This prescription cannot be repeated.' };
  if (p.status === 'Cancelled')
    return {
      ok: false,
      code: 'InvalidPrescriptionTransition',
      reason: 'This prescription was cancelled by the vet.',
    };
  if (p.status === 'Issued')
    return {
      ok: false,
      code: 'FirstSupplyNotDispensed',
      reason: 'The first supply has not been dispensed yet.',
    };
  if (suppliesLeft(p) <= 0)
    return {
      ok: false,
      code: 'NoRepeatsRemaining',
      reason: 'There are no repeats left on this prescription.',
    };
  if (today() > p.validUntil)
    return {
      ok: false,
      code: 'PrescriptionValidityEnded',
      reason: `This prescription could be repeated until ${longDate(p.validUntil)}. A vet needs to authorise a new prescription -- please book a consultation or call the clinic.`,
    };
  if (open)
    return {
      ok: false,
      code: 'RepeatRequestAlreadyOpen',
      reason: 'A repeat has already been requested.',
    };
  return { ok: true, code: null, reason: null };
}

export function allowedActions(p: MPrescription, roles: string[]): PrescriptionAction[] {
  const status = effectiveStatus(p);
  const actions: PrescriptionAction[] = [];
  const isVet = roles.includes('Vet');
  const canDispense = ['Reception', 'Nurse', 'Vet', 'Administrator'].some((r) => roles.includes(r));
  if (canDispense && dispensableToday(p)) actions.push('Dispense');
  if (isVet && (status === 'Issued' || status === 'Dispensed')) actions.push('Cancel');
  if (isVet && (status === 'Dispensed' || status === 'Expired') && !p.renewedById)
    actions.push('Renew');
  if (isVet && p.repeatRequests.some((r) => r.status === 'Requested'))
    actions.push('DeclineRepeat');
  return actions;
}

// ── formatting the strength ─────────────────────────────────────────────────────────────────────
const UNIT_TEXT: Record<string, string> = {
  Mg: 'mg',
  Mcg: 'mcg',
  G: 'g',
  Iu: 'IU',
  Percent: '%',
  Millilitre: 'ml',
  Gram: 'g',
  Tablet: 'tablet',
  Capsule: 'capsule',
  Tube: 'tube',
};
const trim = (n: number) => String(Math.round(n * 10000) / 10000);
export function strengthText(m: MMedicine): string {
  return `${trim(m.strengthAmount)} ${UNIT_TEXT[m.strengthUnit] ?? m.strengthUnit} per ${trim(m.perQuantity)} ${UNIT_TEXT[m.perUnit] ?? m.perUnit}`;
}

// ── formulary views ─────────────────────────────────────────────────────────────────────────────
export function medicineSummary(m: MMedicine): FormularyMedicineSummary {
  return {
    id: m.id,
    code: m.code,
    name: m.name,
    form: m.form as FormularyMedicineSummary['form'],
    strengthText: strengthText(m),
    dosingBasis: m.dosingBasis,
    isControlled: m.isControlled,
    isActive: m.isActive,
    isStocked: m.linkedSkus.length > 0,
  };
}

export function medicineDetail(m: MMedicine): FormularyMedicineDetail {
  return {
    id: m.id,
    code: m.code,
    name: m.name,
    activeIngredients: m.ingredients,
    drugClasses: m.classes,
    form: m.form as FormularyMedicineDetail['form'],
    strength: {
      amount: quantity(m.strengthAmount, m.strengthUnit),
      per: quantity(m.perQuantity, m.perUnit),
      text: strengthText(m),
    },
    defaultRoute: m.defaultRoute as FormularyMedicineDetail['defaultRoute'],
    allowedRoutes: m.allowedRoutes as FormularyMedicineDetail['allowedRoutes'],
    dosingBasis: m.dosingBasis,
    doseRange: m.range
      ? { minMgPerKg: amountText(m.range.min), maxMgPerKg: amountText(m.range.max) }
      : null,
    contraindications: m.contraindications,
    interactions: m.interactions.map((i) => ({
      id: i.id,
      interactsWithMedicineId: i.medicineId,
      interactsWithMedicineName: i.medicineId ? (findMedicine(i.medicineId)?.name ?? null) : null,
      interactsWithTerm: i.term,
      severity: i.severity,
      note: i.note,
    })),
    dispensingUnit: m.dispensingUnit,
    isControlled: m.isControlled,
    controlledSchedule: m.schedule,
    isActive: m.isActive,
    linkedSkus: m.linkedSkus,
    version: m.version,
  };
}

// ── prescription views ──────────────────────────────────────────────────────────────────────────
export function prescriptionSummary(p: MPrescription): PrescriptionSummary {
  const m = findMedicine(p.medicineId)!;
  return {
    id: p.id,
    prescriptionNumber: p.number,
    medicine: {
      name: m.name,
      strength: strengthText(m),
      form: m.form,
      isControlled: m.isControlled,
    },
    dose: quantity(p.doseAmount, p.doseUnit),
    route: p.route,
    frequency: FREQUENCIES[p.frequency] ?? p.frequency,
    durationDays: p.durationDays,
    quantity: quantity(p.quantity, m.dispensingUnit),
    status: effectiveStatus(p),
    isActive: isActive(p),
    courseEndsOn: courseEndsOn(p),
    remainingDays: remainingDays(p),
    suppliesDispensed: p.suppliesDispensed,
    suppliesAuthorised: 1 + p.repeatsAuthorised,
    validUntil: p.validUntil,
    prescriberName: p.prescriberName,
    issuedOn: p.issuedOn,
    hasAcknowledgedWarnings: p.hasAcknowledgedWarnings,
  };
}

function expectedRangeFor(m: MMedicine, kg: number | null) {
  if (!m.range) return null;
  return {
    minMgPerKg: amountText(m.range.min),
    maxMgPerKg: amountText(m.range.max),
    minMg: kg === null ? null : quantity(m.range.min * kg, 'Mg'),
    maxMg: kg === null ? null : quantity(m.range.max * kg, 'Mg'),
  };
}

export function prescriptionDetail(p: MPrescription, roles: string[]): PrescriptionDetail {
  const m = findMedicine(p.medicineId)!;
  const dispensings = db.dispensings.filter((d) => d.prescriptionId === p.id);
  const from = p.renewedFromId ? db.prescriptions.find((x) => x.id === p.renewedFromId) : undefined;
  const by = p.renewedById ? db.prescriptions.find((x) => x.id === p.renewedById) : undefined;
  return {
    ...prescriptionSummary(p),
    catId: p.catId,
    catName: catNameOf(p.catId),
    instructions: p.instructions,
    visitId: p.visitId,
    isRemoteRenewal: p.isRemoteRenewal,
    startsOn: p.startsOn,
    isRepeatable: p.isRepeatable,
    safetyRecord: {
      weightUsed: p.weightKg === null ? null : quantity(p.weightKg, 'Kg'),
      weightRecordedOn: p.weightRecordedOn,
      doseMgPerKg: p.doseMgPerKg,
      expectedRange: expectedRangeFor(m, p.weightKg),
      doseCheckOutcome:
        p.doseCheckOutcome as PrescriptionDetail['safetyRecord']['doseCheckOutcome'],
      acknowledgedByName: p.acknowledgedByName,
      acknowledgedOn: p.acknowledgedOn,
      allergyAcknowledgementReason: p.allergyReason,
      doseAcknowledgementNote: p.doseNote,
    },
    warnings: p.warnings,
    dispensings: dispensings.map((d) => ({
      id: d.id,
      supplyNumber: d.supplyNumber,
      isRepeat: d.isRepeat,
      dispensedOn: localDay(d.dispensedOn),
      dispensedByName: d.dispensedByName,
      status: d.status,
      lines: d.lines.map((l) => ({
        lineId: l.lineId,
        lotNumber: l.lotNumber,
        expiresOn: l.expiresOn,
        quantity: quantity(l.quantity, m.dispensingUnit),
      })),
    })),
    repeatRequests: p.repeatRequests,
    renewedFrom: from ? { id: from.id, prescriptionNumber: from.number } : null,
    renewedBy: by ? { id: by.id, prescriptionNumber: by.number } : null,
    allowedActions: allowedActions(p, roles),
  };
}

export function myPrescription(p: MPrescription): MyPrescription {
  const m = findMedicine(p.medicineId)!;
  const eligibility = evaluateRepeat(p);
  const open = p.repeatRequests.find((r) => r.status === 'Requested') ?? null;
  return {
    id: p.id,
    catId: p.catId,
    catName: catNameOf(p.catId),
    medicine: {
      name: m.name,
      strength: strengthText(m),
      form: m.form,
      isControlled: m.isControlled,
    },
    dose: quantity(p.doseAmount, p.doseUnit),
    route: p.route,
    frequency: FREQUENCIES[p.frequency] ?? p.frequency,
    remainingDays: remainingDays(p),
    courseEndsOn: courseEndsOn(p),
    issuingVetName: p.prescriberName,
    issuedOn: p.issuedOn,
    status: effectiveStatus(p),
    isActive: isActive(p),
    repeat: {
      isRepeatable: p.isRepeatable,
      suppliesLeft: Math.max(0, suppliesLeft(p)),
      validUntil: p.validUntil,
      canRequest: eligibility.ok,
      cannotRequestReason: eligibility.reason,
      cannotRequestCode: eligibility.code,
      openRequest: open,
    },
  };
}

// ── safety assessment (the SERVER's job) ────────────────────────────────────────────────────────
const MOCK_WEIGHT_MAX_AGE_DAYS = 45;

export interface CatFacts {
  allergies: string[];
  latestKg: number | null;
  latestOn: string | null;
}

/** Looks the cat up through the cat mock's own endpoints (a mock handler's look-up `fetch`). */
export async function catFacts(catId: string): Promise<CatFacts> {
  const facts: CatFacts = { allergies: [], latestKg: null, latestOn: null };
  try {
    const cat = await fetch(apiUrl(`/api/cats/${catId}`), { credentials: 'include' });
    if (cat.ok) {
      const body = (await cat.json()) as { allergies?: Array<{ allergen: string }> };
      facts.allergies = (body.allergies ?? []).map((a) => a.allergen);
    }
    const weights = await fetch(apiUrl(`/api/cats/${catId}/weights?pageNumber=1&pageSize=50`), {
      credentials: 'include',
    });
    if (weights.ok) {
      const body = (await weights.json()) as {
        items: Array<{ recordedOn: string; kilograms: number }>;
      };
      const last = [...body.items].sort((a, b) => a.recordedOn.localeCompare(b.recordedOn)).at(-1);
      if (last) {
        facts.latestKg = last.kilograms;
        facts.latestOn = last.recordedOn.slice(0, 10);
      }
    }
  } catch {
    /* an unreachable cat mock leaves the facts empty: weight Missing, no allergies */
  }
  return facts;
}

const MG_PER_UNIT = (m: MMedicine): number =>
  m.strengthUnit === 'Mg' ? m.strengthAmount / m.perQuantity : 0;

export function mgForDose(m: MMedicine, amount: number, unit: string): number | null {
  if (unit === 'Mg') return amount;
  if (unit === 'Mcg') return amount / 1000;
  if (unit === 'G') return amount * 1000;
  const perUnit = MG_PER_UNIT(m);
  if (perUnit === 0) return null;
  // Ml / Tablet / Capsule: the dose is in the medicine's own dispensing unit.
  return amount * perUnit;
}

function activeOthers(catId: string, excludeId?: string): MPrescription[] {
  return db.prescriptions.filter((p) => p.catId === catId && p.id !== excludeId && isActive(p));
}

function fingerprintOf(parts: unknown): string {
  const text = JSON.stringify(parts);
  let hash = 5381;
  for (let i = 0; i < text.length; i += 1) hash = ((hash << 5) + hash + text.charCodeAt(i)) >>> 0;
  return `fp-${hash.toString(16)}`;
}

export async function assess(
  catId: string,
  m: MMedicine,
  doseAmount: number,
  doseUnit: string,
  excludeRx?: string,
): Promise<SafetyAssessment> {
  const facts = await catFacts(catId);
  const name = catNameOf(catId);
  let requirement = 'Missing';
  let latest: SafetyAssessment['weight']['latest'] = null;
  if (facts.latestKg !== null && facts.latestOn) {
    const age = dayNumber(today()) - dayNumber(facts.latestOn);
    requirement = age > MOCK_WEIGHT_MAX_AGE_DAYS ? 'Stale' : 'Satisfied';
    latest = { weight: quantity(facts.latestKg, 'Kg'), recordedOn: facts.latestOn, ageDays: age };
  }

  const warnings: SafetyWarning[] = [];
  const mg = mgForDose(m, doseAmount, doseUnit);
  let outcome: SafetyAssessment['doseCheck']['outcome'] = 'NotCheckable';
  let doseMgPerKg: string | null = null;
  let range: SafetyAssessment['doseCheck']['expectedRange'] = null;
  if (m.dosingBasis === 'PerKg' && m.range && mg !== null && facts.latestKg) {
    const perKg = mg / facts.latestKg;
    doseMgPerKg = amountText(perKg);
    range = expectedRangeFor(m, facts.latestKg);
    const rounded = Math.round(perKg * 10000) / 10000;
    outcome =
      rounded > m.range.max ? 'AboveRange' : rounded < m.range.min ? 'BelowRange' : 'WithinRange';
    const text = `${amountText(perKg)} mg/kg against ${amountText(m.range.min)}–${amountText(m.range.max)} mg/kg.`;
    if (outcome === 'AboveRange')
      warnings.push({
        kind: 'DoseAboveRange',
        blocking: false,
        requiresAcknowledgement: true,
        severity: null,
        subject: 'Dose above range',
        detail: text,
      });
    if (outcome === 'BelowRange')
      warnings.push({
        kind: 'DoseBelowRange',
        blocking: false,
        requiresAcknowledgement: true,
        severity: null,
        subject: 'Dose below range',
        detail: text,
      });
  } else {
    warnings.push({
      kind: 'DoseNotCheckable',
      blocking: false,
      requiresAcknowledgement: false,
      severity: null,
      subject: 'Dose could not be checked',
      detail: 'This dose could not be checked against a mg/kg range.',
    });
  }

  // Allergy matching — ingredients and classes against the recorded allergens.
  const terms = [...m.ingredients, ...m.classes].map((t) => t.toLowerCase());
  for (const allergen of facts.allergies) {
    if (
      terms.some((t) => allergen.toLowerCase().includes(t) || t.includes(allergen.toLowerCase()))
    ) {
      warnings.unshift({
        kind: 'AllergyConflict',
        blocking: true,
        requiresAcknowledgement: true,
        severity: null,
        subject: `Allergy — ${allergen}`,
        detail: `${name} has a recorded allergy to ${allergen}; ${m.name} contains or belongs to a matching ingredient or class.`,
      });
    }
  }

  // Interactions with the cat's active prescriptions.
  for (const other of activeOthers(catId, excludeRx)) {
    const otherMed = findMedicine(other.medicineId);
    if (!otherMed) continue;
    const row =
      m.interactions.find((i) => i.medicineId === otherMed.id) ??
      otherMed.interactions.find((i) => i.medicineId === m.id);
    if (!row) continue;
    warnings.push({
      kind: 'Interaction',
      blocking: false,
      requiresAcknowledgement: row.severity === 'Major' || row.severity === 'Contraindicated',
      severity: row.severity,
      subject: `Interaction — ${otherMed.name.split(' ')[0]}`,
      detail: `With ${other.number} (active). ${row.note}`,
    });
  }
  warnings.sort((a, b) => rank(a) - rank(b));

  const information: string[] = [];
  if (facts.allergies.length === 0) information.push('No allergies recorded.');
  if (m.contraindications) information.push(`Contraindications: ${m.contraindications}`);

  return {
    weight: { requirement, latest },
    doseCheck: {
      outcome,
      doseMg: mg === null ? null : quantity(mg, 'Mg'),
      doseMgPerKg,
      expectedRange: range,
    },
    warnings,
    information,
    contraindications: m.contraindications,
    hasRecordedAllergies: facts.allergies.length > 0,
    fingerprint: fingerprintOf({
      catId,
      m: m.id,
      w: warnings.map((w) => [w.kind, w.subject, w.detail]),
      r: requirement,
    }),
  };
}

function rank(w: SafetyWarning): number {
  if (w.kind === 'AllergyConflict') return 0;
  if (w.kind === 'Interaction')
    return w.severity === 'Major' || w.severity === 'Contraindicated' ? 1 : 3;
  return 2;
}

// ── the dispense plan ───────────────────────────────────────────────────────────────────────────
export function batchesFor(medicineId: string): MBatch[] {
  return db.batches.filter((b) => b.medicineId === medicineId);
}

function isExpired(b: MBatch): boolean {
  return b.expiresOn < today();
}

export function buildPlan(p: MPrescription): DispensePlan {
  const m = findMedicine(p.medicineId)!;
  const batches = batchesFor(m.id).sort((a, b) => a.expiresOn.localeCompare(b.expiresOn));
  const planBatches: PlanBatch[] = batches.map((b) => ({
    skuId: b.skuId,
    productName: b.productName,
    variantLabel: b.variantLabel,
    batchId: b.batchId,
    lotNumber: b.lotNumber,
    barcode: b.barcode,
    expiresOn: b.expiresOn,
    available: quantity(b.available, m.dispensingUnit),
    isExpired: isExpired(b),
    isDispensable: !isExpired(b) && !b.notDispensableReason && b.available > 0,
    notDispensableReason: b.notDispensableReason,
  }));
  // FEFO: soonest-expiring dispensable batch first; a supply is taken in full or not at all.
  const usable = batches.filter((b) => !isExpired(b) && !b.notDispensableReason && b.available > 0);
  const suggested: SuggestedLine[] = [];
  let need = p.quantity;
  for (const b of usable) {
    if (need <= 0) break;
    const take = Math.min(b.available, need);
    suggested.push({
      skuId: b.skuId,
      batchId: b.batchId,
      quantity: quantity(take, m.dispensingUnit),
    });
    need -= take;
  }
  const onlyExpired = usable.length === 0 && batches.some(isExpired);
  const shortfall = need > 0 ? quantity(need, m.dispensingUnit) : null;

  let blockingReason: string | null = null;
  const status = effectiveStatus(p);
  if (status === 'Cancelled') blockingReason = 'This prescription was cancelled by the vet.';
  else if (status === 'Completed' || suppliesLeft(p) <= 0)
    blockingReason = 'There are no supplies left on this prescription.';
  else if (today() > p.validUntil)
    blockingReason = `This prescription's validity ended on ${longDate(p.validUntil)}. A vet needs to authorise a new prescription.`;
  else if (onlyExpired) {
    const expired = batches.find(isExpired)!;
    blockingReason = `The only ${m.name} in the dispensary is expired (batch ${expired.lotNumber}, ${longDate(expired.expiresOn)}). It cannot be dispensed -- ask Inventory to write it off and restock.`;
  } else if (shortfall)
    blockingReason = `Only ${amountText(p.quantity - need)} is available in the dispensary, and a supply is dispensed in full or not at all.`;

  return {
    prescription: prescriptionSummary(p),
    cat: {
      id: p.catId,
      name: catNameOf(p.catId),
      isHighRisk: isHighRisk(p.catId),
      handlingNote: isHighRisk(p.catId)
        ? 'Bites when restrained. Two handlers and a towel wrap required.'
        : null,
    },
    quantity: quantity(p.quantity, m.dispensingUnit),
    isControlled: m.isControlled,
    witnessRoles: m.isControlled ? ['Vet', 'Nurse'] : [],
    canDispense: blockingReason === null,
    blockingReason,
    batches: planBatches,
    suggestedLines: onlyExpired ? [] : suggested,
    onlyExpiredStockAvailable: onlyExpired,
    shortfall,
  };
}

// ── label and dispensing view ───────────────────────────────────────────────────────────────────
const ROUTE_WORDS: Record<string, string> = {
  Oral: 'by mouth',
  Subcutaneous: 'under the skin',
  Intramuscular: 'into the muscle',
  Intravenous: 'into a vein',
  Topical: 'on the skin',
  Ophthalmic: 'in the eye',
  Otic: 'in the ear',
};

export function labelFor(p: MPrescription, d: MDispensing): MedicineLabel {
  const m = findMedicine(p.medicineId)!;
  const unitText = UNIT_TEXT[p.doseUnit] ?? p.doseUnit.toLowerCase();
  const dispensedOn = localDay(d.dispensedOn);
  const earliest = d.lines.map((l) => l.expiresOn).sort()[0] ?? null;
  return {
    catName: catNameOf(p.catId),
    ownerName: customerNameOf(p.catId),
    medicineLine: `${m.name} ${strengthText(m)}`,
    doseLine:
      `${amountText(p.doseAmount)} ${unitText} ${ROUTE_WORDS[p.route] ?? p.route.toLowerCase()} ${FREQUENCIES[p.frequency] ?? ''}`.trim(),
    instructions: p.instructions,
    dispensedOn,
    expiresOn: earliest,
    quantityLine:
      `${amountText(d.lines.reduce((s, l) => s + l.quantity, 0))} ${UNIT_TEXT[m.perUnit] ?? ''}`.trim(),
    prescriptionNumber: p.number,
    prescriberName: p.prescriberName,
    clinicName: CLINIC.name,
    clinicPhone: CLINIC.phone,
    clinicAddress: CLINIC.address,
    controlledNotice: m.isControlled
      ? `Controlled drug (${m.schedule ?? 'scheduled'}). Do not share. Return unused medicine to the clinic.`
      : null,
    standardWarnings:
      'Keep out of reach of children. For animal treatment only. Give as directed by your vet.',
  };
}

export function dispensingView(d: MDispensing, roles: string[]): Dispensing {
  const p = db.prescriptions.find((x) => x.id === d.prescriptionId)!;
  const m = findMedicine(p.medicineId)!;
  const actions: Dispensing['allowedActions'] = [];
  if (['Reception', 'Nurse', 'Vet', 'Administrator'].some((r) => roles.includes(r)))
    actions.push('Reprint');
  if (
    d.status === 'Dispensed' &&
    (roles.includes('Vet') || roles.includes('Administrator')) &&
    localDay(d.dispensedOn) === today()
  )
    actions.push('Reverse');
  return {
    id: d.id,
    prescriptionId: p.id,
    catId: p.catId,
    prescriptionNumber: p.number,
    supplyNumber: d.supplyNumber,
    isRepeat: d.isRepeat,
    status: d.status,
    dispensedOn: d.dispensedOn,
    dispensedOnLocal: localDay(d.dispensedOn),
    dispensedByName: d.dispensedByName,
    witnessName: d.witnessName,
    fefoOverrideReason: d.fefoOverrideReason,
    lines: d.lines.map((l) => ({
      lineId: l.lineId,
      skuId: l.skuId,
      batchId: l.batchId,
      lotNumber: l.lotNumber,
      expiresOn: l.expiresOn,
      quantity: quantity(l.quantity, m.dispensingUnit),
      isPriced: true,
    })),
    label: labelFor(p, d),
    labelPrintCount: d.labelPrintCount,
    reversedOn: d.reversedOn,
    reversalReason: d.reversalReason,
    allowedActions: actions,
  };
}

// ── register views ──────────────────────────────────────────────────────────────────────────────
function unitOf(medicineId: string): string {
  return findMedicine(medicineId)?.dispensingUnit ?? 'Millilitre';
}

export function registerView(e: MRegisterEntry): RegisterEntry {
  const m = findMedicine(e.medicineId)!;
  const sameStock = db.entries
    .filter(
      (x) =>
        x.medicineId === e.medicineId && x.batchId === e.batchId && x.entryNumber <= e.entryNumber,
    )
    .reduce((sum, x) => sum + x.delta, 0);
  return {
    id: e.id,
    entryNumber: e.entryNumber,
    recordedOn: e.recordedOn,
    occurredOn: e.occurredOn,
    kind: e.kind,
    medicine: {
      id: m.id,
      name: m.name,
      strengthText: strengthText(m),
      form: m.form,
      controlledSchedule: m.schedule,
    },
    batchId: e.batchId,
    lotNumber: e.lotNumber,
    batchExpiresOn: e.batchExpiresOn,
    quantityDelta: quantity(e.delta, unitOf(e.medicineId)),
    runningBalance: quantity(sameStock, unitOf(e.medicineId)),
    doseText: e.doseText,
    wastage: e.wastage === null ? null : quantity(e.wastage, unitOf(e.medicineId)),
    catName: e.catName,
    customerName: e.customerName,
    actorName: e.actorName,
    witnessName: e.witnessName,
    source: { type: e.sourceType, id: e.sourceId, label: e.sourceLabel },
    correctionReason: e.correctionReason,
    notes: e.notes,
    correctsEntryNumber: e.correctsEntryNumber,
    correctedByEntryNumbers: db.entries
      .filter((x) => x.correctsEntryNumber === e.entryNumber)
      .map((x) => x.entryNumber),
    isSealed: e.isSealed,
  };
}

export function chainPositionOf(e: MRegisterEntry): number {
  return e.entryNumber - 1197;
}

export function hashOf(e: MRegisterEntry): string {
  const seed = `${e.entryNumber}-${e.id}`;
  let out = '';
  for (let i = 0; out.length < 64; i += 1) {
    out += ((seed.charCodeAt(i % seed.length) * (i + 31) * 2654435761) >>> 0)
      .toString(16)
      .padStart(8, '0');
  }
  return out.slice(0, 64);
}

export function todayPlus(days: number): string {
  return dateOffset(days);
}
