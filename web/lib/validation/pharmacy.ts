import { z } from 'zod';
import type { FieldValues, Path, UseFormSetError } from 'react-hook-form';
import { ApiConflictError, ApiValidationError } from '@/lib/api/client';
import {
  PHARMACY_CODE_MESSAGES,
  applyValidationErrors,
  describePharmacyError,
  pharmacyViolations,
} from '@/lib/api/errors';

/**
 * `lib/validation/pharmacy.ts` — HO-PHM-002 §8.3. SHAPE checks only (is it a positive decimal? is a
 * required choice made?). Ranges, allergies, interactions and stock are the server's (ADR-FE-060): no
 * rule here knows what a safe dose is.
 */

/** A positive decimal with at most `places` decimal places, as typed. */
export function positiveDecimal(label: string, places = 4) {
  const pattern = new RegExp(`^\\d+(\\.\\d{1,${places}})?$`);
  return z
    .string()
    .trim()
    .min(1, `Enter ${label}.`)
    .refine((value) => pattern.test(value) && Number(value) > 0, {
      message: `Enter ${label} as a number greater than zero${places === 0 ? '' : ` with at most ${places} decimal places`}.`,
    });
}

/** The decimal text as a JSON number for the request body (shape conversion, not arithmetic). */
export function toDecimal(text: string): number {
  return Number(text.trim());
}

/**
 * Runs a form's submit and maps every failure the way the pharmacy screens promise (U-X2):
 * a `400` onto its field (the server's message first, a fallback sentence second, `errorCode` never
 * shown), a `409` onto `onConflict` (or a root message), a `403` onto a permission sentence, anything
 * else onto a plain root message. The form's values are never reset (NFR-USA-5).
 */
export async function submitPharmacyForm<TFieldValues extends FieldValues, TResult>(
  action: () => Promise<TResult>,
  setError: UseFormSetError<TFieldValues>,
  options: {
    fieldMap?: Partial<Record<string, Path<TFieldValues>>>;
    knownFields?: ReadonlyArray<Path<TFieldValues>>;
    onConflict?: () => void;
  } = {},
): Promise<TResult | undefined> {
  const root = 'root' as Path<TFieldValues>;
  try {
    return await action();
  } catch (error) {
    if (error instanceof ApiValidationError) {
      applyValidationErrors(error, setError, {
        fieldMap: options.fieldMap,
        knownFields: options.knownFields,
        codeMessages: PHARMACY_CODE_MESSAGES,
      });
    } else if (error instanceof ApiConflictError && options.onConflict) {
      options.onConflict();
      const [first] = pharmacyViolations(error);
      setError(root, { type: 'server', message: first?.message ?? describePharmacyError(error) });
    } else {
      setError(root, { type: 'server', message: describePharmacyError(error) });
    }
    return undefined;
  }
}
