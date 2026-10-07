'use client';

import { Suspense } from 'react';
import { useRouter, usePathname, useSearchParams } from 'next/navigation';
import {
  DuplicateGroupList,
  DUPLICATE_GROUPS_PAGE_SIZE,
} from '@/components/customer/DuplicateGroupList';
import { useDuplicateCustomerGroups } from '@/lib/hooks/useStaffCustomers';

// US-ACC-11.
// ARCH gate HIGH 3 (first pass) — this page wires useDuplicateCustomerGroups() and owns the
// `?page=` URL param DuplicateGroupList used to read/write itself; the `<Suspense>` boundary
// moves down to wrap the content that actually calls useSearchParams() now.
export default function DuplicatesPage() {
  return (
    <div className="flex flex-col gap-4">
      <h1 className="font-display text-xl font-semibold text-ink">Duplicate customers</h1>
      <Suspense fallback={null}>
        <DuplicatesPageContent />
      </Suspense>
    </div>
  );
}

function DuplicatesPageContent() {
  const router = useRouter();
  const pathname = usePathname();
  const searchParams = useSearchParams();
  const pageNumber = Number(searchParams.get('page') ?? '1');
  const { data, isLoading, isError, error, refetch } = useDuplicateCustomerGroups({
    pageNumber,
    pageSize: DUPLICATE_GROUPS_PAGE_SIZE,
  });

  return (
    <DuplicateGroupList
      data={data}
      isLoading={isLoading}
      isError={isError}
      error={error}
      onRetry={() => refetch()}
      pageNumber={pageNumber}
      onPageChange={(nextPage) => router.replace(`${pathname}?page=${nextPage}`)}
    />
  );
}
