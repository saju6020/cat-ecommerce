'use client';

import { Button } from '@/components/ui/button';
import { EmptyState } from '@/components/common/EmptyState';
import { ErrorState } from '@/components/common/ErrorState';
import { LoadingState } from '@/components/common/LoadingState';
import { PermissionDeniedState } from '@/components/admin/PermissionDeniedState';
import { HistoryItemRow } from './HistoryItem';
import { SectionUnavailable } from './SectionUnavailable';
import { sectionLabel } from './historyLabels';
import { ApiForbiddenError } from '@/lib/api/client';
import { useHistorySection } from '@/lib/hooks/useClinicalHistory';
import type { HistorySectionKey, HistorySectionSummary } from '@/lib/api/clinical-history';

/**
 * ⭐ `HistorySection` — HO-EMR-002 §11.4, ADR-FE-056. A section's FIRST page is fetched only when its tab
 * opens, then **Load more** (no virtual-list library). A section the summary reports
 * `available: false` renders `SectionUnavailable` and makes NO request (U-F2). Loading, empty, error
 * and `403` are each their own state.
 */
export function HistorySection({
  catId,
  section,
  zone,
  onOpenVisit,
  onOpenOrder,
}: {
  catId: string;
  section: HistorySectionSummary;
  zone: string;
  onOpenVisit: (visitId: string) => void;
  onOpenOrder: (orderId: string) => void;
}) {
  const key = section.key as HistorySectionKey;
  const query = useHistorySection(catId, key, section.available);
  if (!section.available) return <SectionUnavailable sectionKey={section.key} />;
  if (query.isLoading)
    return <LoadingState variant="card" rows={5} label={`Loading ${sectionLabel(key)}…`} />;
  if (query.error instanceof ApiForbiddenError) return <PermissionDeniedState />;
  if (query.isError || !query.data) {
    return (
      <ErrorState
        title={`We couldn’t load ${sectionLabel(key).toLowerCase()}.`}
        onRetry={() => void query.refetch()}
      />
    );
  }
  const first = query.data.pages[0];
  if (first && !first.available) return <SectionUnavailable sectionKey={section.key} />;
  const items = query.data.pages.flatMap((page) => page.items);
  if (items.length === 0) {
    return (
      <EmptyState
        title={`No ${sectionLabel(key).toLowerCase()} recorded.`}
        description="Nothing has been recorded for this cat yet."
      />
    );
  }
  return (
    <section aria-label={sectionLabel(key)} className="flex flex-col gap-2">
      <ul className="m-0 list-none p-0">
        {items.map((item) => (
          <HistoryItemRow
            key={`${item.kind}-${item.id}`}
            item={item}
            zone={zone}
            onOpenVisit={onOpenVisit}
            onOpenOrder={onOpenOrder}
          />
        ))}
      </ul>
      <p className="text-xs text-ink-muted">
        Showing {items.length} of {first?.totalCount ?? items.length}.
      </p>
      {query.hasNextPage && (
        <div>
          <Button
            variant="secondary"
            onClick={() => void query.fetchNextPage()}
            disabled={query.isFetchingNextPage}
            aria-busy={query.isFetchingNextPage}
          >
            {query.isFetchingNextPage ? 'Loading…' : 'Load more'}
          </Button>
        </div>
      )}
    </section>
  );
}
