'use client';

import { use } from 'react';
import Link from 'next/link';
import { buttonVariants } from '@/components/ui/button';
import { DiagnosticOrderView } from '@/components/visit/panels/DiagnosticOrderView';
import { VisitCatHeader } from '@/components/visit/VisitCatHeader';
import { useBranchTimeZone } from '@/lib/hooks/useBranchTimeZone';
import { useDiagnosticOrder } from '@/lib/hooks/useDiagnostics';
import { useStaffSession } from '@/lib/hooks/useSession';

/**
 * `/staff/diagnostics/[orderId]` — HO-EMR-002 §8.1/§11.3 (US-CLN-14): the same content as the drawer over
 * the workspace, as a page — the compact cat header (UI-7), a link back to the visit, then the order.
 * Result entry and attaching a file are offered to a Vet.
 */
export default function DiagnosticOrderPage({ params }: { params: Promise<{ orderId: string }> }) {
  const { orderId } = use(params);
  const order = useDiagnosticOrder(orderId);
  const zone = useBranchTimeZone();
  const roles = useStaffSession().data?.roleIds ?? [];
  return (
    <div className="flex flex-col gap-5">
      <h1 className="font-display text-2xl font-semibold text-ink">Diagnostic order</h1>
      {order.data && (
        <div className="flex flex-col gap-3">
          <Link
            href={`/staff/visits/${order.data.visitId}`}
            className={buttonVariants({ variant: 'ghost', size: 'sm', className: 'w-fit' })}
          >
            Back to the visit
          </Link>
          <VisitCatHeader catId={order.data.catId} />
        </div>
      )}
      <DiagnosticOrderView orderId={orderId} canWrite={roles.includes('Vet')} zone={zone} />
    </div>
  );
}
