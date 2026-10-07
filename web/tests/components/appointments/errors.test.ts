import { describe, expect, it } from 'vitest';
import { ApiConflictError, ApiValidationError } from '@/lib/api/client';
import { describeAppointmentError, getViolationCode } from '@/lib/api/errors';

const response = (errorCode: string, errorMessage: string) => ({
  isValid: false,
  errors: [{ propertyName: 'X', errorCode, errorMessage, resourceName: 'Clinic' }],
});

describe('describeAppointmentError — HO-APT-002 §11.8', () => {
  it('prefers the server message and never exposes the code', () => {
    const error = new ApiValidationError(
      response('PerformerBusy', 'Dr Akter already has an appointment 11:30–12:00.'),
    );
    const text = describeAppointmentError(error);
    expect(text).toBe('Dr Akter already has an appointment 11:30–12:00.');
    expect(text).not.toContain('PerformerBusy');
    expect(getViolationCode(error)).toBe('PerformerBusy');
  });

  it('uses the fixed wording for the lost race and the ended hold', () => {
    expect(
      describeAppointmentError(new ApiConflictError(response('SlotNoLongerAvailable', 'Generic.'))),
    ).toBe('Someone has just taken that time. Please choose another.');
    expect(
      describeAppointmentError(new ApiValidationError(response('SlotHoldExpired', 'Generic.'))),
    ).toBe('Your hold has ended — please choose a time again.');
  });

  it('falls back to the mapped text, then the generic text', () => {
    expect(
      describeAppointmentError(new ApiValidationError(response('NoShowTooEarly', ''))),
    ).toMatch(/no-show/i);
    expect(describeAppointmentError(new Error('boom'))).toBe(
      'Something went wrong. Please try again.',
    );
  });

  it('a 409 without a body is the stale-version message', () => {
    expect(describeAppointmentError(new ApiConflictError())).toMatch(/changed/i);
  });
});
