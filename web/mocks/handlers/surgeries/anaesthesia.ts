import { http, type HttpHandler } from 'msw';
import type {
  CorrectEntryRequest,
  LogDrugRequest,
  LogMilestoneRequest,
  LogVitalsRequest,
} from '@/lib/api/anaesthesia-log';
import { db as pharmacyDb, findMedicine } from '../pharmacy/store';
import {
  actorFor,
  bad,
  branchOf,
  callerRoles,
  conflict,
  notFound,
  ok,
  requireAction,
  surgeryFixtureIds as ids,
  violation,
  zoneOfBranch,
  type MockViolation,
} from './fixtures';
import {
  emptyValues,
  findSurgery,
  getDb,
  newId,
  persist,
  STAFF,
  staffName,
  type MLogEntry,
  type MSurgery,
} from './store';
import { latestWeight } from './views';

/**
 * `mocks/handlers/surgeries/anaesthesia.ts` — `StaffSurgeryAnaesthesiaController`. There is no `PUT`,
 * `PATCH` or `DELETE` on any log route (they are unhandled here, as the real API answers `405`). The
 * witness types their own identifier and password into the SAME request (HO-SRG-001 §1 Amended (16)):
 * a vet or a nurse who is not the administering vet; anything else gets the one generic
 * `WitnessNotVerified` answer, which never says which half was wrong. The idempotency key (`entryId`)
 * replays the first result for the same values (HO-SRG-001 §1 Amended (18)).
 */
const WITNESS_NOT_VERIFIED =
  'The witness could not be verified. Ask them to enter their own sign-in details.';

const WITNESSES = [
  {
    identifier: ids.witnessNurseIdentifier,
    password: ids.witnessNursePassword,
    id: 'a2000000-0000-0000-0000-0000000000b1',
    name: 'Wasim Nurse',
    roles: ['Nurse'],
  },
  {
    identifier: ids.witnessVetIdentifier,
    password: ids.witnessVetPassword,
    id: 'a2000000-0000-0000-0000-0000000000b2',
    name: 'Dr Witness Vet',
    roles: ['Vet'],
  },
];

const RANGES: Record<string, [number, number, string]> = {
  heartRateBpm: [20, 300, 'Heart rate'],
  respiratoryRatePerMin: [2, 120, 'Respiratory rate'],
  spO2Percent: [50, 100, 'SpO2'],
  etCo2MmHg: [5, 100, 'EtCO2'],
  temperatureCelsius: [30, 45, 'Temperature'],
  systolicMmHg: [30, 300, 'Systolic pressure'],
  meanArterialMmHg: [20, 250, 'Mean arterial pressure'],
};

const VITAL_KEYS = Object.keys(RANGES) as Array<keyof typeof RANGES>;

/** A known vet, or — for the person signed in, whose id the mock's staff directory does not hold — a vet caller. */
function isVet(id: string, request: Request): boolean {
  const known = STAFF.find((person) => person.id === id);
  return known ? known.roleIds.includes('Vet') : callerRoles(request).includes('Vet');
}

function nameOf(id: string, request: Request): string {
  return STAFF.some((person) => person.id === id)
    ? staffName(id)
    : actorFor(callerRoles(request)).name;
}

function isOpen(s: MSurgery): boolean {
  return ['PreOp', 'InTheatre', 'Recovery'].includes(s.status);
}

function closed() {
  return violation(
    bad(
      'SurgeryId',
      'AnaesthesiaLogClosed',
      'The anaesthesia log is only open while the cat is in pre-op, in theatre or in recovery.',
    ),
  );
}

function implausible(
  fields: Record<string, unknown>,
  confirm: boolean | undefined,
): MockViolation[] {
  if (confirm) return [];
  const out: MockViolation[] = [];
  for (const key of VITAL_KEYS) {
    const value = fields[key];
    const range = RANGES[key];
    if (typeof value === 'number' && range && (value < range[0] || value > range[1])) {
      out.push(
        bad(
          key.charAt(0).toUpperCase() + key.slice(1),
          'ImplausibleVitalValue',
          `${range[2]} of ${value} looks unlikely (usually ${range[0]} to ${range[1]}). Check it, or save it anyway if it is correct.`,
        ),
      );
    }
  }
  return out;
}

/** The medicine, the batch and (controlled) the witness, checked in the real handler's order. */
function planDrug(
  request: Request,
  s: MSurgery,
  input: {
    medicineId: string;
    administeredById: string;
    batchId?: string;
    quantityDrawn?: number;
    quantityWasted?: number;
    witnessIdentifier?: string;
    witnessPassword?: string;
  },
) {
  const medicine = findMedicine(input.medicineId);
  if (!medicine || !medicine.isActive) {
    return {
      refusal: violation(
        bad(
          'MedicineId',
          'MedicineNotFound',
          'That medicine is not in the formulary or is no longer active.',
        ),
      ),
    } as const;
  }
  let batch: (typeof pharmacyDb.batches)[number] | undefined;
  if (medicine.linkedSkus.length > 0) {
    if (!input.batchId || input.quantityDrawn === undefined) {
      return {
        refusal: violation(
          bad(
            'BatchId',
            'BatchRequired',
            `${medicine.name} is a stocked medicine: name the batch and the quantity drawn.`,
          ),
        ),
      } as const;
    }
    batch = pharmacyDb.batches.find((b) => b.batchId === input.batchId);
    if (!batch || !medicine.linkedSkus.some((k) => k.skuId === batch!.skuId)) {
      return {
        refusal: violation(
          bad(
            'BatchId',
            'BatchSkuMismatch',
            `That batch is not one of ${medicine.name}’s batches held at the clinic dispensary.`,
          ),
        ),
      } as const;
    }
    if (batch.notDispensableReason || batch.expiresOn < new Date().toISOString().slice(0, 10)) {
      return {
        refusal: violation(
          bad(
            'BatchId',
            'BatchNotUsable',
            'This batch can’t be used — it has expired or is quarantined. Choose another.',
          ),
        ),
      } as const;
    }
  }
  let witness: (typeof WITNESSES)[number] | undefined;
  if (medicine.isControlled) {
    if (!isVet(input.administeredById, request)) {
      return {
        refusal: violation(
          bad(
            'AdministeredById',
            'AdministeringVetRequired',
            'A controlled drug is administered by a veterinarian; the register names the administering vet.',
          ),
        ),
      } as const;
    }
    if (input.quantityWasted === undefined) {
      return {
        refusal: violation(
          bad(
            'Wasted',
            'WastageRequired',
            'Record the quantity wasted (0 when none) for a controlled drug.',
          ),
        ),
      } as const;
    }
    if (input.quantityDrawn === undefined || input.quantityDrawn <= input.quantityWasted) {
      return {
        refusal: violation(
          bad(
            'Drawn',
            'InvalidQuantity',
            'A controlled drug entry records a quantity drawn greater than the quantity wasted.',
          ),
        ),
      } as const;
    }
    if (!input.witnessIdentifier?.trim() || !input.witnessPassword) {
      return {
        refusal: violation(
          bad(
            'WitnessIdentifier',
            'WitnessRequired',
            'A controlled drug needs a witness. The witness enters their own sign-in details.',
          ),
        ),
      } as const;
    }
    witness = WITNESSES.find(
      (w) =>
        w.identifier === input.witnessIdentifier!.trim() && w.password === input.witnessPassword,
    );
    if (!witness) {
      return {
        refusal: violation(bad('WitnessIdentifier', 'WitnessNotVerified', WITNESS_NOT_VERIFIED)),
      } as const;
    }
    if (witness.id === input.administeredById) {
      return {
        refusal: violation(
          bad(
            'WitnessIdentifier',
            'WitnessSameAsAdministrator',
            'The witness must be a different person from the administering vet.',
          ),
        ),
      } as const;
    }
  }
  void s;
  return {
    medicine,
    batch,
    witness,
    administeredByName: nameOf(input.administeredById, request),
  } as const;
}

function valuesFor(
  plan: Exclude<ReturnType<typeof planDrug>, { refusal: unknown }>,
  input: {
    doseAmount: number;
    doseUnit: string;
    route: string;
    administeredById: string;
    quantityDrawn?: number;
    quantityWasted?: number;
  },
  registerEntryId: string | null,
) {
  return emptyValues({
    medicineId: plan.medicine.id,
    medicineName: plan.medicine.name,
    isControlled: plan.medicine.isControlled,
    doseAmount: input.doseAmount,
    doseUnit: input.doseUnit,
    route: input.route,
    administeredById: input.administeredById,
    administeredByName: plan.administeredByName,
    batchNumber: plan.batch?.lotNumber ?? null,
    quantityDrawn: input.quantityDrawn ?? null,
    quantityWasted: input.quantityWasted ?? null,
    stockUnit: plan.batch ? plan.medicine.dispensingUnit : null,
    witnessId: plan.witness?.id ?? null,
    witnessName: plan.witness?.name ?? null,
    registerEntryId,
    stockShortfall: !!plan.batch && plan.batch.available < (input.quantityDrawn ?? 0),
  });
}

function logOf(s: MSurgery) {
  const rows = getDb().log.filter((e) => e.surgeryId === s.id);
  const correctorOf = new Map<string, MLogEntry>();
  for (const e of [...rows].sort((a, b) => a.loggedOn.localeCompare(b.loggedOn))) {
    if (e.correctsEntryId) correctorOf.set(e.correctsEntryId, e);
  }
  const leaf = (e: MLogEntry): MLogEntry => {
    let current = e;
    while (correctorOf.has(current.id)) current = correctorOf.get(current.id)!;
    return current;
  };
  const weight = latestWeight(s.catId);
  return {
    surgeryId: s.id,
    catId: s.catId,
    branchTimeZoneId: zoneOfBranch(s.branchId),
    isOpen: isOpen(s),
    latestWeight: weight,
    entries: [...rows]
      .sort(
        (a, b) => a.occurredOn.localeCompare(b.occurredOn) || a.loggedOn.localeCompare(b.loggedOn),
      )
      .map((e) => {
        const effective = leaf(e);
        const mg =
          weight && effective.values.doseAmount && effective.values.doseUnit
            ? ({ Mg: 1, Mcg: 0.001, G: 1000 } as Record<string, number>)[effective.values.doseUnit]
            : undefined;
        return {
          id: e.id,
          kind: e.kind,
          occurredOn: e.occurredOn,
          loggedOn: e.loggedOn,
          loggedBy: e.loggedBy,
          loggedByName: e.loggedByName,
          values: e.values,
          effective: effective.values,
          isCorrected: correctorOf.has(e.id),
          correctedByEntryId: correctorOf.get(e.id)?.id ?? null,
          correctsEntryId: e.correctsEntryId,
          correctedKind: e.correctedKind,
          correctionReason: e.correctionReason,
          effectiveDoseMgPerKg:
            mg !== undefined && weight
              ? Number(((effective.values.doseAmount! * mg) / weight.kilograms).toFixed(3))
              : null,
        };
      }),
  };
}

function fingerprintOf(input: LogDrugRequest): string {
  return JSON.stringify([
    input.medicineId,
    input.batchId ?? null,
    input.quantityDrawn ?? null,
    input.doseAmount,
  ]);
}

function stamp(request: Request) {
  const actor = actorFor(callerRoles(request));
  return { loggedBy: actor.id, loggedByName: actor.name, loggedOn: new Date().toISOString() };
}

export const anaesthesiaHandlers: HttpHandler[] = [
  http.get('*/api/staff/surgeries/:id/anaesthesia-log', ({ request, params }) => {
    const refused = requireAction(request, 'Surgery.View');
    if (refused) return refused;
    const s = findSurgery(params.id as string);
    if (!s || s.branchId !== branchOf(request)) return notFound();
    return ok(logOf(s));
  }),

  http.post('*/api/staff/surgeries/:id/anaesthesia-log/drugs', async ({ request, params }) => {
    const refused = requireAction(request, 'Surgery.LogDrug');
    if (refused) return refused;
    const s = findSurgery(params.id as string);
    if (!s || s.branchId !== branchOf(request)) return notFound();
    const body = (await request.json()) as LogDrugRequest;
    const db = getDb();
    const existing = body.entryId ? db.log.find((e) => e.id === body.entryId) : undefined;
    if (existing) {
      if (existing.surgeryId !== s.id) {
        return conflict(
          bad(
            'EntryId',
            'DuplicateAnaesthesiaEntry',
            'This entry id has already been used for another record.',
          ),
        );
      }
      if (existing.fingerprint !== fingerprintOf(body) || !existing.result) {
        return conflict(
          bad(
            'EntryId',
            'AnaesthesiaEntryIdMismatch',
            'This entry id was already used for this surgery with different values.',
          ),
        );
      }
      return ok(existing.result, 201);
    }
    if (!isOpen(s)) return closed();
    const plan = planDrug(request, s, body);
    if ('refusal' in plan) return plan.refusal;
    const registerEntryId = plan.medicine.isControlled ? newId() : null;
    const values = valuesFor(plan, body, registerEntryId);
    const result = {
      entryId: body.entryId,
      registerEntryId,
      stockShortfall: values.stockShortfall,
    };
    db.log.push({
      id: body.entryId,
      surgeryId: s.id,
      kind: 'Drug',
      occurredOn: body.occurredOn,
      ...stamp(request),
      values,
      correctsEntryId: null,
      correctedKind: null,
      correctionReason: null,
      fingerprint: fingerprintOf(body),
      result,
    });
    persist();
    return ok(result, 201);
  }),

  http.post('*/api/staff/surgeries/:id/anaesthesia-log/vitals', async ({ request, params }) => {
    const refused = requireAction(request, 'Surgery.LogVitals');
    if (refused) return refused;
    const s = findSurgery(params.id as string);
    if (!s || s.branchId !== branchOf(request)) return notFound();
    if (!isOpen(s)) return closed();
    const body = (await request.json()) as LogVitalsRequest;
    const problems = implausible(
      body as unknown as Record<string, unknown>,
      body.confirmImplausible,
    );
    if (problems.length > 0) return violation(...problems);
    const id = newId();
    getDb().log.push({
      id,
      surgeryId: s.id,
      kind: 'Vitals',
      occurredOn: body.occurredOn,
      ...stamp(request),
      values: emptyValues({
        heartRateBpm: body.heartRateBpm ?? null,
        respiratoryRatePerMin: body.respiratoryRatePerMin ?? null,
        spO2Percent: body.spO2Percent ?? null,
        etCo2MmHg: body.etCo2MmHg ?? null,
        temperatureCelsius: body.temperatureCelsius ?? null,
        systolicMmHg: body.systolicMmHg ?? null,
        meanArterialMmHg: body.meanArterialMmHg ?? null,
        implausibleConfirmed: !!body.confirmImplausible,
        note: body.note?.trim() || null,
      }),
      correctsEntryId: null,
      correctedKind: null,
      correctionReason: null,
      fingerprint: null,
      result: null,
    });
    persist();
    return ok({ entryId: id }, 201);
  }),

  http.post('*/api/staff/surgeries/:id/anaesthesia-log/milestones', async ({ request, params }) => {
    const refused = requireAction(request, 'Surgery.LogVitals');
    if (refused) return refused;
    const s = findSurgery(params.id as string);
    if (!s || s.branchId !== branchOf(request)) return notFound();
    if (!isOpen(s)) return closed();
    const body = (await request.json()) as LogMilestoneRequest;
    const id = newId();
    getDb().log.push({
      id,
      surgeryId: s.id,
      kind: 'Milestone',
      occurredOn: body.occurredOn,
      ...stamp(request),
      values: emptyValues({ milestone: body.milestone, note: body.note?.trim() || null }),
      correctsEntryId: null,
      correctedKind: null,
      correctionReason: null,
      fingerprint: null,
      result: null,
    });
    persist();
    return ok({ entryId: id }, 201);
  }),

  http.post(
    '*/api/staff/surgeries/:id/anaesthesia-log/:entryId/corrections',
    async ({ request, params }) => {
      const refused = requireAction(request, 'Surgery.LogVitals');
      if (refused) return refused;
      const s = findSurgery(params.id as string);
      if (!s || s.branchId !== branchOf(request)) return notFound();
      if (!isOpen(s)) return closed();
      const db = getDb();
      const target = db.log.find((e) => e.id === params.entryId && e.surgeryId === s.id);
      if (!target) {
        return violation(
          bad(
            'CorrectsEntryId',
            'CorrectionTargetNotFound',
            'That entry can no longer be corrected. Reload the log and correct the latest entry.',
          ),
        );
      }
      if (db.log.some((e) => e.correctsEntryId === target.id)) {
        return violation(
          bad(
            'CorrectsEntryId',
            'CorrectionTargetNotFound',
            'That entry has already been corrected. Reload the log and correct the latest entry.',
          ),
        );
      }
      const body = (await request.json()) as CorrectEntryRequest;
      const reason = body.reason?.trim() ?? '';
      if (reason.length < 10) {
        return violation(
          bad(
            'Reason',
            'MinimumLengthValidator',
            'Give the reason for the correction (at least 10 characters).',
          ),
        );
      }
      const kind = target.correctedKind ?? target.kind;
      let values = target.values;
      if (kind === 'Drug') {
        const vetOnly = requireAction(request, 'Surgery.LogDrug');
        if (vetOnly) return vetOnly;
        if (!body.drug)
          return violation(
            bad('Drug', 'InvalidAnaesthesiaEntry', 'Give the corrected drug details.'),
          );
        const plan = planDrug(request, s, {
          medicineId: target.values.medicineId!,
          administeredById: body.drug.administeredById,
          batchId: pharmacyDb.batches.find((b) => b.lotNumber === target.values.batchNumber)
            ?.batchId,
          quantityDrawn: body.drug.quantityDrawn,
          quantityWasted: body.drug.quantityWasted,
          witnessIdentifier: body.witnessIdentifier,
          witnessPassword: body.witnessPassword,
        });
        if ('refusal' in plan) return plan.refusal;
        values = valuesFor(plan, body.drug, plan.medicine.isControlled ? newId() : null);
      } else if (kind === 'Vitals') {
        const v = body.vitals ?? {};
        const problems = implausible(v as unknown as Record<string, unknown>, v.confirmImplausible);
        if (problems.length > 0) return violation(...problems);
        values = emptyValues({
          ...v,
          note: v.note?.trim() || null,
          implausibleConfirmed: !!v.confirmImplausible,
        } as never);
      } else {
        values = emptyValues({
          milestone: body.milestone?.milestone ?? null,
          note: body.milestone?.note ?? null,
        });
      }
      const id = newId();
      db.log.push({
        id,
        surgeryId: s.id,
        kind: 'Correction',
        occurredOn: body.occurredOn,
        ...stamp(request),
        values,
        correctsEntryId: target.id,
        correctedKind: kind,
        correctionReason: reason,
        fingerprint: null,
        result: null,
      });
      persist();
      return ok({ entryId: id }, 201);
    },
  ),
];
