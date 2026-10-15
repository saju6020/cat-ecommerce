'use client';

import { SectionNav } from '@/components/inventory/SectionNav';
import { inventoryNavItems, STOCK_VIEW_ROLES } from '@/components/inventory/inventoryNavItems';
import { useStaffSession } from '@/lib/hooks/useSession';
import { useAlertSummary } from '@/lib/hooks/useInventory';

/**
 * `app/(staff)/staff/inventory/layout.tsx` — HO-IMP-002 §8.1, ADR-FE-039/045. The Inventory
 * section: its own tab row, carrying the alert badge as TEXT plus a number (*"Alerts 7 open"*).
 * The badge is the one polled query in the slice (every 60 s) — a poll updates the count only and
 * never touches an open form or dialog below.
 */
export default function InventoryLayout({ children }: { children: React.ReactNode }) {
  const { data: session } = useStaffSession();
  const roleIds = session?.roleIds ?? [];
  const canSeeAlerts = STOCK_VIEW_ROLES.some((role) => roleIds.includes(role));
  const summary = useAlertSummary(canSeeAlerts);
  const open = summary.data?.open.total ?? 0;

  const items = inventoryNavItems.map((item) =>
    item.label === 'Alerts' ? { ...item, badge: open > 0 ? `${open} open` : null } : item,
  );

  return (
    <div className="flex flex-col gap-6">
      <SectionNav label="Inventory" items={items} roleIds={roleIds} />
      <div className="min-w-0">{children}</div>
    </div>
  );
}
