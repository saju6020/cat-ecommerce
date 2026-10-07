import type { NavArea } from '../types';

/**
 * Orders navigation entries.
 *
 * Owned entirely by the Orders area — this is the ONE file that area adds to when it builds
 * its screens (web/README.md "Adding a functional area"). No other area, and no shared file,
 * should ever need to change because of this one.
 */
export const ordersNav: NavArea = {
  key: 'orders',
  label: 'Orders',
  audience: 'both',
  items: [],
};
