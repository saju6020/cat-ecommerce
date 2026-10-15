import { emrFixtureIds as ids } from './fixtures';
import type { StoredOrder, StoredResultValue, StoredVisit } from './store';

/**
 * `mocks/handlers/visits/bulk.ts` — HO-EMR-002 §12's "10-year history fixture": for Whiskers, 59
 * generated finalised visits (60 with the live one), 250 weights and 80 lab orders, derived
 * deterministically from today so the history drawer is measured against a realistic cat (E-D2,
 * NFR-PRF-4). Generated lazily and memoised; never persisted.
 */

const DAY = 86_400_000;
const VISIT_COUNT = 59;
const WEIGHT_COUNT = 250;
const ORDER_COUNT = 80;

const pad = (n: number) => String(n).padStart(12, '0');
export const bulkVisitId = (i: number) => `b1b00000-0000-0000-0000-${pad(i)}`;
export const bulkOrderId = (i: number) => `b2b00000-0000-0000-0000-${pad(i)}`;

const PROBLEM_CODES = [
  'b4000000-0000-0000-0000-000000000005',
  'b4000000-0000-0000-0000-000000000006',
  'b4000000-0000-0000-0000-000000000007',
  'b4000000-0000-0000-0000-000000000008',
  ids.codeOtitisId,
  ids.codeCkdId,
  ids.codeNadId,
];

let visits: StoredVisit[] | null = null;
let orders: StoredOrder[] | null = null;
let weights: Array<{ id: string; on: string; kg: number }> | null = null;

function buildVisits(): StoredVisit[] {
  const now = Date.now();
  return Array.from({ length: VISIT_COUNT }, (_, index) => {
    const i = index + 1;
    const opened = new Date(now - (i * 60 + 20) * DAY).toISOString();
    return {
      id: bulkVisitId(i),
      version: 5,
      status: 'Finalised',
      catId: ids.catWhiskersId,
      catName: 'Whiskers',
      branchId: ids.mainBranchId,
      appointmentId: `b1c00000-0000-0000-0000-${pad(i)}`,
      appointmentStatus: 'Completed',
      appointmentStartsOn: opened,
      performerName: ids.vetStaffName,
      serviceId: 's0000000-0000-0000-0000-00000000a001',
      serviceName: i % 7 === 0 ? 'Vaccination' : 'General consultation',
      serviceCategory: i % 7 === 0 ? 'Vaccination' : 'Consultation',
      openedOn: opened,
      openedBy: ids.vetStaffName,
      note: {
        subjective: `Historic visit ${i}: owner reports a minor concern.`,
        objective: 'Examination unremarkable apart from the findings listed.',
        assessment: 'See problem list.',
        plan: 'Monitor at home and return if signs persist.',
        dischargeInstructions: 'Monitor at home.',
        savedOn: opened,
        savedBy: ids.vetStaffName,
        appliedTemplateId: null,
        appliedTemplateVersion: null,
      },
      problems: [
        {
          id: `b7b00000-0000-0000-0000-${pad(i)}`,
          diagnosisCodeId: PROBLEM_CODES[i % PROBLEM_CODES.length]!,
          certainty: i % 2 === 0 ? 'Confirmed' : 'Provisional',
          note: null,
        },
      ],
      vitals: [
        {
          id: `b8b00000-0000-0000-0000-${pad(i)}`,
          measuredOn: opened,
          weightKg: Number((4.8 - i * 0.01).toFixed(2)),
          temperatureC: 38.4,
          heartRateBpm: 170,
          respiratoryRatePerMin: 28,
          bodyConditionScore: 5,
          painScore: 1,
          confirmedOutOfRange: [],
          note: null,
          recordedBy: 'Nadia Nurse',
          recordedOn: opened,
        },
      ],
      photos: [],
      followUp: null,
      addenda: [],
      orderIds: [],
      drafts: {},
      finalisedOn: opened,
      finalisedBy: ids.vetStaffName,
    };
  });
}

function value(
  id: string,
  name: string,
  v: number,
  unit: string,
  low: number,
  high: number,
): StoredResultValue {
  const flag = v < low ? 'Low' : v > high ? 'High' : 'Normal';
  return {
    id,
    name,
    valueKind: 'Numeric',
    numericValue: String(v),
    textValue: null,
    unit,
    referenceLow: String(low),
    referenceHigh: String(high),
    normalText: null,
    flag,
  };
}

function buildOrders(): StoredOrder[] {
  const now = Date.now();
  return Array.from({ length: ORDER_COUNT }, (_, index) => {
    const i = index + 1;
    const ordered = new Date(now - (i * 45 + 10) * DAY).toISOString();
    const abnormal = i % 3 === 0;
    return {
      id: bulkOrderId(i),
      version: 2,
      visitId: bulkVisitId(((i - 1) % VISIT_COUNT) + 1),
      catId: ids.catWhiskersId,
      catName: 'Whiskers',
      branchId: ids.mainBranchId,
      kind: i % 5 === 0 ? 'Urinalysis' : 'BloodWork',
      description: null,
      status: 'Resulted',
      orderedOn: ordered,
      orderedBy: ids.vetStaffName,
      cancelledReason: null,
      analyteIds: [],
      resultSets: [
        {
          id: `bb100000-0000-0000-0000-${pad(i)}`,
          reportedOn: ordered.slice(0, 10),
          note: null,
          supersedesResultSetId: null,
          recordedBy: ids.vetStaffName,
          recordedOn: ordered,
          values: [
            value(
              `bc100000-0000-0000-0000-${pad(i * 2)}`,
              'Creatinine',
              abnormal ? 260 : 150,
              'µmol/L',
              71,
              212,
            ),
            value(`bc100000-0000-0000-0000-${pad(i * 2 + 1)}`, 'Urea', 8.2, 'mmol/L', 5.7, 12.9),
          ],
        },
      ],
      attachments: [],
    };
  });
}

function buildWeights() {
  const now = Date.now();
  return Array.from({ length: WEIGHT_COUNT }, (_, index) => {
    const i = index + 1;
    return {
      id: `bf000000-0000-0000-0000-${pad(i)}`,
      on: new Date(now - i * 14 * DAY).toISOString().slice(0, 10),
      kg: Number((4.5 + Math.sin(i / 9) * 0.4 - i * 0.002).toFixed(2)),
    };
  });
}

export function bulkVisitsFor(catId: string): StoredVisit[] {
  if (catId !== ids.catWhiskersId) return [];
  visits ??= buildVisits();
  return visits;
}

export function bulkOrdersFor(catId: string): StoredOrder[] {
  if (catId !== ids.catWhiskersId) return [];
  orders ??= buildOrders();
  return orders;
}

export function bulkWeightsFor(catId: string): Array<{ id: string; on: string; kg: number }> {
  if (catId !== ids.catWhiskersId) return [];
  weights ??= buildWeights();
  return weights;
}

export function bulkVisitById(id: string): StoredVisit | undefined {
  return id.startsWith('b1b00000')
    ? bulkVisitsFor(ids.catWhiskersId).find((v) => v.id === id)
    : undefined;
}

export function bulkOrderById(id: string): StoredOrder | undefined {
  return id.startsWith('b2b00000')
    ? bulkOrdersFor(ids.catWhiskersId).find((o) => o.id === id)
    : undefined;
}
