'use client';

import { useEffect, useRef } from 'react';
import Link from 'next/link';
import { usePathname } from 'next/navigation';
import {
  Bed,
  Calendar,
  Cat,
  Heart,
  House,
  LogOut,
  Package,
  Pill,
  Receipt,
  Scissors,
  Star,
  User,
  type LucideIcon,
} from 'lucide-react';
import { SignOutButton } from '@/components/auth/SignOutButton';
import { useCustomerProfile } from '@/lib/hooks/useAccount';
import { useCats } from '@/lib/hooks/useCats';
import { useMyOrders } from '@/lib/hooks/useMyOrders';
import { useCustomerSession } from '@/lib/hooks/useSession';
import { cn } from '@/lib/utils';
import {
  ACCOUNT_GROUPS,
  PHONE_PILL_ORDER,
  activeAccountHref,
  type AccountIcon,
  type AccountLink,
} from './account-nav';

const ICONS: Record<AccountIcon, LucideIcon> = {
  home: House,
  cat: Cat,
  calendar: Calendar,
  pill: Pill,
  scissors: Scissors,
  bed: Bed,
  package: Package,
  heart: Heart,
  star: Star,
  receipt: Receipt,
  user: User,
};

/** Orders that are still on their way (the count beside Orders). */
const OPEN_ORDER_STATUSES = new Set(['PendingPayment', 'Confirmed', 'Packing', 'Dispatched']);

export function initialsOf(fullName: string): string {
  const parts = fullName.trim().split(/\s+/).filter(Boolean);
  const first = parts[0]?.[0] ?? '';
  const last = parts.length > 1 ? (parts.at(-1)?.[0] ?? '') : '';
  return (first + last).toUpperCase() || '?';
}

/** "2 cats · member since 2026" — each half only once its figure has loaded. */
function useAccountSummary(): string | null {
  const cats = useCats({ pageNumber: 1, pageSize: 1 });
  const profile = useCustomerProfile();
  const parts: string[] = [];
  const catCount = cats.data?.totalCount;
  if (catCount !== undefined) parts.push(`${catCount} ${catCount === 1 ? 'cat' : 'cats'}`);
  const year = profile.data?.registeredOn
    ? new Date(profile.data.registeredOn).getFullYear()
    : null;
  if (year) parts.push(`member since ${year}`);
  return parts.length > 0 ? parts.join(' · ') : null;
}

function useOpenOrderCount(): number {
  const orders = useMyOrders({ pageNumber: 1, pageSize: 50 });
  return orders.data?.items.filter((o) => OPEN_ORDER_STATUSES.has(o.status)).length ?? 0;
}

/**
 * Option A "Account sidebar" as approved by the owner (2026-10-17;
 * https://claude.ai/artifact/K3WKkwpv67BqLJJdSr3bUh): on desktop a left menu with the customer
 * ("2 cats · member since 2026"), Overview and three groups — My cats & care, Shopping (Orders
 * with its open count), Account (Billing, Profile & security, Sign out). The current page is teal.
 */
export function AccountSidebar() {
  const pathname = usePathname() ?? '';
  const active = activeAccountHref(pathname);
  const { data: session } = useCustomerSession();
  const summary = useAccountSummary();
  const openOrders = useOpenOrderCount();

  return (
    <aside className="hidden w-[300px] flex-none border-r border-border bg-surface md:block">
      <div className="sticky top-0 flex max-h-screen flex-col gap-[18px] overflow-y-auto px-5 py-[22px]">
        {session && (
          <div className="flex items-center gap-3.5 border-b border-border px-0.5 pb-5">
            <span
              aria-hidden="true"
              className="grid size-[58px] flex-none place-items-center rounded-full bg-accent text-[19px] font-bold text-white"
            >
              {initialsOf(session.fullName)}
            </span>
            <span className="flex min-w-0 flex-col">
              <span className="truncate text-[19px] font-bold tracking-[-0.01em] text-ink">
                {session.fullName}
              </span>
              {summary && (
                <span className="text-[15px] leading-snug text-ink-muted">{summary}</span>
              )}
            </span>
          </div>
        )}
        <nav aria-label="Main" className="flex flex-col gap-[18px]">
          {ACCOUNT_GROUPS.map((group) => (
            <div key={group.key} className="flex flex-col gap-1">
              {group.label && (
                <p className="m-0 mb-1.5 mt-1 px-[18px] text-[13px] font-bold uppercase tracking-[0.1em] text-ink-muted">
                  {group.label}
                </p>
              )}
              {group.links.map((link) => (
                <SidebarLink
                  key={link.href}
                  link={link}
                  current={link.href === active}
                  count={link.href === '/portal/orders' ? openOrders : 0}
                />
              ))}
              {group.key === 'account' && (
                <SignOutButton
                  audience="customer"
                  className="h-12 w-full justify-start gap-3.5 rounded-xl px-[18px] text-[17px] font-medium text-ink-muted hover:text-ink [&_svg]:size-[22px]"
                >
                  <LogOut aria-hidden="true" />
                </SignOutButton>
              )}
            </div>
          ))}
        </nav>
      </div>
    </aside>
  );
}

function SidebarLink({
  link,
  current,
  count,
}: {
  link: AccountLink;
  current: boolean;
  count: number;
}) {
  const Icon = ICONS[link.icon];
  return (
    <Link
      href={link.href}
      aria-current={current ? 'page' : undefined}
      className={cn(
        'flex h-12 items-center gap-3.5 rounded-xl px-[18px] text-[17px] font-medium no-underline transition-colors',
        current
          ? 'bg-accent-tint font-semibold text-accent'
          : 'text-ink-muted hover:bg-surface-sunken hover:text-ink',
      )}
    >
      <Icon size={22} strokeWidth={current ? 2.1 : 1.8} aria-hidden="true" />
      {link.label}
      {count > 0 && (
        <span className="ml-auto grid h-[26px] min-w-[30px] place-items-center rounded-full bg-warm px-2 text-[13px] font-bold text-ink">
          <span className="sr-only">(</span>
          {count}
          <span className="sr-only"> on the way)</span>
        </span>
      )}
    </Link>
  );
}

/**
 * The approved design on a phone: the same pages as a row of pills under the header, starting with
 * Orders (where the account opens); the current one filled teal and scrolled into view. Sign out is
 * in the menu under the name.
 */
export function AccountPills() {
  const pathname = usePathname() ?? '';
  const active = activeAccountHref(pathname);
  const rowRef = useRef<HTMLElement>(null);
  const links = ACCOUNT_GROUPS.flatMap((g) => g.links).sort(
    (a, b) => PHONE_PILL_ORDER.indexOf(a.href) - PHONE_PILL_ORDER.indexOf(b.href),
  );

  useEffect(() => {
    rowRef.current
      ?.querySelector('[aria-current="page"]')
      ?.scrollIntoView?.({ block: 'nearest', inline: 'center' });
  }, [pathname]);

  return (
    // Only one of the two "Main" navs is ever displayed: this one below md, the sidebar from md.
    <nav
      ref={rowRef}
      aria-label="Main"
      className="flex gap-2 overflow-x-auto border-b border-border bg-surface px-3.5 py-2.5 [scrollbar-width:none] md:hidden [&::-webkit-scrollbar]:hidden"
    >
      {links.map((link) => {
        const Icon = ICONS[link.icon];
        const current = link.href === active;
        return (
          <Link
            key={link.href}
            href={link.href}
            aria-current={current ? 'page' : undefined}
            className={cn(
              'inline-flex min-h-9 flex-none items-center gap-1.5 whitespace-nowrap rounded-full border px-[13px] text-[13.5px] font-semibold no-underline',
              current
                ? 'border-accent bg-accent text-white'
                : 'border-border bg-surface text-ink-muted',
            )}
          >
            <Icon size={16} aria-hidden="true" />
            {link.label}
          </Link>
        );
      })}
    </nav>
  );
}

/**
 * The account frame: on /portal pages for a signed-in customer, the pills (phone) or the sidebar
 * (desktop) beside the page; anywhere else in the group (booking, checkout, boarding search) just
 * the page, so a flow keeps its focus.
 */
export function AccountFrame({ children }: { children: React.ReactNode }) {
  const pathname = usePathname() ?? '';
  const { data: session } = useCustomerSession();
  const inAccount = !!session && (pathname === '/portal' || pathname.startsWith('/portal/'));

  if (!inAccount) {
    return (
      <main id="main-content" className="mx-auto w-full max-w-[1224px] px-4 py-8 md:px-8">
        {children}
      </main>
    );
  }
  return (
    <>
      <AccountPills />
      <div className="flex min-h-[calc(100vh-75px)]">
        <AccountSidebar />
        <main id="main-content" className="min-w-0 flex-1 px-3.5 py-5 md:px-[34px] md:py-[30px]">
          <div className="mx-auto w-full max-w-[1100px]">{children}</div>
        </main>
      </div>
    </>
  );
}
