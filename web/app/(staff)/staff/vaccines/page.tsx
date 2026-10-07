'use client';

import { Suspense } from 'react';
import { useRouter, useSearchParams } from 'next/navigation';
import { Button } from '@/components/ui/button';
import { Badge } from '@/components/ui/badge';
import { DataTable, type DataTableColumn } from '@/components/common/DataTable';
import { useVaccines } from '@/lib/hooks/useVaccines';
import type { VaccineListItem } from '@/lib/api/vaccines';

const PAGE_SIZE = 20;

/**
 * Vaccine catalogue — `GET /api/vaccines` (Q6). US-CAT-05's dependency (HO-CAT-002 §3.2, §8.3).
 *
 * Pagination is URL-driven (UI-4, §8.4) the same way `CatListPage` does it
 * (`app/(staff)/staff/cats/page.tsx`) — `page` is read from `useSearchParams()` and passed through
 * as `pageNumber` so `DataTable`'s "Next"/"Previous" (which write `?page=`) actually change what's
 * fetched (CODE_REVIEW gate MEDIUM finding on HO-CAT-002).
 */
function VaccineListPage() {
  const router = useRouter();
  const searchParams = useSearchParams();
  const page = Math.max(1, Number(searchParams.get('page')) || 1);
  const { data, isLoading, isError, refetch } = useVaccines({
    pageNumber: page,
    pageSize: PAGE_SIZE,
  });

  const columns: Array<DataTableColumn<VaccineListItem>> = [
    { id: 'name', header: 'Name', cell: (vaccine) => vaccine.name },
    { id: 'code', header: 'Code', cell: (vaccine) => vaccine.code },
    {
      id: 'isCore',
      header: 'Core',
      cell: (vaccine) => (vaccine.isCore ? <Badge variant="success">Core</Badge> : '—'),
    },
    {
      id: 'validityMonths',
      header: 'Validity',
      cell: (vaccine) => `${vaccine.validityMonths} months`,
    },
    {
      id: 'isActive',
      header: 'Status',
      cell: (vaccine) =>
        vaccine.isActive ? (
          <Badge variant="success">Active</Badge>
        ) : (
          <Badge variant="neutral">Inactive</Badge>
        ),
    },
  ];

  return (
    <div className="flex flex-col gap-6">
      <div className="flex flex-wrap items-center justify-between gap-3">
        <h1 className="font-display text-2xl font-semibold text-ink">Vaccine catalogue</h1>
        <Button onClick={() => router.push('/staff/vaccines/new')}>Register a vaccine</Button>
      </div>

      <DataTable
        caption="Vaccines"
        columns={columns}
        rows={data?.items}
        getRowId={(vaccine) => vaccine.id}
        isLoading={isLoading}
        isError={isError}
        onRetry={() => refetch()}
        pagination={{ pageSize: PAGE_SIZE, totalCount: data?.totalCount ?? 0 }}
        emptyState={{
          title: 'No vaccines in the catalogue yet.',
          description: 'Register a vaccine to start recording vaccinations against it.',
          action: (
            <Button onClick={() => router.push('/staff/vaccines/new')}>Register a vaccine</Button>
          ),
        }}
      />
    </div>
  );
}

export default function VaccineListRoute() {
  return (
    <Suspense>
      <VaccineListPage />
    </Suspense>
  );
}
