'use client';

import Link from 'next/link';
import { AccessRefusedState } from '@/components/admin/AccessRefusedState';
import { AmountCell } from '@/components/billing/Amount';
import { PageHeader } from '@/components/billing/PageHeader';
import { QueryBoundary } from '@/components/billing/QueryBoundary';
import { type Column, ResponsiveTable } from '@/components/billing/ResponsiveTable';
import { Badge } from '@/components/ui/badge';
import { Button, buttonVariants } from '@/components/ui/button';
import { ApiForbiddenError } from '@/lib/api/client';
import type { DiscountCode } from '@/lib/api/ordering-admin';
import { formatOrderWhen } from '@/lib/format/orders';
import { useBranchTimeZone } from '@/lib/hooks/useBranchTimeZone';
import { useDiscountCodes, useSetDiscountActivation } from '@/lib/hooks/useOrderingAdmin';

/**
 * `components/ordering-admin/DiscountCodesPage.tsx` — HO-ORD-002 §11.12, US-ORD-05. Codes with their kind
 * and value (the percentage or the amount, as the server sent it), minimum spend, validity window, uses
 * against the limit, and an activate / deactivate switch. Status is text. Rows link to the edit form.
 */
export function DiscountCodesPage() {
  const query = useDiscountCodes();
  const toggle = useSetDiscountActivation();
  const zone = useBranchTimeZone();
  if (query.error instanceof ApiForbiddenError) return <AccessRefusedState error={query.error} />;

  const columns: Array<Column<DiscountCode>> = [
    { id: 'code', header: 'Code', primary: true, sortValue: (c) => c.code, cell: (c) => c.code },
    { id: 'kind', header: 'Discount', cell: (c) => (c.kind === 'Percentage' ? `${c.percent ?? ''}% off` : c.amount ? <AmountCell money={c.amount} /> : '') },
    { id: 'min', header: 'Minimum spend', numeric: true, cell: (c) => <AmountCell money={c.minimumSpend} /> },
    { id: 'window', header: 'Valid', cell: (c) => (c.validFrom || c.validTo ? `${c.validFrom ? formatOrderWhen(c.validFrom, zone) : 'Any time'} to ${c.validTo ? formatOrderWhen(c.validTo, zone) : 'no end'}` : 'Always') },
    { id: 'uses', header: 'Used', cell: (c) => `${c.timesRedeemed}${c.usageLimit !== null ? ` of ${c.usageLimit}` : ''}` },
    { id: 'status', header: 'Status', cell: (c) => <Badge variant={c.isActive ? 'success' : 'neutral'}>{c.isActive ? 'Active' : 'Inactive'}</Badge> },
    {
      id: 'actions',
      header: 'Actions',
      cell: (c) => (
        <span className="flex flex-wrap gap-2">
          <Link href={`/staff/admin/discount-codes/${c.id}`} className={buttonVariants({ variant: 'secondary', size: 'sm' })}>Edit<span className="sr-only"> {c.code}</span></Link>
          <Button variant="ghost" size="sm" disabled={toggle.isPending} onClick={() => toggle.mutate({ codeId: c.id, isActive: !c.isActive })}>
            {c.isActive ? 'Deactivate' : 'Activate'}<span className="sr-only"> {c.code}</span>
          </Button>
        </span>
      ),
    },
  ];

  return (
    <div className="flex flex-col gap-5">
      <PageHeader title="Discount codes" description="Codes customers type at checkout." actions={<Link href="/staff/admin/discount-codes/new" className={buttonVariants({})}>New code</Link>} />
      {toggle.isError && <p role="alert" className="text-sm font-semibold text-danger-ink">We couldn’t change that code. Try again.</p>}
      <QueryBoundary query={query} loadingLabel="Loading codes…" loadingVariant="table">
        {(codes) => (
          <ResponsiveTable caption="Discount codes" columns={columns} rows={codes} getRowId={(c) => c.id} emptyState={{ title: 'No discount codes yet.', description: 'Create one to offer a promotion.' }} />
        )}
      </QueryBoundary>
    </div>
  );
}
