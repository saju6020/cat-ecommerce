import { getDb as getEmrDb } from '../visits/store';
import type {
  ChecklistItem,
  SurgeryAction,
  SurgeryDayItem,
  SurgeryDetail,
  StartBlocker,
} from '@/lib/api/surgeries';
import { surgeryFixtureIds as ids, money, zoneOfBranch } from './fixtures';
import {
  CHECKLIST_ORDER,
  findCat,
  findCustomerOfCat,
  findService,
  addDays,
  getDb,
  localDateOf,
  staffName,
  theatreName,
  type MChecklistItem,
  type MSurgery,
} from './store';

/**
 * `mocks/handlers/surgeries/views.ts` — the mock plays the SERVER: the checklist verdict, the start
 * blockers and `allowedTransitions` (HO-SRG-001 §8.3, §8.6, §8.7) are worked out HERE, so the app under
 * test renders them and never works any of it out itself (ADR-FE-072).
 */
const ALLOWED_FROM: Record<SurgeryAction, string[]> = {
  Reschedule: ['Scheduled'],
  Cancel: ['Scheduled', 'PreOp'],
  Admit: ['Scheduled'],
  Start: ['PreOp'],
  EndProcedure: ['InTheatre'],
  SetRecoveryStatus: ['Recovery'],
  Discharge: ['Recovery'],
  CloseFollowUp: ['Discharged'],
};

export function allowedTransitions(status: string): SurgeryAction[] {
  return (Object.keys(ALLOWED_FROM) as SurgeryAction[]).filter((action) =>
    ALLOWED_FROM[action].includes(status),
  );
}

/** Consent counts only when BOTH kinds (Surgery and Anaesthesia) are recorded for the appointment. */
export function consentRecorded(s: MSurgery): boolean {
  if (s.consentForced) return true;
  const consents = getEmrDb().consents;
  return ['Surgery', 'Anaesthesia'].every((kind) =>
    consents.some(
      (c) =>
        c.catId === s.catId &&
        c.appointmentId === s.appointmentId &&
        c.kind === kind &&
        c.status === 'Recorded',
    ),
  );
}

/** A weight of this cat taken on the day of surgery or up to 14 days before completes the item. */
function weightInWindow(s: MSurgery): { id: string; recordedOn: string } | null {
  const zone = zoneOfBranch(s.branchId);
  const date = localDateOf(s.startsOn, zone);
  const cat = findCat(s.catId);
  const latest = [...(cat?.weights ?? [])]
    .sort((a, b) => a.recordedOn.localeCompare(b.recordedOn))
    .at(-1);
  if (!latest) return null;
  return latest.recordedOn <= date && latest.recordedOn >= addDays(date, -14) ? latest : null;
}

/** The checklist as the server reports it: weight and consent complete from their own sources. */
export function effectiveChecklist(s: MSurgery): MChecklistItem[] {
  return CHECKLIST_ORDER.map((kind) => {
    const stored = s.checklist.find((item) => item.kind === kind)!;
    if (stored.status !== 'Outstanding') return stored;
    if (kind === 'WeightRecorded') {
      const weight = weightInWindow(s);
      if (weight) {
        return {
          ...stored,
          status: 'Done',
          completedBy: ids.nurseTanvirId,
          completedOn: new Date().toISOString(),
          referenceId: weight.id,
        };
      }
    }
    if (kind === 'ConsentSigned' && consentRecorded(s)) {
      return {
        ...stored,
        status: 'Done',
        completedBy: ids.nurseTanvirId,
        completedOn: new Date().toISOString(),
      };
    }
    return stored;
  });
}

export function checklistSummary(s: MSurgery) {
  const outstanding = effectiveChecklist(s)
    .filter((item) => item.status === 'Outstanding')
    .map((item) => item.kind);
  return { isComplete: outstanding.length === 0, outstandingItems: outstanding };
}

const OUTSTANDING_WORDS: Record<string, string> = {
  FastingInstructionsIssued: 'fasting instructions issued',
  PreAnaestheticBloodWork: 'pre-anaesthetic blood work',
  WeightRecorded: 'weight recorded',
  ConsentSigned: 'consent signed',
  AsaGradeAssessed: 'ASA grade assessed',
};

export function startBlockers(s: MSurgery): StartBlocker[] {
  if (s.status !== 'PreOp') {
    return [
      {
        code: 'InvalidSurgeryTransition',
        message: `A surgery that is ${s.status === 'Scheduled' ? 'scheduled' : s.status.toLowerCase()} cannot be started.`,
      },
    ];
  }
  const blockers: StartBlocker[] = [];
  if (!consentRecorded(s)) {
    blockers.push({
      code: 'ConsentNotRecorded',
      message:
        'Consent has not been recorded for this surgery. Record consent, or a veterinarian may override in an emergency.',
    });
  }
  const outstanding = checklistSummary(s).outstandingItems.filter((k) => k !== 'ConsentSigned');
  if (outstanding.length > 0) {
    blockers.push({
      code: 'PreOpChecklistIncomplete',
      message: `The pre-op checklist is not complete: ${outstanding.map((k) => OUTSTANDING_WORDS[k]).join(', ')}.`,
    });
  }
  return blockers;
}

export function latestWeight(catId: string) {
  const latest = [...(findCat(catId)?.weights ?? [])]
    .sort((a, b) => a.recordedOn.localeCompare(b.recordedOn))
    .at(-1);
  return latest ? { kilograms: latest.kilograms, recordedOn: latest.recordedOn } : null;
}

export function detailOf(s: MSurgery): SurgeryDetail {
  const service = findService(s.serviceId)!;
  const cat = findCat(s.catId);
  const customer = findCustomerOfCat(s.catId);
  const items = effectiveChecklist(s);
  const report = getDb().reports.find((r) => r.surgeryId === s.id);
  return {
    id: s.id,
    version: s.version,
    branchId: s.branchId,
    branchTimeZoneId: zoneOfBranch(s.branchId),
    status: s.status,
    catId: s.catId,
    catName: cat?.name ?? 'Cat',
    catIsDeceased: false,
    customerId: customer.id,
    customerName: customer.name,
    customerPhone: customer.phone,
    visitId: s.visitId,
    appointmentId: s.appointmentId,
    procedure: {
      name: service.name,
      category: service.category,
      durationMinutes: service.durationMinutes,
      isSterilisation: service.isSterilisation,
    },
    priceSnapshot: {
      amount: money(s.priceAmount),
      basis: s.priceBasis,
      campaignId: s.campaignId,
      campaignEligibilityNote: s.eligibilityNote,
    },
    theatre: { id: s.theatreId, name: theatreName(s.theatreId) },
    surgeon: { id: s.surgeonId, name: staffName(s.surgeonId) },
    assistant: { id: s.assistantId, name: staffName(s.assistantId) },
    startsOn: s.startsOn,
    endsOn: s.endsOn,
    admitBy: s.admitBy,
    fastingInstructions: s.fastingInstructions,
    asaGrade: s.asaGrade,
    asaIsEmergency: s.asaIsEmergency,
    consentOverride: s.consentOverride,
    admittedOn: s.admittedOn,
    procedureStartedOn: s.procedureStartedOn,
    procedureEndedOn: s.procedureEndedOn,
    dischargedOn: s.dischargedOn,
    outcome: s.outcome,
    recoveryStatus: s.recoveryStatus,
    postOpInstructions: s.postOpInstructions,
    cancelledOn: s.cancelledOn,
    cancellationReason: s.cancellationReason,
    checklist: items.map((item): ChecklistItem => ({
      id: item.id,
      kind: item.kind,
      status: item.status,
      completedBy: item.completedBy,
      completedOn: item.completedOn,
      note: item.note,
      waiverReason: item.waiverReason,
      referenceId: item.referenceId,
    })),
    checklistSummary: checklistSummary(s),
    consentRecorded: consentRecorded(s),
    startBlockers: startBlockers(s),
    allowedTransitions: allowedTransitions(s.status),
    statusHistory: s.statusHistory,
    recoveryHistory: s.recoveryHistory,
    followUps: s.followUps,
    reportStatus: report ? report.status : 'None',
    neuterStatusApplied:
      service.isSterilisation && s.outcome === 'Completed' ? s.neuterApplied : null,
    latestWeight: latestWeight(s.catId),
  };
}

export function dayItemOf(s: MSurgery): SurgeryDayItem {
  const cat = findCat(s.catId);
  const customer = findCustomerOfCat(s.catId);
  return {
    surgeryId: s.id,
    version: s.version,
    startsOn: s.startsOn,
    endsOn: s.endsOn,
    admitBy: s.admitBy,
    status: s.status,
    theatre: { id: s.theatreId, name: theatreName(s.theatreId) },
    surgeon: { id: s.surgeonId, name: staffName(s.surgeonId) },
    assistant: { id: s.assistantId, name: staffName(s.assistantId) },
    cat: {
      id: s.catId,
      name: cat?.name ?? 'Cat',
      isHighRisk: cat?.isHighRisk ?? false,
      isDeceased: false,
    },
    customer: { id: customer.id, name: customer.name, phone: customer.phone },
    procedure: findService(s.serviceId)!.name,
    checklist: checklistSummary(s),
    consentRecorded: consentRecorded(s),
    needsRescheduling: false,
    recoveryStatus: s.recoveryStatus,
  };
}
