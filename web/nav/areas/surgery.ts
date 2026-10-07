import type { NavArea } from '../types';

/**
 * Surgery navigation entries.
 *
 * Owned entirely by the Surgery area — this is the ONE file that area adds to when it builds
 * its screens (web/README.md "Adding a functional area"). No other area, and no shared file,
 * should ever need to change because of this one.
 */
export const surgeryNav: NavArea = {
  key: 'surgery',
  label: 'Surgery',
  audience: 'staff',
  items: [],
};
