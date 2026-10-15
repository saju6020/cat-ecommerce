'use client';

import Link from 'next/link';
import { usePathname } from 'next/navigation';
import { cn } from '@/lib/utils';

export interface SectionNavItem {
  label: string;
  href: string;
  /** Optional count shown after the label as TEXT plus number (`Alerts 7 open`). */
  badge?: string | null;
  /** Staff roles the server grants the underlying action to — presentation only. */
  roles: readonly string[];
}

/**
 * `components/inventory/SectionNav.tsx` — HO-IMP-002 §11.1, ADR-FE-039. The tab row both sections
 * share. Items are filtered by the signed-in staff member's `roleIds` against a presentation map
 * that mirrors HO-IMP-001 §12.3 BY NAME ONLY — convenience, never authorization: every page still
 * renders `PermissionDeniedState` on a server `403`.
 */
export function SectionNav({
  label,
  items,
  roleIds,
}: {
  label: string;
  items: readonly SectionNavItem[];
  roleIds: readonly string[];
}) {
  const pathname = usePathname();
  const visible = items.filter((item) => item.roles.some((role) => roleIds.includes(role)));
  if (visible.length === 0) return null;

  // The longest matching href wins, so the overview ("/staff/inventory") does not light up for
  // every child route.
  const activeHref = visible
    .filter((item) => pathname === item.href || pathname.startsWith(`${item.href}/`))
    .sort((a, b) => b.href.length - a.href.length)[0]?.href;

  return (
    <nav aria-label={label} className="flex flex-wrap gap-1 border-b border-border pb-3">
      {visible.map((item) => {
        const active = item.href === activeHref;
        return (
          <Link
            key={item.href}
            href={item.href}
            aria-current={active ? 'page' : undefined}
            className={cn(
              'rounded-[var(--radius-sm)] px-3 py-1.5 text-sm font-semibold no-underline',
              active ? 'bg-accent-soft text-accent-ink' : 'text-ink-muted hover:bg-surface-sunken',
            )}
          >
            {item.label}
            {item.badge && (
              <span className="ml-1.5 rounded-full bg-warning-bg px-2 py-0.5 text-xs font-bold text-warning-ink">
                {item.badge}
              </span>
            )}
          </Link>
        );
      })}
    </nav>
  );
}
