import type { NavArea } from '../types';

/**
 * Inventory navigation entries.
 *
 * Owned entirely by the Inventory area — this is the ONE file that area adds to when it builds
 * its screens (web/README.md "Adding a functional area"). No other area, and no shared file,
 * should ever need to change because of this one.
 */
export const inventoryNav: NavArea = {
  key: 'inventory',
  label: 'Inventory',
  audience: 'staff',
  items: [
    // HO-IMP-002 §13 names `(staff)/layout.tsx` for "two nav entries", but the shell's nav lives in
    // this registry (the same deviation HO-ADM-002 and HO-SHP-002 recorded); the alert badge lives
    // in the Inventory section's own tab row (`app/(staff)/staff/inventory/layout.tsx`) because the
    // shared sidebar has no badge slot.
    { label: 'Inventory', href: '/staff/inventory', icon: 'boxes' },
    { label: 'Procurement', href: '/staff/procurement/suppliers', icon: 'truck' },
  ],
};
