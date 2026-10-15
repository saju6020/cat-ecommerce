import { Suspense } from 'react';
import { CashUpScreen } from '@/components/billing/CashUpScreen';

/** /staff/billing/cash-up?date - HO-BIL-002 section 11.6 (US-BIL-12). */
export default function CashUpPage() {
  return (
    <Suspense fallback={null}>
      <CashUpScreen />
    </Suspense>
  );
}
