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
  audience: 'both',
  items: [
    // HO-PHM-002 §13 names `(staff)/layout.tsx` ("three nav entries") and `(portal)/layout.tsx`
    // ("one nav entry"), but the shells' nav lives in this registry (the deviation HO-IMP-002 and
    // HO-BIL-002 recorded). The third staff entry, Formulary, is in `adminNavItems.ts`.
    { label: 'Pharmacy', href: '/staff/pharmacy', icon: 'pill', audience: 'staff' },
    {
      label: 'Controlled drugs',
      href: '/staff/pharmacy/controlled-drugs',
      icon: 'shield',
      audience: 'staff',
    },
    { label: 'Prescriptions', href: '/portal/prescriptions', icon: 'pill', audience: 'portal' },
  ],
};
