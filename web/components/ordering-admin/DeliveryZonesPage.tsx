'use client';

import Link from 'next/link';
import { AccessRefusedState } from '@/components/admin/AccessRefusedState';
import { PageHeader } from '@/components/billing/PageHeader';
import { QueryBoundary } from '@/components/billing/QueryBoundary';
import { type Column, ResponsiveTable } from '@/components/billing/ResponsiveTable';
import { Badge } from '@/components/ui/badge';
import { Button, buttonVariants } from '@/components/ui/button';
import { ApiForbiddenError } from '@/lib/api/client';
import type { DeliveryZoneSummary } from '@/lib/api/ordering-admin';
import { serviceLevelLabel } from '@/lib/format/orders';
import { useAdminBranches } from '@/lib/hooks/useAdminBranches';
import { useDeliveryZones, useSetZoneActivation } from '@/lib/hooks/useOrderingAdmin';

/**
 * `components/ordering-admin/DeliveryZonesPage.tsx` — HO-ORD-002 §11.12, US-ORD-06. Zones with their
 * branch, priority, how many districts they cover, the service levels they offer, whether cash on
 * delivery is on, and active / inactive (text). Rows open the form; the switch changes activation.
 */
export function DeliveryZonesPage() {
  const query = useDeliveryZones();
  const branches = useAdminBranches();
  const toggle = useSetZoneActivation();
  if (query.error instanceof ApiForbiddenError) return <AccessRefusedState error={query.error} />;
  const branchName = (id: string) => branches.data?.find((b) => b.id === id)?.name ?? 'Branch';

  const columns: Array<Column<DeliveryZoneSummary>> = [
    { id: 'name', header: 'Zone', primary: true, sortValue: (z) => z.name, cell: (z) => z.name },
    { id: 'branch', header: 'Branch', cell: (z) => branchName(z.branchId) },
    { id: 'priority', header: 'Priority', numeric: true, cell: (z) => z.priority },
    { id: 'coverage', header: 'Districts', numeric: true, cell: (z) => z.coverageCount },
    { id: 'levels', header: 'Services', cell: (z) => z.serviceLevels.map(serviceLevelLabel).join(', ') || 'None active' },
    { id: 'cod', header: 'Cash on delivery', cell: (z) => (z.codEnabled ? 'On' : 'Off') },
    { id: 'status', header: 'Status', cell: (z) => <Badge variant={z.isActive ? 'success' : 'neutral'}>{z.isActive ? 'Active' : 'Inactive'}</Badge> },
    {
      id: 'actions',
      header: 'Actions',
      cell: (z) => (
        <span className="flex flex-wrap gap-2">
          <Link href={`/staff/admin/delivery-zones/${z.id}`} className={buttonVariants({ variant: 'secondary', size: 'sm' })}>Edit<span className="sr-only"> {z.name}</span></Link>
          <Button variant="ghost" size="sm" disabled={toggle.isPending} onClick={() => toggle.mutate({ zoneId: z.id, isActive: !z.isActive })}>
            {z.isActive ? 'Deactivate' : 'Activate'}<span className="sr-only"> {z.name}</span>
          </Button>
        </span>
      ),
    },
  ];

  return (
    <div className="flex flex-col gap-5">
      <PageHeader title="Delivery zones" description="Where each branch delivers, what it charges and when." actions={<Link href="/staff/admin/delivery-zones/new" className={buttonVariants({})}>New zone</Link>} />
      {toggle.isError && <p role="alert" className="text-sm font-semibold text-danger-ink">We couldn’t change that zone. Try again.</p>}
      <QueryBoundary query={query} loadingLabel="Loading zones…" loadingVariant="table">
        {(zones) => (
          <ResponsiveTable caption="Delivery zones" columns={columns} rows={zones} getRowId={(z) => z.id} emptyState={{ title: 'No delivery zones yet.', description: 'Without a zone, customers can only collect from a branch.' }} />
        )}
      </QueryBoundary>
    </div>
  );
}
