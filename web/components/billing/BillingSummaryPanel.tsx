'use client';

import Link from 'next/link';
import { buttonVariants } from '@/components/ui/button';
import { ApiForbiddenError } from '@/lib/api/client';
import { useCustomerBillingSummary } from '@/lib/hooks/billing/useBillingQueries';
import { Amount } from './Amount';
import { Section, Stat } from './PageHeader';

/**
 * `components/billing/BillingSummaryPanel.tsx` — HO-BIL-002 §11.1, added to HO-ACC-002's staff customer
 * page. The customer's money at a glance — outstanding, overdue, free credit, unbilled, loyalty — and a
 * link to their billing account. A role without Billing access (`403`) simply does not see the panel.
 */
export function BillingSummaryPanel({ customerId }: { customerId: string }) {
  const summary = useCustomerBillingSummary(customerId);
  if (summary.error instanceof ApiForbiddenError) return null;

  return (
    <Section
      title="Billing"
      actions={
        <Link href={`/staff/billing/customers/${customerId}`} className={buttonVariants({ variant: 'secondary', size: 'sm' })}>
          Open billing account
        </Link>
      }
    >
      {summary.isLoading ? (
        <p role="status" className="text-sm text-ink-muted">Loading billing…</p>
      ) : summary.isError || !summary.data ? (
        <p role="alert" className="text-sm text-ink">
          We couldn’t load the billing summary.{' '}
          <button type="button" className="font-semibold underline" onClick={() => void summary.refetch()}>
            Try again
          </button>
        </p>
      ) : (
        <dl className="grid gap-3 sm:grid-cols-2 lg:grid-cols-4">
          <Stat label="Outstanding"><Amount money={summary.data.outstanding} /></Stat>
          <Stat label="Overdue"><Amount money={summary.data.overdue} /></Stat>
          <Stat label="Free credit"><Amount money={summary.data.freeCredit} /></Stat>
          <Stat label="Unbilled charges"><Amount money={summary.data.unbilledTotal} /></Stat>
          {summary.data.loyaltyEnabled && (
            <Stat label="Loyalty points"><span className="tabular-nums">{summary.data.loyaltyPoints ?? 0}</span></Stat>
          )}
        </dl>
      )}
    </Section>
  );
}
