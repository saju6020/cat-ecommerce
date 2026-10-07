'use client';

import Link from 'next/link';
import { usePathname } from 'next/navigation';
import { cn } from '@/lib/utils';

/**
 * `components/account/AccountShell.tsx` — HO-ACC-002 §11.7, ADR-FE-008. A route-based tab shell
 * (deep-linkable): Profile · Addresses · Notifications · Your data. Compliance-relevant settings
 * (preferences, deletion) save explicitly on their own pages — this shell is presentation only.
 */
const TABS = [
  { href: '/portal/account/profile', label: 'Profile' },
  { href: '/portal/account/addresses', label: 'Addresses' },
  { href: '/portal/account/notifications', label: 'Notifications' },
  { href: '/portal/account/data', label: 'Your data' },
] as const;

export function AccountShell({ children }: { children: React.ReactNode }) {
  const pathname = usePathname();

  return (
    <div className="flex flex-col gap-6">
      <nav aria-label="Account" className="flex flex-wrap gap-2 border-b border-border">
        {TABS.map((tab) => {
          const isCurrent = pathname?.startsWith(tab.href);
          return (
            <Link
              key={tab.href}
              href={tab.href}
              aria-current={isCurrent ? 'page' : undefined}
              className={cn(
                'rounded-t-[var(--radius-sm)] border-b-2 px-3 py-2 text-sm font-semibold no-underline',
                isCurrent
                  ? 'border-accent text-accent-ink'
                  : 'border-transparent text-ink-muted hover:text-ink',
              )}
            >
              {tab.label}
            </Link>
          );
        })}
      </nav>
      {children}
    </div>
  );
}
