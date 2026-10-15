import type { RestoreCheck } from '@/lib/api/operations';

/**
 * `lib/operations/drill.ts` — HO-XCT-002 §11.5, U-B8. Parses the JSON that `restore-drill.sh`
 * prints (`deploy/backup/scripts/restore-drill.sh`) into the fields of the restore-test form. It
 * reads what the script wrote and asks for nothing more: there is no RPO, RTO or outcome here,
 * because the server computes them (HO-XCT-001 ADR-313).
 */
export interface DrillFields {
  performedOn: string;
  environment: string;
  backupSetLabel: string;
  simulatedFailureAt: string;
  recoveredTo: string;
  restoreStartedAt: string;
  restoreCompletedAt: string;
  checks: RestoreCheck[];
}

export type DrillParseResult =
  | { ok: true; fields: DrillFields }
  | { ok: false; message: string };

const DATE_ONLY = /^\d{4}-\d{2}-\d{2}$/;
const INSTANT_KEYS = [
  'simulatedFailureAt',
  'recoveredTo',
  'restoreStartedAt',
  'restoreCompletedAt',
] as const;

/** An ISO-8601 instant that says which offset it is in (`Z` or `+06:00`). Without one the browser
 *  would read it in its own zone, which can be a different moment from the one the drill measured. */
export function hasUtcOffset(value: string): boolean {
  return /(?:Z|[+-]\d{2}:?\d{2})$/i.test(value.trim());
}

export function parseDrillOutput(text: string): DrillParseResult {
  let value: unknown;
  try {
    value = JSON.parse(text);
  } catch {
    return {
      ok: false,
      message:
        "That isn't readable JSON. Paste the whole output of restore-drill.sh, from the opening { to the closing }.",
    };
  }
  if (typeof value !== 'object' || value === null || Array.isArray(value)) {
    return { ok: false, message: 'The drill output should be one JSON object.' };
  }
  const record = value as Record<string, unknown>;

  const missing: string[] = [];
  const text_ = (key: string): string => {
    const field = record[key];
    if (typeof field !== 'string' || field.trim() === '') {
      missing.push(key);
      return '';
    }
    return field.trim();
  };

  const performedOn = text_('performedOn');
  const environment = text_('environment');
  const backupSetLabel = text_('backupSetLabel');
  const instants = INSTANT_KEYS.map((key) => text_(key));
  if (missing.length > 0) {
    return { ok: false, message: `The drill output is missing: ${missing.join(', ')}.` };
  }
  if (!DATE_ONLY.test(performedOn)) {
    return { ok: false, message: 'performedOn should be a date like 2026-10-10.' };
  }
  const badInstant = INSTANT_KEYS.find((_, i) => Number.isNaN(Date.parse(instants[i] as string)));
  if (badInstant) {
    return { ok: false, message: `${badInstant} is not a date and time.` };
  }
  const noOffset = INSTANT_KEYS.find((_, i) => !hasUtcOffset(instants[i] as string));
  if (noOffset) {
    return { ok: false, message: `${noOffset} has no offset (like Z or +06:00), so its moment is a guess.` };
  }

  const rawChecks = record.checks;
  if (!Array.isArray(rawChecks) || rawChecks.length === 0) {
    return { ok: false, message: 'The drill output has no checks. Run the full drill and paste its output.' };
  }
  const checks: RestoreCheck[] = [];
  for (const [index, raw] of rawChecks.entries()) {
    const check = raw as Record<string, unknown> | null;
    if (!check || typeof check.name !== 'string' || typeof check.passed !== 'boolean') {
      return { ok: false, message: `Check ${index + 1} needs a name and a passed value.` };
    }
    checks.push({
      name: check.name,
      passed: check.passed,
      detail: typeof check.detail === 'string' ? check.detail : null,
    });
  }

  return {
    ok: true,
    fields: {
      performedOn,
      environment,
      backupSetLabel,
      simulatedFailureAt: instants[0] as string,
      recoveredTo: instants[1] as string,
      restoreStartedAt: instants[2] as string,
      restoreCompletedAt: instants[3] as string,
      checks,
    },
  };
}
