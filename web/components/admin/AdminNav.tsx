'use client';

import Link from 'next/link';
import { usePathname } from 'next/navigation';
import { adminNavItems } from './adminNavItems';
import { cn } from '@/lib/utils';

/**
 * `components/admin/AdminNav.tsx` — HO-ADM-002 §11.1, ADR-FE-014 (renumbered — see that ADR's
 * note on the numbering collision with HO-ACC-002). The admin section's own internal navigation,
 * rendered by `app/(staff)/staff/admin/layout.tsx`. Filters `adminNavItems` against the signed-in
 * staff member's `roleIds` for presentation — U-A1 proves a hidden item's page is still refused by
 * the server, not by this filter.
 *
 * ARCH gate precedent (HO-ACC-002 §8.2 rule 4) — `components/admin/**` takes props/callbacks; the
 * page (`admin/layout.tsx`) wires `useStaffSession()` and passes `roleIds` in.
 */
export function AdminNav({ roleIds }: { roleIds: string[] }) {
  const pathname = usePathname();
  const visibleItems = adminNavItems.filter((item) =>
    item.roles.some((role) => roleIds.includes(role)),
  );

  if (visibleItems.length === 0) return null;

  return (
    <nav aria-label="Administration" className="flex flex-wrap gap-1 border-b border-border pb-3">
      {visibleItems.map((item) => {
        const isActive = pathname === item.href || pathname.startsWith(`${item.href}/`);
        return (
          <Link
            key={item.href}
            href={item.href}
            aria-current={isActive ? 'page' : undefined}
            className={cn(
              'rounded-[var(--radius-sm)] px-3 py-1.5 text-sm font-semibold no-underline',
              isActive
                ? 'bg-accent-soft text-accent-ink'
                : 'text-ink-muted hover:bg-surface-sunken',
            )}
          >
            {item.label}
          </Link>
        );
      })}
    </nav>
  );
}
