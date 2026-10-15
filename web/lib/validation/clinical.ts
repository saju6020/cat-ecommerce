import type {
  DraftRequest,
  ProblemCertainty,
  RecordVitalsRequest,
  SaveNoteRequest,
  VisitDetail,
  VitalSign,
  VitalSignRule,
} from '@/lib/api/visits';
import type { ResultFlag } from '@/lib/api/diagnostics';
import { zonedLocalToInstant } from '@/lib/format/appointment';

/**
 * `lib/validation/clinical.ts` — HO-EMR-002. Pure helpers shared by the clinical forms: the server's
 * text limits (`ClinicalNoteRules`), the consultation form model and how it becomes a draft/note
 * request, the finalise checklist HINT (the server still decides — §20.2), the vitals parser and the
 * result-flag PREVIEW (same rule as `ReferenceRangeClassifier`, replaced by the server's flag on
 * save).
 */

// ---- limits (ClinicalNoteRules / DiagnosticRules on master) ---------------------------------

export const MAX_SOAP_FIELD = 20_000;
export const MAX_DISCHARGE_INSTRUCTIONS = 10_000;
export const MAX_PROBLEMS = 20;
export const MAX_PROBLEM_NOTE = 500;
export const MAX_VITALS_NOTE = 500;
export const MAX_PHOTO_NOTE = 500;
export const MAX_ADDENDUM = 4_000;
export const MAX_ADDENDUM_REASON = 500;
export const MAX_DIAGNOSTIC_DESCRIPTION = 500;
export const MAX_RESULT_NOTE = 500;
export const MAX_VALUE_NAME = 100;
export const MAX_TEXT_VALUE = 100;
export const MAX_UNIT = 30;
export const MAX_NORMAL_TEXT = 50;

// ---- the consultation form model -------------------------------------------------------------

export interface ProblemRow {
  /** Stable React key — a new one per added row, the server's `problemId` for a saved one. */
  key: string;
  problemId?: string;
  diagnosisCodeId: string;
  code: string;
  /** Empty while a restored draft's code is still being looked up. */
  name: string;
  certainty: ProblemCertainty;
  note: string;
}

export interface NoteFormValues {
  subjective: string;
  objective: string;
  assessment: string;
  plan: string;
  dischargeInstructions: string;
  problems: ProblemRow[];
}

export type NoteSource = 'draft' | 'saved' | 'template' | 'empty';

export interface InitialNote {
  values: NoteFormValues;
  /** What the record holds — the baseline the unsaved-changes guard compares against. */
  saved: NoteFormValues;
  source: NoteSource;
}

function savedProblems(visit: VisitDetail): ProblemRow[] {
  return visit.problems.map((p) => ({
    key: p.id,
    problemId: p.id,
    diagnosisCodeId: p.diagnosisCodeId,
    code: p.code,
    name: p.name,
    certainty: p.certainty,
    note: p.note ?? '',
  }));
}

/**
 * ADR-FE-050 / §8.3: initialised ONCE per visit load from (restored draft ‖ saved note ‖ template).
 * The server returns a draft only when it is the caller's own AND newer than the note, and a
 * template only while no note has ever been saved.
 */
export function initialNoteValues(visit: VisitDetail): InitialNote {
  const note = visit.note;
  const saved: NoteFormValues = {
    subjective: note.subjective,
    objective: note.objective,
    assessment: note.assessment,
    plan: note.plan,
    dischargeInstructions: note.dischargeInstructions,
    problems: savedProblems(visit),
  };

  if (visit.draft) {
    const byCode = new Map(saved.problems.map((p) => [p.diagnosisCodeId, p]));
    return {
      source: 'draft',
      saved,
      values: {
        subjective: visit.draft.subjective,
        objective: visit.draft.objective,
        assessment: visit.draft.assessment,
        plan: visit.draft.plan,
        dischargeInstructions: visit.draft.dischargeInstructions,
        problems: visit.draft.problemCodeIds.map(
          (codeId) =>
            byCode.get(codeId) ?? {
              key: `draft-${codeId}`,
              diagnosisCodeId: codeId,
              code: '',
              name: '',
              certainty: 'Provisional' as const,
              note: '',
            },
        ),
      },
    };
  }

  if (visit.template && note.savedOn === null) {
    const t = visit.template;
    return {
      source: 'template',
      saved,
      values: {
        subjective: t.subjective,
        objective: t.objective,
        assessment: t.assessment,
        plan: t.plan,
        dischargeInstructions: t.dischargeInstructions,
        problems: [],
      },
    };
  }

  return { source: note.savedOn === null ? 'empty' : 'saved', saved, values: saved };
}

export function toDraftRequest(values: NoteFormValues): DraftRequest {
  return {
    subjective: values.subjective,
    objective: values.objective,
    assessment: values.assessment,
    plan: values.plan,
    dischargeInstructions: values.dischargeInstructions,
    problemCodeIds: values.problems.map((p) => p.diagnosisCodeId),
  };
}

export function toSaveNoteRequest(
  values: NoteFormValues,
  expectedVersion: number,
  template: { id: string; version: number } | null,
): SaveNoteRequest {
  return {
    expectedVersion,
    subjective: values.subjective,
    objective: values.objective,
    assessment: values.assessment,
    plan: values.plan,
    dischargeInstructions: values.dischargeInstructions,
    problems: values.problems.map((p) => ({
      ...(p.problemId ? { problemId: p.problemId } : {}),
      diagnosisCodeId: p.diagnosisCodeId,
      certainty: p.certainty,
      ...(p.note.trim() ? { note: p.note.trim() } : {}),
    })),
    ...(template
      ? { appliedTemplateId: template.id, appliedTemplateVersion: template.version }
      : {}),
  };
}

function comparable(values: NoteFormValues): string {
  return JSON.stringify([
    values.subjective,
    values.objective,
    values.assessment,
    values.plan,
    values.dischargeInstructions,
    values.problems.map((p) => [p.diagnosisCodeId, p.certainty, p.note]),
  ]);
}

/** Dirty against the SAVED note: the working copy protects content, the guard protects intent. */
export function isNoteDirty(current: NoteFormValues, saved: NoteFormValues): boolean {
  return comparable(current) !== comparable(saved);
}

// ---- finalise checklist (a HINT — the server decides, §20.2) -------------------------------

export interface ChecklistItem {
  id: 'problem' | 'assessment' | 'plan';
  label: string;
  done: boolean;
  /** The id of the field the label links to. */
  fieldId: string;
}

export function finaliseChecklist(values: NoteFormValues): ChecklistItem[] {
  return [
    {
      id: 'problem',
      label: 'At least one problem on the list',
      done: values.problems.length > 0,
      fieldId: 'problem-search',
    },
    {
      id: 'assessment',
      label: 'Assessment written',
      done: values.assessment.trim().length > 0,
      fieldId: 'note-assessment',
    },
    {
      id: 'plan',
      label: 'Plan written',
      done: values.plan.trim().length > 0,
      fieldId: 'note-plan',
    },
  ];
}

// ---- vitals -----------------------------------------------------------------------------------

export interface VitalsFormValues {
  weight: string;
  temperature: string;
  heartRate: string;
  respiratoryRate: string;
  bodyConditionScore: string;
  painScore: string;
  measuredOn: string;
  note: string;
}

export const EMPTY_VITALS: VitalsFormValues = {
  weight: '',
  temperature: '',
  heartRate: '',
  respiratoryRate: '',
  bodyConditionScore: '',
  painScore: '',
  measuredOn: '',
  note: '',
};

export const VITAL_FIELDS: Array<{
  field: keyof Omit<VitalsFormValues, 'measuredOn' | 'note'>;
  sign: VitalSign;
  label: string;
  unitName: string;
  fallbackSymbol: string;
  fallbackDecimals: number;
  request: keyof RecordVitalsRequest;
}> = [
  {
    field: 'weight',
    sign: 'Weight',
    label: 'Weight',
    unitName: 'kilograms',
    fallbackSymbol: 'kg',
    fallbackDecimals: 2,
    request: 'weightKg',
  },
  {
    field: 'temperature',
    sign: 'Temperature',
    label: 'Temperature',
    unitName: 'degrees Celsius',
    fallbackSymbol: '°C',
    fallbackDecimals: 1,
    request: 'temperatureC',
  },
  {
    field: 'heartRate',
    sign: 'HeartRate',
    label: 'Heart rate',
    unitName: 'beats per minute',
    fallbackSymbol: 'bpm',
    fallbackDecimals: 0,
    request: 'heartRateBpm',
  },
  {
    field: 'respiratoryRate',
    sign: 'RespiratoryRate',
    label: 'Respiratory rate',
    unitName: 'breaths per minute',
    fallbackSymbol: 'breaths/min',
    fallbackDecimals: 0,
    request: 'respiratoryRatePerMin',
  },
  {
    field: 'bodyConditionScore',
    sign: 'BodyConditionScore',
    label: 'Body condition score',
    unitName: 'out of 9',
    fallbackSymbol: '/9',
    fallbackDecimals: 0,
    request: 'bodyConditionScore',
  },
  {
    field: 'painScore',
    sign: 'PainScore',
    label: 'Pain score',
    unitName: 'out of 10',
    fallbackSymbol: '/10',
    fallbackDecimals: 0,
    request: 'painScore',
  },
];

/** `"Outside the usual range 35.0–41.5 °C — you'll be asked to confirm"` for a value outside the
 *  server's plausible range, else `null`. Informational only: the save is what the server decides. */
export function plausibleRangeHint(value: string, rule: VitalSignRule | undefined): string | null {
  if (!rule || rule.plausibleMin === null || rule.plausibleMax === null) return null;
  const n = Number(value);
  if (value === '' || value === '.' || Number.isNaN(n)) return null;
  if (n >= Number(rule.plausibleMin) && n <= Number(rule.plausibleMax)) return null;
  return `Outside the usual range ${rule.plausibleMin}–${rule.plausibleMax} ${rule.unit} — you’ll be asked to confirm`;
}

export function hasAnyVital(values: VitalsFormValues): boolean {
  return VITAL_FIELDS.some(({ field }) => values[field].trim() !== '' && values[field] !== '.');
}

/** The vitals request. `measuredOn` is entered as branch-local wall-clock time and sent as an ISO
 *  instant WITH its offset (`Z`) — the server normalises to UTC either way. */
export function toVitalsRequest(
  values: VitalsFormValues,
  confirmedOutOfRange: VitalSign[],
  zone: string,
): RecordVitalsRequest {
  const request: RecordVitalsRequest = {};
  for (const { field, request: key } of VITAL_FIELDS) {
    const raw = values[field].trim();
    if (raw !== '' && raw !== '.') (request as Record<string, unknown>)[key] = Number(raw);
  }
  if (values.measuredOn) {
    const [date, time] = values.measuredOn.split('T');
    if (date && time) request.measuredOn = zonedLocalToInstant(date, time, zone);
  }
  if (values.note.trim()) request.note = values.note.trim();
  if (confirmedOutOfRange.length > 0) request.confirmedOutOfRange = confirmedOutOfRange;
  return request;
}

// ---- result flag preview (same rule as the server's ReferenceRangeClassifier) ----------------

export function previewNumericFlag(
  value: number,
  low: number | null,
  high: number | null,
): ResultFlag {
  if (low === null && high === null) return 'NotAssessed';
  if (low !== null && value < low) return 'Low';
  if (high !== null && value > high) return 'High';
  return 'Normal';
}

export function previewQualitativeFlag(value: string, normalText: string): ResultFlag {
  if (!normalText.trim()) return 'NotAssessed';
  return value.trim().toLowerCase() === normalText.trim().toLowerCase() ? 'Normal' : 'Abnormal';
}
