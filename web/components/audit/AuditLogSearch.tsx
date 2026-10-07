'use client';

import { useState } from 'react';
import { ChevronDown, ChevronRight, Download } from 'lucide-react';
import { DataTable } from '@/components/common/DataTable';
import { DiffTable } from './DiffTable';
import { formatInZone } from '@/lib/format/time';
import {
  AUDIT_MAX_RANGE_DAYS,
  type AuditEntry,
  type AuditSearchParams,
} from '@/lib/api/admin-audit';

/**
 * `components/audit/AuditLogSearch.tsx` — HO-ADM-002 §11.7, US-ADM-06. Date range is required
 * (defaulted by the page to the last 7 days) with the server's 366-day cap shown before submit
 * (U-G1). Export is a plain download link to the server's CSV URL — a real navigation, not a
 * second `fetch` call — with a standing note about the 50,000-row cap next to it (U-G3), since a
 * failed download cannot be intercepted and re-rendered without a second fetch path client.ts does
 * not expose.
 */
export interface AuditLogSearchProps {
  filters: Pick<
    AuditSearchParams,
    'actorUserId' | 'entityType' | 'entityId' | 'action' | 'category' | 'from' | 'to'
  >;
  onFilterChange: (name: string, value: string) => void;
  /** TEST gate HIGH (third pass) — the five free-text fields below debounce their URL write
   *  (`useUrlFilterState`'s `debouncedFields`); this writes the field's current value immediately,
   *  cancelling that wait, so leaving the field (blur) or pressing Enter never leaves the URL
   *  stale behind what is on screen. Optional so a caller with no debounced fields can omit it. */
  onFilterCommit?: (name: string) => void;
  entries: AuditEntry[] | undefined;
  totalCount: number | undefined;
  pageSize: number;
  isLoading: boolean;
  isError: boolean;
  onRetry: () => void;
  exportUrl: string;
  timeZoneId: string;
}

export function AuditLogSearch({
  filters,
  onFilterChange,
  onFilterCommit,
  entries,
  totalCount,
  pageSize,
  isLoading,
  isError,
  onRetry,
  exportUrl,
  timeZoneId,
}: AuditLogSearchProps) {
  const [expanded, setExpanded] = useState<Set<string>>(new Set());
  const rangeTooLong =
    !!filters.from &&
    !!filters.to &&
    (new Date(filters.to).getTime() - new Date(filters.from).getTime()) / 86_400_000 >
      AUDIT_MAX_RANGE_DAYS;

  function toggle(key: string) {
    setExpanded((current) => {
      const next = new Set(current);
      if (next.has(key)) next.delete(key);
      else next.add(key);
      return next;
    });
  }

  return (
    <div className="flex flex-col gap-4">
      <div className="flex flex-wrap gap-3">
        {(['actorUserId', 'entityType', 'entityId', 'action', 'category'] as const).map((field) => (
          <div key={field} className="flex flex-col gap-1.5">
            <label htmlFor={`audit-${field}`} className="text-xs font-semibold text-ink-muted">
              {field}
            </label>
            <input
              id={`audit-${field}`}
              value={filters[field] ?? ''}
              onChange={(e) => onFilterChange(field, e.target.value)}
              onBlur={() => onFilterCommit?.(field)}
              onKeyDown={(e) => {
                if (e.key === 'Enter') {
                  e.preventDefault();
                  onFilterCommit?.(field);
                }
              }}
              className="rounded-[var(--radius-sm)] border border-border-strong bg-surface px-3 py-2 text-sm text-ink"
            />
          </div>
        ))}
        <div className="flex flex-col gap-1.5">
          <label htmlFor="audit-from" className="text-xs font-semibold text-ink-muted">
            From
          </label>
          <input
            id="audit-from"
            type="date"
            value={filters.from}
            onChange={(e) => onFilterChange('from', e.target.value)}
            className="rounded-[var(--radius-sm)] border border-border-strong bg-surface px-3 py-2 text-sm text-ink"
          />
        </div>
        <div className="flex flex-col gap-1.5">
          <label htmlFor="audit-to" className="text-xs font-semibold text-ink-muted">
            To
          </label>
          <input
            id="audit-to"
            type="date"
            value={filters.to}
            onChange={(e) => onFilterChange('to', e.target.value)}
            className="rounded-[var(--radius-sm)] border border-border-strong bg-surface px-3 py-2 text-sm text-ink"
          />
        </div>
      </div>

      {rangeTooLong && (
        <p role="alert" className="text-sm font-semibold text-danger-ink">
          The date range is more than {AUDIT_MAX_RANGE_DAYS} days — narrow it before searching.
        </p>
      )}

      <div className="flex items-center gap-3">
        <a
          href={exportUrl}
          download
          className="inline-flex items-center gap-2 rounded-[var(--radius-sm)] border border-border-strong bg-surface px-3 py-2 text-sm font-semibold text-ink no-underline hover:bg-surface-sunken"
        >
          <Download size={14} aria-hidden="true" />
          Export CSV
        </a>
        <p className="text-xs text-ink-muted">
          Too many entries to export — narrow the date range if the export is refused (over 50,000
          matching entries).
        </p>
      </div>

      <DataTable
        caption="Audit log"
        rows={entries}
        getRowId={(row) => `${row.occurredOn}-${row.entityId}-${row.action}`}
        isLoading={isLoading}
        isError={isError}
        onRetry={onRetry}
        emptyState={{ title: 'No audit entries match these filters' }}
        pagination={totalCount !== undefined ? { totalCount, pageSize } : undefined}
        columns={[
          {
            id: 'occurredOn',
            header: 'Time',
            cell: (row) => (
              <div className="flex flex-col">
                <span>{formatInZone(row.occurredOn, timeZoneId)}</span>
              </div>
            ),
          },
          { id: 'actor', header: 'Actor', cell: (row) => row.actorDisplayName },
          { id: 'action', header: 'Action', cell: (row) => row.action },
          { id: 'category', header: 'Category', cell: (row) => row.category },
          { id: 'entity', header: 'Entity', cell: (row) => `${row.entityType} ${row.entityId}` },
          { id: 'ip', header: 'IP', cell: (row) => row.ipAddress ?? '—' },
          {
            id: 'details',
            header: 'Details',
            cell: (row) => {
              const key = `${row.occurredOn}-${row.entityId}-${row.action}`;
              const isOpen = expanded.has(key);
              return (
                <div>
                  <button
                    type="button"
                    onClick={() => toggle(key)}
                    aria-expanded={isOpen}
                    className="inline-flex items-center gap-1 text-sm font-semibold text-accent-ink underline"
                  >
                    {isOpen ? (
                      <ChevronDown size={14} aria-hidden="true" />
                    ) : (
                      <ChevronRight size={14} aria-hidden="true" />
                    )}
                    {isOpen ? 'Hide' : 'Show'} details
                  </button>
                  {isOpen && (
                    <div className="mt-2">
                      {row.reason && <p className="mb-2 text-sm text-ink">Reason: {row.reason}</p>}
                      <DiffTable before={row.beforeValue} after={row.afterValue} />
                    </div>
                  )}
                </div>
              );
            },
          },
        ]}
      />
    </div>
  );
}
