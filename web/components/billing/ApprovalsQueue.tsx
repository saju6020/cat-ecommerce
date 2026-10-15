'use client';

import { useState } from 'react';
import Link from 'next/link';
import { Button } from '@/components/ui/button';
import { useToast } from '@/components/ui/use-toast';
import type { ApproveResult, PendingApproval } from '@/lib/api/billing-staff';
import { describeBillingError } from '@/lib/api/errors';
import { formatDateOnly } from '@/lib/format/time';
import { useApprovals } from '@/lib/hooks/billing/useBillingQueries';
import { useApprovePayment, useRejectPayment } from '@/lib/hooks/billing/useBillingMutations';
import { AccessRefusedState } from '@/components/admin/AccessRefusedState';
import { ApiForbiddenError } from '@/lib/api/client';
import { a11yMoney } from '@/lib/money';
import { Amount } from './Amount';
import { methodLabel } from './labels';
import { PageHeader, Section } from './PageHeader';
import { ReasonDialog } from './ReasonDialog';
import { ResponsiveTable, type Column } from './ResponsiveTable';

/**
 * `components/billing/ApprovalsQueue.tsx` — HO-BIL-002 §11.4 (US-BIL-11 AC2), ⭐. Payments waiting for a
 * second person at the active branch: recorder, method, amount, reference, requested allocations.
 * **Approve** is disabled with the words *"You recorded this payment"* when `canApprove` is false (the
 * server still refuses a forced request with `ApproverMustDiffer`); **Reject** needs a reason. What an
 * approval did — applied, left unallocated — is shown after it, as the server reported it.
 */
export function ApprovalsQueue() {
  const approvals = useApprovals();
  const approve = useApprovePayment();
  const reject = useRejectPayment();
  const { toast } = useToast();
  const [outcomes, setOutcomes] = useState<Array<{ id: string; label: string; result: ApproveResult }>>([]);
  const [errors, setErrors] = useState<Record<string, string>>({});
  const [rejecting, setRejecting] = useState<PendingApproval | null>(null);
  const [busy, setBusy] = useState<string | null>(null);

  async function approveRow(row: PendingApproval) {
    setBusy(row.paymentId);
    setErrors((current) => ({ ...current, [row.paymentId]: '' }));
    try {
      const result = await approve.mutateAsync(row.paymentId);
      setOutcomes((current) => [...current, { id: row.paymentId, label: `${row.customerName} — ${a11yMoney(row.amount)}`, result }]);
    } catch (failure) {
      setErrors((current) => ({ ...current, [row.paymentId]: describeBillingError(failure) }));
    } finally {
      setBusy(null);
    }
  }

  const columns: Array<Column<PendingApproval>> = [
    { id: 'customer', header: 'Customer', primary: true, cell: (r) => <Link href={`/staff/billing/payments/${r.paymentId}`} className="font-semibold text-ink hover:underline">{r.customerName}</Link> },
    { id: 'recorder', header: 'Recorded by', cell: (r) => `${r.recordedByName} · ${formatDateOnly(r.receivedOn)}` },
    { id: 'method', header: 'Method', cell: (r) => methodLabel(r.method) },
    { id: 'amount', header: 'Amount', numeric: true, cell: (r) => <Amount money={r.amount} /> },
    { id: 'reference', header: 'Reference', cell: (r) => r.externalReference ?? '—' },
    {
      id: 'allocations',
      header: 'Requested allocations',
      cell: (r) =>
        r.requestedAllocations.length === 0 ? (
          'None — kept as credit'
        ) : (
          <ul>
            {r.requestedAllocations.map((a) => (
              <li key={a.invoiceId}>
                {a.invoiceNumber} — <Amount money={a.amount} />
              </li>
            ))}
          </ul>
        ),
    },
    {
      id: 'actions',
      header: 'Actions',
      cell: (r) => (
        <div className="flex min-w-0 flex-col gap-1">
          <div className="flex flex-wrap gap-2">
            <Button size="sm" disabled={!r.canApprove || busy !== null} aria-busy={busy === r.paymentId} aria-describedby={r.canApprove ? undefined : `own-${r.paymentId}`} onClick={() => void approveRow(r)}>
              {busy === r.paymentId ? 'Approving…' : 'Approve'}<span className="sr-only"> {r.customerName}</span>
            </Button>
            <Button variant="secondary" size="sm" disabled={busy !== null} onClick={() => setRejecting(r)}>
              Reject<span className="sr-only"> {r.customerName}</span>
            </Button>
          </div>
          {!r.canApprove && (
            <span id={`own-${r.paymentId}`} className="text-xs font-semibold text-ink-muted">
              You recorded this payment
            </span>
          )}
          {errors[r.paymentId] && (
            <span role="alert" className="text-xs font-semibold text-danger-ink">{errors[r.paymentId]}</span>
          )}
        </div>
      ),
    },
  ];

  if (approvals.error instanceof ApiForbiddenError) return <AccessRefusedState error={approvals.error} />;

  return (
    <div className="flex flex-col gap-5">
      <PageHeader title="Approvals" description="Large payments count only once a second person approves them." />
      {outcomes.length > 0 && (
        <Section title="Just approved">
          <ul role="status" className="flex flex-col gap-2 text-sm">
            {outcomes.map((o) => (
              <li key={o.id}>
                <strong>{o.label}</strong> approved.{' '}
                {o.result.applied.length > 0
                  ? o.result.applied.map((a) => <span key={a.invoiceId}>Applied <Amount money={a.amount} /> to {a.number}. </span>)
                  : 'Nothing was applied to an invoice. '}
                Left unallocated: <Amount money={o.result.leftUnallocated} />.
              </li>
            ))}
          </ul>
        </Section>
      )}
      <ResponsiveTable
        caption="Payments waiting for approval"
        columns={columns}
        rows={approvals.data}
        getRowId={(r) => r.paymentId}
        isLoading={approvals.isLoading}
        isError={approvals.isError}
        onRetry={() => void approvals.refetch()}
        emptyState={{ title: 'Nothing is waiting for approval.', description: 'Payments above the approval limit appear here.' }}
      />
      <ReasonDialog
        open={rejecting !== null}
        onOpenChange={(open) => !open && setRejecting(null)}
        title="Reject this payment"
        consequence={`Reject the ${rejecting ? a11yMoney(rejecting.amount) : ''} payment from ${rejecting?.customerName ?? ''}? It will not count towards any invoice or the day’s cash-up. It stays on record, marked rejected, with your reason.`}
        confirmLabel="Reject payment"
        pendingLabel="Rejecting…"
        onSubmit={async (reason) => {
          if (!rejecting) return;
          await reject.mutateAsync({ paymentId: rejecting.paymentId, reason });
          toast({ title: 'Payment rejected' });
        }}
      />
    </div>
  );
}
