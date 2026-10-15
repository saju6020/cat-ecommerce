import { ApiValidationError } from '@/lib/api/client';
import { reportViolations } from '@/lib/api/errors';
import type { ReportCatalogueEntry } from '@/lib/api/reports';

/**
 * Field errors for the filter bar from a `400`, matched to a control by the server's `propertyName`
 * (case-insensitively: `from`, `to`, `branchId`, `groupBy` or a parameter name). A violation that
 * names no control (`ReportTimedOut`) goes to `page` and is shown as a page-level banner.
 */
export function filterFieldErrors(
  error: unknown,
  definition: ReportCatalogueEntry,
): { fields: Record<string, string>; page: string[] } {
  const fields: Record<string, string> = {};
  const page: string[] = [];
  if (!(error instanceof ApiValidationError)) return { fields, page };
  const known = ['from', 'to', 'branchId', 'groupBy', ...definition.parameters.map((p) => p.name)];
  for (const violation of reportViolations(error)) {
    const field = known.find((name) => name.toLowerCase() === violation.propertyName.toLowerCase());
    if (field) fields[field] = violation.message;
    else page.push(violation.message);
  }
  return { fields, page };
}
