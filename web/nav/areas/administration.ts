import type { NavArea } from '../types';

/**
 * Administration navigation entries.
 *
 * Owned entirely by the Administration area — this is the ONE file that area adds to when it builds
 * its screens (web/README.md "Adding a functional area"). No other area, and no shared file,
 * should ever need to change because of this one.
 *
 * HO-ADM-002 — not named in that handover's own §13 allowed-scope list (a dated Amended row
 * records this); without an entry here `/staff/admin/**` and `/staff/leave` would be reachable
 * only by typing the URL directly, which every other area's own sidebar entry avoids. Kept to two
 * items rather than one per admin sub-route: `AdminNav` (`components/admin/AdminNav.tsx`) is the
 * section's own internal navigation once inside it, role-filtered the same presentation-only way.
 */
export const administrationNav: NavArea = {
  key: 'administration',
  label: 'Administration',
  audience: 'staff',
  items: [
    { label: 'Administration', href: '/staff/admin/branches', icon: 'building' },
    { label: 'My leave', href: '/staff/leave', icon: 'calendar' },
  ],
};
