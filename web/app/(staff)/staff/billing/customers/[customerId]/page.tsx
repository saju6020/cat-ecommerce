'use client';

import { Suspense, use } from 'react';
import { CustomerBillingAccount } from '@/components/billing/CustomerBillingAccount';

/** `/staff/billing/customers/[customerId]` — HO-BIL-002 §11.1 (US-BIL-03, US-BIL-08). */
export default function CustomerBillingPage({
  params,
}: {
  params: Promise<{ customerId: string }>;
}) {
  const { customerId } = use(params);
  return (
    <Suspense fallback={null}>
      <CustomerBillingAccount customerId={customerId} />
    </Suspense>
  );
}
