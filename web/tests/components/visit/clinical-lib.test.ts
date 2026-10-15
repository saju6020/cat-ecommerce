import { describe, expect, it } from 'vitest';
import { ApiConflictError, ApiForbiddenError, ApiValidationError } from '@/lib/api/client';
import { describeClinicalError, mapClinicalFormErrors } from '@/lib/api/errors';
import { formatRange, formatValueWithUnit } from '@/lib/format/clinical';
import {
  finaliseChecklist,
  hasAnyVital,
  initialNoteValues,
  isNoteDirty,
  plausibleRangeHint,
  toDraftRequest,
  toSaveNoteRequest,
  toVitalsRequest,
  EMPTY_VITALS,
} from '@/lib/validation/clinical';
import { getVisit } from '@/lib/api/visits';
import { ids, freshClinicBeforeEach } from './helpers';

freshClinicBeforeEach();

const validation = (code: string, property = 'Plan', message = '') =>
  new ApiValidationError({
    isValid: false,
    errors: [
      { propertyName: property, errorCode: code, errorMessage: message, resourceName: 'Visit' },
    ],
  });

describe('error mapping — NFR-USA-4, U-X1', () => {
  it('prefers the server’s message, falls back to the mapped sentence, never returns the code', () => {
    expect(
      describeClinicalError(validation('ProblemRequired', 'Problems', 'Add a diagnosis.')),
    ).toBe('Add a diagnosis.');
    expect(describeClinicalError(validation('AssessmentAndPlanRequired'))).toBe(
      'Write the assessment and the plan before finalising.',
    );
    expect(describeClinicalError(validation('SomethingNew'))).toBe(
      'Something went wrong. Please try again.',
    );
    expect(describeClinicalError(new ApiConflictError())).toMatch(/changed by someone else/);
    expect(describeClinicalError(new ApiForbiddenError({}))).toMatch(/don't have permission/);
  });

  it('puts a field’s violation on that field and everything else on the root', () => {
    const mapped = mapClinicalFormErrors(
      validation('InvalidReferenceRange', 'ReferenceLow', 'Back to front.'),
      { ReferenceLow: 'low' },
    );
    expect(mapped).toEqual({ fields: { low: 'Back to front.' }, root: null });
    const rooted = mapClinicalFormErrors(validation('FollowUpDateInPast', 'Whatever'), {
      ReferenceLow: 'low',
    });
    expect(rooted.fields).toEqual({});
    expect(rooted.root).toBe('Choose a follow-up date after today.');
  });
});

describe('the consultation form model — ADR-FE-050', () => {
  it('initialises from the draft, else the saved note, else the template, else empty', async () => {
    expect(initialNoteValues(await getVisit(ids.visitDraftNewerId)).source).toBe('draft');
    expect(initialNoteValues(await getVisit(ids.visitOpenId)).source).toBe('saved');
    expect(initialNoteValues(await getVisit(ids.visitTemplateId)).source).toBe('template');
    expect(initialNoteValues(await getVisit(ids.visitEmptyId)).source).toBe('empty');
  });

  it('a draft’s problem ids become rows awaiting their names; dirty is measured against the SAVED note', async () => {
    const initial = initialNoteValues(await getVisit(ids.visitDraftNewerId));
    expect(initial.values.problems).toHaveLength(1);
    expect(initial.values.problems[0]!.name).toBe('');
    expect(isNoteDirty(initial.values, initial.saved)).toBe(true);
    expect(isNoteDirty(initial.saved, initial.saved)).toBe(false);
    expect(toDraftRequest(initial.values).problemCodeIds).toEqual([ids.codeCkdId]);
  });

  it('builds a save request with the expected version and the applied template', async () => {
    const { values } = initialNoteValues(await getVisit(ids.visitTemplateId));
    const request = toSaveNoteRequest(values, 7, { id: 't1', version: 2 });
    expect(request).toMatchObject({
      expectedVersion: 7,
      appliedTemplateId: 't1',
      appliedTemplateVersion: 2,
      problems: [],
    });
  });

  it('the finalise checklist hint', () => {
    const empty = finaliseChecklist({
      subjective: '',
      objective: '',
      assessment: ' ',
      plan: '',
      dischargeInstructions: '',
      problems: [],
    });
    expect(empty.map((i) => i.done)).toEqual([false, false, false]);
    expect(empty.map((i) => i.fieldId)).toEqual(['problem-search', 'note-assessment', 'note-plan']);
  });
});

describe('vitals helpers', () => {
  it('requires at least one value and builds the request with numbers and an instant', () => {
    expect(hasAnyVital(EMPTY_VITALS)).toBe(false);
    const request = toVitalsRequest(
      { ...EMPTY_VITALS, weight: '4.20', measuredOn: '2026-10-07T10:30' },
      ['Temperature'],
      'Asia/Dhaka',
    );
    expect(request).toEqual({
      weightKg: 4.2,
      measuredOn: '2026-10-07T04:30:00.000Z',
      confirmedOutOfRange: ['Temperature'],
    });
  });

  it('writes the early hint from the server’s range, only for signs that have a plausible tier', () => {
    const temperature = {
      sign: 'Temperature' as const,
      unit: '°C',
      absoluteMin: '25.0',
      absoluteMax: '45.0',
      plausibleMin: '35.0',
      plausibleMax: '41.5',
      decimalPlaces: 1,
    };
    expect(plausibleRangeHint('42', temperature)).toBe(
      'Outside the usual range 35.0–41.5 °C — you’ll be asked to confirm',
    );
    expect(plausibleRangeHint('38.6', temperature)).toBeNull();
    expect(
      plausibleRangeHint('7', {
        ...temperature,
        sign: 'BodyConditionScore',
        plausibleMin: null,
        plausibleMax: null,
      }),
    ).toBeNull();
  });
});

describe('formatting', () => {
  it('ranges and values carry their unit', () => {
    expect(formatRange('71', '212', 'µmol/L')).toBe('71–212 µmol/L');
    expect(formatRange('5', null, 'mg')).toBe('≥ 5 mg');
    expect(formatRange(null, null, 'mg')).toBeNull();
    expect(formatValueWithUnit('240', 'µmol/L')).toBe('240 µmol/L');
    expect(formatValueWithUnit('Positive', null)).toBe('Positive');
  });
});
