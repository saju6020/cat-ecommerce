'use client';

import { useState } from 'react';
import { Download } from 'lucide-react';
import { AccessRefusedState } from '@/components/admin/AccessRefusedState';
import { buttonVariants } from '@/components/ui/button';
import { ApiForbiddenError } from '@/lib/api/client';
import { exportDownloadUrl, shortHash, type AccountingExportRun } from '@/lib/api/billing-export';
import { formatDateOnly, formatInstantDate } from '@/lib/format/time';
import { useExportRuns } from '@/lib/hooks/billing/useBillingQueries';
import { useActiveBranchId } from '@/lib/hooks/useActiveBranch';
import { useAdminBranches } from '@/lib/hooks/useAdminBranches';
import { useUrlFilterState } from '@/lib/hooks/useUrlFilterState';
import { cn } from '@/lib/utils';
import { SelectField, TextField } from './Fields';
import { PageHeader, Section } from './PageHeader';
import { ResponsiveTable, type Column } from './ResponsiveTable';

/**
 * `components/billing/ExportPanel.tsx` — HO-BIL-002 §11.9 (US-BIL-09). Pick a range (branch-local
 * dates) and optionally a branch, then **Download**: a plain link to the download endpoint, so the
 * browser streams the ZIP. The same range gives the same file; every run is recorded (who, when, range,
 * counts, SHA-256) and listed below. A range over 366 days is refused here and by the server
 * (`ExportRangeTooLarge`) with a message, never by navigating away to an error body.
 *
 * Known gap (owner decision pending, HO-BIL-001 gate XI-15): the server currently ignores the caller's
 * branch assignment on this endpoint; the branch selector is built as the handover specifies.
 */
const MAX_DAYS = 366;

function daysInclusive(from: string, to: string): number {
  return Math.round((Date.parse(`${to}T00:00:00Z`) - Date.parse(`${from}T00:00:00Z`)) / 86_400_000) + 1;
}

export function ExportPanel() {
  const branches = useAdminBranches();
  const { values, setValue } = useUrlFilterState(['from', 'to', 'branch', 'page'] as const);
  const runs = useExportRuns(values.page || 1);
  // Until the owner decides on export branch scope (HO-BIL-001 XI-15) the branch defaults to the ACTIVE
  // branch; "All branches" is an explicit choice (`branch=all`).
  const activeBranchId = useActiveBranchId();
  const branchChoice = values.branch || activeBranchId || 'all';
  const branchId = branchChoice === 'all' ? undefined : branchChoice;
  const [attempted, setAttempted] = useState(false);

  const complete = /^\d{4}-\d{2}-\d{2}$/.test(values.from) && /^\d{4}-\d{2}-\d{2}$/.test(values.to);
  const backwards = complete && values.to < values.from;
  const tooLarge = complete && !backwards && daysInclusive(values.from, values.to) > MAX_DAYS;
  const problem = !complete && attempted
    ? 'Choose the first and last day to export.'
    : backwards
      ? 'The last day cannot be before the first day.'
      : tooLarge
        ? 'An export can cover at most 366 days. Choose a shorter range.'
        : null;
  const ready = complete && !backwards && !tooLarge;

  const columns: Array<Column<AccountingExportRun>> = [
    { id: 'run', header: 'Run', primary: true, sortValue: (r) => r.runOn, cell: (r) => `${formatInstantDate(r.runOn)} by ${r.runByName}` },
    { id: 'range', header: 'Range', cell: (r) => `${formatDateOnly(r.from)} – ${formatDateOnly(r.to)}` },
    { id: 'branch', header: 'Branch', cell: (r) => branches.data?.find((b) => b.id === r.branchId)?.name ?? (r.branchId ? 'One branch' : 'All branches') },
    { id: 'invoices', header: 'Invoices', numeric: true, cell: (r) => `${r.invoiceCount} (${r.invoiceLineCount} lines)` },
    { id: 'payments', header: 'Payments', numeric: true, cell: (r) => `${r.paymentCount} (${r.allocationCount} allocations)` },
    { id: 'credit', header: 'Credit notes / refunds', numeric: true, cell: (r) => `${r.creditNoteCount} / ${r.refundCount}` },
    { id: 'hash', header: 'SHA-256', cell: (r) => <code title={r.sha256} className="text-xs">{shortHash(r.sha256)}…</code> },
  ];

  if (runs.error instanceof ApiForbiddenError) return <AccessRefusedState error={runs.error} />;

  return (
    <div className="flex flex-col gap-5">
      <PageHeader title="Accounting export" description="The invoices, payments, credit notes and refunds of a date range, as one ZIP of CSV files." />
      <Section title="Export a range">
        <form className="grid max-w-3xl gap-4 sm:grid-cols-2" onSubmit={(e) => e.preventDefault()} aria-label="Export range">
          <TextField id="exp-from" label="From" type="date" required value={values.from} onChange={(e) => setValue('from', e.target.value)} />
          <TextField id="exp-to" label="To" type="date" required value={values.to} onChange={(e) => setValue('to', e.target.value)} />
          <SelectField id="exp-branch" label="Branch" value={branchChoice} onChange={(e) => setValue('branch', e.target.value)} hint="Defaults to the branch you are working at; choose all branches for the whole business.">
            <option value="all">All branches</option>
            {(branches.data ?? []).map((b) => (
              <option key={b.id} value={b.id}>{b.name}</option>
            ))}
          </SelectField>
        </form>
        {problem && <p role="alert" className="text-sm font-semibold text-danger-ink">{problem}</p>}
        <div>
          <a
            href={ready ? exportDownloadUrl({ from: values.from, to: values.to, branchId }) : undefined}
            aria-disabled={!ready}
            download
            onClick={(event) => {
              if (!ready) {
                event.preventDefault();
                setAttempted(true);
              }
            }}
            className={cn(buttonVariants({ variant: 'primary' }), !ready && 'pointer-events-auto opacity-50')}
          >
            <Download size={16} aria-hidden="true" />
            Download ZIP
          </a>
        </div>
        <p className="text-sm text-ink-muted">Downloading the same range again produces an identical file.</p>
      </Section>
      <Section title="Run history">
        <ResponsiveTable
          caption="Accounting export runs"
          columns={columns}
          rows={runs.data?.items}
          getRowId={(r) => r.runId}
          isLoading={runs.isLoading}
          isError={runs.isError}
          onRetry={() => void runs.refetch()}
          minDesktopWidth={1024}
          emptyState={{ title: 'No export has been run yet.' }}
          pager={
            runs.data
              ? { pageNumber: runs.data.pageNumber, pageSize: runs.data.pageSize, totalCount: runs.data.totalCount, onPageChange: (n) => setValue('page', String(n)) }
              : undefined
          }
        />
      </Section>
    </div>
  );
}
