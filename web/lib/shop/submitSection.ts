import type { FieldValues, Path, UseFormSetError } from 'react-hook-form';
import { ApiConflictError, ApiValidationError } from '@/lib/api/client';
import {
  applyValidationErrors,
  describeUnexpectedError,
  SHOP_CODE_MESSAGES,
  SHOP_FIELD_MAP,
} from '@/lib/api/errors';

/** The first command of a two-command save succeeded and the second was refused: the form stays
 *  mounted with what was typed and shows this message (NFR-USA-5). */
export class PartialSaveError extends Error {
  constructor(message: string) {
    super(message);
    this.name = 'PartialSaveError';
  }
}

export const CONFLICT_MESSAGE =
  'This product was changed by someone else. Reload to see the latest.';

/**
 * HO-SHP-002 §11.6, ADR-FE-034 — runs one editor section's save. A `400` maps to its field (the
 * shop `propertyName` table, `errorCode` never shown); a `409` becomes a root error of type
 * `conflict` (the section offers **Reload**, and the typed values are kept); anything else is a
 * plain-language root message. Returns true on success.
 */
export async function submitSection<T extends FieldValues>(
  action: () => Promise<unknown>,
  setError: UseFormSetError<T>,
  knownFields: ReadonlyArray<Path<T>>,
): Promise<boolean> {
  try {
    await action();
    return true;
  } catch (error) {
    const root = 'root' as Path<T>;
    if (error instanceof PartialSaveError) {
      setError(root, { type: 'partial', message: error.message });
    } else if (error instanceof ApiConflictError) {
      setError(root, { type: 'conflict', message: CONFLICT_MESSAGE });
    } else if (error instanceof ApiValidationError) {
      applyValidationErrors(error, setError, {
        fieldMap: SHOP_FIELD_MAP as Partial<Record<string, Path<T>>>,
        codeMessages: SHOP_CODE_MESSAGES,
        knownFields,
      });
    } else {
      setError(root, { type: 'server', message: describeUnexpectedError(error) });
    }
    return false;
  }
}
