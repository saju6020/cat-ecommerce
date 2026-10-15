import { http, HttpResponse, type HttpHandler } from 'msw';
import { findAppointment } from '../appointments/store';
import {
  branchOf,
  branchToday,
  callerRoles,
  conflict,
  emrFixtureIds as ids,
  holds,
  noContent,
  notFound,
  ok,
  requireAction,
  staffNameFor,
  violation,
} from './fixtures';
import {
  allowedActionsFor,
  authorKey,
  filePayload,
  findCode,
  findVisit,
  getDb,
  persist,
  SERVICE_CONSULTATION,
  visitForAppointment,
  zoneForBranch,
  type StoredVisit,
  type StoredVitals,
} from './store';
import { dischargeSummaryFor } from './summary';

/**
 * `mocks/handlers/visits/staff.ts` — HO-EMR-002 §12, `StaffVisitsController` as it is on master:
 * a cross-branch visit is a `404`; `treatments` is always `[]`; a photo's `thumbnailUrl` equals its
 * `url`; the stale-note `409` body is `{ error }`; vitals `measuredOn` is normalised to UTC; and
 * `Visits.Finalise` is the Vet alone. Roles come from the mock staff session (Vitest falls back to
 * `setMockCallerRoles`, default Vet).
 */

interface VitalRule {
  sign: string;
  unit: string;
  absoluteMin: number;
  absoluteMax: number;
  plausibleMin: number | null;
  plausibleMax: number | null;
  decimalPlaces: number;
  field: keyof StoredVitals;
}

/** `VitalSignRules.All` on master. */
export const VITAL_RULES: VitalRule[] = [
  {
    sign: 'Weight',
    unit: 'kg',
    absoluteMin: 0.1,
    absoluteMax: 30,
    plausibleMin: 0.3,
    plausibleMax: 12,
    decimalPlaces: 2,
    field: 'weightKg',
  },
  {
    sign: 'Temperature',
    unit: '°C',
    absoluteMin: 25,
    absoluteMax: 45,
    plausibleMin: 35,
    plausibleMax: 41.5,
    decimalPlaces: 1,
    field: 'temperatureC',
  },
  {
    sign: 'HeartRate',
    unit: 'bpm',
    absoluteMin: 20,
    absoluteMax: 400,
    plausibleMin: 100,
    plausibleMax: 260,
    decimalPlaces: 0,
    field: 'heartRateBpm',
  },
  {
    sign: 'RespiratoryRate',
    unit: 'breaths/min',
    absoluteMin: 4,
    absoluteMax: 150,
    plausibleMin: 12,
    plausibleMax: 80,
    decimalPlaces: 0,
    field: 'respiratoryRatePerMin',
  },
  {
    sign: 'BodyConditionScore',
    unit: '/9',
    absoluteMin: 1,
    absoluteMax: 9,
    plausibleMin: null,
    plausibleMax: null,
    decimalPlaces: 0,
    field: 'bodyConditionScore',
  },
  {
    sign: 'PainScore',
    unit: '/10',
    absoluteMin: 0,
    absoluteMax: 10,
    plausibleMin: null,
    plausibleMax: null,
    decimalPlaces: 0,
    field: 'painScore',
  },
];

const fmt = (rule: VitalRule, value: number) => value.toFixed(rule.decimalPlaces);

function measurement(rule: VitalRule, value: number | null) {
  return value === null ? null : { value: fmt(rule, value), unit: rule.unit };
}

function vitalsView(v: StoredVitals) {
  const get = (sign: string) => VITAL_RULES.find((r) => r.sign === sign)!;
  return {
    id: v.id,
    measuredOn: v.measuredOn,
    weight: measurement(get('Weight'), v.weightKg),
    temperature: measurement(get('Temperature'), v.temperatureC),
    heartRate: measurement(get('HeartRate'), v.heartRateBpm),
    respiratoryRate: measurement(get('RespiratoryRate'), v.respiratoryRatePerMin),
    bodyConditionScore: measurement(get('BodyConditionScore'), v.bodyConditionScore),
    painScore: measurement(get('PainScore'), v.painScore),
    confirmedOutOfRange: v.confirmedOutOfRange,
    note: v.note,
    recordedBy: v.recordedBy,
    recordedOn: v.recordedOn,
  };
}

export function vitalsViewFor(v: StoredVitals) {
  return vitalsView(v);
}

function visitInBranch(request: Request, visitId: string): StoredVisit | undefined {
  const visit = findVisit(visitId);
  return visit && visit.branchId === branchOf(request) ? visit : undefined;
}

function problemsView(visit: StoredVisit) {
  return visit.problems.map((p) => {
    const code = findCode(p.diagnosisCodeId);
    return {
      id: p.id,
      diagnosisCodeId: p.diagnosisCodeId,
      code: code?.code ?? '',
      name: code?.name ?? '',
      certainty: p.certainty,
      note: p.note,
    };
  });
}

function orderSummaries(visit: StoredVisit) {
  return visit.orderIds
    .map((id) => getDb().orders.find((o) => o.id === id))
    .filter((o): o is NonNullable<typeof o> => !!o)
    .map((o) => ({
      id: o.id,
      kind: o.kind,
      status: o.status,
      orderedOn: o.orderedOn,
      resultSetCount: o.resultSets.length,
      hasAbnormal: o.resultSets.some((s) =>
        s.values.some((v) => ['Low', 'High', 'Abnormal'].includes(v.flag)),
      ),
    }));
}

function detailView(visit: StoredVisit, request: Request) {
  const roles = callerRoles(request);
  const key = authorKey(roles);
  const template =
    visit.status === 'Open' && visit.note.savedOn === null
      ? getDb().templates.find((t) => t.serviceId === visit.serviceId && t.isActive)
      : undefined;
  const draft = visit.status === 'Open' ? visit.drafts[key] : undefined;
  const draftVisible = draft && (visit.note.savedOn === null || draft.savedOn > visit.note.savedOn);

  return {
    id: visit.id,
    version: visit.version,
    status: visit.status,
    catId: visit.catId,
    catName: visit.catName,
    appointment: {
      id: visit.appointmentId,
      // Live from the Appointments mock when the appointment is one of its own.
      status: findAppointment(visit.appointmentId)?.status ?? visit.appointmentStatus,
      startsOn: visit.appointmentStartsOn,
      performerName: visit.performerName,
    },
    service: { id: visit.serviceId, name: visit.serviceName, category: visit.serviceCategory },
    openedOn: visit.openedOn,
    openedBy: visit.openedBy,
    branchTimeZoneId: zoneForBranch(visit.branchId),
    allowedActions: allowedActionsFor(visit, (action) => holds(roles, action)),
    note: { ...visit.note },
    problems: problemsView(visit),
    vitals: [...visit.vitals]
      .sort((a, b) => a.measuredOn.localeCompare(b.measuredOn))
      .map(vitalsView),
    treatments: [] as unknown[],
    photos: visit.photos.map((p) => {
      const file = filePayload(p.fileId, p.fileName, p.contentType);
      return {
        id: p.id,
        takenOn: p.takenOn,
        note: p.note,
        addedOn: p.addedOn,
        url: file.url,
        thumbnailUrl: file.url,
        contentType: file.contentType,
        urlExpiresOn: file.urlExpiresOn,
      };
    }),
    followUp: visit.followUp,
    addenda: visit.addenda,
    diagnostics: orderSummaries(visit),
    template: template
      ? {
          id: template.id,
          version: template.templateVersion,
          name: template.name,
          subjective: template.subjective,
          objective: template.objective,
          assessment: template.assessment,
          plan: template.plan,
          dischargeInstructions: template.dischargeInstructions,
        }
      : null,
    draft: draftVisible
      ? {
          subjective: draft.subjective,
          objective: draft.objective,
          assessment: draft.assessment,
          plan: draft.plan,
          dischargeInstructions: draft.dischargeInstructions,
          problemCodeIds: draft.problemCodeIds,
          savedOn: draft.savedOn,
        }
      : null,
    finalisedOn: visit.finalisedOn,
    finalisedBy: visit.finalisedBy,
  };
}

const TOO_LONG = (name: string, max: number) => ({
  propertyName: name,
  errorCode: 'ClinicalTextTooLong',
  errorMessage: `Keep ${name} to ${max} characters or fewer.`,
});

const TOLERANCE_MS = 5 * 60_000;

export const visitStaffHandlers: HttpHandler[] = [
  http.post('*/api/staff/visits', async ({ request }) => {
    const refused = requireAction(request, 'Visits.Open');
    if (refused) return refused;
    const { appointmentId } = (await request.json()) as { appointmentId: string };
    const appointment = findAppointment(appointmentId);
    if (!appointment || appointment.branchId !== branchOf(request)) return notFound();
    const existing = visitForAppointment(appointmentId);
    if (existing) return ok({ visitId: existing.id });
    if (appointment.status !== 'CheckedIn' && appointment.status !== 'InConsultation') {
      return violation(null, {
        propertyName: 'AppointmentId',
        errorCode: 'AppointmentNotReadyForVisit',
        errorMessage: 'The appointment must be checked in before a visit can be opened.',
      });
    }
    const id = crypto.randomUUID();
    const now = new Date().toISOString();
    getDb().visits.push({
      id,
      version: 1,
      status: 'Open',
      catId: appointment.catId,
      catName: appointment.catName,
      branchId: appointment.branchId,
      appointmentId,
      appointmentStatus: appointment.status,
      appointmentStartsOn: appointment.startsOn,
      performerName: appointment.performerId ? ids.vetStaffName : null,
      serviceId: SERVICE_CONSULTATION.id,
      serviceName: appointment.service.name,
      serviceCategory: appointment.service.category,
      openedOn: now,
      openedBy: staffNameFor(callerRoles(request)),
      note: {
        subjective: '',
        objective: '',
        assessment: '',
        plan: '',
        dischargeInstructions: '',
        savedOn: null,
        savedBy: null,
        appliedTemplateId: null,
        appliedTemplateVersion: null,
      },
      problems: [],
      vitals: [],
      photos: [],
      followUp: null,
      addenda: [],
      orderIds: [],
      drafts: {},
      finalisedOn: null,
      finalisedBy: null,
    });
    persist();
    return ok({ visitId: id }, 201);
  }),

  http.get('*/api/staff/appointments/:appointmentId/visit', ({ request, params }) => {
    const refused = requireAction(request, 'Visits.View');
    if (refused) return refused;
    const visit = visitForAppointment(params.appointmentId as string);
    return ok(
      visit && visit.branchId === branchOf(request)
        ? { visitId: visit.id, status: visit.status }
        : { visitId: null, status: null },
    );
  }),

  http.get('*/api/staff/visits', ({ request }) => {
    const refused = requireAction(request, 'Visits.View');
    if (refused) return refused;
    const url = new URL(request.url);
    const status = url.searchParams.get('status') ?? 'Open';
    if (status !== 'Open' && status !== 'Finalised') {
      return HttpResponse.json(
        { error: `'${status}' is not a recognised visit status.` },
        { status: 400 },
      );
    }
    const rows = getDb()
      .visits.filter((v) => v.branchId === branchOf(request) && v.status === status)
      .sort((a, b) => b.openedOn.localeCompare(a.openedOn))
      .map((v) => ({
        id: v.id,
        appointmentId: v.appointmentId,
        cat: { id: v.catId, name: v.catName, isHighRisk: v.catId === ids.catTigerId },
        service: v.serviceName,
        openedOn: v.openedOn,
        status: v.status,
        hasVitals: v.vitals.length > 0,
        hasNote: v.note.savedOn !== null,
      }));
    return ok(rows);
  }),

  http.get('*/api/staff/visits/:visitId', ({ request, params }) => {
    const refused = requireAction(request, 'Visits.View');
    if (refused) return refused;
    const visit = visitInBranch(request, params.visitId as string);
    return visit ? ok(detailView(visit, request)) : notFound();
  }),

  http.get('*/api/staff/clinical/vital-sign-rules', ({ request }) => {
    const refused = requireAction(request, 'Visits.View');
    if (refused) return refused;
    return ok(
      VITAL_RULES.map((r) => ({
        sign: r.sign,
        unit: r.unit,
        absoluteMin: fmt(r, r.absoluteMin),
        absoluteMax: fmt(r, r.absoluteMax),
        plausibleMin: r.plausibleMin === null ? null : fmt(r, r.plausibleMin),
        plausibleMax: r.plausibleMax === null ? null : fmt(r, r.plausibleMax),
        decimalPlaces: r.decimalPlaces,
      })),
    );
  }),

  http.post('*/api/staff/visits/:visitId/vitals', async ({ request, params }) => {
    const refused = requireAction(request, 'Visits.RecordVitals');
    if (refused) return refused;
    const visit = visitInBranch(request, params.visitId as string);
    if (!visit) return notFound();
    if (visit.status === 'Finalised') {
      return violation(null, {
        propertyName: 'VisitId',
        errorCode: 'VisitFinalised',
        errorMessage:
          'This visit has been finalised and can no longer be changed. Add an addendum instead.',
      });
    }
    const body = (await request.json()) as Record<string, unknown>;
    const confirmed = ((body.confirmedOutOfRange as string[] | undefined) ?? []) as string[];
    const values = new Map<string, number>();
    for (const rule of VITAL_RULES) {
      const raw =
        rule.sign === 'Weight'
          ? body.weightKg
          : rule.sign === 'Temperature'
            ? body.temperatureC
            : rule.sign === 'HeartRate'
              ? body.heartRateBpm
              : rule.sign === 'RespiratoryRate'
                ? body.respiratoryRatePerMin
                : rule.sign === 'BodyConditionScore'
                  ? body.bodyConditionScore
                  : body.painScore;
      if (typeof raw === 'number') values.set(rule.sign, raw);
    }
    if (values.size === 0) {
      return violation(null, {
        propertyName: 'WeightKg',
        errorCode: 'VitalsRequired',
        errorMessage: 'Enter at least one measurement.',
      });
    }
    const measuredMs = body.measuredOn ? Date.parse(String(body.measuredOn)) : Date.now();
    if (Number.isNaN(measuredMs) || measuredMs > Date.now() + TOLERANCE_MS) {
      return violation(null, {
        propertyName: 'MeasuredOn',
        errorCode: 'ClinicalDateInFuture',
        errorMessage: 'The measurement time cannot be in the future.',
      });
    }
    for (const rule of VITAL_RULES) {
      const value = values.get(rule.sign);
      if (value !== undefined && (value < rule.absoluteMin || value > rule.absoluteMax)) {
        return violation(null, {
          propertyName: rule.sign,
          errorCode: 'VitalSignOutOfRange',
          errorMessage: `${rule.sign} must be between ${fmt(rule, rule.absoluteMin)} and ${fmt(rule, rule.absoluteMax)} ${rule.unit}. Check the value and try again.`,
        });
      }
    }
    const warnings = VITAL_RULES.filter((rule) => {
      const value = values.get(rule.sign);
      return (
        value !== undefined &&
        rule.plausibleMin !== null &&
        rule.plausibleMax !== null &&
        (value < rule.plausibleMin || value > rule.plausibleMax)
      );
    });
    const unconfirmed = warnings.filter((rule) => !confirmed.includes(rule.sign));
    if (unconfirmed.length > 0) {
      return violation(
        {
          outOfRange: unconfirmed.map((rule) => ({
            sign: rule.sign,
            value: fmt(rule, values.get(rule.sign)!),
            unit: rule.unit,
            plausibleMin: fmt(rule, rule.plausibleMin!),
            plausibleMax: fmt(rule, rule.plausibleMax!),
          })),
        },
        {
          propertyName: unconfirmed[0]!.sign,
          errorCode: 'VitalsOutsidePlausibleRange',
          errorMessage: `Some values are outside the usual range. Check them, then confirm to save anyway. (${unconfirmed.map((r) => r.sign).join(', ')})`,
        },
      );
    }
    const entry: StoredVitals = {
      id: crypto.randomUUID(),
      // §12.4 amendment: normalised to UTC by the server.
      measuredOn: new Date(measuredMs).toISOString(),
      weightKg: values.get('Weight') ?? null,
      temperatureC: values.get('Temperature') ?? null,
      heartRateBpm: values.get('HeartRate') ?? null,
      respiratoryRatePerMin: values.get('RespiratoryRate') ?? null,
      bodyConditionScore: values.get('BodyConditionScore') ?? null,
      painScore: values.get('PainScore') ?? null,
      confirmedOutOfRange: warnings.map((rule) => rule.sign),
      note: (body.note as string | undefined) ?? null,
      recordedBy: staffNameFor(callerRoles(request)),
      recordedOn: new Date().toISOString(),
    };
    visit.vitals.push(entry);
    visit.version += 1;
    persist();
    return ok({ vitalsEntryId: entry.id }, 201);
  }),

  http.put('*/api/staff/visits/:visitId/draft', async ({ request, params }) => {
    const refused = requireAction(request, 'Visits.WriteNote');
    if (refused) return refused;
    const visit = visitInBranch(request, params.visitId as string);
    if (!visit) return notFound();
    if (visit.status === 'Finalised') {
      return violation(null, {
        propertyName: 'VisitId',
        errorCode: 'VisitFinalised',
        errorMessage:
          'This visit has been finalised and can no longer be changed. Add an addendum instead.',
      });
    }
    const body = (await request.json()) as Record<string, unknown>;
    visit.drafts[authorKey(callerRoles(request))] = {
      subjective: String(body.subjective ?? ''),
      objective: String(body.objective ?? ''),
      assessment: String(body.assessment ?? ''),
      plan: String(body.plan ?? ''),
      dischargeInstructions: String(body.dischargeInstructions ?? ''),
      problemCodeIds: (body.problemCodeIds as string[] | undefined) ?? [],
      savedOn: new Date().toISOString(),
    };
    persist();
    return noContent();
  }),

  http.put('*/api/staff/visits/:visitId/note', async ({ request, params }) => {
    const refused = requireAction(request, 'Visits.WriteNote');
    if (refused) return refused;
    const visit = visitInBranch(request, params.visitId as string);
    if (!visit) return notFound();
    const body = (await request.json()) as {
      expectedVersion: number;
      subjective?: string;
      objective?: string;
      assessment?: string;
      plan?: string;
      dischargeInstructions?: string;
      problems?: Array<{
        problemId?: string;
        diagnosisCodeId: string;
        certainty: 'Provisional' | 'Confirmed';
        note?: string;
      }>;
      appliedTemplateId?: string;
      appliedTemplateVersion?: number;
    };
    // Finalised-before-stale (backend commit 2f2e597e): a finalised visit is `VisitFinalised`, never a `409`.
    if (visit.status === 'Finalised') {
      return violation(null, {
        propertyName: 'VisitId',
        errorCode: 'VisitFinalised',
        errorMessage:
          'This visit has been finalised and can no longer be changed. Add an addendum instead.',
      });
    }
    if (body.expectedVersion !== visit.version)
      return conflict('This visit was changed by someone else. Reload and try again.');
    for (const [name, max] of [
      ['Subjective', 20000],
      ['Objective', 20000],
      ['Assessment', 20000],
      ['Plan', 20000],
      ['DischargeInstructions', 10000],
    ] as const) {
      const value = body[(name[0]!.toLowerCase() + name.slice(1)) as 'subjective'] ?? '';
      if (value.length > max) return violation(null, TOO_LONG(name, max));
    }
    const problems = body.problems ?? [];
    if (problems.length > 20) {
      return violation(null, {
        propertyName: 'Problems',
        errorCode: 'ClinicalLimitExceeded',
        errorMessage: 'A visit holds at most 20 problems.',
      });
    }
    const seen = new Set<string>();
    for (const problem of problems) {
      const code = findCode(problem.diagnosisCodeId);
      if (!code)
        return violation(null, {
          propertyName: 'Problems',
          errorCode: 'DiagnosisCodeNotFound',
          errorMessage: 'That diagnosis code does not exist.',
        });
      if (!code.isActive)
        return violation(null, {
          propertyName: 'Problems',
          errorCode: 'DiagnosisCodeInactive',
          errorMessage: `${code.name} has been retired. Choose a current code.`,
        });
      if (seen.has(code.id))
        return violation(null, {
          propertyName: 'Problems',
          errorCode: 'DuplicateProblem',
          errorMessage: `${code.name} is already on the problem list.`,
        });
      seen.add(code.id);
    }
    const savedBy = staffNameFor(callerRoles(request));
    visit.note = {
      subjective: body.subjective ?? '',
      objective: body.objective ?? '',
      assessment: body.assessment ?? '',
      plan: body.plan ?? '',
      dischargeInstructions: body.dischargeInstructions ?? '',
      savedOn: new Date().toISOString(),
      savedBy,
      appliedTemplateId: body.appliedTemplateId ?? visit.note.appliedTemplateId,
      appliedTemplateVersion: body.appliedTemplateVersion ?? visit.note.appliedTemplateVersion,
    };
    visit.problems = problems.map((p) => ({
      id: p.problemId ?? crypto.randomUUID(),
      diagnosisCodeId: p.diagnosisCodeId,
      certainty: p.certainty,
      note: p.note ?? null,
    }));
    visit.version += 1;
    persist();
    return ok({ version: visit.version, savedOn: visit.note.savedOn });
  }),

  http.post('*/api/staff/visits/:visitId/finalise', async ({ request, params }) => {
    const refused = requireAction(request, 'Visits.Finalise');
    if (refused) return refused;
    const visit = visitInBranch(request, params.visitId as string);
    if (!visit) return notFound();
    const body = (await request.json()) as { expectedVersion: number };
    if (visit.status === 'Finalised') {
      return violation(null, {
        propertyName: 'VisitId',
        errorCode: 'VisitFinalised',
        errorMessage:
          'This visit has been finalised and can no longer be changed. Add an addendum instead.',
      });
    }
    if (body.expectedVersion !== visit.version)
      return conflict('This visit was changed by someone else. Reload and try again.');
    if (visit.problems.length === 0) {
      return violation(null, {
        propertyName: 'Problems',
        errorCode: 'ProblemRequired',
        errorMessage:
          'Add at least one diagnosis before finalising. Use "Routine visit — no abnormality detected" for a well visit.',
      });
    }
    if (!visit.note.assessment.trim() || !visit.note.plan.trim()) {
      return violation(null, {
        propertyName: 'Plan',
        errorCode: 'AssessmentAndPlanRequired',
        errorMessage: 'Write the assessment and the plan before finalising.',
      });
    }
    visit.status = 'Finalised';
    visit.finalisedOn = new Date().toISOString();
    visit.finalisedBy = staffNameFor(callerRoles(request));
    visit.version += 1;
    persist();
    return noContent();
  }),

  http.post('*/api/staff/visits/:visitId/addenda', async ({ request, params }) => {
    const refused = requireAction(request, 'Visits.AddAddendum');
    if (refused) return refused;
    const visit = visitInBranch(request, params.visitId as string);
    if (!visit) return notFound();
    const body = (await request.json()) as { text: string; reason?: string };
    if (visit.status !== 'Finalised') {
      return violation(null, {
        propertyName: 'VisitId',
        errorCode: 'AddendumRequiresFinalisedVisit',
        errorMessage: 'This visit is still open — edit the note instead.',
      });
    }
    if (!body.text?.trim() || body.text.length > 4000) {
      return violation(null, {
        propertyName: 'Text',
        errorCode: 'ClinicalTextTooLong',
        errorMessage: 'Write the addendum, up to 4000 characters.',
      });
    }
    const id = crypto.randomUUID();
    visit.addenda.push({
      id,
      text: body.text,
      reason: body.reason ?? null,
      author: staffNameFor(callerRoles(request)),
      addedOn: new Date().toISOString(),
    });
    persist();
    return ok({ addendumId: id }, 201);
  }),

  http.post('*/api/staff/visits/:visitId/photos', async ({ request, params }) => {
    const refused = requireAction(request, 'Visits.WriteNote');
    if (refused) return refused;
    const visit = visitInBranch(request, params.visitId as string);
    if (!visit) return notFound();
    const body = (await request.json()) as { fileId: string; takenOn: string; note?: string };
    if (visit.status === 'Finalised') {
      return violation(null, {
        propertyName: 'VisitId',
        errorCode: 'VisitFinalised',
        errorMessage:
          'This visit has been finalised and can no longer be changed. Add an addendum instead.',
      });
    }
    const info = getDb().files[body.fileId];
    if (!info)
      return violation(null, {
        propertyName: 'FileId',
        errorCode: 'FileNotFound',
        errorMessage: 'That file could not be found. Upload it again.',
      });
    if (visit.photos.length >= 20) {
      return violation(null, {
        propertyName: 'FileId',
        errorCode: 'ClinicalLimitExceeded',
        errorMessage: 'A visit holds at most 20 photos.',
      });
    }
    const id = crypto.randomUUID();
    visit.photos.push({
      id,
      fileId: body.fileId,
      fileName: info.name,
      contentType: info.type,
      takenOn: body.takenOn,
      note: body.note ?? null,
      addedOn: new Date().toISOString(),
    });
    visit.version += 1;
    persist();
    return ok({ photoId: id }, 201);
  }),

  http.put('*/api/staff/visits/:visitId/follow-up', async ({ request, params }) => {
    const refused = requireAction(request, 'Visits.WriteNote');
    if (refused) return refused;
    const visit = visitInBranch(request, params.visitId as string);
    if (!visit) return notFound();
    const body = (await request.json()) as {
      kind: 'Appointment' | 'Reminder';
      dueOn: string;
      appointmentId?: string;
    };
    if (visit.status === 'Finalised') {
      return violation(null, {
        propertyName: 'VisitId',
        errorCode: 'VisitFinalised',
        errorMessage:
          'This visit has been finalised and can no longer be changed. Add an addendum instead.',
      });
    }
    const today = branchToday();
    if (body.kind === 'Reminder' && body.dueOn <= today) {
      return violation(null, {
        propertyName: 'DueOn',
        errorCode: 'FollowUpDateInPast',
        errorMessage: 'Choose a follow-up date after today.',
      });
    }
    if (
      body.kind === 'Appointment' &&
      (!body.appointmentId || !findAppointment(body.appointmentId))
    ) {
      return violation(null, {
        propertyName: 'AppointmentId',
        errorCode: 'FollowUpAppointmentInvalid',
        errorMessage: 'That appointment cannot be used as the follow-up for this visit.',
      });
    }
    visit.followUp = {
      kind: body.kind,
      dueOn: body.dueOn,
      appointmentId: body.appointmentId ?? null,
      setOn: new Date().toISOString(),
    };
    visit.version += 1;
    persist();
    return noContent();
  }),

  http.delete('*/api/staff/visits/:visitId/follow-up', ({ request, params }) => {
    const refused = requireAction(request, 'Visits.WriteNote');
    if (refused) return refused;
    const visit = visitInBranch(request, params.visitId as string);
    if (!visit) return notFound();
    visit.followUp = null;
    visit.version += 1;
    persist();
    return noContent();
  }),

  http.get('*/api/staff/visits/:visitId/discharge-summary', ({ request, params }) => {
    const refused = requireAction(request, 'Visits.PrintSummary');
    if (refused) return refused;
    const visit = visitInBranch(request, params.visitId as string);
    if (!visit || visit.status !== 'Finalised') return notFound();
    return ok(dischargeSummaryFor(visit));
  }),

  http.get('*/api/staff/visits/:visitId/discharge-summary.pdf', ({ request, params }) => {
    const refused = requireAction(request, 'Visits.PrintSummary');
    if (refused) return refused;
    const visit = visitInBranch(request, params.visitId as string);
    if (!visit || visit.status !== 'Finalised') return notFound();
    return pdfResponse();
  }),
];

export function pdfResponse() {
  return new HttpResponse('%PDF-1.4\n% mock discharge summary\n', {
    status: 200,
    headers: { 'Content-Type': 'application/pdf', 'Cache-Control': 'private, no-store' },
  });
}
