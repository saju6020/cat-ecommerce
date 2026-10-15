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
  // HO-EMR-002 §11.8/§13 — three entries only (ClinicalCatalogue.Manage: Vet and Administrator).
  { label: 'Diagnosis codes', href: '/staff/admin/clinical/diagnosis-codes', roles: ['Administrator', 'Vet'] },
  { label: 'Lab analytes', href: '/staff/admin/clinical/lab-analytes', roles: ['Administrator', 'Vet'] },
  { label: 'Note templates', href: '/staff/admin/clinical/note-templates', roles: ['Administrator', 'Vet'] },
  // HO-BIL-002 §13 — one entry only. Billing.View reads it; BillingProfile.Manage (Administrator) saves.
  {
    label: 'Billing profile',
    href: '/staff/admin/billing-profile',
    roles: ['Administrator', 'Accountant'],
  }, // Billing.View
  // HO-PHM-002 §13 — one entry only. Formulary.View reads the list; Formulary.Manage (Administrator) edits.
  {
    label: 'Formulary',
    href: '/staff/admin/formulary',
    roles: ['Administrator', 'Reception', 'Nurse', 'Vet', 'Inventory'],
  }, // Formulary.View
  // HO-ORD-002 §12.2/§13 — four entries (roles per HO-ORD-001 §12.3: DeliveryZones.View, Couriers.Manage,
  // Discounts.View, Orders.ManagePaymentSettings).
  {
    label: 'Delivery zones',
    href: '/staff/admin/delivery-zones',
    roles: ['Administrator', 'ShopManager', 'Accountant'],
  },
  {
    label: 'Discount codes',
    href: '/staff/admin/discount-codes',
    roles: ['Administrator', 'ShopManager', 'Accountant'],
  },
  { label: 'Couriers', href: '/staff/admin/couriers', roles: ['Administrator', 'ShopManager'] },
  { label: 'Payment settings', href: '/staff/admin/payment-settings', roles: ['Administrator'] },
  // HO-XCT-002 §11.5/§13 — three entries only (Retention.View: Administrator and Accountant;
  // Operations.View: Administrator; Telemetry.View: Administrator and ShopManager).
  {
    label: 'Retention',
    href: '/staff/admin/retention',
    roles: ['Administrator', 'Accountant'],
  },
  {
    label: 'Backup & restore',
    href: '/staff/admin/operations/backup',
    roles: ['Administrator'],
  },
  {
    label: 'Performance',
    href: '/staff/admin/performance',
    roles: ['Administrator', 'ShopManager'],
  },
  // HO-RPT-002 §11.1/§13 — one entry only (Reports.Schedule: Administrator).
  { label: 'Report schedules', href: '/staff/admin/report-schedules', roles: ['Administrator'] },
];
