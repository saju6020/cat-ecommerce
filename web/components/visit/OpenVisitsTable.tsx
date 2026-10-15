'use client';

import { Check, Minus, TriangleAlert } from 'lucide-react';
import { DataTable, type DataTableColumn } from '@/components/common/DataTable';
import { timeOnlyInZone } from '@/lib/format/time';
import type { OpenVisitListItem } from '@/lib/api/visits';

/**
 * `OpenVisitsTable` — HO-EMR-002 §11.9 (UI-5). The branch's open visits: cat (the first cell links to
 * the workspace), service, opened (branch zone), and *vitals* / *note* each as a tick-or-dash **plus a
 * word** (UI-6 — never an icon alone), with the high-risk flag as icon and text. The parent polls every
 * 30 s; a poll only swaps `rows`, so nothing the user has open is reset.
 */
function Done({ done, label }: { done: boolean; label: string }) {
  return (
    <span className="inline-flex items-center gap-1.5">
      {done ? (
        <Check size={14} className="text-accent-ink" aria-hidden="true" />
      ) : (
        <Minus size={14} className="text-ink-muted" aria-hidden="true" />
      )}
      {done ? `${label} recorded` : `No ${label.toLowerCase()} yet`}
    </span>
  );
}

export function OpenVisitsTable({
  rows,
  zone,
  isLoading,
  isError,
  onRetry,
}: {
  rows: OpenVisitListItem[] | undefined;
  zone: string;
  isLoading: boolean;
  isError: boolean;
  onRetry: () => void;
}) {
  const columns: Array<DataTableColumn<OpenVisitListItem>> = [
    {
      id: 'cat',
      header: 'Cat',
      cell: (row) => (
        <span className="inline-flex flex-wrap items-center gap-2">
          {row.cat.name}
          {row.cat.isHighRisk && (
            <span className="inline-flex items-center gap-1 text-xs font-semibold text-danger-ink">
              <TriangleAlert size={12} aria-hidden="true" /> High risk
            </span>
          )}
        </span>
      ),
    },
    { id: 'service', header: 'Service', cell: (row) => row.service },
    { id: 'opened', header: `Opened (${zone})`, cell: (row) => timeOnlyInZone(row.openedOn, zone) },
    { id: 'vitals', header: 'Vitals', cell: (row) => <Done done={row.hasVitals} label="Vitals" /> },
    { id: 'note', header: 'Note', cell: (row) => <Done done={row.hasNote} label="Note" /> },
  ];
  return (
    <DataTable
      caption="Open visits at this branch"
      columns={columns}
      rows={rows}
      getRowId={(row) => row.id}
      rowHref={(row) => `/staff/visits/${row.id}`}
      isLoading={isLoading}
      isError={isError}
      onRetry={onRetry}
      errorTitle="We couldn’t load the open visits just now."
      emptyState={{
        title: 'No open visits.',
        description:
          'A visit opens when a checked-in cat is seen. It appears here until it is finalised.',
      }}
    />
  );
}
