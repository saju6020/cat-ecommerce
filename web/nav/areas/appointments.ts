import type { NavArea } from '../types';

/**
 * Appointments navigation entries.
 *
 * Owned entirely by the Appointments area — this is the ONE file that area adds to when it builds
 * its screens (web/README.md "Adding a functional area"). No other area, and no shared file,
 * should ever need to change because of this one.
 *
 * HO-APT-002 §13 names the two layouts for "nav entries"; the shells read this registry instead of
 * hard-coding links (HO-CAT-002 §8.3), so the entries live here and the layouts are unchanged
 * (recorded in HO-APT-002 §1 Amended).
 */
export const appointmentsNav: NavArea = {
  key: 'appointments',
  label: 'Appointments',
  audience: 'both',
  items: [
    { label: 'Front desk', href: '/staff/front-desk', icon: 'clipboard-list', audience: 'staff' },
    { label: 'Appointments', href: '/staff/appointments', icon: 'calendar', audience: 'staff' },
    { label: 'Appointments', href: '/portal/appointments', icon: 'calendar', audience: 'portal' },
    { label: 'Book a visit', href: '/book', icon: 'calendar-plus', audience: 'portal' },
  ],
};
