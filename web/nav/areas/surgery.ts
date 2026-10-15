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
  audience: 'both',
  items: [
    // HO-SRG-002 §13 names the two layouts (one nav entry each), but the shells' nav lives in this
    // registry (the deviation HO-PHM-002, HO-IMP-002 and HO-BIL-002 recorded): neither layout is edited.
    { label: 'Surgery', href: '/staff/surgeries', icon: 'scissors', audience: 'staff' },
    { label: 'Surgeries', href: '/portal/surgeries', icon: 'scissors', audience: 'portal' },
  ],
};
