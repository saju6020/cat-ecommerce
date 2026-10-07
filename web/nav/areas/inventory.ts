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
  items: [],
};
