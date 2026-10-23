'use client';

import { useEffect, useState } from 'react';
import Link from 'next/link';
import { usePathname } from 'next/navigation';
import {
  Bed,
  Boxes,
  ChevronDown,
  Building2,
  Calendar,
  CalendarDays,
  CalendarOff,
  CalendarPlus,
  Cat,
  ChartColumn,
  ClipboardList,
  Copy,
  FlaskConical,
  House,
  Inbox,
  LayoutDashboard,
  ListChecks,
  Mail,
  Menu,
  Package,
  PanelLeftClose,
  PawPrint,
  Pill,
  Receipt,
  Scissors,
  Settings,
  Settings2,
  Shield,
  ShieldAlert,
  ShoppingCart,
  Stethoscope,
  Store,
  Syringe,
  Truck,
  Undo2,
  UserPlus,
  Users,
  type LucideIcon,
} from 'lucide-react';
import { navRegistry } from '@/nav';
import type { NavArea } from '@/nav/types';
import {
  activeStaffHref,
  groupStaffNav,
  type StaffNavSection,
} from '@/components/common/staff-nav-groups';
import { useStaffSession } from '@/lib/hooks/useSession';
import { SignOutButton } from '@/components/auth/SignOutButton';
import { Button } from '@/components/ui/button';
import { HearthLogo } from '@/components/hearth/HearthLogo';
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogHeader,
  DialogTitle,
  DialogTrigger,
} from '@/components/ui/dialog';
import { cn } from '@/lib/utils';

// Below `sm` (640px, Tailwind's default breakpoint), the fixed w-64 (256px) sidebar plus any real
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
// At `sm` and up, nothing here changes: the in-flow sticky aside is w-64 expanded or w-16
// collapsed, via its own always-mounted toggle. `collapsed` only ever affects `sm:`-prefixed
// classes, so its initial value (`false`, matching the CSS default `sm:w-64`) produces identical
// server and first-client-render markup — no hydration mismatch, and no JS-decided default below
// `sm` either.
/** An item with no `roles` is for everyone; otherwise the member of staff needs one of them. */
export function canSeeNavItem(
  item: { roles?: readonly string[] },
  roleIds: readonly string[],
): boolean {
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

  // The current page's link stays in view in a long menu (e.g. Team & settings at the bottom).
  useEffect(() => {
    document.querySelector('aside [aria-current="page"]')?.scrollIntoView?.({ block: 'center' });
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
  // Presented by daily job, not by build area (staff-nav-groups.ts, owner 2026-10-16).
  const groups = groupStaffNav(sections);
  const activeHref = activeStaffHref(groups, pathname ?? '');
  const activeLabel =
    groups.flatMap((g) => g.items).find((item) => item.href === activeHref)?.label ?? null;
  // Groups open and close (owner, 2026-10-16): only the current page's group starts open, so the
  // menu fits without a scrollbar; it appears only once the opened groups need it. Arriving at a
  // page in a closed group opens that group; nothing is closed for the user.
  const activeGroupKey =
    groups.find((g) => g.items.some((item) => item.href === activeHref))?.key ?? null;
  const [openGroups, setOpenGroups] = useState<ReadonlySet<string>>(
    () => new Set(activeGroupKey ? [activeGroupKey] : []),
  );
  useEffect(() => {
    if (!activeGroupKey) return;
    setOpenGroups((prev) => (prev.has(activeGroupKey) ? prev : new Set(prev).add(activeGroupKey)));
  }, [activeGroupKey]);
  const toggleGroup = (key: string) =>
    setOpenGroups((prev) => {
      const next = new Set(prev);
      if (next.has(key)) next.delete(key);
      else next.add(key);
      return next;
    });

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
          'sticky top-0 flex h-screen w-16 flex-shrink-0 flex-col border-r border-border bg-surface transition-[width] duration-200',
          !collapsed && 'sm:w-64',
        )}
      >
        <div
          className={cn(
            'flex h-14 flex-shrink-0 items-center justify-center gap-2 border-b border-border px-3',
            !collapsed && 'sm:justify-between sm:px-4',
          )}
        >
          {/* Owner, 2026-10-19: the same PurrCare logo as everywhere else, sized to the sidebar. */}
          <HearthLogo
            variant="portal"
            href="/staff"
            className={cn('sr-only min-w-0 gap-2 text-base', !collapsed && 'sm:not-sr-only')}
          />

          {/* Below sm: opens the nav sheet (a real modal Dialog — see above). At sm and up: the
              ordinary in-flow collapse/expand toggle. Both always mounted, one per viewport, so a
              click never swaps the focused element's identity out from under the keyboard
              (TEST gate, 2nd pass) — each is CSS-hidden at the other viewport, never unmounted. */}
          <Dialog open={sheetOpen} onOpenChange={setSheetOpen}>
            <DialogTrigger asChild>
              <Button variant="ghost" size="icon" className="sm:hidden" aria-label="Expand menu">
                <Menu size={18} aria-hidden="true" />
              </Button>
            </DialogTrigger>
            <DialogContent variant="sheet-left">
              <DialogHeader className="mb-2 flex-row items-center justify-between gap-2">
                <DialogTitle>Menu</DialogTitle>
              </DialogHeader>
              <DialogDescription className="sr-only">
                PurrCare staff navigation, every area.
              </DialogDescription>
              <NavSections
                sections={groups}
                activeHref={activeHref}
                openGroups={openGroups}
                onToggleGroup={toggleGroup}
                mode="full"
                onNavigate={() => setSheetOpen(false)}
              />
            </DialogContent>
          </Dialog>

          <Button
            variant="ghost"
            size="icon"
            className="hidden text-ink-muted sm:inline-flex"
            aria-expanded={!collapsed}
            aria-label={collapsed ? 'Expand menu' : 'Collapse menu'}
            onClick={() => setCollapsed((current) => !current)}
          >
            {collapsed ? (
              <Menu size={18} aria-hidden="true" />
            ) : (
              <PanelLeftClose size={18} aria-hidden="true" />
            )}
          </Button>
        </div>

        <div className="min-h-0 flex-1 overflow-y-auto px-2.5 py-3">
          <NavSections
            sections={groups}
            activeHref={activeHref}
            openGroups={openGroups}
            onToggleGroup={toggleGroup}
            mode="compact"
            collapsed={collapsed}
          />
        </div>
      </aside>

      <div className="flex min-w-0 flex-1 flex-col">
        <header className="flex h-14 flex-shrink-0 items-center justify-between gap-4 border-b border-border bg-surface px-6">
          <nav aria-label="Breadcrumb" className="min-w-0 truncate text-sm text-ink-muted">
            <span className={cn(activeLabel ? 'hidden sm:inline' : 'font-semibold text-ink')}>
              PurrCare staff
            </span>
            {activeLabel && (
              <>
                <span aria-hidden="true" className="mx-2 hidden text-ink-faint sm:inline">
                  /
                </span>
                <span className="font-semibold text-ink">{activeLabel}</span>
              </>
            )}
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

/** Area files name their icons as lucide strings (nav/types.ts); the shell maps them here. */
const NAV_ICONS: Record<string, LucideIcon> = {
  'bar-chart': ChartColumn,
  bed: Bed,
  boxes: Boxes,
  building: Building2,
  calendar: Calendar,
  'calendar-days': CalendarDays,
  'calendar-off': CalendarOff,
  'calendar-plus': CalendarPlus,
  cat: Cat,
  'chart-column': ChartColumn,
  'clipboard-list': ClipboardList,
  copy: Copy,
  'flask-conical': FlaskConical,
  home: House,
  inbox: Inbox,
  'layout-dashboard': LayoutDashboard,
  'list-checks': ListChecks,
  mail: Mail,
  package: Package,
  pill: Pill,
  receipt: Receipt,
  scissors: Scissors,
  settings: Settings,
  'settings-2': Settings2,
  shield: Shield,
  'shield-alert': ShieldAlert,
  'shopping-cart': ShoppingCart,
  stethoscope: Stethoscope,
  store: Store,
  syringe: Syringe,
  truck: Truck,
  undo: Undo2,
  'user-plus': UserPlus,
  users: Users,
};

/**
 * The one renderer for both the always-in-flow aside nav and the below-`sm` sheet's nav
 * (ARCHITECTURE gate LOW, HO-CAT-002 4th pass): one link per item with its icon, the current page
 * marked (`aria-current="page"`, a teal tint), grouped by daily job (staff-nav-groups.ts).
 *
 * `mode="compact"` is the aside: below `sm` it is always the `w-16` rail (icons only, the label as
 * the link's name and tooltip); at `sm`+ it shows labels unless `collapsed`. `mode="full"` is the
 * sheet: always labels, and calls `onNavigate` on every link click so the sheet closes itself.
 */
function NavSections({
  sections,
  activeHref,
  openGroups,
  onToggleGroup,
  mode,
  collapsed = false,
  onNavigate,
}: {
  sections: StaffNavSection[];
  activeHref: string | null;
  openGroups: ReadonlySet<string>;
  onToggleGroup: (key: string) => void;
  mode: 'compact' | 'full';
  collapsed?: boolean;
  onNavigate?: () => void;
}) {
  // Text that only the expanded aside (or the sheet) has room for.
  const labelOnly = mode === 'compact' ? cn('hidden', !collapsed && 'sm:block') : undefined;
  const labelOnlyFlex = mode === 'compact' ? cn('hidden', !collapsed && 'sm:flex') : 'flex';
  // A closed group's links: gone from the sheet and the expanded aside, but the icon rail (below
  // sm, or collapsed) keeps every icon — it has no headings to open.
  // (The sheet uses the hidden attribute: those links are truly gone, for assistive tech too.)
  const closedLinks = mode === 'full' || collapsed ? undefined : 'sm:hidden';

  if (sections.length === 0) {
    return (
      <p className={cn('px-2 text-sm text-ink-muted', labelOnly)}>
        No areas registered yet — see web/README.md.
      </p>
    );
  }

  return (
    <nav aria-label="Main" className="flex flex-col gap-1">
      {sections.map((section) => {
        const open = !section.label || openGroups.has(section.key);
        const holdsCurrent = section.items.some((item) => item.href === activeHref);
        const listId = `staff-nav-${mode}-${section.key}`;
        return (
          <div
            key={section.key}
            className={cn('flex flex-col gap-0.5', open && section.label && 'pb-2')}
          >
            {section.label && (
              <>
                {/* text-ink-muted, not text-ink-faint (HO-CAT-002 E17/UI-6): 4.5:1 on the surface. */}
                <button
                  type="button"
                  aria-expanded={open}
                  aria-controls={listId}
                  onClick={() => onToggleGroup(section.key)}
                  className={cn(
                    'h-8 w-full items-center justify-between gap-2 rounded-[8px] px-2.5 text-left text-[11px] font-bold uppercase tracking-[0.09em] text-ink-muted transition-colors hover:bg-surface-sunken hover:text-ink',
                    labelOnlyFlex,
                  )}
                >
                  <span className="truncate">{section.label}</span>
                  <span className="flex flex-none items-center gap-2">
                    {!open && holdsCurrent && (
                      <span aria-hidden="true" className="size-1.5 rounded-full bg-accent" />
                    )}
                    <ChevronDown
                      size={15}
                      aria-hidden="true"
                      className={cn(
                        'transition-transform duration-200 motion-reduce:transition-none',
                        !open && '-rotate-90',
                      )}
                    />
                  </span>
                </button>
                {mode === 'compact' && (
                  <span
                    aria-hidden="true"
                    className={cn('mx-3 my-1 block h-px bg-border', !collapsed && 'sm:hidden')}
                  />
                )}
              </>
            )}
            <div
              id={listId}
              hidden={mode === 'full' && !open}
              className={cn('flex flex-col gap-0.5', !open && closedLinks)}
            >
              {section.items.map((item) => {
                const Icon = (item.icon && NAV_ICONS[item.icon]) || PawPrint;
                const active = item.href === activeHref;
                return (
                  <Link
                    key={item.href}
                    href={item.href}
                    onClick={onNavigate}
                    title={mode === 'compact' ? item.label : undefined}
                    aria-current={active ? 'page' : undefined}
                    // The accessible name is always the full label, whichever width is showing.
                    aria-label={mode === 'compact' ? item.label : undefined}
                    className={cn(
                      'group flex h-9 items-center gap-3 rounded-[10px] px-2.5 text-sm font-medium no-underline transition-colors',
                      mode === 'compact' && cn('justify-center', !collapsed && 'sm:justify-start'),
                      active
                        ? 'bg-accent-soft text-accent-ink'
                        : 'text-ink-muted hover:bg-surface-sunken hover:text-ink',
                    )}
                  >
                    <Icon
                      size={18}
                      strokeWidth={active ? 2.2 : 1.8}
                      aria-hidden="true"
                      className={cn(
                        'flex-none',
                        active ? 'text-accent' : 'text-ink-faint group-hover:text-ink-muted',
                      )}
                    />
                    <span
                      aria-hidden={mode === 'compact' ? true : undefined}
                      className={cn('truncate', labelOnly)}
                    >
                      {item.label}
                    </span>
                  </Link>
                );
              })}
            </div>
          </div>
        );
      })}
    </nav>
  );
}
