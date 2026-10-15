'use client';

import { useEffect, useState } from 'react';
import Link from 'next/link';
import { usePathname } from 'next/navigation';
import { Menu, ChevronLeft } from 'lucide-react';
import { navRegistry } from '@/nav';
import type { NavArea } from '@/nav/types';
import { useStaffSession } from '@/lib/hooks/useSession';
import { SignOutButton } from '@/components/auth/SignOutButton';
import { Button } from '@/components/ui/button';
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogHeader,
  DialogTitle,
  DialogTrigger,
} from '@/components/ui/dialog';
import { cn } from '@/lib/utils';

// Below `sm` (640px, Tailwind's default breakpoint), the fixed w-60 (240px) sidebar plus any real
// content (a form, a DataTable's Previous/Next pager, …) doesn't fit UI-1's 360px floor — verified
// by HO-CAT-002's E18. Below `sm`, the in-flow `<aside>` is therefore now ALWAYS the collapsed
// w-16 (64px) rail — a pure CSS fact (`w-16` with no `sm:`-gated width class overriding it below
// `sm`), true from the very first byte of server-rendered HTML, through hydration, with no JS
// timing involved (E18's own root cause was a *default* that depended on JS having run at all).
// It can no longer widen at all below `sm`, so E18's overflow, and every later hand-rolled-drawer
// finding built on top of it (scrim keyboard escape, `inert`-on-siblings, focus return, the
// out-of-tree toast), cannot recur structurally — there is nothing below `sm` left to trap focus
// inside by hand.
//
// The rail's own "Expand menu" button instead opens the full nav in a Radix `Dialog` rendered as a
// left sheet (`components/ui/dialog.tsx`'s `sheet-left` variant) — a real modal, so focus-trapping,
// `Escape`-to-close, overlay-click-to-close, focus-return-to-trigger and hiding the rest of the
// document from assistive tech all come from Radix itself (HO-CAT-002 §8.5), not from a second
// hand-rolled implementation of each.
//
// At `sm` and up, nothing here changes: the in-flow sticky aside is w-60 expanded or w-16
// collapsed, via its own always-mounted toggle. `collapsed` only ever affects `sm:`-prefixed
// classes, so its initial value (`false`, matching the CSS default `sm:w-60`) produces identical
// server and first-client-render markup — no hydration mismatch, and no JS-decided default below
// `sm` either.
/** An item with no `roles` is for everyone; otherwise the member of staff needs one of them. */
export function canSeeNavItem(item: { roles?: readonly string[] }, roleIds: readonly string[]): boolean {
  return !item.roles || item.roles.some((role) => roleIds.includes(role));
}

export function StaffShell({ children }: { children: React.ReactNode }) {
  const [collapsed, setCollapsed] = useState(false);
  const [sheetOpen, setSheetOpen] = useState(false);
  const pathname = usePathname();
  // HO-ACC-002 §11.9/ADR-FE-004: `lib/identity.ts` is gone — the signed-in staff member's name
  // comes from the same session query `StaffSessionGuard` (the enclosing layout) already
  // resolved, shared via the TanStack Query cache (no extra request).
  const { data: session } = useStaffSession();
  const roleIds = session?.roleIds ?? [];

  // If the window is resized past `sm` while the below-`sm` sheet is open (e.g. rotating a device,
  // or widening a resizable window), leaving it open would strand the user in a nav-only modal
  // that hides the rest of the page — including the full in-flow nav that's now sitting right
  // behind it, already showing the same links. Closing it is harmless: `sm`+ never depended on
  // `sheetOpen` for its own layout (that stays pure CSS, see above), so this only ever discards an
  // open *interactive* overlay after hydration has already run — it cannot reintroduce E18's
  // pre-hydration timing race, which was about the default *layout*, not a user-opened dialog.
  useEffect(() => {
    if (!sheetOpen) return;
    const query = window.matchMedia('(min-width: 640px)');
    const onChange = (event: MediaQueryListEvent) => {
      if (event.matches) setSheetOpen(false);
    };
    query.addEventListener('change', onChange);
    return () => query.removeEventListener('change', onChange);
  }, [sheetOpen]);

  // CODE_REVIEW gate MEDIUM (HO-CAT-002, 2nd pass): `StaffShell` is a persistent App Router layout
  // component, so `sheetOpen` otherwise survives any route change that doesn't go through one of
  // the sheet's own `SheetNav` links — most notably the browser's Back/Forward buttons, which are
  // chrome, not page DOM, so neither the dialog's overlay nor its focus trap can intercept them. A
  // user could open the sheet, press Back, and land on the previous page with the modal nav sheet
  // still open on top of it, still trapping Tab/Shift+Tab. Closing on every pathname change covers
  // Back/Forward and any other non-link navigation (e.g. `router.push` from elsewhere), not only
  // the in-sheet-link case `onNavigate` already handles.
  useEffect(() => {
    setSheetOpen(false);
  }, [pathname]);

  // Per-item `audience` (nav/types.ts, added by HO-CAT-002) overrides the area's own audience,
  // for an area like Cat Profiles whose single `audience: 'both'` mixes staff-only and
  // portal-only screens in one items array — falls back to the area's audience when an item
  // doesn't set one, which is every pre-existing area, so this is not a behaviour change there.
  const sections: NavArea[] = navRegistry
    .map((area) => ({
      ...area,
      items: area.items.filter(
        (item) => (item.audience ?? area.audience) !== 'portal' && canSeeNavItem(item, roleIds),
      ),
    }))
    .filter((area) => area.items.length > 0);

  return (
    <div className="flex min-h-screen bg-surface-page">
      <a
        href="#main-content"
        className="sr-only focus:not-sr-only focus:fixed focus:left-4 focus:top-4 focus:z-50 focus:rounded-[var(--radius-sm)] focus:bg-accent focus:px-4 focus:py-2 focus:text-white"
      >
        Skip to main content
      </a>

      <aside
        className={cn(
          'sticky top-0 flex h-screen w-16 flex-shrink-0 flex-col gap-4 overflow-y-auto border-r border-border bg-surface-sunken p-3 transition-[width]',
          !collapsed && 'sm:w-60',
        )}
      >
        <div className="flex items-center justify-between gap-2">
          <Link
            href="/"
            className={cn(
              'sr-only flex min-w-0 items-center gap-2 font-display text-lg font-semibold text-ink no-underline',
              !collapsed && 'sm:not-sr-only',
            )}
          >
            PurrCare
          </Link>

          {/* Below sm: opens the nav sheet (a real modal Dialog — see above). At sm and up: the
              ordinary in-flow collapse/expand toggle. Both always mounted, one per viewport, so a
              click never swaps the focused element's identity out from under the keyboard
              (TEST gate, 2nd pass) — each is CSS-hidden at the other viewport, never unmounted. */}
          <Dialog open={sheetOpen} onOpenChange={setSheetOpen}>
            <DialogTrigger asChild>
              <Button
                variant="secondary"
                size="icon"
                className="sm:hidden"
                aria-label="Expand menu"
              >
                <Menu size={14} aria-hidden="true" />
              </Button>
            </DialogTrigger>
            <DialogContent variant="sheet-left">
              <DialogHeader className="mb-4 flex-row items-center justify-between gap-2">
                <DialogTitle>Menu</DialogTitle>
              </DialogHeader>
              <DialogDescription className="sr-only">
                PurrCare staff navigation, every area.
              </DialogDescription>
              <NavSections sections={sections} mode="full" onNavigate={() => setSheetOpen(false)} />
            </DialogContent>
          </Dialog>

          <Button
            variant="secondary"
            size="icon"
            className="hidden sm:inline-flex"
            aria-expanded={!collapsed}
            aria-label={collapsed ? 'Expand menu' : 'Collapse menu'}
            onClick={() => setCollapsed((current) => !current)}
          >
            {collapsed ? (
              <Menu size={14} aria-hidden="true" />
            ) : (
              <ChevronLeft size={14} aria-hidden="true" />
            )}
          </Button>
        </div>

        <NavSections sections={sections} mode="compact" collapsed={collapsed} />
      </aside>

      <div className="flex min-w-0 flex-1 flex-col">
        <header className="flex h-14 flex-shrink-0 items-center justify-between gap-4 border-b border-border bg-surface px-6">
          <nav aria-label="Breadcrumb" className="min-w-0 truncate text-sm text-ink-muted">
            <span className="font-semibold text-ink">PurrCare staff</span>
          </nav>
          <div className="flex flex-shrink-0 items-center gap-3 text-sm text-ink-muted">
            {session && (
              <>
                {/* Below `sm` (the same 640px floor the rail nav collapses at, UI-1), the header
                    has no room left for the name+role text once the breadcrumb and the always-
                    visible Sign out button are accounted for — hidden rather than truncated, since
                    a cut-off role list reads worse than not showing it at all at this width. */}
                <span className="hidden truncate sm:inline">
                  {session.fullName} · {session.roleIds.join(', ')}
                </span>
                <SignOutButton audience="staff" />
              </>
            )}
          </div>
        </header>

        <main id="main-content" className="min-w-0 flex-1 px-6 py-7">
          {children}
        </main>
      </div>
    </div>
  );
}

/**
 * The one renderer for both the always-in-flow aside nav and the below-`sm` sheet's nav
 * (ARCHITECTURE gate LOW, HO-CAT-002 4th pass — `SheetNav` used to be a second render path with
 * its own locally redeclared section/item type instead of `NavArea`/`NavItem` from `@/nav/types`,
 * so a later shared-nav feature such as active-item highlighting or `NavItem.icon` would have had
 * to be built twice and kept in step).
 *
 * `mode="compact"` is the aside: below `sm` it's always the `w-16` rail (single-letter links); at
 * `sm`+ it's full labels unless `collapsed`. `mode="full"` is the sheet: always full labels, no
 * collapse state (there's nothing to collapse inside a modal), and calls `onNavigate` on every
 * link click so the sheet closes itself.
 */
function NavSections({
  sections,
  mode,
  collapsed = false,
  onNavigate,
}: {
  sections: NavArea[];
  mode: 'compact' | 'full';
  collapsed?: boolean;
  onNavigate?: () => void;
}) {
  // Below sm, the aside is always the rail (headings/empty-state text have no room); the sheet
  // (mode="full") always has room, so this only ever hides anything in compact mode.
  const compactHidden = mode === 'compact' && cn('hidden', !collapsed && 'sm:block');

  if (sections.length === 0) {
    return (
      <p className={cn('px-2 text-sm text-ink-muted', compactHidden)}>
        No areas registered yet — see web/README.md.
      </p>
    );
  }

  return (
    <nav aria-label="Main" className="flex flex-col gap-4">
      {sections.map((area) => (
        <div key={area.key}>
          <p
            className={cn(
              // text-ink-muted, not text-ink-faint (HO-CAT-002 E17/UI-6) — --ink-faint
              // (#8c8578) on this sidebar's --surface-sunken (#f2efe7) is 3.18:1, short of
              // WCAG AA's 4.5:1 for this 12px bold label; --ink-muted clears it at 6.23:1.
              'px-2 pb-1 text-xs font-bold uppercase tracking-wide text-ink-muted',
              compactHidden,
            )}
          >
            {area.label}
          </p>
          <div className="flex flex-col gap-0.5">
            {area.items.map((item) => (
              <Link
                key={item.href}
                href={item.href}
                onClick={onNavigate}
                title={mode === 'compact' ? item.label : undefined}
                // The accessible name is always the full label in compact mode, regardless of
                // which of the two visual spans below is showing (full label at sm+ expanded, a
                // single letter everywhere else) — nothing here can be momentarily wrong while
                // CSS/React settle on a width. In full mode (the sheet), the link's own visible
                // text already is the full label, so no override is needed.
                aria-label={mode === 'compact' ? item.label : undefined}
                className="rounded-[var(--radius-sm)] px-2 py-2 text-sm text-ink-muted no-underline hover:bg-accent-soft"
              >
                {mode === 'compact' ? (
                  <>
                    {/* Full label: sm and up, only while expanded. */}
                    <span aria-hidden="true" className={cn('hidden', !collapsed && 'sm:inline')}>
                      {item.label}
                    </span>
                    {/* Single-letter abbreviation: always below sm (the aside is always the
                        rail there), and at sm+ only while collapsed. */}
                    <span aria-hidden="true" className={cn('inline', !collapsed && 'sm:hidden')}>
                      {item.label.slice(0, 1)}
                    </span>
                  </>
                ) : (
                  item.label
                )}
              </Link>
            ))}
          </div>
        </div>
      ))}
    </nav>
  );
}
