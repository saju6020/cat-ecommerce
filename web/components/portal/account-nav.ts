/**
 * The customer account sidebar (owner picked option A "Account sidebar", 2026-10-17; the approved
 * design https://claude.ai/artifact/K3WKkwpv67BqLJJdSr3bUh): Overview, then three groups. The same
 * list drives the desktop sidebar and the phone's row of pills.
 */
export type AccountIcon =
  | 'home'
  | 'cat'
  | 'calendar'
  | 'pill'
  | 'scissors'
  | 'bed'
  | 'package'
  | 'heart'
  | 'star'
  | 'receipt'
  | 'user';

export interface AccountLink {
  label: string;
  href: string;
  icon: AccountIcon;
}

export interface AccountGroup {
  key: string;
  /** null = no heading (Overview, at the top). */
  label: string | null;
  links: AccountLink[];
}

export const ACCOUNT_HOME = '/portal';
/** Where the account opens — the name on the shop and signing in land here (owner, 2026-10-17). */
export const ACCOUNT_LANDING = '/portal/orders';

export const ACCOUNT_GROUPS: readonly AccountGroup[] = [
  {
    key: 'overview',
    label: null,
    links: [{ label: 'Overview', href: ACCOUNT_HOME, icon: 'home' }],
  },
  {
    key: 'care',
    label: 'My cats & care',
    links: [
      { label: 'My cats', href: '/portal/cats', icon: 'cat' },
      { label: 'Appointments', href: '/portal/appointments', icon: 'calendar' },
      { label: 'Prescriptions', href: '/portal/prescriptions', icon: 'pill' },
      { label: 'Surgeries', href: '/portal/surgeries', icon: 'scissors' },
      { label: 'Boarding', href: '/portal/boarding', icon: 'bed' },
    ],
  },
  {
    key: 'shopping',
    label: 'Shopping',
    links: [
      { label: 'Orders', href: '/portal/orders', icon: 'package' },
      { label: 'Wishlist', href: '/portal/wishlist', icon: 'heart' },
      { label: 'My reviews', href: '/portal/reviews', icon: 'star' },
    ],
  },
  {
    key: 'account',
    label: 'Account',
    links: [
      { label: 'Billing', href: '/portal/billing', icon: 'receipt' },
      { label: 'Profile & security', href: '/portal/account/profile', icon: 'user' },
    ],
  },
];

/** The phone's row of pills starts with the landing page, Orders (the approved design). */
export const PHONE_PILL_ORDER: readonly string[] = [
  '/portal/orders',
  '/portal/cats',
  '/portal/appointments',
  '/portal/prescriptions',
  '/portal/surgeries',
  '/portal/boarding',
  '/portal/wishlist',
  '/portal/reviews',
  '/portal/billing',
  '/portal/account/profile',
  ACCOUNT_HOME,
];

/**
 * The link for the current page: the longest matching prefix wins; Overview only matches /portal
 * itself; any /portal/account/* page is Profile & security.
 */
export function activeAccountHref(pathname: string): string | null {
  if (pathname === ACCOUNT_HOME) return ACCOUNT_HOME;
  if (pathname.startsWith('/portal/account')) return '/portal/account/profile';
  let best: string | null = null;
  for (const link of ACCOUNT_GROUPS.flatMap((g) => g.links)) {
    if (link.href === ACCOUNT_HOME) continue;
    const hit = pathname === link.href || pathname.startsWith(`${link.href}/`);
    if (hit && (!best || link.href.length > best.length)) best = link.href;
  }
  return best;
}
