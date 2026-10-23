'use client';

import { useEffect, useId, useRef, useState } from 'react';
import Link from 'next/link';
import { usePathname } from 'next/navigation';
import { ChevronDown, LogOut, Mail, Package, Receipt, Store, User } from 'lucide-react';
import { useCustomerProfile } from '@/lib/hooks/useAccount';
import { useCustomerSession } from '@/lib/hooks/useSession';
import { SignOutButton } from '@/components/auth/SignOutButton';
import { HearthLogo } from '@/components/hearth/HearthLogo';
import { SearchForm } from '@/components/shop/SearchBox';
import { initialsOf } from '@/components/portal/AccountSidebar';
import { ACCOUNT_LANDING } from '@/components/portal/account-nav';
import { cn } from '@/lib/utils';

/**
 * The customer header (HO-CAT-002 §8.3), as the owner approved it on 2026-10-17
 * (https://claude.ai/artifact/K3WKkwpv67BqLJJdSr3bUh): logo, product search, round cart and
 * messages buttons with orange counts (passed in as `sessionActions`), and the customer's initials
 * and first name with an arrow that opens a short menu ending in Sign out. The account's pages are
 * in the left menu (`components/portal/AccountSidebar.tsx`).
 *
 * HO-ACC-002 §11.5/§8.4: the name comes from the session query `CustomerSessionGuard` already
 * resolves (shared cache, no extra request). While it resolves the header shows no name yet.
 */
export function PortalHeader({ sessionActions }: { sessionActions?: React.ReactNode }) {
  const { data: session } = useCustomerSession();

  return (
    <header className="border-b border-border bg-surface">
      <div className="flex w-full items-center gap-3 px-3.5 py-3 md:gap-4 md:px-[22px] md:py-3.5">
        <HearthLogo variant="portal" href="/" />
        <div className="hidden min-w-0 flex-1 md:flex">
          <SearchForm />
        </div>
        <div className="ml-auto flex items-center gap-2 md:ml-0 md:gap-3">
          {session ? (
            <>
              {/* HO-NTF-002 §11.1 / HO-ORD-002 §3.3 — the cart and inbox buttons, only once signed in. */}
              {sessionActions}
              <AccountMenu fullName={session.fullName} />
            </>
          ) : (
            <Link
              href="/sign-in"
              className="inline-flex min-h-11 items-center gap-2 rounded-full px-3 text-sm font-semibold text-ink no-underline hover:bg-surface-sunken"
            >
              <User size={18} aria-hidden="true" />
              Sign in
            </Link>
          )}
        </div>
      </div>
    </header>
  );
}

/**
 * The name button and its menu — a disclosure (a button and a panel of links), not an ARIA `menu`,
 * so the entries stay ordinary links. Closes on Escape (focus back on the button), an outside click
 * and any page change. Messages appears here only on a phone, where the header has no envelope.
 */
function AccountMenu({ fullName }: { fullName: string }) {
  const [open, setOpen] = useState(false);
  const panelId = useId();
  const rootRef = useRef<HTMLDivElement>(null);
  const buttonRef = useRef<HTMLButtonElement>(null);
  const pathname = usePathname();
  const profile = useCustomerProfile();
  const firstName = fullName.trim().split(/\s+/)[0] ?? fullName;

  useEffect(() => {
    setOpen(false);
  }, [pathname]);

  useEffect(() => {
    if (!open) return;
    const onPointer = (event: PointerEvent) => {
      if (!rootRef.current?.contains(event.target as Node)) setOpen(false);
    };
    const onKey = (event: KeyboardEvent) => {
      if (event.key === 'Escape') {
        setOpen(false);
        buttonRef.current?.focus();
      }
    };
    document.addEventListener('pointerdown', onPointer);
    document.addEventListener('keydown', onKey);
    return () => {
      document.removeEventListener('pointerdown', onPointer);
      document.removeEventListener('keydown', onKey);
    };
  }, [open]);

  const row =
    'flex min-h-10 w-full items-center justify-start gap-3 rounded-[10px] px-3 text-[14.5px] font-normal text-ink no-underline hover:bg-surface-sunken';

  return (
    <div ref={rootRef} className="relative">
      <button
        ref={buttonRef}
        type="button"
        aria-expanded={open}
        aria-controls={panelId}
        aria-label={`Account menu for ${fullName}`}
        onClick={() => setOpen((current) => !current)}
        className={cn(
          'inline-flex min-h-11 items-center gap-1 rounded-full border bg-surface p-[3px] pr-2 text-[15px] font-semibold text-ink sm:gap-2.5 sm:pr-3.5',
          open
            ? 'border-accent shadow-[0_0_0_3px_var(--accent-tint)]'
            : 'border-border hover:border-border-strong',
        )}
      >
        <span
          aria-hidden="true"
          className="grid size-9 place-items-center rounded-full bg-accent text-[13px] font-bold text-white sm:size-10 sm:text-[15px]"
        >
          {initialsOf(fullName)}
        </span>
        <span aria-hidden="true" className="hidden max-w-[12ch] truncate sm:inline">
          {firstName}
        </span>
        <ChevronDown
          size={16}
          aria-hidden="true"
          className={cn('text-ink-muted transition-transform', open && 'rotate-180')}
        />
      </button>
      <div
        id={panelId}
        hidden={!open}
        className="absolute right-0 top-[calc(100%+10px)] z-30 w-[270px] rounded-card border border-border bg-surface p-2 shadow-[0_18px_40px_rgba(31,29,26,0.16)]"
      >
        <div className="mb-1 flex items-center gap-3 border-b border-border px-2.5 pb-3.5 pt-2.5">
          <span
            aria-hidden="true"
            className="grid size-10 flex-none place-items-center rounded-full bg-accent text-[15px] font-bold text-white"
          >
            {initialsOf(fullName)}
          </span>
          <span className="flex min-w-0 flex-col">
            <span className="truncate text-[15px] font-bold text-ink">{fullName}</span>
            {profile.data?.emailAddress && (
              <span className="truncate text-[13px] text-ink-muted">
                {profile.data.emailAddress}
              </span>
            )}
          </span>
        </div>
        <ul className="m-0 flex list-none flex-col gap-0.5 p-0">
          <li>
            <Link href={ACCOUNT_LANDING} className={row}>
              <Package size={18} aria-hidden="true" className="text-ink-muted" />
              My orders
            </Link>
          </li>
          <li className="sm:hidden">
            <Link href="/portal/inbox" className={row}>
              <Mail size={18} aria-hidden="true" className="text-ink-muted" />
              Messages
            </Link>
          </li>
          <li>
            <Link href="/portal/account/profile" className={row}>
              <User size={18} aria-hidden="true" className="text-ink-muted" />
              Profile &amp; security
            </Link>
          </li>
          <li>
            <Link href="/portal/billing" className={row}>
              <Receipt size={18} aria-hidden="true" className="text-ink-muted" />
              Billing
            </Link>
          </li>
          <li>
            <Link href="/shop" className={row}>
              <Store size={18} aria-hidden="true" className="text-ink-muted" />
              Shop
            </Link>
          </li>
          <li className="mt-1 border-t border-border pt-1">
            <SignOutButton
              audience="customer"
              className={cn(row, 'font-semibold text-danger-ink hover:text-danger-ink')}
            >
              <LogOut size={18} aria-hidden="true" />
            </SignOutButton>
          </li>
        </ul>
      </div>
    </div>
  );
}
