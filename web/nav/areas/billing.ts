import type { NavArea } from '../types';

/**
 * Billing navigation entries.
 *
 * Owned entirely by the Billing area — this is the ONE file that area adds to when it builds
 * its screens (web/README.md "Adding a functional area"). No other area, and no shared file,
 * should ever need to change because of this one.
 */
export const billingNav: NavArea = {
  key: 'billing',
  label: 'Billing',
  audience: 'both',
  items: [
    // HO-BIL-002 §13: one staff entry and one portal entry (the registry, not the layouts, holds nav).
    { label: 'Billing', href: '/staff/billing', icon: 'receipt', audience: 'staff' },
    { label: 'Billing', href: '/portal/billing', icon: 'receipt', audience: 'portal' },
  ],
};
