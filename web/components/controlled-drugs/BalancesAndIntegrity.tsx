'use client';

import { AlertTriangle, CheckCircle2, ShieldCheck, XCircle } from 'lucide-react';
import { Badge } from '@/components/ui/badge';
import { Quantity } from '@/components/common/Quantity';
import { ResponsiveTable, type Column } from '@/components/billing/ResponsiveTable';
import { ErrorState } from '@/components/common/ErrorState';
import { LoadingState } from '@/components/common/LoadingState';
import type { RegisterBalance, RegisterVerification } from '@/lib/api/controlled-drugs';
import { formatInstantInZone, formatTimeInZone } from '@/lib/format/pharmacy';

/**
 * `BalancesTable` — HO-PHM-002 §11.6. Per medicine and lot, the register's balance next to the
 * stock on hand. A difference is **Mismatch**, an icon and a word, and it is only ever reported — the
 * screen has no way to "fix" a balance (a difference is investigated, then corrected by a new entry).
 */
export function BalancesTable({
  rows,
  isLoading,
  isError,
  onRetry,
}: {
  rows: RegisterBalance[] | undefined;
  isLoading: boolean;
  isError: boolean;
  onRetry: () => void;
}) {
  const columns: Array<Column<RegisterBalance>> = [
    { id: 'medicine', header: 'Medicine', primary: true, cell: (row) => row.medicineName },
    { id: 'lot', header: 'Lot', cell: (row) => row.lotNumber ?? 'No lot' },
    {
      id: 'register',
      header: 'Register balance',
      cell: (row) => <Quantity value={row.registerBalance} />,
    },
    {
      id: 'stock',
      header: 'Stock balance',
      cell: (row) =>
        row.stockBalance ? <Quantity value={row.stockBalance} /> : 'Not in stock records',
    },
    {
      id: 'status',
      header: 'Check',
      cell: (row) =>
        row.isMismatch ? (
          <Badge variant="danger" icon={<AlertTriangle size={12} aria-hidden="true" />}>
            Mismatch
          </Badge>
        ) : (
          <Badge variant="success" icon={<CheckCircle2 size={12} aria-hidden="true" />}>
            Matches
          </Badge>
        ),
    },
  ];
  return (
    <ResponsiveTable
      caption="Register balances against stock"
      columns={columns}
      rows={rows}
      getRowId={(row) => `${row.medicineId}-${row.batchId ?? 'none'}`}
      isLoading={isLoading}
      isError={isError}
      onRetry={onRetry}
      emptyState={{
        title: 'No balances yet',
        description: 'Balances appear once a controlled drug has a register entry.',
      }}
    />
  );
}

/**
 * `IntegrityPanel` — HO-PHM-002 §11.6. *"Register intact — 1,204 entries checked 10:40"*, or
 * *"Integrity check failed at entry position 311"* — error styling AND words, never colour alone —
 * plus the entries still awaiting a seal after the grace period. The verification is the server's.
 */
export function IntegrityPanel({
  data,
  zone,
  isLoading,
  isError,
  onRetry,
}: {
  data: RegisterVerification | undefined;
  zone: string;
  isLoading: boolean;
  isError: boolean;
  onRetry: () => void;
}) {
  if (isLoading) return <LoadingState variant="card" rows={2} label="Checking the register…" />;
  if (isError || !data)
    return <ErrorState title="The register could not be verified just now." onRetry={onRetry} />;
  return (
    <section aria-label="Register integrity" className="flex flex-col gap-3">
      {data.isIntact ? (
        <p
          role="status"
          className="flex items-center gap-2 rounded-[var(--radius-md)] bg-accent-soft p-3 text-sm font-semibold text-accent-ink"
        >
          <ShieldCheck size={18} aria-hidden="true" />
          Register intact — {data.entriesChecked.toLocaleString('en-GB')} entries checked{' '}
          {formatTimeInZone(data.checkedOn, zone)}
        </p>
      ) : (
        <p
          role="alert"
          className="flex items-center gap-2 rounded-[var(--radius-md)] bg-danger-bg p-3 text-sm font-semibold text-danger-ink"
        >
          <XCircle size={18} aria-hidden="true" />
          Integrity check failed at entry position {data.firstBrokenPosition ?? 'unknown'}. Tell an
          administrator now; do not correct anything until it is investigated.
        </p>
      )}
      <div>
        <h3 className="text-sm font-bold text-ink">Awaiting a seal</h3>
        {data.staleUnsealed.length === 0 ? (
          <p className="text-sm text-ink-muted">
            No entry has been waiting for a seal longer than ten minutes.
          </p>
        ) : (
          <ul className="m-0 mt-1 flex list-none flex-col gap-1 p-0 text-sm">
            {data.staleUnsealed.map((entry) => (
              <li key={entry.entryNumber}>
                #{entry.entryNumber} — recorded {formatInstantInZone(entry.recordedOn, zone)}, not
                yet sealed
              </li>
            ))}
          </ul>
        )}
      </div>
    </section>
  );
}
