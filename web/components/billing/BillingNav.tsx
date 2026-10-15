'use client';

import Link from 'next/link';
import { usePathname } from 'next/navigation';
import { cn } from '@/lib/utils';

/**
 * `components/billing/BillingNav.tsx` — HO-BIL-002 §8.1, ADR-FE-079. The Billing section's own
 * navigation (rendered by `app/(staff)/staff/billing/layout.tsx`), mirroring `AdminNav`: a link per
 * screen, filtered by role for presentation only — a hidden link's page is still refused by the
 * server (`PermissionDeniedState`), and a shown one can still be refused.
 */
const ACCOUNTS = ['Administrator', 'Accountant'] as const;

interface Item {
  label: string;
  href: string;
  roles?: readonly string[];
  exact?: boolean;
}

export const billingNavItems: Item[] = [
  { label: 'Overview', href: '/staff/billing', exact: true },
  { label: 'Invoices', href: '/staff/billing/invoices' },
  { label: 'Payments', href: '/staff/billing/payments' },
  { label: 'Approvals', href: '/staff/billing/approvals', roles: ACCOUNTS },
  { label: 'Receivables', href: '/staff/billing/receivables' },
  { label: 'Cash-up', href: '/staff/billing/cash-up' },
  { label: 'COD', href: '/staff/billing/reconciliation/cod', roles: ACCOUNTS },
  { label: 'Bank', href: '/staff/billing/reconciliation/bank', roles: ACCOUNTS },
  { label: 'Export', href: '/staff/billing/exports', roles: ACCOUNTS },
];

export function BillingNav({ roleIds }: { roleIds: string[] }) {
  const pathname = usePathname();
  const visible = billingNavItems.filter(
    (item) => !item.roles || item.roles.some((role) => roleIds.includes(role)),
  );
  return (
    <nav aria-label="Billing" className="flex flex-wrap gap-1 border-b border-border pb-3">
      {visible.map((item) => {
        const active = item.exact
          ? pathname === item.href
          : pathname === item.href || pathname.startsWith(`${item.href}/`);
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
          </Link>
        );
      })}
    </nav>
  );
}
