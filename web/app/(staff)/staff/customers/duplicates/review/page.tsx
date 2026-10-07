'use client';

import { Suspense } from 'react';
import { useSearchParams } from 'next/navigation';
import { MergeReview } from '@/components/customer/MergeReview';
import { ErrorState } from '@/components/common/ErrorState';
import { useStaffCustomersByIds } from '@/lib/hooks/useStaffCustomers';
import { useMergeCustomer } from '@/lib/hooks/useStaffCustomerMutations';
import type { StaffCustomer } from '@/lib/api/staff-customers';

// US-ACC-11.
export default function MergeReviewPage() {
  return (
    <div className="flex flex-col gap-4">
      <h1 className="font-display text-xl font-semibold text-ink">Review merge</h1>
      <Suspense fallback={null}>
        <MergeReviewPageContent />
      </Suspense>
    </div>
  );
}

// ARCH gate HIGH 3 (first pass) — this page wires useStaffCustomersByIds(ids)/useMergeCustomer();
// MergeReview itself takes the results as props. Both hooks are called unconditionally, ahead of
// the ids.length < 2 early return below, per the rules of hooks — `useStaffCustomersByIds([])`
// is a no-op (an empty array of queries).
function MergeReviewPageContent() {
  const searchParams = useSearchParams();
  const ids = (searchParams.get('ids') ?? '').split(',').filter(Boolean);
  const queries = useStaffCustomersByIds(ids);
  const mergeMutation = useMergeCustomer();

  if (ids.length < 2) {
    return (
      <ErrorState
        title="No records selected"
        description="Go back to duplicates and choose a group to review."
      />
    );
  }

  const isLoading = queries.some((q) => q.isLoading);
  const isError = queries.some((q) => q.isError);
  const customers = queries.map((q) => q.data).filter((c): c is StaffCustomer => !!c);

  return (
    <MergeReview
      customerIds={ids}
      isLoading={isLoading}
      isError={isError}
      customers={customers}
      onMerge={(duplicateCustomerId, request) =>
        mergeMutation.mutateAsync({ duplicateCustomerId, request })
      }
      isMergePending={mergeMutation.isPending}
    />
  );
}
