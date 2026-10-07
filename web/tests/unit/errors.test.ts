import { describe, expect, it, vi } from 'vitest';
import { ApiValidationError } from '@/lib/api/client';
import { applyValidationErrors, describeApiError, SHOP_CODE_MESSAGES } from '@/lib/api/errors';

interface Form {
  name: string;
  root?: string;
}

describe('applyValidationErrors', () => {
  it('sets a field-level error when propertyName matches a known field', () => {
    const setError = vi.fn();
    const error = new ApiValidationError({
      isValid: false,
      errors: [
        {
          propertyName: 'Name',
          errorCode: 'Required',
          errorMessage: 'Enter a name.',
          resourceName: 'Cat',
        },
      ],
    });

    applyValidationErrors<Form>(error, setError, {
      fieldMap: { Name: 'name' },
      knownFields: ['name'],
    });

    expect(setError).toHaveBeenCalledWith('name', { type: 'server', message: 'Enter a name.' });
  });

  it('surfaces an unmatched propertyName as a form-level error instead of dropping it', () => {
    const setError = vi.fn();
    const error = new ApiValidationError({
      isValid: false,
      errors: [
        {
          propertyName: 'SomeUnmappedProperty',
          errorCode: 'Unexpected',
          errorMessage: 'Something about this record is invalid.',
          resourceName: 'Cat',
        },
      ],
    });

    applyValidationErrors<Form>(error, setError, { knownFields: ['name'] });

    expect(setError).toHaveBeenCalledWith('root', {
      type: 'server',
      message: 'Something about this record is invalid.',
    });
  });

  it('never passes errorCode to setError — NFR-USA-4', () => {
    const setError = vi.fn();
    const error = new ApiValidationError({
      isValid: false,
      errors: [
        {
          propertyName: 'Name',
          errorCode: 'MicrochipAlreadyRegistered',
          errorMessage: 'That microchip number is already registered.',
          resourceName: 'Cat',
        },
      ],
    });

    applyValidationErrors<Form>(error, setError, { fieldMap: { Name: 'name' } });

    const [, payload] = setError.mock.calls[0] as [string, { message: string }];
    expect(payload.message).not.toContain('MicrochipAlreadyRegistered');
  });
});

describe('codeMessages fallback (HO-SHP-002 S2)', () => {
  const emptyMessageError = () =>
    new ApiValidationError({
      isValid: false,
      errors: [
        {
          propertyName: 'Slug',
          errorCode: 'SlugAlreadyInUse',
          errorMessage: '',
          resourceName: 'Product',
        },
      ],
    });

  it('gives a non-shop caller the generic sentence, not shop wording, for a shop code', () => {
    const setError = vi.fn();
    applyValidationErrors<{ slug: string }>(emptyMessageError(), setError, {
      fieldMap: { Slug: 'slug' },
    });
    expect(setError).toHaveBeenCalledWith('slug', {
      type: 'server',
      message: 'Something went wrong. Please try again.',
    });
    expect(describeApiError(emptyMessageError())).toBe('Something went wrong. Please try again.');
  });

  it('gives a shop caller the shop sentence when it passes the shop map', () => {
    const setError = vi.fn();
    applyValidationErrors<{ slug: string }>(emptyMessageError(), setError, {
      fieldMap: { Slug: 'slug' },
      codeMessages: SHOP_CODE_MESSAGES,
    });
    expect(setError).toHaveBeenCalledWith('slug', {
      type: 'server',
      message: SHOP_CODE_MESSAGES.SlugAlreadyInUse,
    });
    expect(describeApiError(emptyMessageError(), SHOP_CODE_MESSAGES)).toBe(
      'This web address is already used by another product.',
    );
  });
});
