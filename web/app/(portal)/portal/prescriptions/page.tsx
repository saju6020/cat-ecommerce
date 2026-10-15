'use client';

import { Suspense } from 'react';
import { usePathname, useRouter, useSearchParams } from 'next/navigation';
import { Tabs } from '@/components/billing/Tabs';
import { AuthorisedFoodsList } from '@/components/my-prescriptions/AuthorisedFoodsList';
import { MyPrescriptionsList } from '@/components/my-prescriptions/MyPrescriptionsList';
import { DEFAULT_BRANCH_TIME_ZONE } from '@/lib/hooks/useBranchTimeZone';
import { useClinicBranches } from '@/lib/hooks/useBooking';
import { useMyDietAuthorisations, useMyPrescriptions } from '@/lib/hooks/useMyPrescriptions';

const PAGE_SIZE = 20;
type TabId = 'active' | 'past' | 'diets';

/**
 * `/portal/prescriptions` — US-PHM-07, 08: the owner's prescriptions (Active / Past, paged 20 a page)
 * and the foods a vet has authorised. A cat filter appears when the owner has more than one cat with a
 * prescription. The list endpoints return plain arrays, so the cat filter and the paging of "Past" are
 * applied here (HO-PHM-002 §1 Amended).
 */
function MyPrescriptionsPage() {
  const router = useRouter();
  const pathname = usePathname();
  const params = useSearchParams();
  const tab = (
    ['active', 'past', 'diets'].includes(params.get('tab') ?? '') ? params.get('tab') : 'active'
  ) as TabId;
  const catId = params.get('catId') ?? '';
  const page = Math.max(1, Number(params.get('page')) || 1);
  const branches = useClinicBranches();
  const zone = branches.data?.[0]?.timeZoneId ?? DEFAULT_BRANCH_TIME_ZONE;
  const active = useMyPrescriptions('active');
  const past = useMyPrescriptions('past');
  const diets = useMyDietAuthorisations('active', tab === 'diets');
  const dietsPast = useMyDietAuthorisations('past', tab === 'diets');

  function update(next: Record<string, string | undefined>) {
    const search = new URLSearchParams(params.toString());
    for (const [key, value] of Object.entries(next)) {
      if (value) search.set(key, value);
      else search.delete(key);
    }
    if (!('page' in next)) search.delete('page');
    const query = search.toString();
    router.replace(query ? `${pathname}?${query}` : pathname, { scroll: false });
  }

  const cats = new Map<string, string>();
  for (const row of [...(active.data ?? []), ...(past.data ?? [])])
    cats.set(row.catId, row.catName);
  const filter = <T extends { catId: string }>(rows: T[] | undefined) =>
    rows && catId ? rows.filter((row) => row.catId === catId) : rows;
  const dietRows = [...(diets.data ?? []), ...(dietsPast.data ?? [])];

  return (
    <div className="flex flex-col gap-5">
      <div>
        <h1 className="font-display text-2xl font-semibold text-ink">Prescriptions</h1>
        <p className="mt-1 text-sm text-ink-muted">
          The medicines your vet has prescribed, how much is left, and the foods your vet has
          authorised.
        </p>
      </div>
      {cats.size > 1 && (
        <div className="flex max-w-xs flex-col gap-1.5">
          <label htmlFor="mp-cat" className="text-sm font-semibold text-ink">
            Show prescriptions for
          </label>
          <select
            id="mp-cat"
            value={catId}
            onChange={(event) => update({ catId: event.target.value || undefined })}
            className="rounded-[var(--radius-sm)] border border-border-strong bg-surface px-3 py-2 text-sm text-ink focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-accent"
          >
            <option value="">All my cats</option>
            {[...cats.entries()].map(([id, name]) => (
              <option key={id} value={id}>
                {name}
              </option>
            ))}
          </select>
        </div>
      )}
      <Tabs
        label="Prescriptions"
        idPrefix="mp"
        selected={tab}
        onSelect={(id) => update({ tab: id === 'active' ? undefined : id })}
        tabs={[
          {
            id: 'active',
            label: 'Active',
            panel: (
              <MyPrescriptionsList
                rows={filter(active.data)}
                zone={zone}
                isLoading={active.isLoading}
                isError={active.isError}
                onRetry={() => void active.refetch()}
                emptyTitle="No active prescriptions."
                emptyDescription="When your vet prescribes a medicine for one of your cats, it will appear here."
                page={1}
                pageSize={1000}
                onPageChange={() => undefined}
              />
            ),
          },
          {
            id: 'past',
            label: 'Past',
            panel: (
              <MyPrescriptionsList
                rows={filter(past.data)}
                zone={zone}
                isLoading={past.isLoading}
                isError={past.isError}
                onRetry={() => void past.refetch()}
                emptyTitle="No past prescriptions."
                emptyDescription="Finished and cancelled prescriptions are kept here."
                page={page}
                pageSize={PAGE_SIZE}
                onPageChange={(next) => update({ page: String(next) })}
              />
            ),
          },
          {
            id: 'diets',
            label: 'Authorised foods',
            panel: (
              <AuthorisedFoodsList
                rows={filter(dietRows)}
                isLoading={diets.isLoading || dietsPast.isLoading}
                isError={diets.isError || dietsPast.isError}
                onRetry={() => {
                  void diets.refetch();
                  void dietsPast.refetch();
                }}
              />
            ),
          },
        ]}
      />
    </div>
  );
}

export default function PortalPrescriptionsPage() {
  return (
    <Suspense fallback={null}>
      <MyPrescriptionsPage />
    </Suspense>
  );
}
