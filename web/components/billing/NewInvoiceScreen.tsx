'use client';

import { useSearchParams } from 'next/navigation';
import { useStaffCustomer } from '@/lib/hooks/useStaffCustomers';
import { BillingCustomerSearch } from './BillingCustomerSearch';
import { InvoiceComposer } from './InvoiceComposer';
import { PageHeader } from './PageHeader';

/**
 * `components/billing/NewInvoiceScreen.tsx` — HO-BIL-002 §11.2, `/staff/billing/invoices/new?customerId`.
 * Without a customer it asks for one; with one it is the composer on a not-yet-saved invoice, with any
 * `?charges=` the account screen ticked already selected.
 */
export function NewInvoiceScreen() {
  const search = useSearchParams();
  const customerId = search.get('customerId') ?? '';
  const preselected = (search.get('charges') ?? '').split(',').filter(Boolean);
  const customer = useStaffCustomer(customerId);

  if (!customerId) {
    return (
      <div className="flex flex-col gap-5">
        <PageHeader title="New invoice" description="Choose the customer to invoice." />
        <BillingCustomerSearch
          hrefFor={(id) => `/staff/billing/invoices/new?customerId=${encodeURIComponent(id)}`}
        />
      </div>
    );
  }
  return (
    <div className="flex flex-col gap-5">
      <PageHeader
        title="New invoice"
        description={customer.data ? `For ${customer.data.fullName}` : undefined}
        back={{ href: `/staff/billing/customers/${customerId}`, label: 'Customer billing account' }}
      />
      <InvoiceComposer customerId={customerId} initialSelected={preselected} />
    </div>
  );
}
