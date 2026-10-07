'use client';

import { Suspense } from 'react';
import { useAdminContentList } from '@/lib/hooks/useAdminContent';
import { useUrlFilterState } from '@/lib/hooks/useUrlFilterState';
import { ContentTable } from '@/components/content/ContentTable';
import { PermissionDeniedState } from '@/components/admin/PermissionDeniedState';
import { LoadingState } from '@/components/common/LoadingState';
import { ApiForbiddenError } from '@/lib/api/client';
import type { ContentKind, ContentStatus } from '@/lib/api/admin-content';

const CONTENT_FILTER_FIELDS = ['kind', 'status'] as const;

/** `/staff/admin/content` — US-ADM-07, `Content.Manage`. Filter by kind/status via URL params;
 *  the fields are local state synced from the URL, not read from it directly, so two quick
 *  successive filter changes are both kept rather than one silently overwriting the other (U-G6,
 *  TEST gate HIGH, second and third passes). Both fields write immediately — no `debouncedFields`
 *  — a `<select>` doesn't fire the rapid per-keystroke changes a text input does. */
function ContentListContent() {
  // §8.3: back-button safe — each filter change is a history entry.
  const { values, setValue: setParam } = useUrlFilterState(CONTENT_FILTER_FIELDS);
  const kind = (values.kind || undefined) as ContentKind | undefined;
  const status = (values.status || undefined) as ContentStatus | undefined;

  const { data, isLoading, isError, error, refetch } = useAdminContentList({ kind, status });

  if (error instanceof ApiForbiddenError) return <PermissionDeniedState />;

  return (
    <div className="flex flex-col gap-4">
      <div className="flex flex-wrap gap-3">
        <div className="flex flex-col gap-1.5">
          <label htmlFor="content-kind-filter" className="text-xs font-semibold text-ink-muted">
            Kind
          </label>
          <select
            id="content-kind-filter"
            value={kind ?? ''}
            onChange={(e) => setParam('kind', e.target.value)}
            className="rounded-[var(--radius-sm)] border border-border-strong bg-surface px-3 py-2 text-sm text-ink"
          >
            <option value="">All</option>
            <option value="Page">Page</option>
            <option value="Faq">FAQ</option>
            <option value="Banner">Banner</option>
          </select>
        </div>
        <div className="flex flex-col gap-1.5">
          <label htmlFor="content-status-filter" className="text-xs font-semibold text-ink-muted">
            Status
          </label>
          <select
            id="content-status-filter"
            value={status ?? ''}
            onChange={(e) => setParam('status', e.target.value)}
            className="rounded-[var(--radius-sm)] border border-border-strong bg-surface px-3 py-2 text-sm text-ink"
          >
            <option value="">All</option>
            <option value="Draft">Draft</option>
            <option value="Published">Published</option>
            <option value="Unpublished">Unpublished</option>
          </select>
        </div>
      </div>

      <ContentTable
        items={data}
        isLoading={isLoading}
        isError={isError}
        onRetry={() => refetch()}
      />
    </div>
  );
}

export default function ContentListPage() {
  return (
    <Suspense fallback={<LoadingState rows={6} />}>
      <ContentListContent />
    </Suspense>
  );
}
