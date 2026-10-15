import { ApiValidationError } from '@/lib/api/client';
import { describeOrderingError } from '@/lib/api/errors';

/**
 * `components/ordering-admin/serverErrors.ts` — HO-ORD-002 §11.13. Splits a failed administration save
 * into messages that belong to a field (the server's `propertyName`, lower-camelled, first segment —
 * `Coverage[0].District` belongs to `coverage`) and messages that do not. The server's message is used
 * as given; `errorCode` is never read for display.
 */
export interface SplitErrors {
  fields: Record<string, string>;
  root: string | null;
}

export function splitServerErrors(error: unknown, knownFields: readonly string[]): SplitErrors {
  const fields: Record<string, string> = {};
  const rest: string[] = [];
  if (error instanceof ApiValidationError && error.response.errors.length > 0) {
    for (const violation of error.response.errors) {
      const first = violation.propertyName.split(/[.[]/)[0] ?? '';
      const name = first.charAt(0).toLowerCase() + first.slice(1);
      const text = violation.errorMessage || describeOrderingError(error);
      if (knownFields.includes(name)) fields[name] = fields[name] ? `${fields[name]} ${text}` : text;
      else rest.push(text);
    }
    return { fields, root: rest.length > 0 ? rest.join(' ') : null };
  }
  return { fields, root: describeOrderingError(error) };
}
