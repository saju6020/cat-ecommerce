import { describe, expect, it } from 'vitest';
import { ApiConflictError, ApiForbiddenError, ApiValidationError } from '@/lib/api/client';
import { describeSurgeryError, surgeryViolations } from '@/lib/api/errors';
import {
  describeChecklist,
  formatDose,
  formatDrawn,
  formatMgPerKg,
  formatVital,
  formatWeightKg,
  recoveryLabel,
  routeLabel,
} from '@/lib/format/surgery';
import {
  drugFingerprint,
  instantFromTime,
  mapDrugViolations,
  nowTimeInZone,
  parseDecimal,
} from '@/lib/validation/anaesthesia';
import {
  mapBookingViolations,
  overrideReasonSchema,
  parseMissingReportFields,
} from '@/lib/validation/surgery';

/** Numbers and units are joined by a non-breaking space; compare as plain text. */
const plain = (text: string | null) => text?.replace(/ /g, ' ') ?? null;

const violation = (propertyName: string, errorCode: string, errorMessage = '') => ({
  propertyName,
  errorCode,
  errorMessage,
  resourceName: 'Surgery',
});

describe('units are always part of the figure (UI-8)', () => {
  it('formats a dose, a quantity, a vital and a weight with their units', () => {
    expect(plain(formatDose(0.05, 'Ml'))).toBe('0.05 ml');
    expect(plain(formatDose('1', 'Tablet'))).toBe('1 tablet');
    expect(plain(formatDrawn(0.5, 'Millilitre'))).toBe('0.5 ml');
    expect(plain(formatVital('heartRateBpm', 120))).toBe('120 bpm');
    expect(plain(formatVital('temperatureCelsius', 38.6))).toBe('38.6 °C');
    expect(plain(formatVital('spO2Percent', 98))).toBe('98 %');
    expect(plain(formatWeightKg(4.2))).toBe('4.2 kg');
  });

  it('labels the mg/kg figure with the weight and its date, and refuses a non-mass unit', () => {
    expect(plain(formatMgPerKg(0.84, 'Mg', 4.2, '11 Oct 2026'))).toBe(
      '≈ 0.2 mg/kg at 4.2 kg (weighed 11 Oct 2026)',
    );
    expect(plain(formatMgPerKg(200, 'Mcg', 4, '1 Oct'))).toBe(
      '≈ 0.05 mg/kg at 4 kg (weighed 1 Oct)',
    );
    expect(formatMgPerKg(1, 'Ml', 4, '1 Oct')).toBeNull();
    expect(formatMgPerKg(0, 'Mg', 4, '1 Oct')).toBeNull();
  });

  it('writes labels in plain words', () => {
    expect(recoveryLabel('ReadyForDischarge')).toBe('Ready for discharge');
    expect(routeLabel('Intramuscular')).toBe('IM');
    expect(describeChecklist({ isComplete: true, outstandingItems: [] })).toBe('Complete');
    expect(
      describeChecklist({
        isComplete: false,
        outstandingItems: ['ConsentSigned', 'WeightRecorded', 'AsaGradeAssessed'],
      }),
    ).toBe('3 outstanding: consent, weight, ASA grade');
  });
});

describe('typed numbers and times', () => {
  it('accepts a plain decimal only', () => {
    expect(parseDecimal('0.05')).toBe(0.05);
    expect(parseDecimal(' 12 ')).toBe(12);
    expect(parseDecimal('0,05')).toBeNull();
    expect(parseDecimal('1e3')).toBeNull();
    expect(parseDecimal('')).toBeNull();
    expect(parseDecimal('-1')).toBeNull();
  });

  it('turns HH:mm into the branch-zone instant', () => {
    const instant = instantFromTime('10:30', 'Asia/Dhaka', Date.now());
    expect(new Date(instant).toISOString()).toMatch(/T04:30:00\.000Z$/);
    expect(nowTimeInZone('Asia/Dhaka', Date.parse('2026-10-12T04:30:00Z'))).toBe('10:30');
  });

  it('the idempotency fingerprint changes with the medicine, batch, quantity or dose', () => {
    const base = { medicineId: 'm', batchId: 'b', quantityDrawn: 1, doseAmount: 0.2 };
    expect(drugFingerprint(base)).toBe(drugFingerprint({ ...base }));
    for (const change of [
      { medicineId: 'n' },
      { batchId: 'c' },
      { quantityDrawn: 2 },
      { doseAmount: 0.3 },
    ]) {
      expect(drugFingerprint({ ...base, ...change })).not.toBe(drugFingerprint(base));
    }
  });

  it('the override reason needs 10 characters and allows 1000', () => {
    expect(overrideReasonSchema.safeParse('123456789').success).toBe(false);
    expect(overrideReasonSchema.safeParse('1234567890').success).toBe(true);
    expect(overrideReasonSchema.safeParse('x'.repeat(1001)).success).toBe(false);
  });
});

describe('U-X1 — server refusals are mapped, and the code is never the message', () => {
  it('maps every clash of a booking to its field, in order', () => {
    const error = new ApiValidationError({
      isValid: false,
      errors: [
        violation('theatreId', 'TheatreBusy', 'Theatre 1 is already booked.'),
        violation('surgeonId', 'SurgeonBusy'),
        violation(
          'ConfirmNeuterStatusDiscrepancy',
          'CatAlreadyNeutered',
          'Biscuit is already neutered.',
        ),
      ],
    });
    const mapped = mapBookingViolations(error);
    expect(mapped.summary).toEqual([
      'Theatre 1 is already booked.',
      'The surgeon is already booked at that time.', // the mapped fallback for an empty message
      'Biscuit is already neutered.',
    ]);
    expect(mapped.byField.theatreId).toEqual(['Theatre 1 is already booked.']);
    expect(mapped.byField.surgeonId).toHaveLength(1);
    expect(mapped.alreadyNeutered).toBe('Biscuit is already neutered.');
    expect(JSON.stringify(mapped)).not.toContain('TheatreBusy');
  });

  it('keeps the witness failure apart from the field errors', () => {
    const error = new ApiValidationError({
      isValid: false,
      errors: [
        violation('WitnessIdentifier', 'WitnessNotVerified'),
        violation('BatchId', 'BatchNotUsable'),
        violation('Wasted', 'WastageRequired'),
      ],
    });
    const mapped = mapDrugViolations(error);
    expect(mapped.witness).toMatch(/could not be verified/);
    expect(mapped.byField.batch?.[0]).toMatch(/can’t be used/);
    expect(mapped.byField.wasted?.[0]).toMatch(/quantity wasted/);
  });

  it('reads a 409 body and a 403 body that carry a violation', () => {
    const conflict = new ApiConflictError({
      isValid: false,
      errors: [violation('TheatreId', 'TheatreBusy', 'Just booked.')],
    });
    expect(surgeryViolations(conflict)[0]).toMatchObject({
      propertyName: 'theatreId',
      message: 'Just booked.',
    });
    const forbidden = new ApiForbiddenError({
      errors: [{ errorCode: 'ConsentOverrideNotPermitted', errorMessage: '' }],
    });
    expect(describeSurgeryError(forbidden)).toBe(
      'Only a veterinarian can start a surgery without recorded consent.',
    );
    expect(
      describeSurgeryError(
        new ApiForbiddenError({ errors: [{ errorCode: 'ActionNotPermitted', errorMessage: '' }] }),
      ),
    ).toMatch(/don't have permission/);
  });

  it('a bare 409 is a changed record, never a code', () => {
    expect(describeSurgeryError(new ApiConflictError())).toMatch(
      /Someone else changed this record/,
    );
  });

  it('reads the missing parts out of ReportIncomplete', () => {
    expect(
      parseMissingReportFields(
        'The report is incomplete: findings, suture material, complications.',
      ),
    ).toEqual(['findings', 'sutureMaterial', 'complications']);
    expect(parseMissingReportFields('The report is incomplete: surgeon.')).toEqual([]);
  });
});
