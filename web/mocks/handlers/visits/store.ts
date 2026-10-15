import { loadPersisted, savePersisted, clearPersisted } from '../admin/fixtures';
import { findAppointment, zoneOf } from '../appointments/store';
import { emrFixtureIds as ids, signedFile } from './fixtures';
import { bulkOrderById, bulkVisitById } from './bulk';

/**
 * `mocks/handlers/visits/store.ts` — HO-EMR-002 §12. The in-memory stand-in for the clinic schema:
 * visits, diagnostic orders, consents and the three catalogues, seeded with one visit in each status,
 * a draft newer than the note, a service with a template, orders in every state with every flag, a
 * 10-year history for Whiskers (60 visits, 250 weights, 80 lab orders) and consent forms
 * published/unpublished. Persisted to `sessionStorage` (like `appointments/store.ts`) ONLY inside a
 * real browser running MSW — it is this mock's stand-in for Postgres, never the app's own storage;
 * the app itself writes nothing to browser storage (ADR-FE-050).
 */

export type Roles = string[];

export interface StoredDraft {
  subjective: string;
  objective: string;
  assessment: string;
  plan: string;
  dischargeInstructions: string;
  problemCodeIds: string[];
  savedOn: string;
}

export interface StoredProblem {
  id: string;
  diagnosisCodeId: string;
  certainty: 'Provisional' | 'Confirmed';
  note: string | null;
}

export interface StoredVitals {
  id: string;
  measuredOn: string;
  weightKg: number | null;
  temperatureC: number | null;
  heartRateBpm: number | null;
  respiratoryRatePerMin: number | null;
  bodyConditionScore: number | null;
  painScore: number | null;
  confirmedOutOfRange: string[];
  note: string | null;
  recordedBy: string;
  recordedOn: string;
}

export interface StoredPhoto {
  id: string;
  fileId: string;
  fileName: string;
  contentType: string;
  takenOn: string;
  note: string | null;
  addedOn: string;
}

export interface StoredVisit {
  id: string;
  version: number;
  status: 'Open' | 'Finalised';
  catId: string;
  catName: string;
  branchId: string;
  appointmentId: string;
  appointmentStatus: string;
  appointmentStartsOn: string | null;
  performerName: string | null;
  serviceId: string;
  serviceName: string;
  serviceCategory: string;
  openedOn: string;
  openedBy: string;
  note: {
    subjective: string;
    objective: string;
    assessment: string;
    plan: string;
    dischargeInstructions: string;
    savedOn: string | null;
    savedBy: string | null;
    appliedTemplateId: string | null;
    appliedTemplateVersion: number | null;
  };
  problems: StoredProblem[];
  vitals: StoredVitals[];
  photos: StoredPhoto[];
  followUp: {
    kind: 'Appointment' | 'Reminder';
    dueOn: string;
    appointmentId: string | null;
    setOn: string;
  } | null;
  addenda: Array<{
    id: string;
    text: string;
    reason: string | null;
    author: string;
    addedOn: string;
  }>;
  orderIds: string[];
  drafts: Record<string, StoredDraft>;
  finalisedOn: string | null;
  finalisedBy: string | null;
}

export interface StoredResultValue {
  id: string;
  name: string;
  valueKind: 'Numeric' | 'Qualitative';
  numericValue: string | null;
  textValue: string | null;
  unit: string | null;
  referenceLow: string | null;
  referenceHigh: string | null;
  normalText: string | null;
  flag: 'Normal' | 'Low' | 'High' | 'Abnormal' | 'NotAssessed';
}

export interface StoredResultSet {
  id: string;
  reportedOn: string;
  note: string | null;
  supersedesResultSetId: string | null;
  recordedBy: string;
  recordedOn: string;
  values: StoredResultValue[];
}

export interface StoredAttachment {
  id: string;
  resultDate: string;
  note: string | null;
  attachedBy: string;
  attachedOn: string;
  fileId: string;
  fileName: string;
  contentType: string;
  sizeInBytes: number;
}

export interface StoredOrder {
  id: string;
  version: number;
  visitId: string;
  catId: string;
  catName: string;
  branchId: string;
  kind: string;
  description: string | null;
  status: 'Pending' | 'Resulted' | 'Cancelled';
  orderedOn: string;
  orderedBy: string;
  cancelledReason: string | null;
  analyteIds: string[];
  resultSets: StoredResultSet[];
  attachments: StoredAttachment[];
}

export interface StoredConsent {
  id: string;
  version: number;
  catId: string;
  branchId: string;
  appointmentId: string;
  kind: 'Surgery' | 'Anaesthesia' | 'Euthanasia';
  procedureDescription: string;
  method: 'OnScreenSignature' | 'SignedUpload';
  signedByName: string;
  signerRelationship: string;
  signedByCustomerId: string | null;
  signedOn: string;
  policyKey: string;
  policyVersionNumber: number;
  capturedBy: string;
  witness: string | null;
  status: 'Recorded' | 'Withdrawn';
  withdrawnOn: string | null;
  withdrawnBy: string | null;
  withdrawalReason: string | null;
  strokes: number[][][] | null;
  fileId: string | null;
  fileName: string | null;
  fileContentType: string | null;
}

export interface StoredCode {
  id: string;
  code: string;
  name: string;
  category: string | null;
  externalSystem: string | null;
  externalCode: string | null;
  isActive: boolean;
  isSystem: boolean;
  version: number;
}

export interface StoredAnalyte {
  id: string;
  code: string;
  name: string;
  valueKind: 'Numeric' | 'Qualitative';
  diagnosticKind: string;
  unit: string | null;
  referenceLow: string | null;
  referenceHigh: string | null;
  normalText: string | null;
  isActive: boolean;
  version: number;
}

export interface StoredTemplate {
  id: string;
  serviceId: string;
  serviceName: string;
  name: string;
  subjective: string;
  objective: string;
  assessment: string;
  plan: string;
  dischargeInstructions: string;
  templateVersion: number;
  isActive: boolean;
  version: number;
}

export interface StoredFile {
  name: string;
  type: string;
  size: number;
}

export interface ConsentFormDoc {
  policyKey: string;
  versionNumber: number;
  title: string;
  body: string;
}

interface Db {
  visits: StoredVisit[];
  orders: StoredOrder[];
  consents: StoredConsent[];
  codes: StoredCode[];
  analytes: StoredAnalyte[];
  templates: StoredTemplate[];
  files: Record<string, StoredFile>;
  forms: Record<string, ConsentFormDoc | null>;
}

const PERSIST_KEY = '__purrcare_emr_mock_db__';
const DAY = 86_400_000;
const iso = (ms: number) => new Date(ms).toISOString();
const dateOnly = (ms: number) => new Date(ms).toISOString().slice(0, 10);

export const SERVICE_CONSULTATION = {
  id: 's0000000-0000-0000-0000-00000000a001',
  name: 'General consultation',
  category: 'Consultation',
};
const SERVICE_SPAY = { id: 's0000000-0000-0000-0000-00000000a0b2', name: 'Spay', category: 'Spay' };

/** Cat basics the history summary needs (the cat-profile mock owns the full profile). */
export const CAT_BASICS: Record<
  string,
  { name: string; sex: string; breed: string | null; dob: string; isHighRisk: boolean }
> = {
  [ids.catWhiskersId]: {
    name: 'Whiskers',
    sex: 'Male',
    breed: 'Domestic shorthair',
    dob: '2016-03-12',
    isHighRisk: false,
  },
  [ids.catMittensId]: {
    name: 'Mittens',
    sex: 'Female',
    breed: 'Domestic shorthair',
    dob: '2023-06-01',
    isHighRisk: false,
  },
  [ids.catTigerId]: {
    name: 'Tiger',
    sex: 'Male',
    breed: 'Tabby',
    dob: '2020-01-20',
    isHighRisk: true,
  },
  '20000000-0000-0000-0000-000000000002': {
    name: 'Biscuit',
    sex: 'Female',
    breed: null,
    dob: '2022-05-05',
    isHighRisk: false,
  },
};

function seedCodes(): StoredCode[] {
  const row = (
    id: string,
    code: string,
    name: string,
    category: string | null,
    extra: Partial<StoredCode> = {},
  ): StoredCode => ({
    id,
    code,
    name,
    category,
    externalSystem: null,
    externalCode: null,
    isActive: true,
    isSystem: false,
    version: 1,
    ...extra,
  });
  return [
    row(ids.codeNadId, 'SYS-NAD', 'Routine visit — no abnormality detected', 'Routine', {
      isSystem: true,
    }),
    row(ids.codeOtitisId, 'EAR-OTE', 'Otitis externa', 'Ear'),
    row(ids.codeCkdId, 'REN-CKD2', 'Chronic kidney disease, IRIS stage 2', 'Renal', {
      externalSystem: 'VeNom',
      externalCode: '1234',
    }),
    row(ids.codeRetiredId, 'OLD-GEN', 'Gingivitis (retired code)', 'Dental', { isActive: false }),
    row('b4000000-0000-0000-0000-000000000005', 'DER-FLA', 'Flea allergy dermatitis', 'Skin'),
    row('b4000000-0000-0000-0000-000000000006', 'GIT-VOM', 'Vomiting', 'Digestive'),
    row(
      'b4000000-0000-0000-0000-000000000007',
      'RES-URI',
      'Upper respiratory infection',
      'Respiratory',
    ),
    row('b4000000-0000-0000-0000-000000000008', 'DEN-TAR', 'Dental tartar', 'Dental'),
    row('b4000000-0000-0000-0000-000000000009', 'END-HYP', 'Hyperthyroidism', 'Endocrine'),
    row('b4000000-0000-0000-0000-00000000000a', 'EYE-CON', 'Conjunctivitis', 'Eye'),
    row('b4000000-0000-0000-0000-00000000000b', 'ORT-ART', 'Arthritis', 'Orthopaedic'),
    row(
      'b4000000-0000-0000-0000-00000000000c',
      'URI-FLU',
      'Feline lower urinary tract disease',
      'Urinary',
    ),
  ];
}

function seedAnalytes(): StoredAnalyte[] {
  const num = (
    id: string,
    code: string,
    name: string,
    unit: string,
    low: string,
    high: string,
  ): StoredAnalyte => ({
    id,
    code,
    name,
    valueKind: 'Numeric',
    diagnosticKind: 'BloodWork',
    unit,
    referenceLow: low,
    referenceHigh: high,
    normalText: null,
    isActive: true,
    version: 1,
  });
  return [
    num(ids.analyteCreatinineId, 'CREA', 'Creatinine', 'µmol/L', '71', '212'),
    num(ids.analyteUreaId, 'UREA', 'Urea', 'mmol/L', '5.7', '12.9'),
    num('b5000000-0000-0000-0000-000000000004', 'GLU', 'Glucose', 'mmol/L', '3.9', '8.3'),
    num('b5000000-0000-0000-0000-000000000005', 'ALT', 'ALT', 'U/L', '12', '130'),
    num('b5000000-0000-0000-0000-000000000006', 'T4', 'Total T4', 'nmol/L', '10', '55'),
    {
      id: ids.analyteFelvId,
      code: 'FELV',
      name: 'FeLV antigen',
      valueKind: 'Qualitative',
      diagnosticKind: 'FelvFivTest',
      unit: null,
      referenceLow: null,
      referenceHigh: null,
      normalText: 'Negative',
      isActive: true,
      version: 1,
    },
    {
      id: 'b5000000-0000-0000-0000-000000000007',
      code: 'FIV',
      name: 'FIV antibody',
      valueKind: 'Qualitative',
      diagnosticKind: 'FelvFivTest',
      unit: null,
      referenceLow: null,
      referenceHigh: null,
      normalText: 'Negative',
      isActive: true,
      version: 1,
    },
    {
      id: 'b5000000-0000-0000-0000-000000000008',
      code: 'SG',
      name: 'Specific gravity',
      valueKind: 'Numeric',
      diagnosticKind: 'Urinalysis',
      unit: null,
      referenceLow: '1.035',
      referenceHigh: '1.060',
      normalText: null,
      isActive: false,
      version: 2,
    },
  ];
}

function seedTemplates(): StoredTemplate[] {
  return [
    {
      id: 'b6000000-0000-0000-0000-000000000001',
      serviceId: SERVICE_CONSULTATION.id,
      serviceName: SERVICE_CONSULTATION.name,
      name: 'General consultation',
      subjective: 'Presenting complaint:\nDuration:\nAppetite / water / litter:',
      objective: 'Demeanour:\nMucous membranes:\nAbdominal palpation:',
      assessment: '',
      plan: '',
      dischargeInstructions: 'Return if your cat stops eating or becomes lethargic.',
      templateVersion: 2,
      isActive: true,
      version: 2,
    },
  ];
}

function seedForms(): Record<string, ConsentFormDoc | null> {
  return {
    Surgery: {
      policyKey: 'consent-surgery',
      versionNumber: 3,
      title: 'Consent to surgery',
      body: '## Consent to surgery\n\nI authorise the clinic to perform the procedure described below on my cat.\n\n- I understand the risks, including those of anaesthesia.\n- I have been told the estimated cost.\n\nSigned by the owner or their authorised agent.',
    },
    Anaesthesia: {
      policyKey: 'consent-anaesthesia',
      versionNumber: 1,
      title: 'Consent to anaesthesia',
      body: '## Consent to anaesthesia\n\nI consent to general anaesthesia for my cat.\n\n- Fasting instructions were explained to me.',
    },
    Euthanasia: null,
  };
}

function vitalsEntry(id: string, ms: number, over: Partial<StoredVitals>): StoredVitals {
  return {
    id,
    measuredOn: iso(ms),
    weightKg: null,
    temperatureC: null,
    heartRateBpm: null,
    respiratoryRatePerMin: null,
    bodyConditionScore: null,
    painScore: null,
    confirmedOutOfRange: [],
    note: null,
    recordedBy: 'Nadia Nurse',
    recordedOn: iso(ms),
    ...over,
  };
}

const emptyNote = () => ({
  subjective: '',
  objective: '',
  assessment: '',
  plan: '',
  dischargeInstructions: '',
  savedOn: null,
  savedBy: null,
  appliedTemplateId: null,
  appliedTemplateVersion: null,
});

function baseVisit(
  over: Partial<StoredVisit> & Pick<StoredVisit, 'id' | 'catId' | 'appointmentId'>,
): StoredVisit {
  const basics = CAT_BASICS[over.catId];
  const now = Date.now();
  return {
    version: 1,
    status: 'Open',
    catName: basics?.name ?? 'Cat',
    branchId: ids.mainBranchId,
    appointmentStatus: 'InConsultation',
    appointmentStartsOn: iso(now - 50 * 60_000),
    performerName: ids.vetStaffName,
    serviceId: SERVICE_CONSULTATION.id,
    serviceName: SERVICE_CONSULTATION.name,
    serviceCategory: SERVICE_CONSULTATION.category,
    openedOn: iso(now - 45 * 60_000),
    openedBy: ids.vetStaffName,
    note: emptyNote(),
    problems: [],
    vitals: [],
    photos: [],
    followUp: null,
    addenda: [],
    orderIds: [],
    drafts: {},
    finalisedOn: null,
    finalisedBy: null,
    ...over,
  };
}

function seedVisits(): { visits: StoredVisit[]; orders: StoredOrder[] } {
  const now = Date.now();
  const actualAppt = findAppointment('a4000000-0000-0000-0000-00000000000c');
  const open = baseVisit({
    id: ids.visitOpenId,
    catId: ids.catWhiskersId,
    appointmentId: 'a4000000-0000-0000-0000-00000000000c',
    version: 4,
    appointmentStartsOn: actualAppt?.startsOn ?? iso(now - 50 * 60_000),
    note: {
      subjective:
        'Owner reports head shaking and scratching at the left ear for 5 days. Eating and drinking normally.',
      objective: 'Left ear canal: erythema, brown waxy discharge. Right ear unremarkable.',
      assessment: 'Left otitis externa, likely yeast overgrowth.',
      plan: 'Ear flush today. Topical drops once daily for 10 days.',
      dischargeInstructions: 'Apply 4 drops into the left ear once a day.',
      savedOn: iso(now - 20 * 60_000),
      savedBy: ids.vetStaffName,
      appliedTemplateId: null,
      appliedTemplateVersion: null,
    },
    problems: [
      {
        id: 'b7000000-0000-0000-0000-000000000001',
        diagnosisCodeId: ids.codeOtitisId,
        certainty: 'Provisional',
        note: 'left ear',
      },
      {
        id: 'b7000000-0000-0000-0000-000000000002',
        diagnosisCodeId: ids.codeCkdId,
        certainty: 'Confirmed',
        note: null,
      },
    ],
    vitals: [
      vitalsEntry('b8000000-0000-0000-0000-000000000001', now - 40 * 60_000, {
        weightKg: 4.2,
        temperatureC: 39.6,
        heartRateBpm: 188,
        respiratoryRatePerMin: 32,
        bodyConditionScore: 5,
        painScore: 2,
      }),
    ],
    photos: [
      {
        id: 'b9000000-0000-0000-0000-000000000001',
        fileId: 'seed-photo-1',
        fileName: 'left-ear.png',
        contentType: 'image/png',
        takenOn: dateOnly(now),
        note: 'Left ear canal before flush',
        addedOn: iso(now - 30 * 60_000),
      },
    ],
    orderIds: [ids.orderPendingId, ids.orderResultedId, ids.orderImagingId],
  });

  const draftNewer = baseVisit({
    id: ids.visitDraftNewerId,
    catId: ids.catTigerId,
    appointmentId: 'b1a00000-0000-0000-0000-000000000002',
    version: 2,
    note: {
      ...emptyNote(),
      subjective: 'Saved earlier: limping on the left hind leg.',
      savedOn: iso(now - 30 * 60_000),
      savedBy: ids.vetStaffName,
    },
    drafts: {
      Vet: {
        subjective:
          'Saved earlier: limping on the left hind leg. Draft addition: worse after jumping.',
        objective: 'Pain on palpation of the left stifle.',
        assessment: '',
        plan: '',
        dischargeInstructions: '',
        problemCodeIds: [ids.codeCkdId],
        savedOn: iso(now - 5 * 60_000),
      },
    },
  });

  const template = baseVisit({
    id: ids.visitTemplateId,
    catId: ids.catMittensId,
    appointmentId: 'b1a00000-0000-0000-0000-000000000003',
  });
  const empty = baseVisit({
    id: ids.visitEmptyId,
    catId: '20000000-0000-0000-0000-000000000002',
    appointmentId: 'b1a00000-0000-0000-0000-000000000004',
    serviceId: SERVICE_SPAY.id,
    serviceName: SERVICE_SPAY.name,
    serviceCategory: SERVICE_SPAY.category,
  });

  const finalised = baseVisit({
    id: ids.visitFinalisedId,
    catId: ids.catMittensId,
    appointmentId: 'b1a00000-0000-0000-0000-000000000005',
    version: 9,
    status: 'Finalised',
    appointmentStatus: 'InConsultation',
    openedOn: iso(now - 3 * DAY),
    appointmentStartsOn: iso(now - 3 * DAY),
    note: {
      subjective: 'Vomiting twice yesterday. Eating normally today.',
      objective: 'Bright and alert. Abdomen soft.',
      assessment: 'Mild gastritis.',
      plan: 'Bland diet for 3 days.',
      dischargeInstructions: 'Small frequent meals of the prescribed diet.',
      savedOn: iso(now - 3 * DAY + 3_000_000),
      savedBy: ids.vetStaffName,
      appliedTemplateId: null,
      appliedTemplateVersion: null,
    },
    problems: [
      {
        id: 'b7000000-0000-0000-0000-000000000009',
        diagnosisCodeId: 'b4000000-0000-0000-0000-000000000006',
        certainty: 'Confirmed',
        note: null,
      },
    ],
    vitals: [
      vitalsEntry('b8000000-0000-0000-0000-000000000009', now - 3 * DAY, {
        weightKg: 3.85,
        temperatureC: 38.5,
        heartRateBpm: 180,
      }),
    ],
    followUp: {
      kind: 'Reminder',
      dueOn: dateOnly(now + 11 * DAY),
      appointmentId: null,
      setOn: iso(now - 3 * DAY + 3_100_000),
    },
    addenda: [
      {
        id: 'ba000000-0000-0000-0000-000000000001',
        text: 'Owner phoned: vomiting has stopped.',
        reason: 'Telephone follow-up',
        author: ids.vetStaffName,
        addedOn: iso(now - 2 * DAY),
      },
    ],
    orderIds: [ids.orderFinalisedId],
    finalisedOn: iso(now - 3 * DAY + 3_200_000),
    finalisedBy: ids.vetStaffName,
  });

  const otherBranch = baseVisit({
    id: ids.visitOtherBranchId,
    catId: ids.catTigerId,
    appointmentId: 'b1a00000-0000-0000-0000-000000000006',
    branchId: ids.londonBranchId,
  });

  const orders: StoredOrder[] = [
    {
      id: ids.orderPendingId,
      version: 1,
      visitId: ids.visitOpenId,
      catId: ids.catWhiskersId,
      catName: 'Whiskers',
      branchId: ids.mainBranchId,
      kind: 'BloodWork',
      description: null,
      status: 'Pending',
      orderedOn: iso(now - 38 * 60_000),
      orderedBy: ids.vetStaffName,
      cancelledReason: null,
      analyteIds: [ids.analyteCreatinineId, ids.analyteUreaId],
      resultSets: [],
      attachments: [],
    },
    {
      id: ids.orderResultedId,
      version: 3,
      visitId: ids.visitOpenId,
      catId: ids.catWhiskersId,
      catName: 'Whiskers',
      branchId: ids.mainBranchId,
      kind: 'BloodWork',
      description: null,
      status: 'Resulted',
      orderedOn: iso(now - 37 * 60_000),
      orderedBy: ids.vetStaffName,
      cancelledReason: null,
      analyteIds: [
        ids.analyteCreatinineId,
        ids.analyteUreaId,
        'b5000000-0000-0000-0000-000000000004',
        ids.analyteFelvId,
      ],
      resultSets: [
        {
          id: 'bb000000-0000-0000-0000-000000000001',
          reportedOn: dateOnly(now - DAY),
          note: 'First run — haemolysed sample',
          supersedesResultSetId: null,
          recordedBy: ids.vetStaffName,
          recordedOn: iso(now - 25 * 60_000),
          values: [
            resultValue(
              'bc000000-0000-0000-0000-000000000001',
              'Creatinine',
              '300',
              'µmol/L',
              '71',
              '212',
            ),
          ],
        },
        {
          id: 'bb000000-0000-0000-0000-000000000002',
          reportedOn: dateOnly(now),
          note: 'Repeat sample',
          supersedesResultSetId: 'bb000000-0000-0000-0000-000000000001',
          recordedBy: ids.vetStaffName,
          recordedOn: iso(now - 15 * 60_000),
          values: [
            resultValue(
              'bc000000-0000-0000-0000-000000000002',
              'Creatinine',
              '240',
              'µmol/L',
              '71',
              '212',
            ),
            resultValue(
              'bc000000-0000-0000-0000-000000000003',
              'Urea',
              '4.1',
              'mmol/L',
              '5.7',
              '12.9',
            ),
            resultValue(
              'bc000000-0000-0000-0000-000000000004',
              'Glucose',
              '5.5',
              'mmol/L',
              '3.9',
              '8.3',
            ),
            {
              id: 'bc000000-0000-0000-0000-000000000005',
              name: 'FeLV antigen',
              valueKind: 'Qualitative',
              numericValue: null,
              textValue: 'Positive',
              unit: null,
              referenceLow: null,
              referenceHigh: null,
              normalText: 'Negative',
              flag: 'Abnormal',
            },
            {
              id: 'bc000000-0000-0000-0000-000000000006',
              name: 'Sample quality',
              valueKind: 'Qualitative',
              numericValue: null,
              textValue: 'Adequate',
              unit: null,
              referenceLow: null,
              referenceHigh: null,
              normalText: null,
              flag: 'NotAssessed',
            },
          ],
        },
      ],
      attachments: [],
    },
    {
      id: ids.orderImagingId,
      version: 2,
      visitId: ids.visitOpenId,
      catId: ids.catWhiskersId,
      catName: 'Whiskers',
      branchId: ids.mainBranchId,
      kind: 'Imaging',
      description: 'Left stifle radiograph',
      status: 'Pending',
      orderedOn: iso(now - 36 * 60_000),
      orderedBy: ids.vetStaffName,
      cancelledReason: null,
      analyteIds: [],
      resultSets: [],
      attachments: [
        {
          id: 'bd000000-0000-0000-0000-000000000001',
          resultDate: dateOnly(now),
          note: 'Report',
          attachedBy: ids.vetStaffName,
          attachedOn: iso(now - 10 * 60_000),
          fileId: 'seed-file-pdf',
          fileName: 'radiology-report.pdf',
          contentType: 'application/pdf',
          sizeInBytes: 182_000,
        },
        {
          id: 'bd000000-0000-0000-0000-000000000002',
          resultDate: dateOnly(now),
          note: 'DICOM study',
          attachedBy: ids.vetStaffName,
          attachedOn: iso(now - 9 * 60_000),
          fileId: 'seed-file-dcm',
          fileName: 'stifle-study.dcm',
          contentType: 'application/dicom',
          sizeInBytes: 4_400_000,
        },
      ],
    },
    {
      id: ids.orderFinalisedId,
      version: 2,
      visitId: ids.visitFinalisedId,
      catId: ids.catMittensId,
      catName: 'Mittens',
      branchId: ids.mainBranchId,
      kind: 'BloodWork',
      description: null,
      status: 'Resulted',
      orderedOn: iso(now - 3 * DAY),
      orderedBy: ids.vetStaffName,
      cancelledReason: null,
      analyteIds: [ids.analyteCreatinineId],
      resultSets: [
        {
          id: 'bb000000-0000-0000-0000-000000000009',
          reportedOn: dateOnly(now - 3 * DAY),
          note: null,
          supersedesResultSetId: null,
          recordedBy: ids.vetStaffName,
          recordedOn: iso(now - 3 * DAY + 600_000),
          values: [
            resultValue(
              'bc000000-0000-0000-0000-000000000009',
              'Creatinine',
              '180',
              'µmol/L',
              '71',
              '212',
            ),
          ],
        },
      ],
      attachments: [
        {
          id: 'bd000000-0000-0000-0000-000000000009',
          resultDate: dateOnly(now - 3 * DAY),
          note: null,
          attachedBy: ids.vetStaffName,
          attachedOn: iso(now - 3 * DAY),
          fileId: 'seed-file-pdf',
          fileName: 'lab-report.pdf',
          contentType: 'application/pdf',
          sizeInBytes: 90_000,
        },
      ],
    },
  ];

  return { visits: [open, draftNewer, template, empty, finalised, otherBranch], orders };
}

export function classify(
  value: number,
  low: number | null,
  high: number | null,
): StoredResultValue['flag'] {
  if (low === null && high === null) return 'NotAssessed';
  if (low !== null && value < low) return 'Low';
  if (high !== null && value > high) return 'High';
  return 'Normal';
}

function resultValue(
  id: string,
  name: string,
  value: string,
  unit: string,
  low: string,
  high: string,
): StoredResultValue {
  return {
    id,
    name,
    valueKind: 'Numeric',
    numericValue: value,
    textValue: null,
    unit,
    referenceLow: low,
    referenceHigh: high,
    normalText: null,
    flag: classify(Number(value), Number(low), Number(high)),
  };
}

function seedConsents(): StoredConsent[] {
  const now = Date.now();
  const base = (id: string, over: Partial<StoredConsent>): StoredConsent => ({
    id,
    version: 1,
    catId: ids.catMittensId,
    branchId: ids.mainBranchId,
    appointmentId: 'b1a00000-0000-0000-0000-000000000010',
    kind: 'Surgery',
    procedureDescription: 'Spay (ovariohysterectomy)',
    method: 'OnScreenSignature',
    signedByName: 'Sam Rahman',
    signerRelationship: 'Owner',
    signedByCustomerId: null,
    signedOn: iso(now - 2 * DAY),
    policyKey: 'consent-surgery',
    policyVersionNumber: 3,
    capturedBy: 'Reception Desk',
    witness: 'Nadia Nurse',
    status: 'Recorded',
    withdrawnOn: null,
    withdrawnBy: null,
    withdrawalReason: null,
    strokes: [
      [
        [0.1, 0.5],
        [0.2, 0.3],
        [0.3, 0.6],
        [0.4, 0.4],
        [0.5, 0.55],
        [0.6, 0.3],
        [0.7, 0.5],
      ],
    ],
    fileId: null,
    fileName: null,
    fileContentType: null,
    ...over,
  });
  return [
    base(ids.consentRecordedId, {}),
    base(ids.consentWithdrawnId, {
      kind: 'Anaesthesia',
      procedureDescription: 'Dental under anaesthesia',
      signedOn: iso(now - 9 * DAY),
      policyKey: 'consent-anaesthesia',
      policyVersionNumber: 1,
      status: 'Withdrawn',
      withdrawnOn: iso(now - 8 * DAY),
      withdrawnBy: 'Reception Desk',
      withdrawalReason: 'Owner postponed the procedure.',
    }),
    base(ids.consentUploadId, {
      method: 'SignedUpload',
      strokes: null,
      fileId: 'seed-file-pdf',
      fileName: 'signed-consent.pdf',
      fileContentType: 'application/pdf',
      signedOn: iso(now - 20 * DAY),
    }),
  ];
}

function seed(): Db {
  const { visits, orders } = seedVisits();
  return {
    visits,
    orders,
    consents: seedConsents(),
    codes: seedCodes(),
    analytes: seedAnalytes(),
    templates: seedTemplates(),
    files: {
      'seed-photo-1': { name: 'left-ear.png', type: 'image/png', size: 70 },
      'seed-file-pdf': { name: 'radiology-report.pdf', type: 'application/pdf', size: 182_000 },
      'seed-file-dcm': { name: 'stifle-study.dcm', type: 'application/dicom', size: 4_400_000 },
    },
    forms: seedForms(),
  };
}

let db: Db = loadPersisted<Db>(PERSIST_KEY) ?? seed();

export function getDb(): Db {
  return db;
}

export function persist(): void {
  savePersisted(PERSIST_KEY, db);
}

export function resetEmrMockData(): void {
  clearPersisted(PERSIST_KEY);
  db = seed();
}

export function rememberFile(fileId: string, file: StoredFile): void {
  db.files[fileId] = file;
  persist();
}

export function fileInfo(fileId: string): StoredFile {
  return db.files[fileId] ?? { name: 'attachment', type: 'application/octet-stream', size: 1000 };
}

export function filePayload(fileId: string, fallbackName?: string, fallbackType?: string) {
  const info = db.files[fileId];
  return signedFile(
    fileId,
    fallbackType ?? info?.type ?? 'application/octet-stream',
    fallbackName ?? info?.name ?? 'attachment',
    info?.size,
  );
}

// ---- lookups ---------------------------------------------------------------------------------

export function findVisit(id: string): StoredVisit | undefined {
  return db.visits.find((v) => v.id === id) ?? bulkVisitById(id);
}

export function findOrder(id: string): StoredOrder | undefined {
  return db.orders.find((o) => o.id === id) ?? bulkOrderById(id);
}

export function findCode(id: string): StoredCode | undefined {
  return db.codes.find((c) => c.id === id);
}

export function visitForAppointment(appointmentId: string): StoredVisit | undefined {
  return db.visits.find((v) => v.appointmentId === appointmentId);
}

export function zoneForBranch(branchId: string): string {
  return zoneOf(branchId);
}

export function authorKey(roles: Roles): string {
  return roles.includes('Vet') ? 'Vet' : roles.includes('Nurse') ? 'Nurse' : (roles[0] ?? 'Staff');
}

/** `VisitAllowedActions.For` — computed from the same role table the endpoints enforce. */
export function allowedActionsFor(
  visit: StoredVisit,
  holdsAction: (action: string) => boolean,
): string[] {
  const live = visit.appointmentStatus !== 'Cancelled' && visit.appointmentStatus !== 'NoShow';
  const actions: string[] = [];
  if (visit.status === 'Open' && live) {
    if (holdsAction('Visits.RecordVitals')) actions.push('recordVitals');
    if (holdsAction('Visits.WriteNote'))
      actions.push('editNote', 'orderDiagnostic', 'attachPhoto', 'setFollowUp');
    if (holdsAction('Visits.Finalise')) actions.push('finalise');
  } else if (visit.status === 'Finalised') {
    if (holdsAction('Visits.AddAddendum')) actions.push('addAddendum');
  }
  return actions;
}
