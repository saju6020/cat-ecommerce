/**
 * `components/admin/adminNavItems.ts` — HO-ADM-002 §11.1. The ONE presentation map `AdminNav`
 * filters against. Mirrors HO-ADM-001 §12.3's `ActionRoles` table **by name only** — this is
 * convenience, not authorization: every page still renders `PermissionDeniedState` on a server
 * `403` (U-A1), and hiding a link here never substitutes for the check the server already makes
 * (ADR-043/ADR-044, HO-ACC-002 ADR-FE-009).
 *
 * `STAFF_ROLE_IDS` is SRS Appendix D's eight staff roles, spelled the way `ActionRoles`/
 * `StaffRoles.All` spell them (verified against HO-ADM-001 §12.3's own role columns and the
 * existing `RoleAssignmentField`/staff-admin mock role ids). Only used here, for presentation —
 * the actual, authoritative role catalogue always comes from `GET /api/staff-roles`
 * (`useStaffRoleCatalogue`, HO-ACC-002 ADR-FE-009), which every checkbox list in this area
 * (`ServiceForm`'s qualified roles) reads from instead of this constant.
 */
export const STAFF_ROLE_IDS = [
  'Administrator',
  'Reception',
  'Nurse',
  'Vet',
  'Caretaker',
  'Inventory',
  'ShopManager',
  'Accountant',
] as const;

export interface AdminNavItem {
  label: string;
  href: string;
  /** The staff roles HO-ADM-001 §12.3 grants the underlying action to — presentation only. */
  roles: readonly string[];
}

export const adminNavItems: AdminNavItem[] = [
  { label: 'Branches', href: '/staff/admin/branches', roles: STAFF_ROLE_IDS }, // Branches.View
  { label: 'Leave approvals', href: '/staff/admin/leave', roles: ['Administrator'] }, // Leave.Decide
  { label: 'Services', href: '/staff/admin/services', roles: STAFF_ROLE_IDS }, // Services.View
  {
    label: 'Tax classes',
    href: '/staff/admin/tax-classes',
    roles: ['Administrator', 'Accountant', 'ShopManager'],
  }, // TaxClasses.View
  { label: 'Settings', href: '/staff/admin/settings', roles: ['Administrator'] }, // Settings.View
  { label: 'Audit log', href: '/staff/admin/audit-log', roles: ['Administrator', 'Accountant'] }, // AuditLog.View
  { label: 'Content', href: '/staff/admin/content', roles: ['Administrator', 'ShopManager'] }, // Content.Manage
  { label: 'Policies', href: '/staff/admin/policies', roles: ['Administrator', 'ShopManager'] }, // Content.Manage (list — see PolicyList's own Administrator-only create/publish gate)
  { label: 'System', href: '/staff/admin/system', roles: ['Administrator'] }, // System.ManageMaintenance
  // HO-NTF-002 §8.1/§13 — one entry only.
  {
    label: 'Message templates',
    href: '/staff/admin/notification-templates',
    roles: ['Administrator', 'ShopManager'],
  }, // NotificationTemplates.View
];
