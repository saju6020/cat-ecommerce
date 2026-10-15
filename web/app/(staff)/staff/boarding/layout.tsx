'use client';

import Link from 'next/link';
import { usePathname } from 'next/navigation';
import {
  STAFF_BOARDING,
  STAFF_BOARDING_BOARD,
  STAFF_BOARDING_DAYCARE,
  STAFF_BOARDING_INCIDENTS,
  STAFF_BOARDING_STAYS,
  STAFF_BOARDING_TASKS,
} from '@/lib/routing/boarding-links';
import { cn } from '@/lib/utils';

const TABS = [
  { href: STAFF_BOARDING, label: 'Dashboard', exact: true },
  { href: STAFF_BOARDING_STAYS, label: 'Stays' },
  { href: STAFF_BOARDING_BOARD, label: 'Occupancy' },
  { href: STAFF_BOARDING_TASKS, label: 'Tasks' },
  { href: STAFF_BOARDING_INCIDENTS, label: 'Incidents' },
  { href: STAFF_BOARDING_DAYCARE, label: 'Daycare' },
];

/** The Boarding section's own tab row (HO-BRD-002 §13): the shared sidebar has two entries, this row
 *  reaches the rest. Pages answer their own `403` (ADR-FE-107) — nothing is hidden by role here. */
export default function StaffBoardingLayout({ children }: { children: React.ReactNode }) {
  const pathname = usePathname();
  return (
    <div className="flex flex-col gap-5">
      <nav aria-label="Boarding" className="flex flex-wrap gap-1 border-b border-border">
        {TABS.map((t) => {
          const on = t.exact ? pathname === t.href : pathname.startsWith(t.href);
          return (
            <Link
              key={t.href}
              href={t.href}
              aria-current={on ? 'page' : undefined}
              className={cn(
                'flex min-h-11 items-center border-b-2 px-3 text-sm font-semibold',
                on ? 'border-accent text-ink' : 'border-transparent text-ink-muted',
              )}
            >
              {t.label}
            </Link>
          );
        })}
      </nav>
      {children}
    </div>
  );
}
