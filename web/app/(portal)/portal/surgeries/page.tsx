'use client';

import { Suspense, useState } from 'react';
import { usePathname, useRouter, useSearchParams } from 'next/navigation';
import { Tabs } from '@/components/billing/Tabs';
import { MySurgeriesList } from '@/components/surgery/MySurgeriesList';
import { useMySurgeries } from '@/lib/hooks/useSurgeryCatalogue';

type Scope = 'upcoming' | 'past';

function Panel({ scope }: { scope: Scope }) {
  const [page, setPage] = useState(1);
  const list = useMySurgeries(scope, page);
  return (
    <MySurgeriesList
      scope={scope}
      rows={list.data?.items}
      isLoading={list.isLoading}
      isError={list.isError}
      onRetry={() => void list.refetch()}
      hasMore={!!list.data && list.data.pageNumber * list.data.pageSize < list.data.totalCount}
      onMore={() => setPage((p) => p + 1)}
    />
  );
}

/**
 * `/portal/surgeries` — HO-SRG-002 §11.9 (US-SRG-07): the owner's cats' surgeries, upcoming and past.
 * Only the customer endpoints are used; the status shown is the owner-facing one.
 */
function MySurgeriesPage() {
  const router = useRouter();
  const pathname = usePathname();
  const params = useSearchParams();
  const scope: Scope = params.get('tab') === 'past' ? 'past' : 'upcoming';
  return (
    <div className="flex flex-col gap-5">
      <div>
        <h1 className="font-display text-2xl font-semibold text-ink">Surgeries</h1>
        <p className="mt-1 text-sm text-ink-muted">
          What to do before a surgery, and how to look after your cat afterwards.
        </p>
      </div>
      <Tabs
        label="Surgeries"
        idPrefix="my-surgeries"
        selected={scope}
        onSelect={(id) => router.replace(id === 'past' ? `${pathname}?tab=past` : pathname)}
        tabs={[
          { id: 'upcoming', label: 'Upcoming', panel: <Panel scope="upcoming" /> },
          { id: 'past', label: 'Past', panel: <Panel scope="past" /> },
        ]}
      />
    </div>
  );
}

export default function Page() {
  return (
    <Suspense fallback={null}>
      <MySurgeriesPage />
    </Suspense>
  );
}
