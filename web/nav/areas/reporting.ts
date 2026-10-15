import type { NavArea } from '../types';

/**
 * The roles that hold `Reports.Any` (ActionRoles.Reports.cs): everyone who can open at least one
 * report category. A Nurse has the dashboard but no report category, so the Reports entry is
 * hidden for a Nurse (owner decision 2026-10-14). Presentation only: the page still renders
 * `PermissionDeniedState` on the server's 403 (defence in depth).
 */
export const REPORT_ROLES = [
  'Reception',
  'Vet',
  'Caretaker',
  'Inventory',
  'ShopManager',
  'Accountant',
  'Administrator',
] as const;

/**
 * Reporting navigation entries.
 *
 * Owned entirely by the Reporting area — this is the ONE file that area adds to when it builds
 * its screens (web/README.md "Adding a functional area").
 *
 * HO-RPT-002 §11.1 — two entries: Home (the role dashboard, ADR-FE-130; every staff role) and
 * Reports (the catalogue; `REPORT_ROLES` only, via the optional `roles` on `NavItem`).
 * "Report schedules" is an `AdminNav` entry (Administrator only), not a main-nav one.
 */
export const reportingNav: NavArea = {
  key: 'reporting',
  label: 'Reporting',
  audience: 'staff',
  items: [
    { label: 'Home', href: '/staff', icon: 'home' },
    { label: 'Reports', href: '/staff/reports', icon: 'bar-chart', roles: REPORT_ROLES },
  ],
};
