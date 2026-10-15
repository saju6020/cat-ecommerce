'use client';

import Link from 'next/link';
import { usePathname } from 'next/navigation';
import {
  STAFF_ADMIN_BOARDING_RATE_CARDS,
  STAFF_ADMIN_BOARDING_ROUTINE,
  STAFF_ADMIN_BOARDING_UNITS,
} from '@/lib/routing/boarding-links';
import { cn } from '@/lib/utils';

const TABS = [
  { href: STAFF_ADMIN_BOARDING_UNITS, label: 'Units' },
  { href: STAFF_ADMIN_BOARDING_RATE_CARDS, label: 'Rate cards' },
  { href: STAFF_ADMIN_BOARDING_ROUTINE, label: 'Care routine' },
];

/** The Boarding group of the admin section (HO-BRD-002 §13): its own tab row, since the shared admin
 *  nav is another slice's file. Every page answers a `403` itself (ADR-FE-107). */
export default function BoardingAdminLayout({ children }: { children: React.ReactNode }) {
  const pathname = usePathname();
  return (
    <div className="flex flex-col gap-5">
      <h1 className="font-display text-2xl font-semibold text-ink">Boarding setup</h1>
      <nav aria-label="Boarding setup" className="flex gap-1 border-b border-border">
        {TABS.map((t) => (
          <Link
            key={t.href}
            href={t.href}
            aria-current={pathname.startsWith(t.href) ? 'page' : undefined}
            className={cn(
              'flex min-h-11 items-center border-b-2 px-3 text-sm font-semibold',
              pathname.startsWith(t.href)
                ? 'border-accent text-ink'
                : 'border-transparent text-ink-muted',
            )}
          >
            {t.label}
          </Link>
        ))}
      </nav>
      {children}
    </div>
  );
}
