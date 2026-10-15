import { Suspense } from 'react';
import { InvoiceListScreen } from '@/components/billing/InvoiceListScreen';

/** `/staff/billing/invoices` — HO-BIL-002 §3.2 (US-BIL-01). Filters live in the URL. */
export default function InvoicesPage() {
  return (
    <Suspense fallback={null}>
      <InvoiceListScreen />
    </Suspense>
  );
}
