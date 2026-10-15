'use client';

import Link from 'next/link';
import { usePathname } from 'next/navigation';
import { PageHeading } from '@/components/inventory/fields';
import { cn } from '@/lib/utils';

const TABS = [
  { label: 'Near expiry', href: '/staff/inventory/reports/near-expiry' },
  { label: 'Stock on hand', href: '/staff/inventory/reports/stock-on-hand' },
  { label: 'Reconciliation', href: '/staff/inventory/reports/reconciliation' },
];

/** `app/(staff)/staff/inventory/reports/layout.tsx` — the three inventory reports (US-IMP-10, §8.13).
 *  They carry costs, so the server needs `InventoryReports.View`; its `403` is shown by each page. */
export default function ReportsLayout({ children }: { children: React.ReactNode }) {
  const pathname = usePathname();
  return (
    <div className="flex flex-col gap-4">
      <PageHeading title="Inventory reports" description="Operational reports for this branch. Valuation and margin dashboards live in Reporting." />
      <nav aria-label="Reports" className="flex flex-wrap gap-4 border-b border-border">
        {TABS.map((tab) => {
          const active = pathname === tab.href;
          return (
            <Link
              key={tab.href}
              href={tab.href}
              aria-current={active ? 'page' : undefined}
              className={cn(
                '-mb-px border-b-2 px-1 py-2 text-sm font-semibold no-underline',
                active ? 'border-accent text-ink' : 'border-transparent text-ink-muted hover:text-ink',
              )}
            >
              {tab.label}
            </Link>
          );
        })}
      </nav>
      <div className="min-w-0">{children}</div>
    </div>
  );
}
