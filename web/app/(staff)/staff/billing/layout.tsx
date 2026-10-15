'use client';

import { BillingNav } from '@/components/billing/BillingNav';
import { useStaffSession } from '@/lib/hooks/useSession';

/**
 * `app/(staff)/staff/billing/layout.tsx` — HO-BIL-002 §8.1, ADR-FE-079. A route-based section with
 * the customer billing account as its hub; every screen is a deep-linkable route.
 */
export default function BillingLayout({ children }: { children: React.ReactNode }) {
  const { data: session } = useStaffSession();
  return (
    <div className="flex flex-col gap-6">
      <BillingNav roleIds={session?.roleIds ?? []} />
      <div className="min-w-0">{children}</div>
    </div>
  );
}
