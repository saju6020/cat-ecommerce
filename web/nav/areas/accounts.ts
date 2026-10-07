import type { NavArea } from '../types';

/**
 * Accounts & Identity navigation entries.
 *
 * Owned entirely by the Accounts & Identity area — this is the ONE file that area adds to when it builds
 * its screens (web/README.md "Adding a functional area"). No other area, and no shared file,
 * should ever need to change because of this one.
 */
export const accountsNav: NavArea = {
  key: 'accounts',
  label: 'Accounts & Identity',
  audience: 'both',
  items: [
    { label: 'My account', href: '/portal/account/profile', icon: 'user', audience: 'portal' },
    {
      label: 'Register walk-in',
      href: '/staff/customers/new',
      icon: 'user-plus',
      audience: 'staff',
    },
    {
      label: 'Duplicate customers',
      href: '/staff/customers/duplicates',
      icon: 'copy',
      audience: 'staff',
    },
    {
      label: 'Staff accounts',
      href: '/staff/admin/staff-accounts',
      icon: 'shield',
      audience: 'staff',
    },
  ],
};
