import type { NavArea } from '../types';

/**
 * Pharmacy navigation entries.
 *
 * Owned entirely by the Pharmacy area — this is the ONE file that area adds to when it builds
 * its screens (web/README.md "Adding a functional area"). No other area, and no shared file,
 * should ever need to change because of this one.
 */
export const pharmacyNav: NavArea = {
  key: 'pharmacy',
  label: 'Pharmacy',
  audience: 'staff',
  items: [],
};
