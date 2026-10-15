'use client';

import { useSearchParams } from 'next/navigation';
import { useStaffCustomer } from '@/lib/hooks/useStaffCustomers';
import { BillingCustomerSearch } from './BillingCustomerSearch';
import { PageHeader } from './PageHeader';
import { RecordPaymentForm } from './RecordPaymentForm';

/**
 * `components/billing/NewPaymentScreen.tsx` — HO-BIL-002 §11.4, `/staff/billing/payments/new?customerId&invoiceId`.
 * Without a customer it asks for one; with one it is `RecordPaymentForm`, pre-pointed at the invoice
 * the account or the invoice page came from.
 */
export function NewPaymentScreen() {
  const search = useSearchParams();
  const customerId = search.get('customerId') ?? '';
  const invoiceId = search.get('invoiceId');
  const customer = useStaffCustomer(customerId);

  if (!customerId) {
    return (
      <div className="flex flex-col gap-5">
        <PageHeader title="Record a payment" description="Choose the customer who is paying." />
        <BillingCustomerSearch hrefFor={(id) => `/staff/billing/payments/new?customerId=${encodeURIComponent(id)}`} />
      </div>
    );
  }
  return (
    <div className="flex flex-col gap-5">
      <PageHeader
        title="Record a payment"
        description={customer.data ? `From ${customer.data.fullName}` : undefined}
        back={{ href: `/staff/billing/customers/${customerId}`, label: 'Customer billing account' }}
      />
      <RecordPaymentForm customerId={customerId} invoiceIds={invoiceId ? [invoiceId] : []} />
    </div>
  );
}
