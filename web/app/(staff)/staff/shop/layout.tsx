'use client';

import Link from 'next/link';
import { usePathname } from 'next/navigation';
import { cn } from '@/lib/utils';

const ITEMS = [
  { label: 'Products', href: '/staff/shop/products' },
  { label: 'Origins', href: '/staff/shop/origins' },
  { label: 'Reviews', href: '/staff/shop/reviews' },
];

/** `/staff/shop/**` — the shop section nav (HO-SHP-002 §8.1). The staff guard is the parent layout's. */
export default function StaffShopLayout({ children }: { children: React.ReactNode }) {
  const pathname = usePathname();
  return (
    <div className="flex flex-col gap-6">
      <nav aria-label="Shop" className="flex flex-wrap gap-1 border-b border-border pb-3">
        {ITEMS.map((item) => {
          const active = pathname === item.href || pathname.startsWith(`${item.href}/`);
          return (
            <Link
              key={item.href}
              href={item.href}
              aria-current={active ? 'page' : undefined}
              className={cn(
                'rounded-[var(--radius-sm)] px-3 py-1.5 text-sm font-semibold no-underline',
                active
                  ? 'bg-accent-soft text-accent-ink'
                  : 'text-ink-muted hover:bg-surface-sunken',
              )}
            >
              {item.label}
            </Link>
          );
        })}
      </nav>
      <div>{children}</div>
    </div>
  );
}
