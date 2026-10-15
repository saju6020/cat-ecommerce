'use client';

import { useRouter } from 'next/navigation';
import { AccessRefusedState } from '@/components/admin/AccessRefusedState';
import { ApiForbiddenError } from '@/lib/api/client';
import type { BankStatementSummary } from '@/lib/api/billing-reconciliation';
import { formatDateOnly, formatInstantDate } from '@/lib/format/time';
import { useBankStatements } from '@/lib/hooks/billing/useBillingQueries';
import { useImportBankStatement } from '@/lib/hooks/billing/useBillingMutations';
import { useUrlFilterState } from '@/lib/hooks/useUrlFilterState';
import { PageHeader, Section } from './PageHeader';
import { ResponsiveTable, type Column } from './ResponsiveTable';
import { StatementUpload } from './StatementUpload';

/**
 * `components/billing/BankStatementsScreen.tsx` — HO-BIL-002 §11.8, `/staff/billing/reconciliation/bank`.
 * Import a statement CSV, then work through the imports: each row opens its match view with how many
 * lines are matched so far.
 */
export function BankStatementsScreen() {
  const router = useRouter();
  const { values, setValue } = useUrlFilterState(['page'] as const);
  const statements = useBankStatements(values.page || 1);
  const importer = useImportBankStatement();

  const columns: Array<Column<BankStatementSummary>> = [
    { id: 'label', header: 'Bank account', primary: true, cell: (s) => s.bankAccountLabel },
    { id: 'period', header: 'Period', cell: (s) => `${formatDateOnly(s.periodFrom)} – ${formatDateOnly(s.periodTo)}` },
    { id: 'file', header: 'File', cell: (s) => s.sourceFileName },
    { id: 'lines', header: 'Lines', numeric: true, cell: (s) => s.lineCount },
    { id: 'matched', header: 'Matched', numeric: true, cell: (s) => `${s.matchedCount} of ${s.lineCount}` },
    { id: 'imported', header: 'Imported', cell: (s) => `${formatInstantDate(s.importedOn)} by ${s.importedByName}` },
  ];

  if (statements.error instanceof ApiForbiddenError) return <AccessRefusedState error={statements.error} />;

  return (
    <div className="flex flex-col gap-5">
      <PageHeader title="Bank reconciliation" description="Import a bank statement and match its credits to bank-transfer payments." />
      <Section title="Import a statement">
        <StatementUpload onImport={(request) => importer.mutateAsync(request)} onImported={(id) => router.push(`/staff/billing/reconciliation/bank/${id}`)} />
      </Section>
      <Section title="Imported statements">
        <ResponsiveTable
          caption="Imported bank statements"
          columns={columns}
          rows={statements.data?.items}
          getRowId={(s) => s.statementId}
          rowHref={(s) => `/staff/billing/reconciliation/bank/${s.statementId}`}
          isLoading={statements.isLoading}
          isError={statements.isError}
          onRetry={() => void statements.refetch()}
          emptyState={{ title: 'No statements imported yet.', description: 'Import a CSV above to start matching.' }}
          pager={
            statements.data
              ? { pageNumber: statements.data.pageNumber, pageSize: statements.data.pageSize, totalCount: statements.data.totalCount, onPageChange: (n) => setValue('page', String(n)) }
              : undefined
          }
        />
      </Section>
    </div>
  );
}
