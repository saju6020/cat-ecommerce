import { Suspense } from 'react';
import { PaymentListScreen } from '@/components/billing/PaymentListScreen';

/** `/staff/billing/payments` — HO-BIL-002 §3.2 (US-BIL-11). Filters live in the URL. */
export default function PaymentsPage() {
  return (
    <Suspense fallback={null}>
      <PaymentListScreen />
    </Suspense>
  );
}
