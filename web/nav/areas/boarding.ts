import type { NavArea } from '../types';

/**
 * Boarding navigation entries.
 *
 * Owned entirely by the Boarding area — this is the ONE file that area adds to when it builds
 * its screens (web/README.md "Adding a functional area"). No other area, and no shared file,
 * should ever need to change because of this one.
 *
 * HO-BRD-002 §13 names `(portal)/layout.tsx` (one entry), `(staff)/layout.tsx` (two) and
 * `admin/layout.tsx` (the Boarding group) for nav entries; the shells read this registry instead of
 * hard-coding links (HO-CAT-002 §8.3), so the entries live here and the layouts are unchanged (the
 * same deviation HO-APT-002, HO-IMP-002 and HO-BIL-002 recorded; HO-BRD-002 §1 Amended). The admin
 * Boarding group is one staff entry, "Boarding setup", that opens the section's own tab row
 * (`app/(staff)/staff/admin/boarding/layout.tsx`: Units, Rate cards, Care routine).
 */
export const boardingNav: NavArea = {
  key: 'boarding',
  label: 'Boarding',
  audience: 'both',
  items: [
    { label: 'Boarding', href: '/staff/boarding', icon: 'bed', audience: 'staff' },
    { label: 'Care tasks', href: '/staff/boarding/tasks', icon: 'list-checks', audience: 'staff' },
    {
      label: 'Boarding setup',
      href: '/staff/admin/boarding/units',
      icon: 'settings',
      audience: 'staff',
    },
    { label: 'Boarding', href: '/portal/boarding', icon: 'bed', audience: 'portal' },
  ],
};
