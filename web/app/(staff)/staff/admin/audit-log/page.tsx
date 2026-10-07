'use client';

import { Suspense, useMemo } from 'react';
import { useSearchParams } from 'next/navigation';
import { useDebouncedValue } from '@/lib/hooks/useDebouncedValue';
import { useAuditEntries } from '@/lib/hooks/useAdminAudit';
import { useUrlFilterState, FILTER_WRITE_DEBOUNCE_MS } from '@/lib/hooks/useUrlFilterState';
import { useMyBranches } from '@/lib/hooks/useActiveBranch';
import { useAdminBranches } from '@/lib/hooks/useAdminBranches';
import { AuditLogSearch } from '@/components/audit/AuditLogSearch';
import { PermissionDeniedState } from '@/components/admin/PermissionDeniedState';
import { LoadingState } from '@/components/common/LoadingState';
import { getAuditExportUrl, defaultAuditRange } from '@/lib/api/admin-audit';
import { ApiForbiddenError } from '@/lib/api/client';

const PAGE_SIZE = 50;

const AUDIT_FILTER_FIELDS = [
  'actorUserId',
  'entityType',
  'entityId',
  'action',
  'category',
  'from',
  'to',
] as const;

// TEST gate HIGH (third pass) — the five free-text fields debounce their URL write (~300ms after
// the last keystroke, flushed immediately on blur/Enter — `AuditLogSearch`'s own `onFilterCommit`);
// `from`/`to` are dates and write immediately, unchanged.
const DEBOUNCED_FILTER_FIELDS = [
  'actorUserId',
  'entityType',
  'entityId',
  'action',
  'category',
] as const;

/** `/staff/admin/audit-log` — US-ADM-06. Filters round-trip through the URL and are back-button
 *  safe (U-G4); the fields themselves are local state synced from the URL, not read from it
 *  directly, so fast typing and quick successive edits are never lost (U-G6, TEST gate HIGH,
 *  second and third passes). */
function AuditLogContent() {
  const searchParams = useSearchParams();

  const { from: defaultFrom, to: defaultTo } = useMemo(() => defaultAuditRange(), []);

  // §8.3: back-button safe — each filter edit is a history entry. §8.3/U-G6: `filters` is this
  // hook's own local state, not a direct read of `searchParams`.
  const {
    values: filters,
    setValue: handleFilterChange,
    flush: flushFilterChange,
  } = useUrlFilterState(
    AUDIT_FILTER_FIELDS,
    { from: defaultFrom, to: defaultTo },
    DEBOUNCED_FILTER_FIELDS,
  );
  const urlPageNumber = Number(searchParams.get('page') ?? '1') || 1;

  // ADM backlog 31: a cleared date (or Chrome's transient empty value mid-edit) means the default
  // range, so the request and export agree with what a reload of the URL (no param) shows.
  const effective = {
    ...filters,
    from: filters.from || defaultFrom,
    to: filters.to || defaultTo,
  };
  // ADM backlog 32: `filters` is local state that runs ahead of the URL write. While any filter
  // differs from the URL the URL's `page` belongs to the OLD filters, so paging restarts at 1.
  const filtersAheadOfUrl = AUDIT_FILTER_FIELDS.some((name) => {
    const inUrl =
      searchParams.get(name) ?? (name === 'from' ? defaultFrom : name === 'to' ? defaultTo : '');
    return effective[name] !== inUrl;
  });
  const pageNumber = filtersAheadOfUrl ? 1 : urlPageNumber;

  // ...and the request itself waits for typing to settle (same window as the URL write), so a
  // burst of keystrokes is one request, not one per character.
  const searchParamsForQuery = useMemo(
    () => ({ ...effective, pageNumber, pageSize: PAGE_SIZE }),
    // eslint-disable-next-line react-hooks/exhaustive-deps
    [
      effective.actorUserId,
      effective.entityType,
      effective.entityId,
      effective.action,
      effective.category,
      effective.from,
      effective.to,
      pageNumber,
    ],
  );
  const debouncedParams = useDebouncedValue(searchParamsForQuery, FILTER_WRITE_DEBOUNCE_MS);
  const { data, isLoading, isError, error, refetch } = useAuditEntries(debouncedParams);
  const myBranches = useMyBranches();
  const branches = useAdminBranches();
  const primaryZone =
    branches.data?.find((b) => b.id === myBranches.data?.primaryBranchId)?.timeZoneId ??
    'Asia/Dhaka';

  if (error instanceof ApiForbiddenError) return <PermissionDeniedState />;

  return (
    <div className="flex flex-col gap-4">
      <h1 className="font-display text-xl font-semibold text-ink">Audit log</h1>
      <AuditLogSearch
        filters={filters}
        onFilterChange={handleFilterChange}
        onFilterCommit={flushFilterChange}
        entries={data?.items}
        totalCount={data?.totalCount}
        pageSize={PAGE_SIZE}
        isLoading={isLoading}
        isError={isError}
        onRetry={() => refetch()}
        exportUrl={getAuditExportUrl({ ...effective, pageNumber: 1, pageSize: PAGE_SIZE })}
        timeZoneId={primaryZone}
      />
    </div>
  );
}

export default function AuditLogPage() {
  return (
    <Suspense fallback={<LoadingState rows={6} />}>
      <AuditLogContent />
    </Suspense>
  );
}
