import { loadPersisted, savePersisted, zonedTimeToUtcIso } from '../admin/fixtures';
import { accountsFixtureIds } from '../areas/accounts';
import { appointmentsFixtureIds } from '../appointments/fixtures';
import { pharmacyFixtureIds } from '../pharmacy/fixtures';
import type {
  AsaGrade,
  ChecklistKind,
  ChecklistStatus,
  ProcedureOutcome,
  RecoveryStatus,
  SurgeryStatus,
} from '@/lib/api/surgeries';
import type { EntryValues } from '@/lib/api/anaesthesia-log';
import { surgeryFixtureIds as ids, zoneOfBranch } from './fixtures';

/**
 * `mocks/handlers/surgeries/store.ts` — HO-SRG-002 §12. The in-memory "database" behind the surgery
 * mocks (sessionStorage-persisted in a real browser so a reload keeps it, always fresh in Vitest — the
 * pharmacy and inventory mocks' pattern). Dates are seeded RELATIVE to today in the branch zone, so the
 * day list always has a "today". Instants are real UTC instants for a branch wall-clock time.
 */
const PERSIST_KEY = '__purrcare_surgery_mock_db__';

export const CHECKLIST_ORDER: ChecklistKind[] = [
  'FastingInstructionsIssued',
  'PreAnaestheticBloodWork',
  'WeightRecorded',
  'ConsentSigned',
  'AsaGradeAssessed',
];

export interface MChecklistItem {
  id: string;
  kind: ChecklistKind;
  status: ChecklistStatus;
  completedBy: string | null;
  completedOn: string | null;
  note: string | null;
  waiverReason: string | null;
  referenceId: string | null;
}

export interface MFollowUp {
  id: string;
  kind: 'WoundCheck' | 'SutureRemoval' | 'Recheck';
  dueOn: string;
  windowFrom: string;
  windowTo: string;
  status: 'Pending' | 'Done' | 'Cancelled';
  closedOn: string | null;
}

export interface MSurgery {
  id: string;
  version: number;
  branchId: string;
  status: SurgeryStatus;
  catId: string;
  customerId: string;
  appointmentId: string;
  visitId: string;
  serviceId: string;
  theatreId: string;
  surgeonId: string;
  assistantId: string;
  startsOn: string;
  endsOn: string;
  admitBy: string;
  fastingInstructions: string;
  priceAmount: number;
  priceBasis: 'Standard' | 'Campaign';
  campaignId: string | null;
  eligibilityNote: string | null;
  asaGrade: AsaGrade | null;
  asaIsEmergency: boolean;
  consentOverride: {
    overriddenBy: string;
    overriddenByName: string;
    overriddenOn: string;
    reason: string;
  } | null;
  /** A test hook: treat consent as recorded without an EMR consent record. */
  consentForced: boolean;
  checklist: MChecklistItem[];
  admittedOn: string | null;
  procedureStartedOn: string | null;
  procedureEndedOn: string | null;
  dischargedOn: string | null;
  outcome: ProcedureOutcome | null;
  recoveryStatus: RecoveryStatus | null;
  recoveryHistory: Array<{
    from: string | null;
    to: string;
    changedOn: string;
    changedBy: string;
    changedByName: string;
    note: string | null;
  }>;
  statusHistory: Array<{ from: string | null; to: string; changedOn: string; changedBy: string }>;
  postOpInstructions: string | null;
  followUps: MFollowUp[];
  cancelledOn: string | null;
  cancellationReason: string | null;
  neuterApplied: boolean | null;
}

export interface MService {
  id: string;
  name: string;
  category: string;
  durationMinutes: number;
  price: number;
  isSterilisation: boolean;
  requiresSex: 'Female' | 'Male' | null;
}

export interface MStaff {
  id: string;
  name: string;
  roleIds: string[];
}

export interface MLogEntry {
  id: string;
  surgeryId: string;
  kind: 'Drug' | 'Vitals' | 'Milestone' | 'Correction';
  occurredOn: string;
  loggedOn: string;
  loggedBy: string;
  loggedByName: string;
  values: EntryValues;
  correctsEntryId: string | null;
  correctedKind: string | null;
  correctionReason: string | null;
  /** The idempotency key's fingerprint (medicine, batch, quantity, dose) — a replay must match it. */
  fingerprint: string | null;
  result: { entryId: string; registerEntryId: string | null; stockShortfall: boolean } | null;
}

export interface MReport {
  surgeryId: string;
  version: number;
  status: 'Draft' | 'Finalised';
  procedurePerformed: string;
  findings: string;
  technique: string;
  sutureMaterial: string;
  hadComplications: boolean;
  complications: string | null;
  surgeonId: string;
  assistantId: string;
  finalisedOn: string | null;
  finalisedBy: string | null;
  finalisedByName: string | null;
  addenda: Array<{
    id: string;
    text: string;
    addedBy: string;
    addedByName: string;
    addedOn: string;
  }>;
}

export interface MProtocol {
  serviceId: string;
  isDefault: boolean;
  fastingInstructions: string;
  fastingHours: number;
  postOpInstructions: string;
  followUps: Array<{
    kind: 'WoundCheck' | 'SutureRemoval' | 'Recheck';
    dayOffset: number;
    windowFromDay: number;
    windowToDay: number;
  }>;
  updatedOn: string | null;
  version: number;
}

export interface MCampaign {
  id: string;
  version: number;
  code: string;
  name: string;
  description: string;
  validFrom: string;
  validTo: string;
  branchIds: string[];
  prices: Array<{ serviceId: string; amount: number }>;
  maxCases: number | null;
  isActive: boolean;
}

export interface MCat {
  id: string;
  ownerId: string;
  name: string;
  sex: 'Female' | 'Male';
  neuterStatus: 'Intact' | 'Neutered' | 'Unknown';
  neuteredOn: string | null;
  isHighRisk: boolean;
  allergies: string[];
  weights: Array<{ id: string; recordedOn: string; kilograms: number }>;
}

export interface MCustomer {
  id: string;
  fullName: string;
  mobileNumber: string;
  catIds: string[];
}

export interface MBusy {
  branchId: string;
  resource: 'theatre' | 'surgeon' | 'assistant';
  resourceId: string;
  startsOn: string;
  endsOn: string;
  label: string;
}

interface Db {
  surgeries: MSurgery[];
  log: MLogEntry[];
  reports: MReport[];
  protocols: MProtocol[];
  campaigns: MCampaign[];
  cats: MCat[];
  extraBusy: MBusy[];
  raceTriggered: boolean;
}

export const SERVICES: MService[] = [
  {
    id: ids.serviceSpayId,
    name: 'Spay',
    category: 'Surgery',
    durationMinutes: 60,
    price: 2000,
    isSterilisation: true,
    requiresSex: 'Female',
  },
  {
    id: ids.serviceCastrationId,
    name: 'Castration',
    category: 'Surgery',
    durationMinutes: 45,
    price: 1500,
    isSterilisation: true,
    requiresSex: 'Male',
  },
  {
    id: ids.serviceDentalId,
    name: 'Dental extractions',
    category: 'Surgery',
    durationMinutes: 45,
    price: 3000,
    isSterilisation: false,
    requiresSex: null,
  },
  {
    id: ids.serviceMassRemovalId,
    name: 'Mass removal',
    category: 'Surgery',
    durationMinutes: 60,
    price: 4500,
    isSterilisation: false,
    requiresSex: null,
  },
];

export const STAFF: MStaff[] = [
  { id: ids.surgeonRahmanId, name: 'Dr Rahman', roleIds: ['Vet'] },
  { id: ids.surgeonAkterId, name: 'Dr Akter', roleIds: ['Vet'] },
  { id: ids.nurseTanvirId, name: 'Nurse Tanvir', roleIds: ['Nurse'] },
  { id: ids.nurseSadiaId, name: 'Nurse Sadia', roleIds: ['Nurse'] },
];

export const THEATRES = [
  { id: ids.theatre1Id, name: 'Theatre 1' },
  { id: ids.theatre2Id, name: 'Theatre 2' },
];

export const DEFAULT_FASTING =
  'No food after 10 pm the night before. Water is fine until the morning. Bring your cat in a carrier.';
export const DEFAULT_POST_OP =
  'Keep your cat indoors and warm for 24 hours. Offer a small meal tonight. Check the wound twice a day and do not let her lick it. Call us if you see redness, swelling or discharge.';

export const CUSTOMERS: MCustomer[] = [
  {
    id: ids.customerFarhanaId,
    fullName: 'Farhana Yasmin',
    mobileNumber: ids.customerFarhanaMobile,
    catIds: [ids.catPepperId, ids.catBiscuitId],
  },
  {
    id: ids.customerKamrulId,
    fullName: 'Kamrul Hasan',
    mobileNumber: ids.customerKamrulMobile,
    catIds: [ids.catNoodleId],
  },
  {
    id: ids.customerImranId,
    fullName: 'Imran Chowdhury',
    mobileNumber: ids.customerImranMobile,
    catIds: [ids.catShadowId],
  },
];

// ---- time -----------------------------------------------------------------------------------

export function todayIn(zone: string): string {
  return new Intl.DateTimeFormat('en-CA', { timeZone: zone }).format(new Date());
}

export function addDays(dateIso: string, days: number): string {
  const base = new Date(`${dateIso}T12:00:00Z`);
  base.setUTCDate(base.getUTCDate() + days);
  return base.toISOString().slice(0, 10);
}

export function localDateOf(instant: string, zone: string): string {
  return new Intl.DateTimeFormat('en-CA', { timeZone: zone }).format(new Date(instant));
}

export function localTimeOf(instant: string, zone: string): string {
  return new Intl.DateTimeFormat('en-GB', {
    timeZone: zone,
    hour: '2-digit',
    minute: '2-digit',
    hour12: false,
  }).format(new Date(instant));
}

export function instantAt(dateIso: string, time: string, zone: string): string {
  return zonedTimeToUtcIso(dateIso, time, zone);
}

export function plusMinutes(instant: string, minutes: number): string {
  return new Date(new Date(instant).getTime() + minutes * 60_000).toISOString();
}

export function minutesAgo(minutes: number): string {
  return new Date(Date.now() - minutes * 60_000).toISOString();
}

export function newId(): string {
  return crypto.randomUUID();
}

// ---- seed ------------------------------------------------------------------------------------

function checklistOf(
  states: Partial<Record<ChecklistKind, ChecklistStatus>>,
  by = ids.nurseTanvirId,
): MChecklistItem[] {
  return CHECKLIST_ORDER.map((kind) => {
    const status = states[kind] ?? 'Outstanding';
    return {
      id: newId(),
      kind,
      status,
      completedBy: status === 'Outstanding' ? null : by,
      completedOn: status === 'Outstanding' ? null : minutesAgo(120),
      note: null,
      waiverReason: status === 'Waived' ? 'Healthy young cat; owner declined blood work.' : null,
      referenceId: null,
    };
  });
}

const ALL_DONE: Partial<Record<ChecklistKind, ChecklistStatus>> = {
  FastingInstructionsIssued: 'Done',
  PreAnaestheticBloodWork: 'Done',
  WeightRecorded: 'Done',
  ConsentSigned: 'Done',
  AsaGradeAssessed: 'Done',
};

interface SeedArgs {
  id: string;
  branchId?: string;
  status: SurgeryStatus;
  catId: string;
  customerId: string;
  serviceId: string;
  dayOffset: number;
  time: string;
  theatreId?: string;
  surgeonId?: string;
  assistantId?: string;
  checklist?: Partial<Record<ChecklistKind, ChecklistStatus>>;
  asa?: AsaGrade | null;
  consentForced?: boolean;
  priceBasis?: 'Standard' | 'Campaign';
  campaignId?: string | null;
}

function seedSurgery(args: SeedArgs): MSurgery {
  const branchId = args.branchId ?? ids.mainBranchId;
  const zone = zoneOfBranch(branchId);
  const service = SERVICES.find((s) => s.id === args.serviceId)!;
  const date = addDays(todayIn(zone), args.dayOffset);
  const startsOn = instantAt(date, args.time, zone);
  const status = args.status;
  const admitted = status !== 'Scheduled' && status !== 'Cancelled';
  const isSterilisation = service.isSterilisation;
  return {
    id: args.id,
    version: 1,
    branchId,
    status,
    catId: args.catId,
    customerId: args.customerId,
    appointmentId: newId(),
    visitId: newId(),
    serviceId: args.serviceId,
    theatreId: args.theatreId ?? ids.theatre1Id,
    surgeonId: args.surgeonId ?? ids.surgeonRahmanId,
    assistantId: args.assistantId ?? ids.nurseTanvirId,
    startsOn,
    endsOn: plusMinutes(startsOn, service.durationMinutes),
    admitBy: plusMinutes(startsOn, -45),
    fastingInstructions: DEFAULT_FASTING,
    priceAmount: args.priceBasis === 'Campaign' ? 800 : service.price,
    priceBasis: args.priceBasis ?? 'Standard',
    campaignId: args.campaignId ?? null,
    eligibilityNote: args.campaignId ? 'Community cat, trapped by the neighbourhood group.' : null,
    asaGrade: args.asa ?? null,
    asaIsEmergency: false,
    consentOverride: null,
    consentForced: args.consentForced ?? false,
    checklist: checklistOf(args.checklist ?? {}),
    admittedOn: admitted ? plusMinutes(startsOn, -30) : null,
    procedureStartedOn: ['InTheatre', 'Recovery', 'Discharged'].includes(status)
      ? plusMinutes(startsOn, 5)
      : null,
    procedureEndedOn: ['Recovery', 'Discharged'].includes(status)
      ? plusMinutes(startsOn, service.durationMinutes)
      : null,
    dischargedOn:
      status === 'Discharged' ? plusMinutes(startsOn, service.durationMinutes + 180) : null,
    outcome: ['Recovery', 'Discharged'].includes(status) ? 'Completed' : null,
    recoveryStatus: status === 'Recovery' || status === 'Discharged' ? 'Recovering' : null,
    recoveryHistory: [],
    statusHistory: [
      {
        from: null,
        to: 'Scheduled',
        changedOn: minutesAgo(60 * 24 * 3),
        changedBy: ids.nurseTanvirId,
      },
    ],
    postOpInstructions: status === 'Discharged' ? DEFAULT_POST_OP : null,
    followUps: [],
    cancelledOn: status === 'Cancelled' ? minutesAgo(60 * 20) : null,
    cancellationReason: status === 'Cancelled' ? 'The owner needs to move the date.' : null,
    neuterApplied: isSterilisation && status === 'Discharged' ? true : null,
  };
}

function entry(
  surgeryId: string,
  kind: MLogEntry['kind'],
  minutesBefore: number,
  values: Partial<EntryValues>,
  extra: Partial<MLogEntry> = {},
): MLogEntry {
  const occurredOn = minutesAgo(minutesBefore);
  return {
    id: newId(),
    surgeryId,
    kind,
    occurredOn,
    loggedOn: occurredOn,
    loggedBy: ids.surgeonAkterId,
    loggedByName: 'Dr Akter',
    values: emptyValues(values),
    correctsEntryId: null,
    correctedKind: null,
    correctionReason: null,
    fingerprint: null,
    result: null,
    ...extra,
  };
}

export function emptyValues(over: Partial<EntryValues> = {}): EntryValues {
  return {
    medicineId: null,
    medicineName: null,
    isControlled: false,
    doseAmount: null,
    doseUnit: null,
    route: null,
    administeredById: null,
    administeredByName: null,
    batchNumber: null,
    quantityDrawn: null,
    quantityWasted: null,
    stockUnit: null,
    witnessId: null,
    witnessName: null,
    registerEntryId: null,
    stockShortfall: false,
    heartRateBpm: null,
    respiratoryRatePerMin: null,
    spO2Percent: null,
    etCo2MmHg: null,
    temperatureCelsius: null,
    systolicMmHg: null,
    meanArterialMmHg: null,
    implausibleConfirmed: false,
    milestone: null,
    note: null,
    ...over,
  };
}

function seed(): Db {
  const sam = appointmentsFixtureIds.signedInCustomerId;
  const surgeries: MSurgery[] = [
    seedSurgery({
      id: ids.surgeryScheduledTodayId,
      status: 'Scheduled',
      catId: ids.catPepperId,
      customerId: ids.customerFarhanaId,
      serviceId: ids.serviceSpayId,
      dayOffset: 0,
      time: '09:00',
    }),
    seedSurgery({
      id: ids.surgeryPreOpConsentOnlyId,
      status: 'PreOp',
      catId: ids.catNoodleId,
      customerId: ids.customerKamrulId,
      serviceId: ids.serviceCastrationId,
      dayOffset: 0,
      time: '10:30',
      surgeonId: ids.surgeonAkterId,
      assistantId: ids.nurseSadiaId,
      checklist: { ...ALL_DONE, ConsentSigned: 'Outstanding' },
      asa: 'I',
    }),
    seedSurgery({
      id: ids.surgeryPreOpIncompleteId,
      status: 'PreOp',
      catId: ids.catShadowId,
      customerId: ids.customerImranId,
      serviceId: ids.serviceDentalId,
      dayOffset: 0,
      time: '14:00',
      theatreId: ids.theatre2Id,
      checklist: { FastingInstructionsIssued: 'Done', PreAnaestheticBloodWork: 'Waived' },
    }),
    seedSurgery({
      id: ids.surgeryOverriddenId,
      status: 'InTheatre',
      catId: ids.catMomoId,
      customerId: sam,
      serviceId: ids.serviceDentalId,
      dayOffset: 0,
      time: '07:30',
      theatreId: ids.theatre2Id,
      checklist: { ...ALL_DONE, ConsentSigned: 'Outstanding' },
      asa: 'II',
    }),
    seedSurgery({
      id: ids.surgeryInTheatreId,
      status: 'InTheatre',
      catId: ids.catBiscuitId,
      customerId: ids.customerFarhanaId,
      serviceId: ids.serviceMassRemovalId,
      dayOffset: 0,
      time: '08:00',
      surgeonId: ids.surgeonAkterId,
      assistantId: ids.nurseSadiaId,
      checklist: ALL_DONE,
      asa: 'I',
      consentForced: true,
    }),
    seedSurgery({
      id: ids.surgeryRecoveryId,
      status: 'Recovery',
      catId: ids.catMomoId,
      customerId: sam,
      serviceId: ids.serviceMassRemovalId,
      dayOffset: 0,
      time: '06:30',
      checklist: ALL_DONE,
      asa: 'I',
      consentForced: true,
    }),
    seedSurgery({
      id: 'e5000000-0000-4000-8000-0000000000a1',
      status: 'Recovery',
      catId: ids.catShadowId,
      customerId: ids.customerImranId,
      serviceId: ids.serviceDentalId,
      dayOffset: 0,
      time: '06:00',
      theatreId: ids.theatre2Id,
      checklist: ALL_DONE,
      asa: 'II',
      consentForced: true,
    }),
    seedSurgery({
      id: 'e5000000-0000-4000-8000-0000000000a2',
      status: 'Recovery',
      catId: ids.catNoodleId,
      customerId: ids.customerKamrulId,
      serviceId: ids.serviceCastrationId,
      dayOffset: 0,
      time: '05:30',
      checklist: ALL_DONE,
      asa: 'I',
      consentForced: true,
    }),
    seedSurgery({
      id: ids.surgeryDischargedId,
      status: 'Discharged',
      catId: ids.catBiscuitId,
      customerId: ids.customerFarhanaId,
      serviceId: ids.serviceCastrationId,
      dayOffset: -5,
      time: '09:00',
      checklist: ALL_DONE,
      asa: 'I',
      consentForced: true,
    }),
    seedSurgery({
      id: ids.surgeryOwnerAfterId,
      status: 'Discharged',
      catId: ids.catMomoId,
      customerId: sam,
      serviceId: ids.serviceSpayId,
      dayOffset: -6,
      time: '09:00',
      checklist: ALL_DONE,
      asa: 'I',
      consentForced: true,
    }),
    seedSurgery({
      id: ids.surgeryCancelledId,
      status: 'Cancelled',
      catId: ids.catPepperId,
      customerId: ids.customerFarhanaId,
      serviceId: ids.serviceSpayId,
      dayOffset: -2,
      time: '11:00',
    }),
    seedSurgery({
      id: ids.surgeryTomorrowId,
      status: 'Scheduled',
      catId: ids.catBiscuitId,
      customerId: ids.customerFarhanaId,
      serviceId: ids.serviceMassRemovalId,
      dayOffset: 1,
      time: '09:00',
    }),
    seedSurgery({
      id: ids.surgeryOwnerBeforeId,
      status: 'Scheduled',
      catId: ids.catMomoId,
      customerId: sam,
      serviceId: ids.serviceDentalId,
      dayOffset: 4,
      time: '10:00',
      theatreId: ids.theatre2Id,
    }),
    seedSurgery({
      id: ids.surgeryLockedPricesId,
      status: 'Scheduled',
      catId: ids.catShadowId,
      customerId: ids.customerImranId,
      serviceId: ids.serviceSpayId,
      dayOffset: 3,
      time: '09:00',
      priceBasis: 'Campaign',
      campaignId: ids.campaignOutreachId,
    }),
    seedSurgery({
      id: ids.surgeryLondonId,
      branchId: ids.londonBranchId,
      status: 'Scheduled',
      catId: ids.catPepperId,
      customerId: ids.customerFarhanaId,
      serviceId: ids.serviceSpayId,
      dayOffset: 1,
      time: '10:00',
    }),
  ];

  const byId = (id: string) => surgeries.find((s) => s.id === id)!;

  // Recovery history, follow-ups and the override.
  const recovery = byId(ids.surgeryRecoveryId);
  recovery.recoveryHistory = [
    {
      from: null,
      to: 'Recovering',
      changedOn: minutesAgo(75),
      changedBy: ids.nurseTanvirId,
      changedByName: 'Nurse Tanvir',
      note: null,
    },
  ];
  const stable = byId('e5000000-0000-4000-8000-0000000000a1');
  stable.recoveryStatus = 'Stable';
  stable.recoveryHistory = [
    {
      from: null,
      to: 'Recovering',
      changedOn: minutesAgo(120),
      changedBy: ids.nurseTanvirId,
      changedByName: 'Nurse Tanvir',
      note: null,
    },
    {
      from: 'Recovering',
      to: 'Stable',
      changedOn: minutesAgo(40),
      changedBy: ids.nurseTanvirId,
      changedByName: 'Nurse Tanvir',
      note: 'Eating a little.',
    },
  ];
  const ready = byId('e5000000-0000-4000-8000-0000000000a2');
  ready.recoveryStatus = 'ReadyForDischarge';
  ready.recoveryHistory = [
    {
      from: null,
      to: 'Recovering',
      changedOn: minutesAgo(150),
      changedBy: ids.nurseTanvirId,
      changedByName: 'Nurse Tanvir',
      note: null,
    },
    {
      from: 'Recovering',
      to: 'ReadyForDischarge',
      changedOn: minutesAgo(15),
      changedBy: ids.nurseTanvirId,
      changedByName: 'Nurse Tanvir',
      note: 'Walking, alert.',
    },
  ];

  const overridden = byId(ids.surgeryOverriddenId);
  overridden.consentOverride = {
    overriddenBy: ids.surgeonRahmanId,
    overriddenByName: 'Dr Rahman',
    overriddenOn: minutesAgo(130),
    reason: 'Owner unreachable; cat in pain and must be treated now.',
  };

  for (const done of [byId(ids.surgeryDischargedId), byId(ids.surgeryOwnerAfterId)]) {
    const base = done.dischargedOn!.slice(0, 10);
    done.followUps = [
      {
        id: newId(),
        kind: 'WoundCheck',
        dueOn: addDays(base, 3),
        windowFrom: addDays(base, 2),
        windowTo: addDays(base, 5),
        status: 'Pending',
        closedOn: null,
      },
      {
        id: newId(),
        kind: 'SutureRemoval',
        dueOn: addDays(base, 10),
        windowFrom: addDays(base, 9),
        windowTo: addDays(base, 14),
        status: 'Pending',
        closedOn: null,
      },
    ];
    done.recoveryStatus = 'ReadyForDischarge';
  }

  const log: MLogEntry[] = [];
  const theatre = ids.surgeryInTheatreId;
  log.push(
    entry(
      theatre,
      'Milestone',
      60,
      { milestone: 'Premedication', note: null },
      { loggedBy: ids.nurseTanvirId, loggedByName: 'Nurse Tanvir' },
    ),
    entry(theatre, 'Drug', 58, {
      medicineId: pharmacyFixtureIds.medMeloxicamId,
      medicineName: 'Meloxicam',
      doseAmount: 0.2,
      doseUnit: 'Mg',
      route: 'Subcutaneous',
      administeredById: ids.surgeonAkterId,
      administeredByName: 'Dr Akter',
      batchNumber: 'MX-2207',
      quantityDrawn: 0.1,
      stockUnit: 'Millilitre',
    }),
    entry(theatre, 'Milestone', 50, { milestone: 'Induction' }),
    entry(
      theatre,
      'Vitals',
      45,
      { heartRateBpm: 120, respiratoryRatePerMin: 18, spO2Percent: 98, temperatureCelsius: 38.6 },
      { loggedBy: ids.nurseTanvirId, loggedByName: 'Nurse Tanvir' },
    ),
  );
  // A controlled drug with its witness, then a correction chain on a non-controlled drug.
  log.push(
    entry(theatre, 'Drug', 40, {
      medicineId: pharmacyFixtureIds.medBuprenorphineId,
      medicineName: 'Buprenorphine injection',
      isControlled: true,
      doseAmount: 0.02,
      doseUnit: 'Mg',
      route: 'Intramuscular',
      administeredById: ids.surgeonAkterId,
      administeredByName: 'Dr Akter',
      batchNumber: 'BUP-5531',
      quantityDrawn: 1,
      quantityWasted: 0.5,
      stockUnit: 'Millilitre',
      witnessId: ids.nurseTanvirId,
      witnessName: 'Nurse Tanvir',
      registerEntryId: newId(),
    }),
  );
  const wrong = entry(theatre, 'Drug', 35, {
    medicineId: pharmacyFixtureIds.medMeloxicamId,
    medicineName: 'Meloxicam',
    doseAmount: 0.5,
    doseUnit: 'Mg',
    route: 'Subcutaneous',
    administeredById: ids.surgeonAkterId,
    administeredByName: 'Dr Akter',
    batchNumber: 'MX-2207',
    quantityDrawn: 0.25,
    stockUnit: 'Millilitre',
    stockShortfall: true,
  });
  log.push(wrong);
  log.push(
    entry(
      theatre,
      'Correction',
      20,
      {
        medicineId: pharmacyFixtureIds.medMeloxicamId,
        medicineName: 'Meloxicam',
        doseAmount: 0.05,
        doseUnit: 'Mg',
        route: 'Subcutaneous',
        administeredById: ids.surgeonAkterId,
        administeredByName: 'Dr Akter',
        batchNumber: 'MX-2207',
        quantityDrawn: 0.25,
        stockUnit: 'Millilitre',
      },
      {
        occurredOn: wrong.occurredOn,
        correctsEntryId: wrong.id,
        correctedKind: 'Drug',
        correctionReason: 'Entered 0.5 by mistake; the dose given was 0.05.',
      },
    ),
    entry(
      theatre,
      'Vitals',
      10,
      {
        heartRateBpm: 110,
        respiratoryRatePerMin: 16,
        spO2Percent: 99,
        temperatureCelsius: 38.2,
        etCo2MmHg: 36,
      },
      { loggedBy: ids.nurseTanvirId, loggedByName: 'Nurse Tanvir' },
    ),
  );

  const overrideLog = ids.surgeryOverriddenId;
  log.push(
    entry(
      overrideLog,
      'Milestone',
      90,
      { milestone: 'Premedication' },
      { loggedBy: ids.nurseTanvirId, loggedByName: 'Nurse Tanvir' },
    ),
    entry(
      overrideLog,
      'Vitals',
      60,
      { heartRateBpm: 128, spO2Percent: 97 },
      { loggedBy: ids.nurseTanvirId, loggedByName: 'Nurse Tanvir' },
    ),
  );

  const reports: MReport[] = [
    {
      surgeryId: ids.surgeryOwnerAfterId,
      version: 4,
      status: 'Finalised',
      procedurePerformed: 'Ovariohysterectomy (spay)',
      findings: 'Routine. Uterus and ovaries unremarkable.',
      technique: 'Midline approach, three-clamp technique.',
      sutureMaterial: '3-0 absorbable (Monocryl), intradermal skin closure',
      hadComplications: false,
      complications: null,
      surgeonId: ids.surgeonRahmanId,
      assistantId: ids.nurseTanvirId,
      finalisedOn: byId(ids.surgeryOwnerAfterId).dischargedOn,
      finalisedBy: ids.surgeonRahmanId,
      finalisedByName: 'Dr Rahman',
      addenda: [
        {
          id: newId(),
          text: 'Owner reported mild swelling at day 2; reviewed, no treatment needed.',
          addedBy: ids.surgeonRahmanId,
          addedByName: 'Dr Rahman',
          addedOn: minutesAgo(60 * 24 * 3),
        },
      ],
    },
    {
      surgeryId: ids.surgeryDischargedId,
      version: 3,
      status: 'Finalised',
      procedurePerformed: 'Castration',
      findings: 'Both testes descended, normal.',
      technique: 'Scrotal approach, open technique.',
      sutureMaterial: 'None (second intention)',
      hadComplications: false,
      complications: null,
      surgeonId: ids.surgeonRahmanId,
      assistantId: ids.nurseTanvirId,
      finalisedOn: byId(ids.surgeryDischargedId).dischargedOn,
      finalisedBy: ids.surgeonRahmanId,
      finalisedByName: 'Dr Rahman',
      addenda: [],
    },
  ];

  const protocols: MProtocol[] = [
    {
      serviceId: ids.serviceSpayId,
      isDefault: false,
      fastingInstructions: DEFAULT_FASTING,
      fastingHours: 10,
      postOpInstructions: DEFAULT_POST_OP,
      followUps: [
        { kind: 'WoundCheck', dayOffset: 3, windowFromDay: 2, windowToDay: 5 },
        { kind: 'SutureRemoval', dayOffset: 10, windowFromDay: 9, windowToDay: 14 },
      ],
      updatedOn: minutesAgo(60 * 24 * 20),
      version: 2,
    },
  ];
  for (const service of SERVICES.filter((s) => s.id !== ids.serviceSpayId)) {
    protocols.push({
      serviceId: service.id,
      isDefault: true,
      fastingInstructions: DEFAULT_FASTING,
      fastingHours: 8,
      postOpInstructions: DEFAULT_POST_OP,
      followUps: [{ kind: 'WoundCheck', dayOffset: 3, windowFromDay: 2, windowToDay: 5 }],
      updatedOn: null,
      version: 0,
    });
  }

  const today = todayIn(ids.mainZone);
  const campaigns: MCampaign[] = [
    {
      id: ids.campaignOutreachId,
      version: 1,
      code: 'TNR-OCT',
      name: 'Community cat spay and neuter drive',
      description: 'Reduced prices for trapped community cats during October.',
      validFrom: addDays(today, -10),
      validTo: addDays(today, 30),
      branchIds: [ids.mainBranchId],
      prices: [
        { serviceId: ids.serviceSpayId, amount: 800 },
        { serviceId: ids.serviceCastrationId, amount: 600 },
      ],
      maxCases: 20,
      isActive: true,
    },
    {
      id: ids.campaignExhaustedId,
      version: 1,
      code: 'KITTEN-1',
      name: 'Kitten desexing week',
      description: 'Small, fully booked programme.',
      validFrom: addDays(today, -5),
      validTo: addDays(today, 20),
      branchIds: [ids.mainBranchId],
      prices: [{ serviceId: ids.serviceSpayId, amount: 700 }],
      maxCases: 1,
      isActive: true,
    },
  ];

  const cats: MCat[] = [
    cat(ids.catPepperId, ids.customerFarhanaId, 'Pepper', 'Female', false, 3.8),
    cat(ids.catNoodleId, ids.customerKamrulId, 'Noodle', 'Male', false, 4.4),
    cat(ids.catShadowId, ids.customerImranId, 'Shadow', 'Female', true, 3.1, ['Penicillin']),
    {
      ...cat(ids.catBiscuitId, ids.customerFarhanaId, 'Biscuit', 'Male', false, 5.2),
      neuterStatus: 'Neutered',
      neuteredOn: addDays(today, -5),
    },
    cat(ids.catMomoId, sam, 'Momo', 'Female', false, 3.6),
  ];
  // The discharged spay on Momo set her status.
  const momo = cats.find((c) => c.id === ids.catMomoId)!;
  momo.neuterStatus = 'Neutered';
  momo.neuteredOn = addDays(today, -6);

  // Busy time that is not a surgery: a vaccination for the assistant (E-A2).
  const extraBusy: MBusy[] = [
    {
      branchId: ids.mainBranchId,
      resource: 'assistant',
      resourceId: ids.nurseSadiaId,
      startsOn: instantAt(addDays(today, 2), '10:00', ids.mainZone),
      endsOn: instantAt(addDays(today, 2), '10:30', ids.mainZone),
      label: 'Nurse Sadia: vaccination appointment',
    },
    {
      branchId: ids.mainBranchId,
      resource: 'theatre',
      resourceId: ids.theatre1Id,
      startsOn: instantAt(addDays(today, ids.noStartDayOffset), '08:00', ids.mainZone),
      endsOn: instantAt(addDays(today, ids.noStartDayOffset), '19:00', ids.mainZone),
      label: 'Theatre 1: closed for maintenance',
    },
    {
      branchId: ids.mainBranchId,
      resource: 'theatre',
      resourceId: ids.theatre2Id,
      startsOn: instantAt(addDays(today, ids.noStartDayOffset), '08:00', ids.mainZone),
      endsOn: instantAt(addDays(today, ids.noStartDayOffset), '19:00', ids.mainZone),
      label: 'Theatre 2: closed for maintenance',
    },
  ];

  return { surgeries, log, reports, protocols, campaigns, cats, extraBusy, raceTriggered: false };
}

function cat(
  id: string,
  ownerId: string,
  name: string,
  sex: 'Female' | 'Male',
  isHighRisk: boolean,
  kilograms: number,
  allergies: string[] = [],
): MCat {
  const today = todayIn(ids.mainZone);
  return {
    id,
    ownerId,
    name,
    sex,
    neuterStatus: 'Intact',
    neuteredOn: null,
    isHighRisk,
    allergies,
    weights: [{ id: newId(), recordedOn: addDays(today, -40), kilograms }],
  };
}

let db: Db = load();

function load(): Db {
  return loadPersisted<Db>(PERSIST_KEY) ?? seed();
}

export function getDb(): Db {
  return db;
}

export function persist(): void {
  savePersisted(PERSIST_KEY, db);
}

/** Fresh seed — Vitest calls this between tests that mutate (and e2e through `window`-free reload). */
export function resetSurgeryMockData(): void {
  db = seed();
  persist();
}

export function findSurgery(id: string): MSurgery | undefined {
  return db.surgeries.find((s) => s.id === id);
}

export function findService(id: string): MService | undefined {
  return SERVICES.find((s) => s.id === id);
}

export function staffName(id: string): string {
  return STAFF.find((s) => s.id === id)?.name ?? 'Staff member';
}

export function theatreName(id: string): string {
  return THEATRES.find((t) => t.id === id)?.name ?? 'Theatre';
}

export function findCat(id: string): MCat | undefined {
  return db.cats.find((c) => c.id === id);
}

export function findCustomerOfCat(catId: string): { id: string; name: string; phone: string } {
  const own = CUSTOMERS.find((c) => c.catIds.includes(catId));
  if (own) return { id: own.id, name: own.fullName, phone: own.mobileNumber };
  return {
    id: appointmentsFixtureIds.signedInCustomerId,
    name: 'Sam Rahman',
    phone: accountsFixtureIds.customerMobile,
  };
}
