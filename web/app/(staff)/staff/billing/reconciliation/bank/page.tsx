import { Suspense } from 'react';
import { BankStatementsScreen } from '@/components/billing/BankStatementsScreen';

/** /staff/billing/reconciliation/bank - HO-BIL-002 section 11.8 (US-BIL-06 AC2): import and list. */
export default function BankPage() {
  return (
    <Suspense fallback={null}>
      <BankStatementsScreen />
    </Suspense>
  );
}
