import type { NavArea } from '../types';

/**
 * Cross-cutting navigation entries.
 *
 * Owned entirely by the Cross-cutting area — this is the ONE file that area adds to when it builds
 * its screens (web/README.md "Adding a functional area"). No other area, and no shared file,
 * should ever need to change because of this one.
 */
export const crossCuttingNav: NavArea = {
  key: 'cross-cutting',
  label: 'Cross-cutting',
  audience: 'staff',
  items: [],
};
