'use client';

import { Suspense } from 'react';
import { useRouter, useSearchParams } from 'next/navigation';
import { Button } from '@/components/ui/button';
import { DataTable, type DataTableColumn } from '@/components/common/DataTable';
import { HighRiskBadge } from '@/components/cat/HighRiskBadge';
import { useCats } from '@/lib/hooks/useCats';
import type { CatListItem } from '@/lib/api/cats';

/**
 * Cat list — `GET /api/cats` (Q2). US-CAT-09 AC1/AC2, UI-4 (HO-CAT-002 §8.3).
 *
 * Filters/sorts/paginates (UI-4): pagination is real (URL-driven, §8.4); "Include deceased" is
 * the one domain filter this list has. There is no free-text search or column sort here because
 * `GetCatsQuery` (HO-CAT-001 §12.3 Q2) doesn't accept either — it always orders by name and only
 * filters by `ownerId`/`includeDeceased` — so `DataTable`'s `search` prop and `sortable` columns
 * are deliberately not used: enabling a control the API can't act on would be worse than omitting
 * it (HO-CAT-002 §20.7-style finding, recorded in the handover report rather than worked around
 * with a client-side fake sort).
 */
function CatListPage() {
  const router = useRouter();
  const searchParams = useSearchParams();
  const includeDeceased = searchParams.get('includeDeceased') === 'true';
  const page = Math.max(1, Number(searchParams.get('page')) || 1);
  const pageSize = 10;

  const { data, isLoading, isError, refetch } = useCats({
    includeDeceased,
    pageNumber: page,
    pageSize,
  });

  function setIncludeDeceased(next: boolean) {
    const params = new URLSearchParams(searchParams.toString());
    if (next) params.set('includeDeceased', 'true');
    else params.delete('includeDeceased');
    params.delete('page');
    router.replace(`/staff/cats?${params.toString()}`, { scroll: false });
  }

  const columns: Array<DataTableColumn<CatListItem>> = [
    { id: 'name', header: 'Name', cell: (cat) => cat.name },
    { id: 'species', header: 'Species', cell: (cat) => cat.species },
    { id: 'breed', header: 'Breed', cell: (cat) => cat.breed ?? '—' },
    { id: 'sex', header: 'Sex', cell: (cat) => cat.sex },
    {
      id: 'status',
      header: 'Status',
      cell: (cat) => (cat.lifeStatus === 'Deceased' ? 'Deceased' : 'Alive'),
    },
    {
      id: 'highRisk',
      header: 'High risk',
      cell: (cat) =>
        // Owner decision, 2026-09-27 (HO-CAT-002 §1 Amended, supersedes §11.4's compact-only
        // design for this list): staff must see handling warnings without opening each cat, so
        // the note is now shown here too -- truncated, full text via the native `title` tooltip
        // (`variant="list"`, HighRiskBadge.tsx). The portal list (customer-facing) is unchanged
        // and still uses `variant="compact"` (badge only).
        cat.isHighRisk ? <HighRiskBadge handlingNote={cat.handlingNote} variant="list" /> : null,
    },
  ];

  return (
    <div className="flex flex-col gap-6">
      <div className="flex flex-wrap items-center justify-between gap-3">
        <h1 className="font-display text-2xl font-semibold text-ink">Cats</h1>
        <Button onClick={() => router.push('/staff/cats/new')}>Register a cat</Button>
      </div>

      <label className="flex w-fit items-center gap-2 text-sm text-ink">
        <input
          type="checkbox"
          checked={includeDeceased}
          onChange={(event) => setIncludeDeceased(event.target.checked)}
        />
        Include deceased cats
      </label>

      <DataTable
        caption="Cats"
        columns={columns}
        rows={data?.items}
        getRowId={(cat) => cat.id}
        rowHref={(cat) => `/staff/cats/${cat.id}`}
        isLoading={isLoading}
        isError={isError}
        onRetry={() => refetch()}
        errorTitle="We couldn't load the cat list just now."
        pagination={{ pageSize, totalCount: data?.totalCount ?? 0 }}
        emptyState={{
          title: includeDeceased
            ? 'No cats registered yet.'
            : 'No living cats to show — try including deceased cats.',
          description: 'Register a cat to get started.',
          action: <Button onClick={() => router.push('/staff/cats/new')}>Register a cat</Button>,
        }}
      />
    </div>
  );
}

export default function CatListRoute() {
  return (
    <Suspense>
      <CatListPage />
    </Suspense>
  );
}
