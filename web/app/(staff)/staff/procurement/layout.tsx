'use client';

import { SectionNav } from '@/components/inventory/SectionNav';
import { procurementNavItems } from '@/components/inventory/inventoryNavItems';
import { useStaffSession } from '@/lib/hooks/useSession';

/** `app/(staff)/staff/procurement/layout.tsx` — HO-IMP-002 §8.1, ADR-FE-039. The buying section's
 *  own tab row (Suppliers · Purchase orders · Consignments · Reorder). */
export default function ProcurementLayout({ children }: { children: React.ReactNode }) {
  const { data: session } = useStaffSession();
  return (
    <div className="flex flex-col gap-6">
      <SectionNav label="Procurement" items={procurementNavItems} roleIds={session?.roleIds ?? []} />
      <div className="min-w-0">{children}</div>
    </div>
  );
}
