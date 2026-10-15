import { z } from 'zod';
import type {
  IssuePrescriptionRequest,
  PrescriptionDetail,
  PrescriptionFields,
  SafetyAssessment,
  SafetyWarning,
} from '@/lib/api/prescriptions';
import { frequencyNameFromLabel } from '@/lib/format/pharmacy';
import { positiveDecimal, toDecimal } from '@/lib/validation/pharmacy';

/**
 * `composerModel.ts` — HO-PHM-002 §11.2. The composer's form SHAPE (zod: is it a number, is a choice
 * made, is the text within its limit), how it becomes a request, and how the server's property names
 * map back onto fields. Nothing here knows what a safe dose is (ADR-FE-060).
 */
export const composerSchema = z
  .object({
    medicineId: z.string().min(1, 'Choose a medicine.'),
    doseAmount: positiveDecimal('the dose'),
    doseUnit: z.string().min(1, 'Choose the dose unit.'),
    route: z.string().min(1, 'Choose a route.'),
    frequency: z.string().min(1, 'Choose how often.'),
    durationDays: z
      .string()
      .trim()
      .regex(/^\d+$/, 'Enter the duration in whole days.')
      .refine(
        (v) => Number(v) >= 1 && Number(v) <= 365,
        'Duration must be between 1 and 365 days.',
      ),
    quantity: positiveDecimal('the quantity per supply', 2),
    instructions: z
      .string()
      .trim()
      .min(1, 'Enter the instructions printed on the label.')
      .max(1000, 'Keep the instructions to 1000 characters or fewer.'),
    isRepeatable: z.boolean(),
    repeatsAuthorised: z.string().trim(),
    validUntil: z.string().trim(),
  })
  .superRefine((values, context) => {
    if (!values.isRepeatable) return;
    if (
      !/^\d+$/.test(values.repeatsAuthorised) ||
      Number(values.repeatsAuthorised) < 1 ||
      Number(values.repeatsAuthorised) > 12
    )
      context.addIssue({
        code: 'custom',
        path: ['repeatsAuthorised'],
        message: 'Enter 1 to 12 repeats.',
      });
    if (!values.validUntil)
      context.addIssue({
        code: 'custom',
        path: ['validUntil'],
        message: 'Enter the date the repeats stop.',
      });
  });

export type ComposerValues = z.infer<typeof composerSchema>;

export const EMPTY_COMPOSER: ComposerValues = {
  medicineId: '',
  doseAmount: '',
  doseUnit: '',
  route: '',
  frequency: '',
  durationDays: '',
  quantity: '',
  instructions: '',
  isRepeatable: false,
  repeatsAuthorised: '',
  validUntil: '',
};

/** The server's property names (PascalCase or camelCase, dotted for the dose) onto this form's fields. */
export const COMPOSER_FIELD_MAP = {
  MedicineId: 'medicineId',
  medicineId: 'medicineId',
  'Dose.Amount': 'doseAmount',
  'Dose.Unit': 'doseUnit',
  Dose: 'doseAmount',
  Route: 'route',
  Frequency: 'frequency',
  DurationDays: 'durationDays',
  Quantity: 'quantity',
  Instructions: 'instructions',
  IsRepeatable: 'isRepeatable',
  RepeatsAuthorised: 'repeatsAuthorised',
  ValidUntil: 'validUntil',
} as const;
export const COMPOSER_KNOWN_FIELDS = Object.values(COMPOSER_FIELD_MAP);

/** A renewal starts from the prescription it renews; the vet reviews every field. */
export function valuesFromPrescription(
  source: PrescriptionDetail,
  medicineId: string,
): ComposerValues {
  return {
    medicineId,
    doseAmount: source.dose.amount,
    doseUnit: source.dose.unit,
    route: source.route,
    frequency: frequencyNameFromLabel(source.frequency),
    durationDays: String(source.durationDays),
    quantity: source.quantity.amount,
    instructions: source.instructions,
    isRepeatable: source.isRepeatable,
    repeatsAuthorised: source.isRepeatable
      ? String(Math.max(1, source.suppliesAuthorised - 1))
      : '',
    validUntil: '',
  };
}

export interface Acknowledgement {
  /** The fingerprint these ticks were given against (ADR-FE-060). */
  fingerprint: string;
  ticks: Record<string, boolean>;
  allergyReason: string;
  doseNote: string;
}

export const EMPTY_ACK: Acknowledgement = {
  fingerprint: '',
  ticks: {},
  allergyReason: '',
  doseNote: '',
};

/** A stable key for a warning inside one assessment. */
export function warningKey(warning: SafetyWarning, index: number): string {
  return `${index}:${warning.kind}:${warning.subject}`;
}

export const isAllergy = (warning: SafetyWarning) => warning.kind === 'AllergyConflict';
export const isDoseWarning = (warning: SafetyWarning) =>
  warning.kind === 'DoseAboveRange' || warning.kind === 'DoseBelowRange';

export interface AckProblems {
  ticks: Record<string, string>;
  allergyReason?: string;
}

/** What is missing before the fingerprint may be sent — a tick for every warning that needs one, and an allergy reason. */
export function ackProblems(assessment: SafetyAssessment, ack: Acknowledgement): AckProblems {
  const problems: AckProblems = { ticks: {} };
  assessment.warnings.forEach((warning, index) => {
    const key = warningKey(warning, index);
    if (warning.requiresAcknowledgement && !ack.ticks[key])
      problems.ticks[key] = 'Tick to confirm you have reviewed this warning.';
  });
  if (assessment.warnings.some(isAllergy) && ack.allergyReason.trim() === '')
    problems.allergyReason = 'Say why you are prescribing despite the allergy.';
  return problems;
}

export const hasProblems = (problems: AckProblems) =>
  Object.keys(problems.ticks).length > 0 || Boolean(problems.allergyReason);

/** Form values plus the acknowledgement become the request body. `visitId` is absent on a remote renewal. */
export function toRequest(
  values: ComposerValues,
  assessment: SafetyAssessment,
  ack: Acknowledgement,
): PrescriptionFields {
  return {
    medicineId: values.medicineId,
    dose: { amount: toDecimal(values.doseAmount), unit: values.doseUnit },
    route: values.route,
    frequency: values.frequency,
    durationDays: Number(values.durationDays),
    quantity: toDecimal(values.quantity),
    instructions: values.instructions.trim(),
    isRepeatable: values.isRepeatable,
    repeatsAuthorised: values.isRepeatable ? Number(values.repeatsAuthorised) : 0,
    validUntil: values.validUntil || null,
    acknowledgedWarningsFingerprint: assessment.fingerprint,
    allergyAcknowledgementReason: assessment.warnings.some(isAllergy)
      ? ack.allergyReason.trim()
      : null,
    doseAcknowledgementNote: ack.doseNote.trim() || null,
  };
}

export function toIssueRequest(
  visitId: string,
  fields: PrescriptionFields,
): IssuePrescriptionRequest {
  return { ...fields, visitId };
}
