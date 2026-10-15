import type {
  BoardingStayStatus,
  BoardingUnitType,
  BelongingKind,
  CareProfile,
  CareRoutine,
  CareTaskKind,
  CareTaskStatus,
  CatMood,
  DaycareBasis,
  DaycareVisitStatus,
  DepositState,
  GateReason,
  IncidentKind,
  IncidentSeverity,
  IsolationEntry,
  LitterOutput,
  PortionEaten,
  SignatureMethod,
  StayAttentionReason,
  StayChargeLine,
  StayInvoiceRef,
  StayQuote,
  StayAllocation,
  StaffStayStatusChange,
  TriagePriority,
  VaccinationGateStatus,
  VetApproval,
  VetReferral,
  WaterIntake,
  BoardingWaitlistStatus,
} from '@/lib/api/types';

/** What the mock server stores — a superset of the view models (HO-BRD-002 §12). All JSON-safe so
 *  the whole database persists to sessionStorage like the other area mocks. */

export interface StoredUnit {
  id: string;
  branchId: string;
  code: string;
  name: string;
  unitType: BoardingUnitType;
  capacityCats: number;
  block: string | null;
  sortOrder: number;
  isActive: boolean;
}

export interface StoredMaintenance {
  id: string;
  unitId: string;
  branchId: string;
  startsOn: string;
  endsOn: string;
  reason: string;
  isCancelled: boolean;
}

export interface StoredRateCard {
  id: string;
  unitType: BoardingUnitType;
  nightly: number;
  weekly: number;
  monthly: number;
  additionalCat: number;
  taxClassId: string;
  effectiveFrom: string;
  setBy: string;
}

export interface StoredDaycareRateCard {
  id: string;
  hourly: number;
  day: number;
  taxClassId: string;
  effectiveFrom: string;
  setBy: string;
}

export interface StoredBelonging {
  id: string;
  kind: BelongingKind;
  description: string;
  reference: string | null;
}

export interface StoredCat {
  catId: string;
  catName: string;
  isHighRisk: boolean;
  handlingNote: string | null;
  careProfile: CareProfile | null;
  gateStatus: VaccinationGateStatus;
  gateReasons: GateReason[];
  gateOverriddenBy: string | null;
  gateOverrideReason: string | null;
  suspectedInfectious: boolean;
  awayFromUnit: boolean;
  awayReason: string | null;
  checkIn: {
    weightKg: string;
    observation: string;
    belongings: StoredBelonging[];
    signature: { signerName: string; method: SignatureMethod; signedOn: string };
  } | null;
  checkOut: {
    weightKg: string;
    returns: Record<string, { returned: boolean; note: string | null }>;
    signature: { signerName: string; method: SignatureMethod; signedOn: string };
  } | null;
}

export interface StoredStay {
  id: string;
  branchId: string;
  customerId: string;
  customerName: string;
  customerPhone: string | null;
  channel: 'Online' | 'Reception';
  status: BoardingStayStatus;
  arrivalOn: string;
  departureOn: string;
  requestedUnitType: BoardingUnitType;
  unitId: string | null;
  cats: StoredCat[];
  quote: StayQuote | null;
  depositRequired: number;
  depositState: DepositState;
  payBy: string | null;
  version: number;
  statusHistory: StaffStayStatusChange[];
  allocations: StayAllocation[];
  isolationEntries: IsolationEntry[];
  referrals: VetReferral[];
  approvals: VetApproval[];
  charges: StayChargeLine[];
  invoices: StayInvoiceRef[];
  attention: { needsAttention: boolean; reason: StayAttentionReason | null; note: string | null };
  depositForfeited: boolean | null;
  checkedInOn: string | null;
  checkedOutOn: string | null;
  createdOn: string;
}

export interface StoredTask {
  id: string;
  stayId: string;
  catId: string;
  catName: string;
  unitId: string;
  kind: CareTaskKind;
  title: string;
  dueOn: string;
  status: CareTaskStatus;
  assignedToName: string | null;
  assignedToMe: boolean;
  completedOn: string | null;
  completedByName: string | null;
  skipReason: string | null;
}

export interface StoredObservation {
  id: string;
  stayId: string;
  catId: string;
  catName: string;
  recordedOn: string;
  portionEaten: PortionEaten;
  waterIntake: WaterIntake;
  litterOutput: LitterOutput;
  mood: CatMood;
  notes: string | null;
  recordedByName: string;
}

export interface StoredIncident {
  id: string;
  stayId: string;
  catId: string;
  kind: IncidentKind;
  severity: IncidentSeverity;
  description: string;
  immediateAction: string;
  occurredOn: string;
  reportedOn: string;
  reportedByName: string;
  ownerNotified: boolean;
  ownerAcknowledgedOn: string | null;
  acknowledgedVia: 'Portal' | 'Staff' | null;
  escalatedOn: string | null;
  phoneCallOn: string | null;
  phoneCallOutcome: string | null;
  resolvedOn: string | null;
  resolution: string | null;
}

export interface StoredMedia {
  id: string;
  stayId: string;
  catId: string | null;
  incidentId: string | null;
  caption: string | null;
  capturedOnLocalDate: string;
  reference: string | null;
  status: 'Visible' | 'Hidden' | 'Purged';
  uploadedByName: string;
}

export interface StoredWaitlist {
  id: string;
  customerId: string;
  branchId: string;
  arrivalOn: string;
  departureOn: string;
  catCount: number;
  unitType: BoardingUnitType | null;
  status: BoardingWaitlistStatus;
  notifiedOn: string | null;
}

export interface StoredDaycare {
  id: string;
  branchId: string;
  customerId: string;
  customerName: string;
  catId: string;
  catName: string;
  catIsHighRisk: boolean;
  handlingNote: string | null;
  localDate: string;
  basis: DaycareBasis;
  expectedHours: number | null;
  status: DaycareVisitStatus;
  chargeAmount: number | null;
  checkedInOn: string | null;
  checkedOutOn: string | null;
}

export interface BoardingDb {
  units: StoredUnit[];
  maintenance: StoredMaintenance[];
  rateCards: StoredRateCard[];
  daycareRateCards: StoredDaycareRateCard[];
  routine: CareRoutine;
  stays: Record<string, StoredStay>;
  tasks: StoredTask[];
  observations: StoredObservation[];
  incidents: StoredIncident[];
  media: StoredMedia[];
  waitlist: StoredWaitlist[];
  daycare: StoredDaycare[];
  seq: number;
}

export type { TriagePriority };
