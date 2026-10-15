import { http, HttpResponse, type HttpHandler } from 'msw';
import type { CorrectionInput } from '@/lib/api/controlled-drugs';
import { accountsFixtureIds } from '../areas/accounts';
import {
  branchOf,
  callerRoles,
  amountText,
  notFound,
  ok,
  pharmacyFixtureIds as ids,
  quantity,
  requireAction,
  staffNameFor,
  violation,
  type MockViolation,
} from './fixtures';
import { db, findMedicine, newId, persist, type MRegisterEntry } from './store';
import { catNameOf, chainPositionOf, hashOf, registerView } from './views';

/**
 * `mocks/handlers/pharmacy/register.ts` — `StaffControlledDrugsController`. There is no endpoint that
 * edits or deletes an entry (C-2): a correction is a NEW linked entry, vet only (owner decision
 * 2026-10-10: an administrator cannot correct the register), and it needs a witness who is a vet or a
 * nurse. Entries are newest first; the running balance is computed per medicine and batch.
 */
const bad = (propertyName: string, errorCode: string, errorMessage: string): MockViolation => ({
  propertyName,
  errorCode,
  errorMessage,
});

const WITNESSES: Record<string, { password: string; name: string }> = {
  [ids.witnessNurseIdentifier]: { password: ids.witnessNursePassword, name: 'Wasim Nurse' },
  [ids.witnessVetIdentifier]: { password: ids.witnessVetPassword, name: 'Dr Witness Vet' },
};

function forBranch(request: Request): MRegisterEntry[] {
  const branch = branchOf(request);
  return db.entries.filter((e) => e.branchId === branch);
}

export const registerHandlers: HttpHandler[] = [
  // Registered before `/register/:entryId` so "export" is not read as an id.
  http.get('*/api/staff/controlled-drugs/register/export', ({ request }) => {
    const denied = requireAction(request, 'ControlledDrugs.View');
    if (denied) return denied;
    const url = new URL(request.url);
    const medicineId = url.searchParams.get('medicineId');
    const rows = forBranch(request)
      .filter((e) => !medicineId || e.medicineId === medicineId)
      .sort((a, b) => a.entryNumber - b.entryNumber);
    const header =
      'EntryNumber,RecordedOn,Kind,Medicine,Lot,QuantityDelta,Unit,Actor,Witness,Source';
    const lines = rows.map((e) =>
      [
        e.entryNumber,
        e.recordedOn,
        e.kind,
        findMedicine(e.medicineId)?.name ?? '',
        e.lotNumber ?? '',
        e.delta,
        findMedicine(e.medicineId)?.dispensingUnit ?? '',
        e.actorName,
        e.witnessName ?? '',
        e.sourceLabel,
      ].join(','),
    );
    return new HttpResponse([header, ...lines].join('\n'), {
      headers: {
        'Content-Type': 'text/csv',
        'Content-Disposition': 'attachment; filename="controlled-drug-register.csv"',
      },
    });
  }),

  http.get('*/api/staff/controlled-drugs/register', ({ request }) => {
    const denied = requireAction(request, 'ControlledDrugs.View');
    if (denied) return denied;
    const url = new URL(request.url);
    const param = (name: string) => url.searchParams.get(name);
    const from = param('from');
    const to = param('to');
    const pageNumber = Number(param('pageNumber') ?? 1);
    const pageSize = Number(param('pageSize') ?? 25);
    const rows = forBranch(request)
      .filter((e) => !param('medicineId') || e.medicineId === param('medicineId'))
      .filter((e) => !param('batchId') || e.batchId === param('batchId'))
      .filter((e) => !param('kind') || e.kind === param('kind'))
      .filter((e) => !from || e.recordedOn.slice(0, 10) >= from)
      .filter((e) => !to || e.recordedOn.slice(0, 10) <= to)
      .sort((a, b) => b.entryNumber - a.entryNumber);
    const start = (pageNumber - 1) * pageSize;
    return ok({
      items: rows.slice(start, start + pageSize).map(registerView),
      pageNumber,
      pageSize,
      totalCount: rows.length,
    });
  }),

  http.get('*/api/staff/controlled-drugs/register/:entryId', ({ request, params }) => {
    const denied = requireAction(request, 'ControlledDrugs.View');
    if (denied) return denied;
    const entry = forBranch(request).find((e) => e.id === params.entryId);
    if (!entry) return notFound();
    return ok({
      entry: registerView(entry),
      corrections: forBranch(request)
        .filter((e) => e.correctsEntryNumber === entry.entryNumber)
        .sort((a, b) => a.entryNumber - b.entryNumber)
        .map(registerView),
      seal: entry.isSealed
        ? {
            chainPosition: chainPositionOf(entry),
            entryHash: hashOf(entry),
            sealedOn: entry.recordedOn,
          }
        : null,
    });
  }),

  http.post(
    '*/api/staff/controlled-drugs/register/:entryId/corrections',
    async ({ request, params }) => {
      const denied = requireAction(request, 'ControlledDrugs.Correct');
      if (denied) return denied;
      const target = forBranch(request).find((e) => e.id === params.entryId);
      if (!target) return notFound();
      const body = (await request.json()) as CorrectionInput;
      if (!body.reason?.trim())
        return violation(
          null,
          bad('Reason', 'CorrectionReasonRequired', 'Give the reason for the correction.'),
        );
      if (target.kind === 'Correction')
        return violation(
          null,
          bad(
            'CorrectsEntryId',
            'CorrectionTargetMismatch',
            'Correct the original entry, not a correction.',
          ),
        );
      if (!body.witnessIdentifier || !body.witnessPassword)
        return violation(
          null,
          bad(
            'WitnessIdentifier',
            'WitnessRequired',
            'A controlled drug needs a witness. The witness enters their own identifier and password.',
          ),
        );
      const witness = WITNESSES[body.witnessIdentifier];
      if (
        !witness ||
        witness.password !== body.witnessPassword ||
        body.witnessIdentifier === accountsFixtureIds.adminIdentifier
      )
        return violation(
          null,
          bad(
            'WitnessIdentifier',
            'WitnessNotVerified',
            'The witness could not be verified. Ask them to enter their own sign-in details.',
          ),
        );

      const entryNumber = Math.max(...db.entries.map((e) => e.entryNumber)) + 1;
      const created: MRegisterEntry = {
        ...target,
        id: newId('eb'),
        entryNumber,
        kind: 'Correction',
        delta: body.quantityDelta,
        doseText: body.correctedDoseText ?? target.doseText,
        wastage: null,
        catName: body.correctedCatId ? catNameOf(body.correctedCatId) : target.catName,
        actorName: staffNameFor(callerRoles(request)),
        witnessName: witness.name,
        sourceType: 'Correction',
        sourceId: target.id,
        sourceLabel: `Correction of #${target.entryNumber}`,
        correctionReason: body.reason,
        notes: body.notes ?? null,
        correctsEntryNumber: target.entryNumber,
        recordedOn: new Date().toISOString(),
        occurredOn: new Date().toISOString(),
        isSealed: false,
      };
      db.entries.push(created);
      if (body.quantityDelta !== 0) {
        const batch = db.batches.find((b) => b.batchId === target.batchId);
        if (batch) batch.available = Math.max(0, batch.available + body.quantityDelta);
      }
      persist();
      return ok({ entryId: created.id, entryNumber }, 201);
    },
  ),

  http.get('*/api/staff/controlled-drugs/balances', ({ request }) => {
    const denied = requireAction(request, 'ControlledDrugs.View');
    if (denied) return denied;
    const rows = forBranch(request);
    const keys = [...new Set(rows.map((e) => `${e.medicineId}|${e.batchId}`))];
    return ok(
      keys.map((key) => {
        const [medicineId, batchId] = key.split('|') as [string, string];
        const medicine = findMedicine(medicineId)!;
        const register = rows
          .filter((e) => e.medicineId === medicineId && String(e.batchId) === batchId)
          .reduce((s, e) => s + e.delta, 0);
        const batch = db.batches.find((b) => b.batchId === batchId);
        const stock = batch ? batch.available : null;
        return {
          medicineId,
          medicineName: medicine.name,
          batchId,
          lotNumber: batch?.lotNumber ?? null,
          registerBalance: quantity(register, medicine.dispensingUnit),
          stockBalance: stock === null ? null : quantity(stock, medicine.dispensingUnit),
          isMismatch: stock !== null && amountText(stock) !== amountText(register),
        };
      }),
    );
  }),

  http.get('*/api/staff/controlled-drugs/verification', ({ request }) => {
    const denied = requireAction(request, 'ControlledDrugs.View');
    if (denied) return denied;
    const rows = forBranch(request);
    return ok({
      isIntact: db.brokenAtPosition === null,
      entriesChecked: rows.filter((e) => e.isSealed).length,
      firstBrokenPosition: db.brokenAtPosition,
      staleUnsealed: rows
        .filter((e) => !e.isSealed && Date.now() - Date.parse(e.recordedOn) > 10 * 60 * 1000)
        .map((e) => ({ entryNumber: e.entryNumber, recordedOn: e.recordedOn })),
      checkedOn: new Date().toISOString(),
    });
  }),
];
