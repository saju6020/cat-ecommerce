import { Suspense } from 'react';
import { NewInvoiceScreen } from '@/components/billing/NewInvoiceScreen';

/** `/staff/billing/invoices/new?customerId` — HO-BIL-002 §11.2. */
export default function NewInvoicePage() {
  return (
    <Suspense fallback={null}>
      <NewInvoiceScreen />
    </Suspense>
  );
}
