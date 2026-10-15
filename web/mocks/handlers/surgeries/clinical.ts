import { http, type HttpHandler } from 'msw';
import type { ChecklistKind, DischargeRequest } from '@/lib/api/surgeries';
import {
  actorFor,
  bad,
  branchOf,
  callerRoles,
  noContent,
  notFound,
  ok,
  requireAction,
  violation,
  zoneOfBranch,
} from './fixtures';
import {
  DEFAULT_POST_OP,
  addDays,
  findCat,
  findService,
  findSurgery,
  getDb,
  localDateOf,
  newId,
  persist,
  todayIn,
  type MProtocol,
  type MSurgery,
} from './store';
import {
  allowedTransitions,
  checklistSummary,
  consentRecorded,
  detailOf,
  startBlockers,
} from './views';

/**
 * `mocks/handlers/surgeries/clinical.ts` — the pre-op, theatre, recovery and discharge half of
 * `StaffSurgeriesController` (HO-SRG-001 §8.3, §8.6 to §8.10). The mock plays the SERVER: the start gate,
 * the override (vet only, reason of at least 10 characters), the neuter status after a completed
 * sterilisation, and the discharge window rules are decided here.
 */
function find(request: Request, id: string): MSurgery | null {
  const s = findSurgery(id);
  return s && s.branchId === branchOf(request) ? s : null;
}

function notAllowed(action: string, s: MSurgery) {
  return violation(
    bad(
      'Status',
      'InvalidSurgeryTransition',
      `A surgery that is ${s.status.toLowerCase()} cannot be ${action}.`,
    ),
  );
}

function setStatus(s: MSurgery, to: MSurgery['status'], by: string) {
  s.statusHistory.push({ from: s.status, to, changedOn: new Date().toISOString(), changedBy: by });
  s.status = to;
  s.version += 1;
}

function protocolFor(serviceId: string): MProtocol {
  return getDb().protocols.find((p) => p.serviceId === serviceId)!;
}

function plannedFollowUps(s: MSurgery) {
  const protocol = protocolFor(s.serviceId);
  const base = s.procedureEndedOn
    ? localDateOf(s.procedureEndedOn, zoneOfBranch(s.branchId))
    : todayIn(zoneOfBranch(s.branchId));
  return protocol.followUps.map((f) => ({
    kind: f.kind,
    dueOn: addDays(base, f.dayOffset),
    windowFrom: addDays(base, f.windowFromDay),
    windowTo: addDays(base, f.windowToDay),
  }));
}

function applyNeuter(s: MSurgery): void {
  const service = findService(s.serviceId);
  const cat = findCat(s.catId);
  if (!service?.isSterilisation || !cat) return;
  cat.neuterStatus = 'Neutered';
  cat.neuteredOn = localDateOf(
    s.procedureEndedOn ?? new Date().toISOString(),
    zoneOfBranch(s.branchId),
  );
  s.neuterApplied = true;
}

export const clinicalHandlers: HttpHandler[] = [
  http.get('*/api/staff/surgeries/recovery-board', ({ request }) => {
    const refused = requireAction(request, 'Surgery.View');
    if (refused) return refused;
    const branchId = branchOf(request);
    const entries = getDb()
      .surgeries.filter(
        (s) => s.branchId === branchId && s.status === 'Recovery' && s.recoveryStatus,
      )
      .map((s) => {
        const last = s.recoveryHistory.at(-1);
        const customer = detailOf(s);
        return {
          surgeryId: s.id,
          catId: s.catId,
          catName: findCat(s.catId)?.name ?? 'Cat',
          catIsHighRisk: findCat(s.catId)?.isHighRisk ?? false,
          customerName: customer.customerName,
          customerPhone: customer.customerPhone,
          procedure: findService(s.serviceId)!.name,
          procedureEndedOn: s.procedureEndedOn,
          recoveryStatus: s.recoveryStatus!,
          statusChangedOn: last?.changedOn ?? null,
          statusChangedBy: last?.changedBy ?? null,
          statusChangedByName: last?.changedByName ?? null,
        };
      });
    return ok({ generatedOn: new Date().toISOString(), entries });
  }),

  http.post('*/api/staff/surgeries/:id/checklist/:kind/complete', async ({ request, params }) => {
    const refused = requireAction(request, 'Surgery.ManageChecklist');
    if (refused) return refused;
    const s = find(request, params.id as string);
    if (!s) return notFound();
    const kind = params.kind as ChecklistKind;
    const item = s.checklist.find((i) => i.kind === kind);
    if (!item)
      return violation(
        bad('Kind', 'ChecklistItemNotManual', 'That checklist item is completed automatically.'),
      );
    if (kind === 'ConsentSigned' || kind === 'AsaGradeAssessed') {
      return violation(
        bad('Kind', 'ChecklistItemNotManual', 'That checklist item is completed automatically.'),
      );
    }
    if (item.status === 'Done') {
      return violation(bad('Kind', 'ChecklistItemAlreadyDone', 'That item is already done.'));
    }
    const body = (await request.json().catch(() => ({}))) as { note?: string };
    const actor = actorFor(callerRoles(request));
    item.status = 'Done';
    item.completedBy = actor.id;
    item.completedOn = new Date().toISOString();
    item.note = body.note?.trim() || null;
    s.version += 1;
    persist();
    return noContent();
  }),

  http.post('*/api/staff/surgeries/:id/checklist/:kind/waive', async ({ request, params }) => {
    const refused = requireAction(request, 'Surgery.AssessRisk');
    if (refused) return refused;
    const s = find(request, params.id as string);
    if (!s) return notFound();
    const kind = params.kind as ChecklistKind;
    if (kind !== 'PreAnaestheticBloodWork') {
      return violation(
        bad('Kind', 'ChecklistItemNotWaivable', 'That checklist item cannot be waived.'),
      );
    }
    const body = (await request.json()) as { reason?: string };
    if (!body.reason?.trim()) {
      return violation(bad('Reason', 'NotEmptyValidator', 'Give the reason for waiving it.'));
    }
    const item = s.checklist.find((i) => i.kind === kind)!;
    item.status = 'Waived';
    item.completedBy = actorFor(callerRoles(request)).id;
    item.completedOn = new Date().toISOString();
    item.waiverReason = body.reason.trim();
    s.version += 1;
    persist();
    return noContent();
  }),

  http.put('*/api/staff/surgeries/:id/asa-grade', async ({ request, params }) => {
    const refused = requireAction(request, 'Surgery.AssessRisk');
    if (refused) return refused;
    const s = find(request, params.id as string);
    if (!s) return notFound();
    const body = (await request.json()) as { grade: MSurgery['asaGrade']; isEmergency: boolean };
    s.asaGrade = body.grade;
    s.asaIsEmergency = body.isEmergency;
    const item = s.checklist.find((i) => i.kind === 'AsaGradeAssessed')!;
    item.status = 'Done';
    item.completedBy = actorFor(callerRoles(request)).id;
    item.completedOn = new Date().toISOString();
    s.version += 1;
    persist();
    return noContent();
  }),

  http.post('*/api/staff/surgeries/:id/admit', ({ request, params }) => {
    const refused = requireAction(request, 'Surgery.Admit');
    if (refused) return refused;
    const s = find(request, params.id as string);
    if (!s) return notFound();
    if (!allowedTransitions(s.status).includes('Admit')) return notAllowed('admitted', s);
    const zone = zoneOfBranch(s.branchId);
    if (localDateOf(s.startsOn, zone) !== todayIn(zone)) {
      return violation(
        bad(
          'SurgeryId',
          'AdmissionNotToday',
          'The cat can only be admitted on the day of surgery.',
        ),
      );
    }
    s.admittedOn = new Date().toISOString();
    setStatus(s, 'PreOp', actorFor(callerRoles(request)).id);
    persist();
    return noContent();
  }),

  http.post('*/api/staff/surgeries/:id/start', ({ request, params }) => {
    const refused = requireAction(request, 'Surgery.Start');
    if (refused) return refused;
    const s = find(request, params.id as string);
    if (!s) return notFound();
    if (!allowedTransitions(s.status).includes('Start')) return notAllowed('started', s);
    const blockers = startBlockers(s);
    if (blockers.length > 0) {
      return violation(...blockers.map((b) => bad('SurgeryId', b.code, b.message)));
    }
    s.procedureStartedOn = new Date().toISOString();
    setStatus(s, 'InTheatre', actorFor(callerRoles(request)).id);
    persist();
    return noContent();
  }),

  http.post(
    '*/api/staff/surgeries/:id/start-with-consent-override',
    async ({ request, params }) => {
      const refused = requireAction(request, 'Surgery.OverrideConsent');
      if (refused) return refused;
      const s = find(request, params.id as string);
      if (!s) return notFound();
      if (!allowedTransitions(s.status).includes('Start')) return notAllowed('started', s);
      const body = (await request.json()) as { reason?: string };
      const reason = body.reason?.trim() ?? '';
      const hadConsent = consentRecorded(s);
      if (!hadConsent && reason.length < 10) {
        return violation(
          bad(
            'Reason',
            'ConsentOverrideReasonRequired',
            'Give the reason for starting without consent (at least 10 characters).',
          ),
        );
      }
      const others = checklistSummary(s).outstandingItems.filter((k) => k !== 'ConsentSigned');
      if (others.length > 0) {
        return violation(
          bad('SurgeryId', 'PreOpChecklistIncomplete', 'The pre-op checklist is not complete.'),
        );
      }
      const actor = actorFor(callerRoles(request));
      if (!hadConsent) {
        s.consentOverride = {
          overriddenBy: actor.id,
          overriddenByName: actor.name,
          overriddenOn: new Date().toISOString(),
          reason,
        };
      }
      s.procedureStartedOn = new Date().toISOString();
      setStatus(s, 'InTheatre', actor.id);
      persist();
      return hadConsent ? ok({ consentWasOverridden: false, overrideIgnored: true }) : noContent();
    },
  ),

  http.post('*/api/staff/surgeries/:id/end-procedure', async ({ request, params }) => {
    const refused = requireAction(request, 'Surgery.EndProcedure');
    if (refused) return refused;
    const s = find(request, params.id as string);
    if (!s) return notFound();
    if (!allowedTransitions(s.status).includes('EndProcedure')) return notAllowed('ended', s);
    const body = (await request.json()) as { outcome: 'Completed' | 'Abandoned' };
    const actor = actorFor(callerRoles(request));
    s.outcome = body.outcome;
    s.procedureEndedOn = new Date().toISOString();
    setStatus(s, 'Recovery', actor.id);
    s.recoveryStatus = 'Recovering';
    s.recoveryHistory.push({
      from: null,
      to: 'Recovering',
      changedOn: new Date().toISOString(),
      changedBy: actor.id,
      changedByName: actor.name,
      note: null,
    });
    if (body.outcome === 'Completed') applyNeuter(s);
    persist();
    return noContent();
  }),

  http.post('*/api/staff/surgeries/:id/apply-sterilisation-outcome', ({ request, params }) => {
    const refused = requireAction(request, 'Surgery.WriteReport');
    if (refused) return refused;
    const s = find(request, params.id as string);
    if (!s) return notFound();
    applyNeuter(s);
    persist();
    return noContent();
  }),

  http.put('*/api/staff/surgeries/:id/recovery-status', async ({ request, params }) => {
    const refused = requireAction(request, 'Surgery.ManageRecovery');
    if (refused) return refused;
    const s = find(request, params.id as string);
    if (!s) return notFound();
    if (!allowedTransitions(s.status).includes('SetRecoveryStatus'))
      return notAllowed('given a recovery status', s);
    if (s.recoveryHistory.length >= 50) {
      return violation(
        bad(
          'Status',
          'RecoveryHistoryLimitReached',
          'This surgery has reached its limit of recovery status changes.',
        ),
      );
    }
    const body = (await request.json()) as {
      status: NonNullable<MSurgery['recoveryStatus']>;
      note?: string;
    };
    const actor = actorFor(callerRoles(request));
    s.recoveryHistory.push({
      from: s.recoveryStatus,
      to: body.status,
      changedOn: new Date().toISOString(),
      changedBy: actor.id,
      changedByName: actor.name,
      note: body.note?.trim() || null,
    });
    s.recoveryStatus = body.status;
    s.version += 1;
    persist();
    return noContent();
  }),

  http.get('*/api/staff/surgeries/:id/discharge-draft', ({ request, params }) => {
    const refused = requireAction(request, 'Surgery.ManageRecovery');
    if (refused) return refused;
    const s = find(request, params.id as string);
    if (!s) return notFound();
    return ok({
      surgeryId: s.id,
      status: s.status,
      recoveryStatus: s.recoveryStatus,
      catIsDeceased: false,
      postOpInstructions: protocolFor(s.serviceId).postOpInstructions || DEFAULT_POST_OP,
      followUps: plannedFollowUps(s),
    });
  }),

  http.post('*/api/staff/surgeries/:id/discharge', async ({ request, params }) => {
    const refused = requireAction(request, 'Surgery.ManageRecovery');
    if (refused) return refused;
    const s = find(request, params.id as string);
    if (!s) return notFound();
    if (!allowedTransitions(s.status).includes('Discharge')) return notAllowed('discharged', s);
    if (s.recoveryStatus !== 'ReadyForDischarge') {
      return violation(
        bad(
          'RecoveryStatus',
          'NotReadyForDischarge',
          'The cat is not ready for discharge. Set the recovery status to ready for discharge first.',
        ),
      );
    }
    const body = ((await request.json().catch(() => ({}))) ?? {}) as DischargeRequest;
    const plan = plannedFollowUps(s);
    const chosen = body.followUps ?? plan.map((p) => ({ kind: p.kind, dueOn: p.dueOn }));
    const errors = [];
    const seen = new Set<string>();
    for (const item of chosen) {
      const planned = plan.find((p) => p.kind === item.kind);
      if (!planned || seen.has(item.kind)) {
        errors.push(
          bad(
            'followUps',
            'FollowUpOutsideWindow',
            `This procedure has no ${item.kind.toLowerCase()} follow-up.`,
          ),
        );
        continue;
      }
      seen.add(item.kind);
      if (item.dueOn < planned.windowFrom || item.dueOn > planned.windowTo) {
        errors.push(
          bad(
            'dueOn',
            'FollowUpOutsideWindow',
            `A ${item.kind} follow-up must fall between ${planned.windowFrom} and ${planned.windowTo}.`,
          ),
        );
      }
    }
    if (errors.length > 0) return violation(...errors);
    const instructions = (
      body.postOpInstructions ?? protocolFor(s.serviceId).postOpInstructions
    ).trim();
    if (!instructions) {
      return violation(
        bad(
          'PostOpInstructions',
          'PostOpInstructionsRequired',
          'Write the post-op instructions for the owner.',
        ),
      );
    }
    s.postOpInstructions = instructions;
    s.followUps = chosen.map((item) => {
      const planned = plan.find((p) => p.kind === item.kind)!;
      return {
        id: newId(),
        kind: item.kind,
        dueOn: item.dueOn,
        windowFrom: planned.windowFrom,
        windowTo: planned.windowTo,
        status: 'Pending' as const,
        closedOn: null,
      };
    });
    s.dischargedOn = new Date().toISOString();
    setStatus(s, 'Discharged', actorFor(callerRoles(request)).id);
    persist();
    return ok({ surgery: detailOf(s) });
  }),

  http.post('*/api/staff/surgeries/:id/follow-ups/:followUpId/complete', ({ request, params }) => {
    const refused = requireAction(request, 'Surgery.ManageRecovery');
    if (refused) return refused;
    const s = find(request, params.id as string);
    const f = s?.followUps.find((x) => x.id === params.followUpId);
    if (!s || !f) return notFound();
    if (f.status !== 'Pending') {
      return violation(
        bad('FollowUpId', 'FollowUpNotPending', 'That follow-up is already closed.'),
      );
    }
    f.status = 'Done';
    f.closedOn = new Date().toISOString();
    persist();
    return noContent();
  }),

  http.post(
    '*/api/staff/surgeries/:id/follow-ups/:followUpId/cancel',
    async ({ request, params }) => {
      const refused = requireAction(request, 'Surgery.ManageRecovery');
      if (refused) return refused;
      const s = find(request, params.id as string);
      const f = s?.followUps.find((x) => x.id === params.followUpId);
      if (!s || !f) return notFound();
      const body = (await request.json()) as { reason?: string };
      if (!body.reason?.trim()) {
        return violation(bad('Reason', 'NotEmptyValidator', 'Give a reason for cancelling.'));
      }
      if (f.status !== 'Pending') {
        return violation(
          bad('FollowUpId', 'FollowUpNotPending', 'That follow-up is already closed.'),
        );
      }
      f.status = 'Cancelled';
      f.closedOn = new Date().toISOString();
      persist();
      return noContent();
    },
  ),
];
