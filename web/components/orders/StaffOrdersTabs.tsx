'use client';

import Link from 'next/link';
import { usePathname } from 'next/navigation';
import { cn } from '@/lib/utils';

/**
 * `components/orders/StaffOrdersTabs.tsx` — HO-ORD-002 §12.2. The awaiting-payment list, reconciliation
 * issues, pick list and refunds due are tabs of the staff Orders screens rather than sidebar entries
 * (the sidebar has *Orders*, *Returns* and *POS*). Plain links with `aria-current`, so they work
 * without scripting and are one tab stop each.
 */
const TABS = [
  { href: '/staff/orders', label: 'Queue' },
  { href: '/staff/orders/awaiting-payment', label: 'Awaiting payment' },
  { href: '/staff/fulfilment/pick-list', label: 'Pick list' },
  { href: '/staff/refunds', label: 'Refunds due' },
  { href: '/staff/orders/reconciliation', label: 'Reconciliation' },
] as const;

export function StaffOrdersTabs() {
  const pathname = usePathname();
  return (
    <nav aria-label="Order screens" className="overflow-x-auto">
      <ul className="m-0 flex list-none gap-1 border-b border-border p-0">
        {TABS.map((tab) => {
          const current = pathname === tab.href;
          return (
            <li key={tab.href}>
              <Link
                href={tab.href}
                aria-current={current ? 'page' : undefined}
                className={cn(
                  'inline-block whitespace-nowrap border-b-2 px-3 py-2 text-sm font-semibold no-underline',
                  current ? 'border-accent text-accent-ink' : 'border-transparent text-ink-muted hover:text-ink',
                )}
              >
                {tab.label}
              </Link>
            </li>
          );
        })}
      </ul>
    </nav>
  );
}
