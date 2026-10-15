import { Suspense } from 'react';
import { NewPaymentScreen } from '@/components/billing/NewPaymentScreen';

/** `/staff/billing/payments/new?customerId&invoiceId` — HO-BIL-002 §11.4. */
export default function NewPaymentPage() {
  return (
    <Suspense fallback={null}>
      <NewPaymentScreen />
    </Suspense>
  );
}
