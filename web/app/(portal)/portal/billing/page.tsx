import { Suspense } from 'react';
import { MyBillingPage } from '@/components/billing/MyBilling';

/** /portal/billing - HO-BIL-002 section 11.11: balance, invoices, payments, credit, loyalty. */
export default function PortalBillingPage() {
  return (
    <Suspense fallback={null}>
      <MyBillingPage />
    </Suspense>
  );
}
