'use client';

import Link from 'next/link';
import { ClipboardCheck, FileText, HandCoins } from 'lucide-react';
import { addDaysIso } from '@/lib/format/appointment';
import { formatDateOnly, todayInZone } from '@/lib/format/time';
import { useActiveBranchZone } from '@/lib/hooks/billing/useActiveBranchZone';
import { useApprovals, useInvoices, usePayments, useUncapturedSources } from '@/lib/hooks/billing/useBillingQueries';
import { useCaptureSource } from '@/lib/hooks/billing/useBillingMutations';
import { useStaffSession } from '@/lib/hooks/useSession';
import { Amount } from './Amount';
import { BillingCustomerSearch } from './BillingCustomerSearch';
import { PageHeader, Section } from './PageHeader';
import { PaymentStatusPill } from './StatusPill';
import { UncapturedSourcesCard } from './UncapturedSourcesCard';

/**
 * `components/billing/BillingHome.tsx` — HO-BIL-002 §11.1. The Billing overview: customer search, then
 * cards for *Pending approvals* (approvers only — hidden otherwise; the server still decides),
 * *Drafts*, *Uncaptured charges* (with **Capture**) and *Today's payments* at the active branch.
 */
const APPROVER_ROLES = ['Administrator', 'Accountant'];

export function BillingHome() {
  const { zone } = useActiveBranchZone();
  const today = todayInZone(zone);
  const { data: session } = useStaffSession();
  const isApprover = (session?.roleIds ?? []).some((role) => APPROVER_ROLES.includes(role));

  const approvals = useApprovals(isApprover);
  const drafts = useInvoices({ status: 'Draft', pageSize: 8 });
  const todays = usePayments({ from: today, to: today, pageSize: 8 });
  const uncaptured = useUncapturedSources(addDaysIso(today, -30), today);
  const capture = useCaptureSource();

  return (
    <div className="flex flex-col gap-5">
      <PageHeader title="Billing" description="Charges, invoices, payments and the day’s close." />
      <Section title="Find a customer">
        <BillingCustomerSearch />
      </Section>
      <div className="grid min-w-0 gap-5 lg:grid-cols-2">
        {isApprover && (
          <Section title={`Pending approvals (${approvals.data?.length ?? 0})`} description="Large payments wait for a second person.">
            <p className="text-sm text-ink">
              {approvals.isLoading
                ? 'Loading…'
                : approvals.isError
                  ? 'We couldn’t load the queue.'
                  : approvals.data?.length
                    ? `${approvals.data.length} waiting for approval.`
                    : 'Nothing is waiting.'}
            </p>
            <Link href="/staff/billing/approvals" className="inline-flex w-fit items-center gap-2 text-sm font-semibold text-accent-ink hover:underline">
              <ClipboardCheck size={14} aria-hidden="true" />
              Open the approvals queue
            </Link>
          </Section>
        )}
        <Section title={`Drafts (${drafts.data?.totalCount ?? 0})`} description="Invoices saved but not issued.">
          {drafts.isLoading ? (
            <p role="status" className="text-sm text-ink-muted">Loading…</p>
          ) : drafts.isError ? (
            <p role="alert" className="text-sm text-danger-ink">We couldn’t load the drafts.</p>
          ) : (drafts.data?.items.length ?? 0) === 0 ? (
            <p className="text-sm text-ink-muted">No drafts.</p>
          ) : (
            <ul className="flex flex-col gap-2">
              {drafts.data?.items.map((d) => (
                <li key={d.invoiceId}>
                  <Link href={`/staff/billing/invoices/${d.invoiceId}`} className="flex flex-wrap items-center justify-between gap-2 rounded-[var(--radius-sm)] border border-border p-2 text-sm no-underline hover:bg-surface-sunken">
                    <span className="flex items-center gap-2 font-semibold text-ink">
                      <FileText size={14} aria-hidden="true" />
                      Draft for {d.customerName}
                    </span>
                    <Amount money={d.total} />
                  </Link>
                </li>
              ))}
            </ul>
          )}
        </Section>
        <UncapturedSourcesCard
          sources={uncaptured.data}
          zone={zone}
          isLoading={uncaptured.isLoading}
          isError={uncaptured.isError}
          onRetry={() => void uncaptured.refetch()}
          onCapture={(source) => capture.mutateAsync({ sourceKind: source.sourceKind, sourceId: source.sourceId })}
        />
        <Section title={`Today’s payments (${todays.data?.totalCount ?? 0})`} description={formatDateOnly(today)}>
          {todays.isLoading ? (
            <p role="status" className="text-sm text-ink-muted">Loading…</p>
          ) : todays.isError ? (
            <p role="alert" className="text-sm text-danger-ink">We couldn’t load today’s payments.</p>
          ) : (todays.data?.items.length ?? 0) === 0 ? (
            <p className="text-sm text-ink-muted">No payments recorded today.</p>
          ) : (
            <ul className="flex flex-col gap-2">
              {todays.data?.items.map((p) => (
                <li key={p.paymentId}>
                  <Link href={`/staff/billing/payments/${p.paymentId}`} className="flex flex-wrap items-center justify-between gap-2 rounded-[var(--radius-sm)] border border-border p-2 text-sm no-underline hover:bg-surface-sunken">
                    <span className="flex flex-wrap items-center gap-2 text-ink">
                      <HandCoins size={14} aria-hidden="true" />
                      <span className="font-semibold">{p.receiptNumber ?? 'Awaiting approval'}</span>
                      <span className="text-ink-muted">{p.customerName}</span>
                      <PaymentStatusPill status={p.status} />
                    </span>
                    <Amount money={p.amount} />
                  </Link>
                </li>
              ))}
            </ul>
          )}
          <Link href="/staff/billing/payments" className="w-fit text-sm font-semibold text-accent-ink hover:underline">
            All payments
          </Link>
        </Section>
      </div>
    </div>
  );
}
