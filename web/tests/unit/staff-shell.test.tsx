import { beforeEach, describe, expect, it, vi } from 'vitest';
import { render, screen, waitFor, within } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import userEvent from '@testing-library/user-event';
import { StaffShell } from '@/components/common/staff-shell';

// HO-ACC-002 §11.9: StaffShell now reads the staff session via `useStaffSession()` (TanStack
// Query), replacing the removed `lib/identity.ts` (ADR-FE-004) — every render needs a
// QueryClientProvider ancestor. No staff session cookie is set in these tests, so the query
// resolves to `null` ("not signed in") — fine here, since none of these tests assert on the
// header's name/sign-out area, only on the nav sheet this file exists to cover.
function renderShell(children: React.ReactNode) {
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return render(<QueryClientProvider client={queryClient}>{children}</QueryClientProvider>);
}

// StaffShell watches `usePathname()` to close the below-sm sheet on any route change, including
// the browser's own Back/Forward (CODE_REVIEW gate MEDIUM, HO-CAT-002 2nd pass) — see
// `staff-shell-sheet-closes-on-back-navigation.spec.ts` for the route-change regression itself.
// This file's own tests never navigate, so a fixed pathname is all they need.
const pathname = '/staff/cats';
vi.mock('next/navigation', () => ({
  usePathname: () => pathname,
}));

/**
 * Rewritten for the Radix `Dialog`-based below-`sm` sheet (HO-CAT-002 TEST gate, 7th pass) — the
 * hand-rolled drawer this file used to cover (`override` tri-state, a `matchMedia`-driven `inert`
 * effect, a hand-rolled scrim, a manual `Escape` `keydown` listener) is gone. `StaffShell` now has
 * exactly two pieces of interactive state: `collapsed` (the sm+ in-flow toggle, unchanged in
 * behaviour from before) and `sheetOpen` (a real, controlled Radix `Dialog`). Focus-trapping,
 * `Escape`-to-close, overlay-click-to-close and focus-return-to-trigger for the sheet all come from
 * Radix itself — already covered by `components/ui/dialog.tsx`'s own contract and exercised
 * end-to-end by `confirm-dialog.test.tsx` — so this file tests StaffShell's own integration (which
 * button opens what, what the sheet contains, that a nav click closes it, and the sm+ toggle's
 * pre-existing focus-stability guarantee) rather than re-proving Radix's guarantees itself.
 *
 * jsdom applies no Tailwind CSS, so both the below-sm trigger and the sm+ toggle are always in the
 * DOM here (real browsers CSS-hide whichever doesn't match the current viewport — see the e2e
 * specs for that).
 */

// StaffShell only reaches `window.matchMedia` while the sheet is open, to close it if the viewport
// crosses to sm+ (jsdom has no matchMedia at all). `stubMatchMedia` records the registered
// listener so a test can simulate that resize.
function stubMatchMedia() {
  let listener: ((event: MediaQueryListEvent) => void) | null = null;
  window.matchMedia = ((query: string) => ({
    matches: false,
    media: query,
    onchange: null,
    addEventListener: (_event: string, cb: (event: MediaQueryListEvent) => void) => {
      listener = cb;
    },
    removeEventListener: () => {
      listener = null;
    },
    addListener: () => {},
    removeListener: () => {},
    dispatchEvent: () => false,
  })) as typeof window.matchMedia;
  return {
    fireMinWidth640: (matches: boolean) => listener?.({ matches } as MediaQueryListEvent),
  };
}

beforeEach(() => {
  stubMatchMedia();
});

function mobileTrigger() {
  return screen.getByRole('button', { name: 'Expand menu' });
}

describe('StaffShell — sm and up collapse toggle (unchanged)', () => {
  it('starts expanded', () => {
    renderShell(<StaffShell>content</StaffShell>);
    // Two controls share the "expand/collapse menu" accessible name below sm vs sm+; the sm+ one
    // starts expanded (aria-expanded="true"), matching the pure-CSS `sm:w-60` default.
    const buttons = screen.getAllByRole('button', { name: /(expand|collapse) menu/i });
    const desktop = buttons.find((b) => b.getAttribute('aria-label') === 'Collapse menu');
    expect(desktop).toBeDefined();
    expect(desktop).toHaveAttribute('aria-expanded', 'true');
  });

  it('keeps focus on the same element across the first and later clicks', async () => {
    const user = userEvent.setup();
    renderShell(<StaffShell>content</StaffShell>);
    // Captured once, then re-checked by direct reference (not re-queried by role/name, since the
    // accessible name itself flips on click) — a remount would lose DOM focus even though the
    // element's props/label look the same afterwards, so `toHaveFocus()` on this same reference is
    // the identity check that matters here.
    const toggle = screen.getByRole('button', { name: 'Collapse menu' });

    await user.click(toggle);
    expect(toggle).toHaveFocus();
    expect(toggle).toHaveAttribute('aria-expanded', 'false');
    expect(toggle).toHaveAttribute('aria-label', 'Expand menu');

    await user.click(toggle);
    expect(toggle).toHaveFocus();
    expect(toggle).toHaveAttribute('aria-expanded', 'true');
    expect(toggle).toHaveAttribute('aria-label', 'Collapse menu');
  });
});

describe('StaffShell — below-sm nav sheet (Radix Dialog)', () => {
  it('is closed by default and opens on the rail trigger', async () => {
    const user = userEvent.setup();
    renderShell(<StaffShell>content</StaffShell>);
    expect(screen.queryByRole('dialog')).not.toBeInTheDocument();

    await user.click(mobileTrigger());
    expect(screen.getByRole('dialog')).toBeInTheDocument();
  });

  it('has an accessible title and the full, un-abbreviated nav labels', async () => {
    const user = userEvent.setup();
    renderShell(<StaffShell>content</StaffShell>);
    await user.click(mobileTrigger());

    const dialog = screen.getByRole('dialog');
    expect(dialog).toHaveAccessibleName(/menu/i);
    expect(screen.getByRole('link', { name: 'Cats' })).toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'Vaccine catalogue' })).toBeInTheDocument();
  });

  it('closes when a nav link inside it is clicked', async () => {
    const user = userEvent.setup();
    renderShell(<StaffShell>content</StaffShell>);
    await user.click(mobileTrigger());

    await user.click(screen.getByRole('link', { name: 'Cats' }));
    await waitFor(() => expect(screen.queryByRole('dialog')).not.toBeInTheDocument());
  });

  it('Escape closes it and returns focus to the rail trigger (Radix default)', async () => {
    const user = userEvent.setup();
    renderShell(<StaffShell>content</StaffShell>);
    const trigger = mobileTrigger();
    await user.click(trigger);
    expect(screen.getByRole('dialog')).toBeInTheDocument();

    await user.keyboard('{Escape}');
    await waitFor(() => expect(screen.queryByRole('dialog')).not.toBeInTheDocument());
    await waitFor(() => expect(trigger).toHaveFocus());
  });

  it('closes if the viewport crosses to sm+ while it is open, rather than stranding the user in it', async () => {
    const user = userEvent.setup();
    const { fireMinWidth640 } = stubMatchMedia();
    renderShell(<StaffShell>content</StaffShell>);
    await user.click(mobileTrigger());
    expect(screen.getByRole('dialog')).toBeInTheDocument();

    fireMinWidth640(true);
    await waitFor(() => expect(screen.queryByRole('dialog')).not.toBeInTheDocument());
  });

  it('does not touch matchMedia while closed', () => {
    const spy = vi.spyOn(window, 'matchMedia');
    renderShell(<StaffShell>content</StaffShell>);
    expect(spy).not.toHaveBeenCalled();
  });
});

// Owner, 2026-10-16: groups open and close; only the current page's group starts open, so the
// menu needs no scrollbar at first. (pathname is /staff/cats here, so Clinic is the open group.)
describe('StaffShell — expandable menu groups', () => {
  function asideGroup(name: string) {
    return within(document.querySelector('aside')!).getByRole('button', { name });
  }

  it("opens only the current page's group", () => {
    renderShell(<StaffShell>content</StaffShell>);
    expect(asideGroup('Clinic')).toHaveAttribute('aria-expanded', 'true');
    for (const closed of ['Front desk', 'Pharmacy', 'Shop & orders', 'Team & settings'])
      expect(asideGroup(closed)).toHaveAttribute('aria-expanded', 'false');
    const current = document.querySelector('aside [aria-current="page"]');
    expect(current).toHaveAttribute('aria-label', 'Cats');
  });

  it('a group heading opens and closes its links, and says which list it controls', async () => {
    const user = userEvent.setup();
    renderShell(<StaffShell>content</StaffShell>);
    const pharmacy = asideGroup('Pharmacy');
    const list = document.getElementById(pharmacy.getAttribute('aria-controls')!)!;
    expect(list).toHaveClass('sm:hidden');

    await user.click(pharmacy);
    expect(pharmacy).toHaveAttribute('aria-expanded', 'true');
    expect(list).not.toHaveClass('sm:hidden');

    await user.click(pharmacy);
    expect(pharmacy).toHaveAttribute('aria-expanded', 'false');
  });

  it('in the phone sheet a closed group hides its links and opening it shows them', async () => {
    const user = userEvent.setup();
    renderShell(<StaffShell>content</StaffShell>);
    await user.click(mobileTrigger());
    const sheet = screen.getByRole('dialog');
    expect(within(sheet).queryByRole('link', { name: 'Orders' })).not.toBeInTheDocument();

    await user.click(within(sheet).getByRole('button', { name: 'Shop & orders' }));
    expect(within(sheet).getByRole('link', { name: 'Orders' })).toBeVisible();
  });
});

// Owner, 2026-10-19: the staff area shows the same PurrCare logo as the shop (cat badge + "Shop" tag), linking to /staff.
describe('StaffShell — the PurrCare logo', () => {
  it('is the cat-badge logo, linking to the staff home', () => {
    renderShell(<StaffShell>content</StaffShell>);
    const logo = screen.getByRole('link', { name: 'PurrCare Shop' });
    expect(logo).toHaveAttribute('href', '/staff');
    expect(logo.querySelector('[aria-hidden="true"].bg-accent')).not.toBeNull();
  });
});
