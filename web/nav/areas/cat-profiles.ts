import type { NavArea } from '../types';

/**
 * Cat Profiles navigation entries.
 *
 * Owned entirely by the Cat Profiles area — this is the ONE file that area adds to when it builds
 * its screens (web/README.md "Adding a functional area"). No other area, and no shared file,
 * should ever need to change because of this one.
 */
export const catProfilesNav: NavArea = {
  key: 'cat-profiles',
  label: 'Cat Profiles',
  audience: 'both',
  items: [
    { label: 'Cats', href: '/staff/cats', icon: 'cat', audience: 'staff' },
    { label: 'Vaccine catalogue', href: '/staff/vaccines', icon: 'syringe', audience: 'staff' },
    { label: 'My cats', href: '/portal/cats', icon: 'cat', audience: 'portal' },
  ],
};
