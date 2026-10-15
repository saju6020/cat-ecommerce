import type { NavArea } from '../types';

/**
 * My PurrCare navigation entries.
 *
 * Owned entirely by the My PurrCare area — this is the ONE file that area adds to when it builds
 * its screens (web/README.md "Adding a functional area"). No other area, and no shared file,
 * should ever need to change because of this one.
 *
 * HO-POR-002 §2 "nav entry Home -> /portal": the shells read this registry (the layout holds no
 * links, HO-APT-002's precedent), so the entry lives here. Its position among the other areas'
 * portal items follows `nav/index.ts` registry order, which this handover may not change (§14).
 */
export const portalNav: NavArea = {
  key: 'portal',
  label: 'My PurrCare',
  audience: 'portal',
  items: [{ label: 'Home', href: '/portal', icon: 'home', audience: 'portal' }],
};
