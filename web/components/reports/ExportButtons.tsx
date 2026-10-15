'use client';

import { useState } from 'react';
import { Download, TriangleAlert } from 'lucide-react';
import { PermissionDeniedState } from '@/components/admin/PermissionDeniedState';
import { Button } from '@/components/ui/button';
import { ApiForbiddenError } from '@/lib/api/client';
import { describeReportError, reportViolations } from '@/lib/api/errors';
import { reportExportPath, type ReportExportFormat } from '@/lib/api/reports';
import { downloadReportExport } from '@/lib/download/reportExport';

type Outcome = { kind: 'error'; message: string } | { kind: 'forbidden' } | { kind: 'done' } | null;

const LABELS: Record<ReportExportFormat, string> = { csv: 'Export CSV', pdf: 'Export PDF' };

/**
 * `components/reports/ExportButtons.tsx` — ⭐ HO-RPT-002 §11.3, ADR-FE-132. **Export CSV** and
 * **Export PDF** FETCH the file with the current filters (no paging) and save the blob the server
 * produced under its own filename, revoking the object URL straight away. A refusal arrives as a
 * typed error and is shown beside the buttons in plain words — `413` ("Too many rows for a PDF —
 * export CSV instead."), `400 ReportTimedOut`, a `429` with its wait — and a `403` becomes
 * `PermissionDeniedState`. While one export runs its button is busy, disabled and announced; the
 * table stays usable. `disabled` greys both (used when the filters themselves are invalid).
 */
export function ExportButtons({
  reportKey,
  query,
  fallbackBaseName,
  disabled = false,
}: {
  reportKey: string;
  /** The server query string the rows were requested with. */
  query: string;
  /** Used when the server's filename header is not readable, e.g. `purrcare-{key}-{from}-to-{to}`. */
  fallbackBaseName: string;
  disabled?: boolean;
}) {
  const [busy, setBusy] = useState<ReportExportFormat | null>(null);
  const [outcome, setOutcome] = useState<Outcome>(null);

  async function run(format: ReportExportFormat) {
    setBusy(format);
    setOutcome(null);
    try {
      await downloadReportExport(
        reportExportPath(reportKey, format, query),
        `${fallbackBaseName}.${format}`,
      );
      setOutcome({ kind: 'done' });
    } catch (failure) {
      setOutcome(
        failure instanceof ApiForbiddenError &&
          reportViolations(failure).every(
            (violation) => violation.errorCode === 'ActionNotPermitted',
          )
          ? { kind: 'forbidden' }
          : { kind: 'error', message: describeReportError(failure) },
      );
    } finally {
      setBusy(null);
    }
  }

  return (
    <div className="flex min-w-0 flex-col items-stretch gap-2 sm:items-end">
      <div className="flex flex-wrap gap-2">
        {(['csv', 'pdf'] as const).map((format) => (
          <Button
            key={format}
            variant="secondary"
            disabled={disabled || busy !== null}
            aria-busy={busy === format}
            onClick={() => void run(format)}
          >
            <Download size={14} aria-hidden="true" />
            {busy === format ? 'Preparing…' : LABELS[format]}
          </Button>
        ))}
      </div>
      <div role="status" aria-live="polite" className="max-w-md text-sm">
        {busy && <span className="sr-only">Preparing your {busy.toUpperCase()} file…</span>}
        {outcome?.kind === 'done' && <span className="text-ink-muted">Downloaded.</span>}
      </div>
      {outcome?.kind === 'error' && (
        <p
          role="alert"
          className="m-0 flex max-w-md items-start gap-2 rounded-[var(--radius-sm)] bg-warning-bg p-2.5 text-sm font-semibold text-warning-ink"
        >
          <TriangleAlert size={16} className="mt-0.5 flex-none" aria-hidden="true" />
          {outcome.message}
        </p>
      )}
      {outcome?.kind === 'forbidden' && <PermissionDeniedState />}
    </div>
  );
}
