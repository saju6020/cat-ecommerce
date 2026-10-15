import { http, type HttpHandler } from 'msw';
import type { DispenseRequest } from '@/lib/api/pharmacy';
import { accountsFixtureIds } from '../areas/accounts';
import {
  branchOf,
  callerRoles,
  noContent,
  notFound,
  ok,
  pharmacyFixtureIds as ids,
  requireAction,
  staffNameFor,
  violation,
  type MockViolation,
} from './fixtures';
import {
  db,
  findMedicine,
  findPrescription,
  localDay,
  newId,
  persist,
  today,
  type MDispensing,
  type MRegisterEntry,
} from './store';
import {
  buildPlan,
  catNameOf,
  customerNameOf,
  dispensingView,
  effectiveStatus,
  isHighRisk,
  prescriptionSummary,
  suppliesLeft,
} from './views';

/**
 * `mocks/handlers/pharmacy/dispensing.ts` — `StaffPharmacyController` + `StaffDispensingsController`.
 * The mock enforces what the real handlers do: an expired batch is refused with no override
 * (`BatchExpired` / `OnlyExpiredStockAvailable`), a non-FEFO choice needs a reason, the batches must
 * add up to the prescribed quantity, and a controlled medicine needs a witness who is a vet or a nurse
 * and signs in with their OWN details (owner decision 2026-10-10) — any other sign-in gets the one
 * generic `WitnessNotVerified` message (HO-PHM-001 ADR-146).
 */
const bad = (propertyName: string, errorCode: string, errorMessage: string): MockViolation => ({
  propertyName,
  errorCode,
  errorMessage,
});

const WITNESS_NOT_VERIFIED =
  'The witness could not be verified. Ask them to enter their own sign-in details.';

/** The sign-ins the witness step accepts: a vet or a nurse. The administrator's is a valid account but the wrong role. */
const WITNESSES = [
  {
    identifier: ids.witnessNurseIdentifier,
    password: ids.witnessNursePassword,
    name: 'Wasim Nurse',
    roles: ['Nurse'],
  },
  {
    identifier: ids.witnessVetIdentifier,
    password: ids.witnessVetPassword,
    name: 'Dr Witness Vet',
    roles: ['Vet'],
  },
  {
    identifier: accountsFixtureIds.adminIdentifier,
    password: accountsFixtureIds.adminPassword,
    name: 'Ada Administrator',
    roles: ['Administrator'],
  },
];

function verifyWitness(identifier?: string | null, password?: string | null): string | null {
  const found = WITNESSES.find((w) => w.identifier === identifier && w.password === password);
  if (!found) return null;
  return found.roles.some((r) => r === 'Vet' || r === 'Nurse') ? found.name : null;
}

let entrySeq = 0;
function nextEntryNumber(): number {
  entrySeq = Math.max(entrySeq, ...db.entries.map((e) => e.entryNumber)) + 1;
  return entrySeq;
}

function dispensingFor(id: string, request: Request): MDispensing | undefined {
  const branch = branchOf(request);
  return db.dispensings.find((d) => d.id === id && d.branchId === branch);
}

export const dispensingHandlers: HttpHandler[] = [
  http.get('*/api/staff/pharmacy/queue', ({ request }) => {
    const denied = requireAction(request, 'Dispensing.View');
    if (denied) return denied;
    const branch = branchOf(request);
    return ok({
      generatedOn: new Date().toISOString(),
      awaitingFirstSupply: db.prescriptions
        .filter(
          (p) => p.branchId === branch && p.status === 'Issued' && effectiveStatus(p) === 'Issued',
        )
        .sort((a, b) => a.issuedOn.localeCompare(b.issuedOn))
        .map((p) => ({
          prescription: prescriptionSummary(p),
          catName: catNameOf(p.catId),
          catIsHighRisk: isHighRisk(p.catId),
          customerName: customerNameOf(p.catId),
        })),
      repeatRequests: db.prescriptions.flatMap((p) =>
        p.repeatRequests
          .filter((r) => r.status === 'Requested' && r.preferredBranchId === branch)
          .map((r) => ({
            requestId: r.id,
            prescriptionId: p.id,
            prescriptionNumber: p.number,
            catName: catNameOf(p.catId),
            customerName: customerNameOf(p.catId),
            medicine: prescriptionSummary(p).medicine,
            requestedOn: r.requestedOn,
            note: r.note,
          })),
      ),
    });
  }),

  http.get('*/api/staff/pharmacy/lookup', ({ request }) => {
    const denied = requireAction(request, 'Dispensing.View');
    if (denied) return denied;
    const number = (new URL(request.url).searchParams.get('number') ?? '').trim().toUpperCase();
    const found = db.prescriptions.find((p) => p.number === number);
    return found ? ok({ prescriptionId: found.id }) : notFound();
  }),

  http.get('*/api/staff/prescriptions/:prescriptionId/dispense-plan', ({ request, params }) => {
    const denied = requireAction(request, 'Dispensing.Dispense');
    if (denied) return denied;
    const p = findPrescription(params.prescriptionId as string);
    return p ? ok(buildPlan(p)) : notFound();
  }),

  http.post(
    '*/api/staff/prescriptions/:prescriptionId/dispensings',
    async ({ request, params }) => {
      const denied = requireAction(request, 'Dispensing.Dispense');
      if (denied) return denied;
      const p = findPrescription(params.prescriptionId as string);
      if (!p) return notFound();
      const body = (await request.json()) as DispenseRequest;
      const medicine = findMedicine(p.medicineId)!;
      const plan = buildPlan(p);

      if (plan.onlyExpiredStockAvailable)
        return violation(
          null,
          bad('Lines', 'OnlyExpiredStockAvailable', plan.blockingReason ?? ''),
        );
      if (suppliesLeft(p) <= 0)
        return violation(
          null,
          bad(
            'PrescriptionId',
            'NoSuppliesRemaining',
            'There are no supplies left on this prescription.',
          ),
        );
      if (today() > p.validUntil)
        return violation(
          null,
          bad('PrescriptionId', 'PrescriptionValidityEnded', plan.blockingReason ?? ''),
        );
      if (body.lines.length === 0 || body.lines.length > 5)
        return violation(
          null,
          bad(
            'Lines',
            'TooManyDispensingLines',
            'A supply can be taken from at most five batches.',
          ),
        );

      let total = 0;
      for (const line of body.lines) {
        const batch = db.batches.find(
          (b) => b.batchId === line.batchId && b.medicineId === medicine.id,
        );
        if (!batch)
          return violation(
            null,
            bad('Lines', 'SkuNotLinkedToMedicine', 'That batch is not a batch of this medicine.'),
          );
        if (batch.expiresOn < today())
          return violation(
            null,
            bad(
              'Lines',
              'BatchExpired',
              `Batch ${batch.lotNumber} expired on ${batch.expiresOn} and cannot be dispensed.`,
            ),
          );
        if (batch.notDispensableReason)
          return violation(null, bad('Lines', 'BatchNotDispensable', batch.notDispensableReason));
        if (batch.available < line.quantity)
          return violation(
            null,
            bad(
              'Lines',
              'InsufficientBatchStock',
              'That batch no longer has enough stock. The batch list has been refreshed.',
            ),
          );
        total += line.quantity;
      }
      if (Math.abs(total - p.quantity) > 0.0001)
        return violation(
          null,
          bad(
            'Lines',
            'DispenseQuantityMismatch',
            'The batches chosen must add up to the prescribed quantity.',
          ),
        );

      const suggestedIds = new Set(plan.suggestedLines.map((l) => l.batchId));
      const departs = body.lines.some((l) => !suggestedIds.has(l.batchId));
      if (departs && (body.fefoOverrideReason ?? '').trim().length < 5)
        return violation(
          null,
          bad(
            'FefoOverrideReason',
            'FefoOverrideReasonRequired',
            'Say why you are not using the batch that expires first.',
          ),
        );

      let witnessName: string | null = null;
      if (medicine.isControlled) {
        if (!body.witnessIdentifier || !body.witnessPassword)
          return violation(
            null,
            bad(
              'WitnessIdentifier',
              'WitnessRequired',
              'A controlled drug needs a witness. The witness enters their own identifier and password.',
            ),
          );
        witnessName = verifyWitness(body.witnessIdentifier, body.witnessPassword);
        if (!witnessName)
          return violation(
            null,
            bad('WitnessIdentifier', 'WitnessNotVerified', WITNESS_NOT_VERIFIED),
          );
      }

      const roles = callerRoles(request);
      const dispensing: MDispensing = {
        id: newId('e3'),
        prescriptionId: p.id,
        supplyNumber: p.suppliesDispensed + 1,
        isRepeat: p.suppliesDispensed > 0,
        status: 'Dispensed',
        dispensedOn: new Date().toISOString(),
        dispensedByName: staffNameFor(roles),
        witnessName,
        fefoOverrideReason: departs ? (body.fefoOverrideReason ?? null) : null,
        lines: body.lines.map((l) => {
          const batch = db.batches.find((b) => b.batchId === l.batchId)!;
          batch.available -= l.quantity;
          return {
            lineId: newId('ea'),
            skuId: l.skuId,
            batchId: l.batchId,
            lotNumber: batch.lotNumber,
            expiresOn: batch.expiresOn,
            quantity: l.quantity,
          };
        }),
        labelPrintCount: 0,
        reversedOn: null,
        reversalReason: null,
        branchId: branchOf(request),
      };
      db.dispensings.push(dispensing);
      p.suppliesDispensed += 1;
      p.lastDispensedOn = today();
      p.status = suppliesLeft(p) <= 0 ? 'Completed' : 'Dispensed';
      if (body.repeatRequestId) {
        const repeat = p.repeatRequests.find((r) => r.id === body.repeatRequestId);
        if (repeat) {
          repeat.status = 'Dispensed';
          repeat.dispensingId = dispensing.id;
          repeat.decidedOn = new Date().toISOString();
        }
      }
      if (medicine.isControlled) {
        const first = dispensing.lines[0]!;
        const entry: MRegisterEntry = {
          id: newId('eb'),
          entryNumber: nextEntryNumber(),
          recordedOn: new Date().toISOString(),
          occurredOn: new Date().toISOString(),
          kind: 'Dispensed',
          medicineId: medicine.id,
          batchId: first.batchId,
          lotNumber: first.lotNumber,
          batchExpiresOn: first.expiresOn,
          delta: -total,
          doseText: `${p.doseAmount} ${p.doseUnit.toLowerCase()}`,
          wastage: null,
          catName: catNameOf(p.catId),
          customerName: customerNameOf(p.catId),
          actorName: staffNameFor(roles),
          witnessName,
          sourceType: 'Dispensing',
          sourceId: dispensing.id,
          sourceLabel: `Dispensing ${p.number} #${dispensing.supplyNumber}`,
          correctionReason: null,
          notes: null,
          correctsEntryNumber: null,
          isSealed: false,
          branchId: dispensing.branchId,
        };
        db.entries.push(entry);
      }
      persist();
      const view = dispensingView(dispensing, roles);
      // One unpriced line when the medicine's SKU has no price set (the mock marks gabapentin-like unstocked cases never; maropitant stands in).
      const unpriced = medicine.code.startsWith('FUS') ? [dispensing.lines[0]!.lineId] : [];
      return ok(
        {
          dispensingId: dispensing.id,
          supplyNumber: dispensing.supplyNumber,
          label: view.label,
          unpricedLines: unpriced,
        },
        201,
      );
    },
  ),

  http.get('*/api/staff/dispensings/:dispensingId', ({ request, params }) => {
    const denied = requireAction(request, 'Dispensing.View');
    if (denied) return denied;
    const d = dispensingFor(params.dispensingId as string, request);
    return d ? ok(dispensingView(d, callerRoles(request))) : notFound();
  }),

  http.post('*/api/staff/dispensings/:dispensingId/label-prints', ({ request, params }) => {
    const denied = requireAction(request, 'Dispensing.Dispense');
    if (denied) return denied;
    const d = dispensingFor(params.dispensingId as string, request);
    if (!d) return notFound();
    d.labelPrintCount += 1;
    persist();
    return noContent();
  }),

  http.post('*/api/staff/dispensings/:dispensingId/reverse', async ({ request, params }) => {
    const denied = requireAction(request, 'Dispensing.Reverse');
    if (denied) return denied;
    const d = dispensingFor(params.dispensingId as string, request);
    if (!d) return notFound();
    const body = (await request.json()) as {
      reason: string;
      witnessIdentifier?: string;
      witnessPassword?: string;
    };
    const p = findPrescription(d.prescriptionId)!;
    const medicine = findMedicine(p.medicineId)!;
    if (d.status !== 'Dispensed')
      return violation(
        null,
        bad('DispensingId', 'DispensingNotReversible', 'This dispensing cannot be reversed.'),
      );
    if (localDay(d.dispensedOn) !== today())
      return violation(
        null,
        bad('DispensingId', 'ReversalWindowPassed', 'This dispensing can no longer be reversed.'),
      );
    if (!body.reason?.trim())
      return violation(
        null,
        bad('Reason', 'ReversalReasonRequired', 'Give a reason for the reversal.'),
      );
    let witnessName: string | null = null;
    if (medicine.isControlled) {
      if (!body.witnessIdentifier || !body.witnessPassword)
        return violation(
          null,
          bad(
            'WitnessIdentifier',
            'WitnessRequired',
            'A controlled drug needs a witness. The witness enters their own identifier and password.',
          ),
        );
      witnessName = verifyWitness(body.witnessIdentifier, body.witnessPassword);
      if (!witnessName)
        return violation(
          null,
          bad('WitnessIdentifier', 'WitnessNotVerified', WITNESS_NOT_VERIFIED),
        );
    }
    d.status = 'Reversed';
    d.reversedOn = new Date().toISOString();
    d.reversalReason = body.reason;
    p.suppliesDispensed = Math.max(0, p.suppliesDispensed - 1);
    p.status = p.suppliesDispensed === 0 ? 'Issued' : 'Dispensed';
    p.lastDispensedOn = p.suppliesDispensed === 0 ? null : p.lastDispensedOn;
    if (medicine.isControlled) {
      const first = d.lines[0]!;
      db.entries.push({
        id: newId('eb'),
        entryNumber: nextEntryNumber(),
        recordedOn: new Date().toISOString(),
        occurredOn: new Date().toISOString(),
        kind: 'Returned',
        medicineId: medicine.id,
        batchId: first.batchId,
        lotNumber: first.lotNumber,
        batchExpiresOn: first.expiresOn,
        delta: d.lines.reduce((s, l) => s + l.quantity, 0),
        doseText: null,
        wastage: null,
        catName: catNameOf(p.catId),
        customerName: customerNameOf(p.catId),
        actorName: staffNameFor(callerRoles(request)),
        witnessName,
        sourceType: 'DispensingReversal',
        sourceId: d.id,
        sourceLabel: `Reversal of dispensing ${p.number} #${d.supplyNumber}`,
        correctionReason: null,
        notes: null,
        correctsEntryNumber: null,
        isSealed: false,
        branchId: d.branchId,
      });
    }
    persist();
    return noContent();
  }),
];
