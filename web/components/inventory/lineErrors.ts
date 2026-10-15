import { ApiConflictError, ApiValidationError } from '@/lib/api/client';
import { describeInventoryError, describeUnexpectedError, inventoryViolations, parseIndexedProperty, toFieldName } from '@/lib/api/errors';

/**
 * `components/inventory/lineErrors.ts` — HO-IMP-002 §11.3/§11.9, U-C6, U-X1. A `400` from a
 * line-based command (receipt, purchase order, transfer, adjustment, count) carries EVERY violation,
 * named `Lines[i].Field`. This sorts them into: errors for a top-level field (`supplierId`), errors
 * for one field of one line, and everything else — so each lands on its own field and none is lost.
 * `errorCode` is never exposed; the text is the server's own message (or the mapped fallback).
 */
export interface DistributedErrors {
  fields: Record<string, string>;
  lines: Record<number, Record<string, string>>;
  general: string[];
  /** The first violation in screen order, as an element id suffix: `field` or `line-{i}-{field}`. */
  firstTarget: string | null;
  /** The row keys, in order, when these errors were produced - so a line error follows ITS row when
   *  rows are removed or reordered, rather than landing on whichever row now has that index. */
  rowKeys?: string[];
}

export const EMPTY_ERRORS: DistributedErrors = { fields: {}, lines: {}, general: [], firstTarget: null };

export function distributeViolations(
  error: unknown,
  options: { collection?: string; knownFields?: readonly string[] } = {},
): DistributedErrors {
  const { collection = 'lines', knownFields } = options;
  const violations = inventoryViolations(error);
  if (violations.length === 0) {
    return { ...EMPTY_ERRORS, general: [describeInventoryError(error)] };
  }
  const result: DistributedErrors = { fields: {}, lines: {}, general: [], firstTarget: null };
  const setTarget = (target: string) => {
    result.firstTarget ??= target;
  };
  for (const violation of violations) {
    const indexed = parseIndexedProperty(violation.propertyName);
    if (indexed && indexed.collection === collection && indexed.field) {
      const forLine = (result.lines[indexed.index] ??= {});
      forLine[indexed.field] ??= violation.message;
      setTarget(`line-${indexed.index}-${indexed.field}`);
      continue;
    }
    const field = violation.propertyName ? toFieldName(violation.propertyName.split('.')[0] ?? '') : '';
    if (field && (!knownFields || knownFields.includes(field))) {
      result.fields[field] ??= violation.message;
      setTarget(field);
    } else {
      result.general.push(violation.message);
    }
  }
  return result;
}

/** Scrolls to and focuses the element for `target` (an id), if it exists. */
export function focusErrorTarget(prefix: string, target: string | null): void {
  if (!target || typeof document === 'undefined') return;
  const element = document.getElementById(`${prefix}-${target}`);
  if (element) {
    element.scrollIntoView?.({ block: 'center' });
    element.focus({ preventScroll: true });
  }
}

export function hasErrors(errors: DistributedErrors): boolean {
  return (
    Object.keys(errors.fields).length > 0 || Object.keys(errors.lines).length > 0 || errors.general.length > 0
  );
}

let counter = 0;
/** A stable React key for an editable row. */
export function newRowKey(): string {
  counter += 1;
  return `row-${Date.now().toString(36)}-${counter}`;
}

/**
 * Any failed submit, sorted for the screen: a `400` — or a `409` that carries its violations, such
 * as `InsufficientStock` — is distributed to its fields; anything else (a `403`, a network failure)
 * becomes one plain sentence.
 */
export function distributeError(
  error: unknown,
  options: { collection?: string; knownFields?: readonly string[] } = {},
): DistributedErrors {
  const carriesViolations =
    error instanceof ApiValidationError || (error instanceof ApiConflictError && !!error.response);
  return carriesViolations
    ? distributeViolations(error, options)
    : { ...EMPTY_ERRORS, general: [describeUnexpectedError(error)] };
}

/** Records which row each indexed line error belongs to. */
export function stampRowKeys(errors: DistributedErrors, rowKeys: string[]): DistributedErrors {
  return { ...errors, rowKeys };
}

/** The errors for one row, found by its key (falling back to the index for unstamped errors). */
export function errorsForRow(errors: DistributedErrors, rowKey: string, index: number): Record<string, string> {
  if (!errors.rowKeys) return errors.lines[index] ?? {};
  const original = errors.rowKeys.indexOf(rowKey);
  return original < 0 ? {} : (errors.lines[original] ?? {});
}

/** Renames a per-line field in already-distributed errors (e.g. the server's `quantityDelta` is the
 *  form's `quantity`), keeping `firstTarget` pointing at the right element. */
export function renameLineField(errors: DistributedErrors, from: string, to: string): DistributedErrors {
  const lines: DistributedErrors['lines'] = {};
  for (const [index, fields] of Object.entries(errors.lines)) {
    lines[parseInt(index, 10)] = Object.fromEntries(Object.entries(fields).map(([k, v]) => [k === from ? to : k, v]));
  }
  return {
    ...errors,
    lines,
    firstTarget: errors.firstTarget ? errors.firstTarget.replace(new RegExp(`-${from}$`), `-${to}`) : null,
  };
}
