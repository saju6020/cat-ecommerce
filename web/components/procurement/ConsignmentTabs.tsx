'use client';

import Link from 'next/link';
import { usePathname } from 'next/navigation';
import { cn } from '@/lib/utils';

/** `components/procurement/ConsignmentTabs.tsx` — the consignment's own tab row: Details ·
 *  Documents · Costs & rates · Batches, plus the printable inspection pack. Route-based, so every
 *  tab is a deep link (HO-IMP-002 §8.1). */
export function ConsignmentTabs({ consignmentId }: { consignmentId: string }) {
  const pathname = usePathname();
  const base = `/staff/procurement/consignments/${consignmentId}`;
  const tabs = [
    { label: 'Details', href: base },
    { label: 'Documents', href: `${base}/documents` },
    { label: 'Costs & rates', href: `${base}/costs` },
    { label: 'Batches', href: `${base}/batches` },
    { label: 'Inspection pack', href: `${base}/inspection-pack` },
  ];
  return (
    <nav aria-label="Consignment sections" className="flex flex-wrap gap-4 border-b border-border">
      {tabs.map((tab) => {
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
  );
}
