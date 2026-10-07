import type { NavArea } from '../types';

/**
 * Reporting navigation entries.
 *
 * Owned entirely by the Reporting area — this is the ONE file that area adds to when it builds
 * its screens (web/README.md "Adding a functional area"). No other area, and no shared file,
 * should ever need to change because of this one.
 */
export const reportingNav: NavArea = {
  key: 'reporting',
  label: 'Reporting',
  audience: 'staff',
  items: [],
};
