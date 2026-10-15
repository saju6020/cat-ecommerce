import type { NavArea } from '../types';

/**
 * Orders navigation entries.
 *
 * Owned entirely by the Orders area — this is the ONE file that area adds to when it builds
 * its screens (web/README.md "Adding a functional area"). No other area, and no shared file,
 * should ever need to change because of this one.
 *
 * HO-ORD-002 §12.2: one portal entry (*Orders*) and three staff entries (*Orders*, *Returns*,
 * *POS*). The awaiting-payment list, reconciliation issues, refunds due and the pick list are tabs
 * of the staff Orders screens (`StaffOrdersTabs`), not sidebar entries; the four administration
 * screens are in the administration section's own navigation.
 */
export const ordersNav: NavArea = {
  key: 'orders',
  label: 'Orders',
  audience: 'both',
  items: [
    { label: 'Orders', href: '/portal/orders', icon: 'package', audience: 'portal' },
    { label: 'Orders', href: '/staff/orders', icon: 'package', audience: 'staff' },
    { label: 'Returns', href: '/staff/returns', icon: 'undo', audience: 'staff' },
    { label: 'POS', href: '/staff/pos', icon: 'shopping-cart', audience: 'staff' },
  ],
};
