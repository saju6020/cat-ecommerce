import { http, type HttpHandler } from 'msw';
import type {
  IssuePrescriptionRequest,
  PrescriptionFields,
  RenewPrescriptionRequest,
  SafetyCheckRequest,
} from '@/lib/api/prescriptions';
import { dateOffset, instantOffset } from '../inventory/fixtures';
import { findVisit } from '../visits/store';
import {
  branchOf,
  callerRoles,
  conflictViolation,
  noContent,
  notFound,
  ok,
  requireAction,
  staffNameFor,
  violation,
  type MockViolation,
} from './fixtures';
import {
  db,
  findMedicine,
  findPrescription,
  newId,
  nextSeq,
  persist,
  today,
  type MPrescription,
} from './store';
import { assess, isActive, prescriptionDetail, prescriptionSummary } from './views';

/**
 * `mocks/handlers/pharmacy/prescribing.ts` — `StaffPrescriptionsController` (HO-PHM-001 §12.4). The
 * safety check writes nothing; issue/renew re-run it and refuse exactly as the real handlers do
 * (`RecentWeightRequired`, `PrescriptionWarningsNotAcknowledged`, `PrescriptionWarningsChanged`,
 * `AllergyAcknowledgementReasonRequired`, `VisitNotOpen`). A renewal needs no visit (owner decision
 * 2026-10-10: remote renewal) and is flagged `isRemoteRenewal`.
 */
const bad = (propertyName: string, errorCode: string, errorMessage: string): MockViolation => ({
  propertyName,
  errorCode,
  errorMessage,
});

function nextNumber(): string {
  return `RX-${new Date().getFullYear()}-${String(nextSeq()).padStart(6, '0')}`;
}

function shapeProblems(body: PrescriptionFields): MockViolation[] {
  const problems: MockViolation[] = [];
  if (!(body.dose.amount > 0))
    problems.push(bad('Dose.Amount', 'GreaterThanValidator', 'Enter a dose greater than zero.'));
  if (!body.dose.unit)
    problems.push(bad('Dose.Unit', 'NotEmptyValidator', 'Choose the dose unit.'));
  if (!body.route) problems.push(bad('Route', 'NotEmptyValidator', 'Choose a route.'));
  if (!body.frequency) problems.push(bad('Frequency', 'NotEmptyValidator', 'Choose how often.'));
  if (!(body.durationDays >= 1 && body.durationDays <= 365))
    problems.push(
      bad('DurationDays', 'InclusiveBetweenValidator', 'Duration must be between 1 and 365 days.'),
    );
  if (!(body.quantity > 0))
    problems.push(bad('Quantity', 'GreaterThanValidator', 'Enter a quantity greater than zero.'));
  if (!body.instructions?.trim())
    problems.push(
      bad('Instructions', 'NotEmptyValidator', 'Enter the instructions for the label.'),
    );
  if (body.isRepeatable && !(body.repeatsAuthorised >= 1 && body.repeatsAuthorised <= 12))
    problems.push(
      bad(
        'RepeatsAuthorised',
        'InclusiveBetweenValidator',
        'A repeatable prescription authorises one to twelve repeats.',
      ),
    );
  if (body.isRepeatable && !body.validUntil)
    problems.push(
      bad(
        'ValidUntil',
        'NotNullValidator',
        'A repeatable prescription needs the date its repeats stop.',
      ),
    );
  return problems;
}

async function create(
  request: Request,
  catId: string,
  medicineId: string,
  body: PrescriptionFields,
  options: { visitId: string | null; renewFromId?: string },
): Promise<Response> {
  const roles = callerRoles(request);
  const medicine = findMedicine(medicineId);
  if (!medicine)
    return violation(
      null,
      bad('MedicineId', 'FormularyMedicineNotFound', 'That medicine could not be found.'),
    );
  if (!medicine.isActive)
    return violation(
      null,
      bad('MedicineId', 'MedicineInactive', 'This medicine is no longer active in the formulary.'),
    );

  const problems = shapeProblems(body);
  if (!medicine.allowedRoutes.includes(body.route))
    problems.push(
      bad('Route', 'RouteNotAllowedForMedicine', `${medicine.name} is not given by that route.`),
    );
  if (problems.length > 0) return violation(null, ...problems);

  if (options.visitId) {
    const visit = findVisit(options.visitId);
    if (!visit)
      return violation(null, bad('VisitId', 'VisitNotFound', 'That visit could not be found.'));
    if (visit.catId !== catId)
      return violation(
        null,
        bad('VisitId', 'VisitCatMismatch', 'That visit is for a different cat.'),
      );
    if (visit.status !== 'Open')
      return violation(
        null,
        bad('VisitId', 'VisitNotOpen', 'A prescription can only be written from an open visit.'),
      );
  }

  const assessment = await assess(
    catId,
    medicine,
    body.dose.amount,
    body.dose.unit,
    options.renewFromId,
  );
  if (assessment.weight.requirement !== 'Satisfied') {
    const message =
      assessment.weight.latest === null
        ? 'Record this cat’s weight before prescribing -- none has been recorded.'
        : `Record this cat’s weight before prescribing -- the latest was ${assessment.weight.latest.weight.amount} kg on ${assessment.weight.latest.recordedOn} (${assessment.weight.latest.ageDays} days ago).`;
    return violation({ assessment }, bad('Weight', 'RecentWeightRequired', message));
  }

  const mustAcknowledge = assessment.warnings.some((w) => w.requiresAcknowledgement);
  if (mustAcknowledge && !body.acknowledgedWarningsFingerprint)
    return violation(
      { assessment },
      bad(
        'AcknowledgedWarningsFingerprint',
        'PrescriptionWarningsNotAcknowledged',
        'Review and acknowledge the warnings before issuing.',
      ),
    );
  if (
    body.acknowledgedWarningsFingerprint &&
    body.acknowledgedWarningsFingerprint !== assessment.fingerprint
  )
    return conflictViolation(
      { assessment },
      bad(
        'AcknowledgedWarningsFingerprint',
        'PrescriptionWarningsChanged',
        'The warnings changed since you reviewed them. Please review them again.',
      ),
    );
  const allergy = assessment.warnings.some((w) => w.kind === 'AllergyConflict');
  if (allergy && !body.allergyAcknowledgementReason?.trim())
    return violation(
      null,
      bad(
        'AllergyAcknowledgementReason',
        'AllergyAcknowledgementReasonRequired',
        'Say why you are prescribing despite the allergy.',
      ),
    );

  const id = newId('e2');
  const created: MPrescription = {
    id,
    number: nextNumber(),
    catId,
    medicineId,
    visitId: options.visitId ?? '',
    doseAmount: body.dose.amount,
    doseUnit: body.dose.unit,
    route: body.route,
    frequency: body.frequency,
    durationDays: body.durationDays,
    quantity: body.quantity,
    instructions: body.instructions,
    startsOn: body.startsOn ?? today(),
    lastDispensedOn: null,
    isRepeatable: body.isRepeatable,
    repeatsAuthorised: body.isRepeatable ? body.repeatsAuthorised : 0,
    validUntil: body.validUntil ?? dateOffset(90),
    status: 'Issued',
    prescriberName: staffNameFor(roles),
    issuedOn: instantOffset(0, new Date().getUTCHours()),
    isRemoteRenewal: options.visitId === null,
    hasAcknowledgedWarnings: mustAcknowledge,
    weightKg: assessment.weight.latest ? Number(assessment.weight.latest.weight.amount) : null,
    weightRecordedOn: assessment.weight.latest?.recordedOn ?? null,
    doseMgPerKg: assessment.doseCheck.doseMgPerKg,
    doseCheckOutcome: assessment.doseCheck.outcome,
    acknowledgedByName: mustAcknowledge ? staffNameFor(roles) : null,
    acknowledgedOn: mustAcknowledge ? new Date().toISOString() : null,
    allergyReason: allergy ? (body.allergyAcknowledgementReason ?? null) : null,
    doseNote: body.doseAcknowledgementNote?.trim() ? body.doseAcknowledgementNote : null,
    warnings: assessment.warnings,
    repeatRequests: [],
    renewedFromId: options.renewFromId ?? null,
    renewedById: null,
    suppliesDispensed: 0,
    branchId: branchOf(request),
  };
  db.prescriptions.push(created);
  if (options.renewFromId) {
    const source = findPrescription(options.renewFromId);
    if (source) source.renewedById = id;
  }
  persist();
  return ok({ prescriptionId: id, prescriptionNumber: created.number }, 201);
}

export const prescribingHandlers: HttpHandler[] = [
  http.post('*/api/staff/cats/:catId/prescriptions/safety-check', async ({ request, params }) => {
    const denied = requireAction(request, 'Prescriptions.Issue');
    if (denied) return denied;
    const body = (await request.json()) as SafetyCheckRequest;
    const medicine = findMedicine(body.medicineId);
    if (!medicine) return notFound();
    return ok(await assess(params.catId as string, medicine, body.dose.amount, body.dose.unit));
  }),

  http.post('*/api/staff/cats/:catId/prescriptions', async ({ request, params }) => {
    const denied = requireAction(request, 'Prescriptions.Issue');
    if (denied) return denied;
    const body = (await request.json()) as IssuePrescriptionRequest;
    return create(request, params.catId as string, body.medicineId, body, {
      visitId: body.visitId,
    });
  }),

  http.get('*/api/staff/cats/:catId/prescriptions', ({ request, params }) => {
    const denied = requireAction(request, 'Prescriptions.View');
    if (denied) return denied;
    const url = new URL(request.url);
    const scope = url.searchParams.get('scope') ?? 'active';
    const pageNumber = Number(url.searchParams.get('pageNumber') ?? 1);
    const pageSize = Number(url.searchParams.get('pageSize') ?? 20);
    const rows = db.prescriptions
      .filter((p) => p.catId === params.catId)
      .filter((p) => scope === 'all' || isActive(p))
      .sort((a, b) => b.issuedOn.localeCompare(a.issuedOn));
    const start = (pageNumber - 1) * pageSize;
    return ok({
      items: rows.slice(start, start + pageSize).map(prescriptionSummary),
      pageNumber,
      pageSize,
      totalCount: rows.length,
    });
  }),

  http.get('*/api/staff/prescriptions/:prescriptionId', ({ request, params }) => {
    const denied = requireAction(request, 'Prescriptions.View');
    if (denied) return denied;
    const p = findPrescription(params.prescriptionId as string);
    return p ? ok(prescriptionDetail(p, callerRoles(request))) : notFound();
  }),

  http.post('*/api/staff/prescriptions/:prescriptionId/cancel', async ({ request, params }) => {
    const denied = requireAction(request, 'Prescriptions.Issue');
    if (denied) return denied;
    const p = findPrescription(params.prescriptionId as string);
    if (!p) return notFound();
    const { reason } = (await request.json()) as { reason: string };
    if (!reason?.trim())
      return violation(
        null,
        bad('Reason', 'CancellationReasonRequired', 'Give a reason for cancelling.'),
      );
    p.status = 'Cancelled';
    for (const r of p.repeatRequests) {
      if (r.status === 'Requested') {
        r.status = 'Declined';
        r.declineReason = 'The prescription was cancelled.';
        r.decidedOn = new Date().toISOString();
      }
    }
    persist();
    return noContent();
  }),

  http.post('*/api/staff/prescriptions/:prescriptionId/renew', async ({ request, params }) => {
    const denied = requireAction(request, 'Prescriptions.Issue');
    if (denied) return denied;
    const source = findPrescription(params.prescriptionId as string);
    if (!source) return notFound();
    if (source.renewedById)
      return violation(
        null,
        bad(
          'FromPrescriptionId',
          'PrescriptionAlreadyRenewed',
          'This prescription has already been renewed.',
        ),
      );
    const body = (await request.json()) as RenewPrescriptionRequest;
    return create(
      request,
      source.catId,
      source.medicineId,
      { ...body, medicineId: source.medicineId },
      { visitId: body.visitId ?? null, renewFromId: source.id },
    );
  }),

  http.post(
    '*/api/staff/prescriptions/:prescriptionId/repeat-requests/:requestId/decline',
    async ({ request, params }) => {
      const denied = requireAction(request, 'Prescriptions.Issue');
      if (denied) return denied;
      const p = findPrescription(params.prescriptionId as string);
      const repeat = p?.repeatRequests.find((r) => r.id === params.requestId);
      if (!p || !repeat) return notFound();
      const { reason } = (await request.json()) as { reason: string };
      if (!reason?.trim())
        return violation(
          null,
          bad('Reason', 'DeclineReasonRequired', 'Give a reason for declining.'),
        );
      repeat.status = 'Declined';
      repeat.declineReason = reason;
      repeat.decidedOn = new Date().toISOString();
      persist();
      return noContent();
    },
  ),

  http.get('*/api/staff/visits/:visitId/prescriptions', ({ request, params }) => {
    const denied = requireAction(request, 'Prescriptions.View');
    if (denied) return denied;
    return ok({
      prescriptions: db.prescriptions
        .filter((p) => p.visitId === params.visitId)
        .map(prescriptionSummary),
      dietAuthorisations: db.diets
        .filter((d) => d.visitId === params.visitId)
        .map((d) => ({
          id: d.id,
          productName: d.productName,
          validFrom: d.validFrom,
          validTo: d.validTo,
          status: 'Active',
        })),
    });
  }),
];
