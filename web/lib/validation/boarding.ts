import { z } from 'zod';
import type {
  BoardingMoney,
  CareProfile,
  FoodSource,
  LitterType,
  SignatureCapture,
} from '@/lib/api/types';

/**
 * `lib/validation/boarding.ts` — HO-BRD-002 §13: boarding schemas only. They mirror the HO-BRD-001
 * validators' SHAPE rules (§11.2: counts, lengths, enum membership, `LocalTime` distinctness,
 * money non-negative with scale 2); availability, the gate, capacity and checklist completeness are
 * the server's (ADR-FE-105) and are never re-implemented here. The server stays the authority
 * (NFR-USA-4); these exist so values survive an error and the user is told early.
 */

const TIME = /^([01]\d|2[0-3]):[0-5]\d$/;
const MONEY = /^\d{1,9}(\.\d{1,2})?$/;

export const MAX_CATS_PER_STAY = 6;
export const MAX_FEEDINGS = 6;
export const MAX_MEDICATIONS = 10;

export const LITTER_TYPES: LitterType[] = [
  'Clumping',
  'Silica',
  'Tofu',
  'Bentonite',
  'Paper',
  'Other',
];
export const FOOD_SOURCES: FoodSource[] = ['OwnFood', 'HouseFood'];

const timeField = z.string().regex(TIME, 'Enter a time such as 08:30.');
const dateOnly = z.string().regex(/^\d{4}-\d{2}-\d{2}$/, 'Choose a date.');

export const staySearchSchema = z
  .object({
    arrivalOn: dateOnly,
    departureOn: dateOnly,
    catCount: z.number().int().min(1, 'Choose at least one cat.').max(MAX_CATS_PER_STAY),
  })
  .refine((v) => v.departureOn > v.arrivalOn, {
    path: ['departureOn'],
    message: 'Departure must be after arrival.',
  });

// ---- care profile (FR-BRD-6) ----

const feedingSchema = z.object({
  localTime: timeField,
  portion: z
    .string()
    .trim()
    .min(1, 'Enter the portion.')
    .max(100, 'Keep this to 100 characters or fewer.'),
  foodSource: z.enum(['OwnFood', 'HouseFood']),
  foodDescription: z
    .string()
    .trim()
    .min(1, 'Describe the food.')
    .max(200, 'Keep this to 200 characters or fewer.'),
});

/** A comma-separated list of `HH:mm` (the medication times field). */
export function parseTimes(text: string): string[] {
  return text
    .split(/[,\s]+/)
    .map((t) => t.trim())
    .filter(Boolean);
}

const medicationSchema = z.object({
  name: z
    .string()
    .trim()
    .min(1, 'Enter the medicine.')
    .max(100, 'Keep this to 100 characters or fewer.'),
  dose: z
    .string()
    .trim()
    .min(1, 'Enter the dose.')
    .max(100, 'Keep this to 100 characters or fewer.'),
  route: z
    .string()
    .trim()
    .min(1, 'Enter the route.')
    .max(50, 'Keep this to 50 characters or fewer.'),
  times: z.string().superRefine((value, ctx) => {
    const times = parseTimes(value);
    if (times.length === 0) {
      ctx.addIssue({ code: 'custom', message: 'Add at least one time, such as 08:00.' });
      return;
    }
    if (times.length > 6) ctx.addIssue({ code: 'custom', message: 'Up to 6 times a day.' });
    if (times.some((t) => !TIME.test(t))) {
      ctx.addIssue({ code: 'custom', message: 'Write each time as 08:30, separated by commas.' });
    }
    if (new Set(times).size !== times.length) {
      ctx.addIssue({ code: 'custom', message: 'Each time can only appear once.' });
    }
  }),
  instructions: z.string().max(500, 'Keep this to 500 characters or fewer.'),
  fromOn: z.string(),
  toOn: z.string(),
});

export const careProfileSchema = z
  .object({
    feedings: z
      .array(feedingSchema)
      .min(1, 'Add at least one feeding.')
      .max(MAX_FEEDINGS, `Up to ${MAX_FEEDINGS} feedings a day.`),
    medications: z
      .array(medicationSchema)
      .max(MAX_MEDICATIONS, `Up to ${MAX_MEDICATIONS} medicines.`),
    litterType: z.enum(['Clumping', 'Silica', 'Tofu', 'Bentonite', 'Paper', 'Other']),
    litterNote: z.string().max(200, 'Keep this to 200 characters or fewer.'),
    vetVisitAuthorised: z.enum(['yes', 'no'], { error: 'Choose yes or no.' }),
    vetSpendingLimit: z
      .string()
      .trim()
      .min(1, 'Enter a spending limit — 0 if you want nothing spent without asking.')
      .regex(MONEY, 'Enter an amount such as 2000 or 2000.50.'),
    emergencyName: z.string().trim().min(1, 'Enter an emergency contact.').max(200),
    emergencyPhone: z.string().trim().min(1, 'Enter a phone number.').max(30),
    emergencyRelationship: z.string().max(50, 'Keep this to 50 characters or fewer.'),
    notes: z.string().max(1000, 'Keep this to 1000 characters or fewer.'),
  })
  .superRefine((value, ctx) => {
    const seen = new Set<string>();
    value.feedings.forEach((feeding, index) => {
      if (seen.has(feeding.localTime)) {
        ctx.addIssue({
          code: 'custom',
          path: ['feedings', index, 'localTime'],
          message: 'Two feedings cannot share a time.',
        });
      }
      seen.add(feeding.localTime);
    });
  });

export type CareProfileFormValues = z.infer<typeof careProfileSchema>;

export const EMPTY_CARE_PROFILE: CareProfileFormValues = {
  feedings: [{ localTime: '08:00', portion: '', foodSource: 'OwnFood', foodDescription: '' }],
  medications: [],
  litterType: 'Clumping',
  litterNote: '',
  vetVisitAuthorised: undefined as unknown as 'yes' | 'no',
  vetSpendingLimit: '',
  emergencyName: '',
  emergencyPhone: '',
  emergencyRelationship: '',
  notes: '',
};

export function careProfileToForm(profile: CareProfile): CareProfileFormValues {
  return {
    feedings: profile.feedings.map((f) => ({
      localTime: f.localTime,
      portion: f.portion,
      foodSource: f.foodSource,
      foodDescription: f.foodDescription,
    })),
    medications: profile.medications.map((m) => ({
      name: m.name,
      dose: m.dose,
      route: m.route,
      times: m.times.join(', '),
      instructions: m.instructions ?? '',
      fromOn: m.fromOn ?? '',
      toOn: m.toOn ?? '',
    })),
    litterType: profile.litterType,
    litterNote: profile.litterNote ?? '',
    vetVisitAuthorised: profile.vetVisitAuthorised ? 'yes' : 'no',
    vetSpendingLimit: profile.vetSpendingLimit.amount,
    emergencyName: profile.emergencyContact.name,
    emergencyPhone: profile.emergencyContact.phone,
    emergencyRelationship: profile.emergencyContact.relationship ?? '',
    notes: profile.notes ?? '',
  };
}

/** Money is entered as text and sent as text with scale 2 — never through arithmetic (ADR-FE-105). */
export function toMoneyText(value: string): string {
  const trimmed = value.trim();
  if (!trimmed.includes('.')) return `${trimmed}.00`;
  const [whole, fraction = ''] = trimmed.split('.');
  return `${whole}.${fraction.padEnd(2, '0')}`;
}

export function formToCareProfile(
  values: CareProfileFormValues,
  currencyCode: string,
): CareProfile {
  const limit: BoardingMoney = { amount: toMoneyText(values.vetSpendingLimit), currencyCode };
  return {
    feedings: values.feedings.map((f) => ({
      localTime: f.localTime,
      portion: f.portion.trim(),
      foodSource: f.foodSource,
      foodDescription: f.foodDescription.trim(),
    })),
    medications: values.medications.map((m) => ({
      name: m.name.trim(),
      dose: m.dose.trim(),
      route: m.route.trim(),
      times: parseTimes(m.times),
      instructions: m.instructions.trim() || null,
      fromOn: m.fromOn || null,
      toOn: m.toOn || null,
    })),
    litterType: values.litterType,
    litterNote: values.litterNote.trim() || null,
    vetVisitAuthorised: values.vetVisitAuthorised === 'yes',
    vetSpendingLimit: limit,
    emergencyContact: {
      name: values.emergencyName.trim(),
      phone: values.emergencyPhone.trim(),
      relationship: values.emergencyRelationship.trim() || null,
    },
    notes: values.notes.trim() || null,
  };
}

// ---- signature (ADR-FE-103) ----

export const SIGNATURE_MAX_POINT = 10_000;
export const SIGNATURE_MAX_BYTES = 32 * 1024;

/** Keeps strokes numeric, integer and in range, and under the 32 KB the server accepts; returns
 *  `null` when nothing drawn would survive. */
export function boundStrokes(
  strokes: Array<Array<[number, number]>>,
): Array<Array<[number, number]>> | null {
  const clamp = (n: number) => Math.min(SIGNATURE_MAX_POINT, Math.max(0, Math.round(n)));
  const cleaned = strokes
    .map((stroke) =>
      stroke
        .filter(([x, y]) => Number.isFinite(x) && Number.isFinite(y))
        .map(([x, y]): [number, number] => [clamp(x), clamp(y)]),
    )
    .filter((stroke) => stroke.length > 0);
  if (cleaned.length === 0) return null;
  // Thin the points evenly until the payload fits.
  let current = cleaned;
  for (
    let step = 2;
    JSON.stringify(current).length > SIGNATURE_MAX_BYTES && step <= 16;
    step += 1
  ) {
    current = cleaned.map((stroke) =>
      stroke.filter((_, index) => index % step === 0 || index === stroke.length - 1),
    );
  }
  return JSON.stringify(current).length > SIGNATURE_MAX_BYTES ? null : current;
}

export interface SignatureDraft {
  method: 'Drawn' | 'Typed';
  signerName: string;
  strokes: Array<Array<[number, number]>>;
  confirmed: boolean;
}

export const EMPTY_SIGNATURE: SignatureDraft = {
  method: 'Typed',
  signerName: '',
  strokes: [],
  confirmed: false,
};

export function signatureError(draft: SignatureDraft): string | null {
  if (!draft.signerName.trim()) return 'Enter the name of the person signing.';
  if (draft.method === 'Drawn' && boundStrokes(draft.strokes) === null) {
    return 'Draw the signature, or type the name instead.';
  }
  if (!draft.confirmed) return 'Tick the box to confirm the statement.';
  return null;
}

export function draftToSignature(draft: SignatureDraft): SignatureCapture {
  return {
    signerName: draft.signerName.trim(),
    method: draft.method,
    strokes: draft.method === 'Drawn' ? (boundStrokes(draft.strokes) ?? undefined) : undefined,
    confirmedStatement: draft.confirmed,
  };
}

// ---- check-in / check-out drafts (ADR-FE-108) ----

export const WEIGHT_MIN_KG = 0.1;
export const WEIGHT_MAX_KG = 30;

/** A belonging at check-in; its photos are files already uploaded through HO-FIL-002's uploader
 *  (purpose `BoardingBelongings`) -- the check-in sends their ids (owner decision 2026-10-14). */
export interface BelongingDraft {
  kind: string;
  description: string;
  fileIds: string[];
  fileNames: string[];
}

export interface CheckInCatDraft {
  identityConfirmed: boolean;
  vaccinationProofVerified: boolean;
  weightKg: string;
  arrivalHealthObservation: string;
  noBelongings: boolean;
  belongings: BelongingDraft[];
  signature: SignatureDraft;
}

export const EMPTY_CHECK_IN_CAT: CheckInCatDraft = {
  identityConfirmed: false,
  vaccinationProofVerified: false,
  weightKg: '',
  arrivalHealthObservation: '',
  noBelongings: false,
  belongings: [],
  signature: EMPTY_SIGNATURE,
};

export function weightError(text: string): string | null {
  const n = Number(text);
  if (!text.trim() || !Number.isFinite(n)) return 'Enter the weight in kg.';
  if (n < WEIGHT_MIN_KG || n > WEIGHT_MAX_KG) {
    return `Enter a weight between ${WEIGHT_MIN_KG.toFixed(2)} and ${WEIGHT_MAX_KG.toFixed(2)} kg.`;
  }
  return null;
}

export type ChecklistErrors = Partial<Record<string, string>>;

/** Client-side completeness for one cat's check-in — the same items the server lists as missing. */
export function checkInCatErrors(draft: CheckInCatDraft): ChecklistErrors {
  const errors: ChecklistErrors = {};
  if (!draft.identityConfirmed) errors.identityConfirmed = 'Confirm the cat’s identity.';
  if (!draft.vaccinationProofVerified) {
    errors.vaccinationProofVerified = 'Confirm the vaccination proof was checked.';
  }
  const weight = weightError(draft.weightKg);
  if (weight) errors.weightKg = weight;
  const observation = draft.arrivalHealthObservation.trim();
  if (!observation) errors.arrivalHealthObservation = 'Describe how the cat looks on arrival.';
  else if (observation.length > 1000)
    errors.arrivalHealthObservation = 'Keep this to 1000 characters or fewer.';
  if (!draft.noBelongings && draft.belongings.length === 0) {
    errors.belongings = 'Add the belongings, or tick “No belongings”.';
  }
  const signature = signatureError(draft.signature);
  if (signature) errors.signature = signature;
  return errors;
}

export interface CheckOutCatDraft {
  weightKg: string;
  returns: Record<string, { returned: boolean; note: string }>;
  signature: SignatureDraft;
}

export function checkOutCatErrors(draft: CheckOutCatDraft): ChecklistErrors {
  const errors: ChecklistErrors = {};
  const weight = weightError(draft.weightKg);
  if (weight) errors.weightKg = weight;
  for (const [id, item] of Object.entries(draft.returns)) {
    if (!item.returned && !item.note.trim()) {
      errors[`belonging:${id}`] = 'Say why this was not returned.';
    }
  }
  const signature = signatureError(draft.signature);
  if (signature) errors.signature = signature;
  return errors;
}

// ---- small forms ----

export const reasonSchema = (message: string, max = 500) =>
  z.object({
    reason: z.string().trim().min(1, message).max(max, `Keep this to ${max} characters or fewer.`),
  });

export const incidentSchema = z.object({
  catId: z.string().min(1, 'Choose the cat.'),
  kind: z.enum(['Injury', 'Illness', 'EscapeAttempt', 'Fight', 'RefusalToEat', 'Other']),
  severity: z.enum(['Low', 'Medium', 'High']),
  description: z
    .string()
    .trim()
    .min(1, 'Describe what happened.')
    .max(2000, 'Keep this to 2000 characters or fewer.'),
  immediateAction: z
    .string()
    .trim()
    .min(1, 'Say what was done straight away.')
    .max(2000, 'Keep this to 2000 characters or fewer.'),
});
export type IncidentFormValues = z.infer<typeof incidentSchema>;

export const observationSchema = z.object({
  portionEaten: z.enum(['None', 'Quarter', 'Half', 'ThreeQuarters', 'All'], {
    error: 'Choose how much was eaten.',
  }),
  waterIntake: z.enum(['None', 'Low', 'Normal', 'High']),
  litterOutput: z.enum(['Normal', 'None', 'Abnormal']),
  mood: z.enum(['Relaxed', 'Playful', 'Anxious', 'Withdrawn', 'Aggressive']),
  notes: z.string().max(1000, 'Keep this to 1000 characters or fewer.'),
});
export type ObservationFormValues = z.infer<typeof observationSchema>;

export const unitSchema = z.object({
  code: z.string().trim().min(1, 'Enter a code.').max(20, 'Keep this to 20 characters or fewer.'),
  name: z.string().trim().min(1, 'Enter a name.').max(100, 'Keep this to 100 characters or fewer.'),
  unitType: z.enum(['Standard', 'Deluxe', 'Family', 'Isolation']),
  capacityCats: z.coerce.number().int().min(1, 'At least 1 cat.').max(6, 'Up to 6 cats.'),
  block: z.string().max(50, 'Keep this to 50 characters or fewer.'),
  sortOrder: z.coerce.number().int().min(0, 'Enter 0 or more.'),
});
export type UnitFormValues = z.input<typeof unitSchema>;

export const maintenanceSchema = z
  .object({
    startsOn: dateOnly,
    endsOn: dateOnly,
    reason: z
      .string()
      .trim()
      .min(1, 'Enter a reason.')
      .max(200, 'Keep this to 200 characters or fewer.'),
  })
  .refine((v) => v.endsOn >= v.startsOn, {
    path: ['endsOn'],
    message: 'The last day must be on or after the first day.',
  });

export const moneyText = (label: string) =>
  z
    .string()
    .trim()
    .min(1, `Enter the ${label}.`)
    .regex(MONEY, 'Enter an amount such as 1500 or 1500.50.');

export const rateCardSchema = z.object({
  nightlyRate: moneyText('nightly rate'),
  weeklyRate: moneyText('weekly rate'),
  monthlyRate: moneyText('monthly rate'),
  additionalCatNightlyRate: moneyText('additional-cat rate'),
  taxClassId: z.string().min(1, 'Choose a tax class.'),
  effectiveFrom: z.string().min(1, 'Choose when it starts.'),
});
export type RateCardFormValues = z.infer<typeof rateCardSchema>;

export const daycareRateCardSchema = z.object({
  hourlyRate: moneyText('hourly rate'),
  dayRate: moneyText('day rate'),
  taxClassId: z.string().min(1, 'Choose a tax class.'),
  effectiveFrom: z.string().min(1, 'Choose when it starts.'),
});
export type DaycareRateCardFormValues = z.infer<typeof daycareRateCardSchema>;
