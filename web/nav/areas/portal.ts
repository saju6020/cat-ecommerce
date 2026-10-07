import type { NavArea } from '../types';

/**
 * My PurrCare navigation entries.
 *
 * Owned entirely by the My PurrCare area — this is the ONE file that area adds to when it builds
 * its screens (web/README.md "Adding a functional area"). No other area, and no shared file,
 * should ever need to change because of this one.
 */
export const portalNav: NavArea = {
  key: 'portal',
  label: 'My PurrCare',
  audience: 'portal',
  items: [],
};
