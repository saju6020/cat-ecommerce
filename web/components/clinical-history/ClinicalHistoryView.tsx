'use client';

import { useRef, useState } from 'react';
import { ArrowLeft } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { PermissionDeniedState } from '@/components/admin/PermissionDeniedState';
import { ErrorState } from '@/components/common/ErrorState';
import { LoadingState } from '@/components/common/LoadingState';
import { DiagnosticOrderView } from '@/components/visit/panels/DiagnosticOrderView';
import { VisitRecordView } from '@/components/visit/VisitRecordView';
import { HistoryOverview } from './HistoryOverview';
import { HistorySection } from './HistorySection';
import { sectionLabel } from './historyLabels';
import { ApiForbiddenError, ApiNotFoundError } from '@/lib/api/client';
import { useBranchTimeZone } from '@/lib/hooks/useBranchTimeZone';
import { useClinicalHistory } from '@/lib/hooks/useClinicalHistory';
import { useVisit } from '@/lib/hooks/useVisit';
import { cn } from '@/lib/utils';

/**
 * ⭐ `ClinicalHistoryView` — HO-EMR-002 §11.4 (US-CLN-13), the body of both `HistoryDrawer` and the
 * `/staff/cats/[catId]/history` page. Tabs come from the summary's `sections[]` IN SERVER ORDER with
 * **Overview** first (ADR-FE-056); only the open tab fetches. A visit or order opens NESTED and
 * read-only with a Back button — never a navigation (ADR-FE-049). The tab list follows the ARIA
 * tabs pattern (←/→ or ↑/↓ move, Home/End jump); on the page it is a vertical list.
 */
type Nested = { type: 'visit' | 'order'; id: string } | null;

export function ClinicalHistoryView({
  catId,
  layout,
}: {
  catId: string;
  layout: 'drawer' | 'page';
}) {
  const summary = useClinicalHistory(catId);
  const zone = useBranchTimeZone();
  const [tab, setTab] = useState('overview');
  const [nested, setNested] = useState<Nested>(null);
  const tabRefs = useRef<Record<string, HTMLButtonElement | null>>({});

  if (summary.isLoading)
    return <LoadingState variant="card" rows={6} label="Loading the clinical history…" />;
  if (summary.error instanceof ApiForbiddenError) return <PermissionDeniedState />;
  if (summary.error instanceof ApiNotFoundError) {
    return <ErrorState title="We couldn’t find this cat’s history." />;
  }
  if (summary.isError || !summary.data)
    return <ErrorState onRetry={() => void summary.refetch()} />;

  const data = summary.data;
  const tabs = [
    { key: 'overview', label: 'Overview' },
    ...data.sections.map((s) => ({ key: s.key, label: sectionLabel(s.key) })),
  ];
  const vertical = layout === 'page';
  const section = data.sections.find((s) => s.key === tab);

  function move(from: number, delta: number | 'first' | 'last') {
    const next =
      delta === 'first'
        ? 0
        : delta === 'last'
          ? tabs.length - 1
          : (from + delta + tabs.length) % tabs.length;
    const key = tabs[next]!.key;
    setTab(key);
    setNested(null);
    tabRefs.current[key]?.focus();
  }

  return (
    <div className={cn('flex flex-col gap-4', vertical && 'md:flex-row md:items-start')}>
      <div
        role="tablist"
        aria-label="Clinical history sections"
        aria-orientation={vertical ? 'vertical' : 'horizontal'}
        className={cn(
          'flex flex-wrap gap-1 border-border',
          vertical
            ? 'border-b pb-2 md:w-52 md:flex-none md:flex-col md:border-b-0 md:border-r md:pb-0 md:pr-3'
            : 'border-b pb-1',
        )}
      >
        {tabs.map((t, index) => (
          <button
            key={t.key}
            ref={(el) => {
              tabRefs.current[t.key] = el;
            }}
            id={`history-tab-${t.key}`}
            type="button"
            role="tab"
            aria-selected={tab === t.key}
            aria-controls="history-panel"
            tabIndex={tab === t.key ? 0 : -1}
            onClick={() => {
              setTab(t.key);
              setNested(null);
            }}
            onKeyDown={(event) => {
              if (event.key === 'ArrowRight' || event.key === 'ArrowDown') {
                event.preventDefault();
                move(index, 1);
              } else if (event.key === 'ArrowLeft' || event.key === 'ArrowUp') {
                event.preventDefault();
                move(index, -1);
              } else if (event.key === 'Home') {
                event.preventDefault();
                move(index, 'first');
              } else if (event.key === 'End') {
                event.preventDefault();
                move(index, 'last');
              }
            }}
            className={cn(
              'rounded-[var(--radius-sm)] px-3 py-2 text-left text-sm font-semibold focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-accent',
              tab === t.key ? 'bg-accent-soft text-accent-ink' : 'text-ink-muted hover:text-ink',
            )}
          >
            {t.label}
          </button>
        ))}
      </div>
      <div
        id="history-panel"
        role="tabpanel"
        aria-labelledby={`history-tab-${tab}`}
        tabIndex={-1}
        className="min-w-0 flex-1"
      >
        {nested ? (
          <NestedRecord nested={nested} zone={zone} onBack={() => setNested(null)} />
        ) : tab === 'overview' ? (
          <HistoryOverview
            summary={data}
            zone={zone}
            onOpenVisit={(id) => setNested({ type: 'visit', id })}
            onOpenOrder={(id) => setNested({ type: 'order', id })}
          />
        ) : section ? (
          <HistorySection
            catId={catId}
            section={section}
            zone={zone}
            onOpenVisit={(id) => setNested({ type: 'visit', id })}
            onOpenOrder={(id) => setNested({ type: 'order', id })}
          />
        ) : null}
      </div>
    </div>
  );
}

function NestedRecord({
  nested,
  zone,
  onBack,
}: {
  nested: NonNullable<Nested>;
  zone: string;
  onBack: () => void;
}) {
  return (
    <div className="flex flex-col gap-4">
      <div>
        <Button variant="secondary" size="sm" onClick={onBack}>
          <ArrowLeft size={14} aria-hidden="true" /> Back to the history
        </Button>
      </div>
      {nested.type === 'visit' ? (
        <NestedVisit visitId={nested.id} />
      ) : (
        <DiagnosticOrderView orderId={nested.id} canWrite={false} zone={zone} />
      )}
    </div>
  );
}

function NestedVisit({ visitId }: { visitId: string }) {
  const query = useVisit(visitId);
  if (query.isLoading) return <LoadingState variant="card" rows={6} label="Loading the visit…" />;
  if (query.error instanceof ApiForbiddenError) return <PermissionDeniedState />;
  if (query.error instanceof ApiNotFoundError)
    return (
      <ErrorState
        title="We couldn’t find this visit."
        description="It may belong to another branch."
      />
    );
  if (query.isError || !query.data) return <ErrorState onRetry={() => void query.refetch()} />;
  return <VisitRecordView visit={query.data} />;
}
