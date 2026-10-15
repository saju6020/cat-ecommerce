import type { NavArea } from '../types';

/**
 * Consultation navigation entries.
 *
 * Owned entirely by the Consultation area — this is the ONE file that area adds to when it builds
 * its screens (web/README.md "Adding a functional area"). No other area, and no shared file,
 * should ever need to change because of this one.
 */
export const consultationNav: NavArea = {
  key: 'consultation',
  label: 'Consultation',
  audience: 'staff',
  // HO-EMR-002 §13 names the staff layout for two nav entries; the shells read this registry instead
  // of hard-coding links (HO-CAT-002 §8.3), so the entries live here and the layout is unchanged.
  items: [
    { label: 'Visits', href: '/staff/visits', icon: 'stethoscope', audience: 'staff' },
    { label: 'Diagnostics', href: '/staff/diagnostics', icon: 'flask-conical', audience: 'staff' },
  ],
};
