import { z } from 'zod';
import { surgeryViolations } from '@/lib/api/errors';

/**
 * `lib/validation/surgery.ts` — HO-SRG-002 §13. Limits mirror `SurgeryRules` (HO-SRG-001): the server
 * enforces them again; these only stop a request that cannot succeed and show the counter.
 */
export const SURGERY_LIMITS = {
  overrideReasonMin: 10,
  overrideReasonMax: 1000,
  correctionReasonMin: 10,
  correctionReasonMax: 500,
  cancellationReasonMax: 500,
  waiverReasonMax: 500,
  noteMax: 500,
  postOpInstructionsMax: 4000,
  fastingInstructionsMax: 2000,
  reportTextMax: 4000,
  addendumMax: 4000,
  eligibilityNoteMax: 500,
  campaignCodeMax: 30,
  campaignNameMax: 200,
  campaignDescriptionMax: 2000,
  maxFollowUps: 5,
} as const;

export const overrideReasonSchema = z
  .string()
  .trim()
  .min(
    SURGERY_LIMITS.overrideReasonMin,
    `Give at least ${SURGERY_LIMITS.overrideReasonMin} characters.`,
  )
  .max(
    SURGERY_LIMITS.overrideReasonMax,
    `Use ${SURGERY_LIMITS.overrideReasonMax} characters or fewer.`,
  );

export const cancelReasonSchema = z
  .string()
  .trim()
  .min(1, 'Give a reason for cancelling.')
  .max(
    SURGERY_LIMITS.cancellationReasonMax,
    `Use ${SURGERY_LIMITS.cancellationReasonMax} characters or fewer.`,
  );

export const correctionReasonSchema = z
  .string()
  .trim()
  .min(
    SURGERY_LIMITS.correctionReasonMin,
    `Give at least ${SURGERY_LIMITS.correctionReasonMin} characters.`,
  )
  .max(
    SURGERY_LIMITS.correctionReasonMax,
    `Use ${SURGERY_LIMITS.correctionReasonMax} characters or fewer.`,
  );

export type BookingField =
  | 'cat'
  | 'procedure'
  | 'start'
  | 'theatreId'
  | 'surgeonId'
  | 'assistantId'
  | 'campaign'
  | 'campaignNote';

/** The server's `propertyName` (lower-camel) → the booking field it belongs beside. */
const BOOKING_FIELDS: Readonly<Record<string, BookingField>> = {
  catId: 'cat',
  confirmNeuterStatusDiscrepancy: 'cat',
  serviceId: 'procedure',
  startsOn: 'start',
  theatreId: 'theatreId',
  surgeonId: 'surgeonId',
  assistantId: 'assistantId',
  campaignId: 'campaign',
  campaignEligibilityNote: 'campaignNote',
};

export interface MappedBookingErrors {
  /** Every message, in the server's order — the summary at the top (focus moves to it). */
  summary: string[];
  byField: Partial<Record<BookingField, string[]>>;
  /** The server said the cat is recorded as already neutered (offer *Book anyway*). */
  alreadyNeutered: string | null;
}

/** Maps EVERY violation of a booking/reschedule `400` — never just the first (US-SRG-01). */
export function mapBookingViolations(error: unknown): MappedBookingErrors {
  const out: MappedBookingErrors = { summary: [], byField: {}, alreadyNeutered: null };
  for (const violation of surgeryViolations(error)) {
    out.summary.push(violation.message);
    if (violation.errorCode === 'CatAlreadyNeutered') out.alreadyNeutered = violation.message;
    const field = BOOKING_FIELDS[violation.propertyName];
    if (field) {
      (out.byField[field] ??= []).push(violation.message);
    }
  }
  return out;
}

export type ReportField =
  'procedurePerformed' | 'findings' | 'technique' | 'sutureMaterial' | 'complications';

export const REPORT_FIELD_LABELS: Readonly<Record<ReportField, string>> = {
  procedurePerformed: 'Procedure performed',
  findings: 'Findings',
  technique: 'Technique',
  sutureMaterial: 'Suture material',
  complications: 'Complications',
};

const REPORT_WORDS: Readonly<Record<string, ReportField>> = {
  'procedure performed': 'procedurePerformed',
  findings: 'findings',
  technique: 'technique',
  'suture material': 'sutureMaterial',
  complications: 'complications',
};

/**
 * `ReportIncomplete` arrives as ONE violation whose message names every missing part
 * (*"The report is incomplete: findings, technique."* — `SurgicalReportAggregate.Finalise`). The words
 * are matched to the editor's fields so each can be marked and the first focused; a word this table does
 * not know (surgeon, assistant — prefilled and never empty) is simply not marked.
 */
export function parseMissingReportFields(message: string): ReportField[] {
  const list = message
    .split(':')
    .slice(1)
    .join(':')
    .replace(/\.\s*$/, '');
  return list
    .split(',')
    .map((word) => REPORT_WORDS[word.trim().toLowerCase()])
    .filter((field): field is ReportField => field !== undefined);
}
