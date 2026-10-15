import type { SectionNavItem } from './SectionNav';

/**
 * `components/inventory/inventoryNavItems.ts` — HO-IMP-002 §11.1 (`procurementNavItems`,
 * `inventoryNavItems`). The ONE presentation map the two section navs filter against. It mirrors
 * HO-IMP-001 §12.3's `ActionRoles` **by name only** — convenience, not authorization (ADR-FE-039).
 * Role names are the staff roles of SRS Appendix D, spelled as the server spells them.
 */
export const STOCK_VIEW_ROLES = [
  'Reception',
  'Nurse',
  'Vet',
  'Caretaker',
  'Inventory',
  'ShopManager',
  'Administrator',
] as const; // Stock.View

export const PROCUREMENT_VIEW_ROLES = [
  'Inventory',
  'ShopManager',
  'Accountant',
  'Administrator',
] as const; // Procurement.View, InventoryReports.View

export const INVENTORY_MANAGER_ROLES = ['Inventory', 'Administrator'] as const; // Recalls.Manage, Stock.Receive…

export const inventoryNavItems: readonly SectionNavItem[] = [
  { label: 'Overview', href: '/staff/inventory', roles: STOCK_VIEW_ROLES },
  { label: 'Stock', href: '/staff/inventory/stock', roles: STOCK_VIEW_ROLES },
  { label: 'Batches', href: '/staff/inventory/batches', roles: STOCK_VIEW_ROLES },
  { label: 'Receipts', href: '/staff/inventory/receipts', roles: STOCK_VIEW_ROLES },
  { label: 'Transfers', href: '/staff/inventory/transfers', roles: STOCK_VIEW_ROLES },
  { label: 'Adjustments', href: '/staff/inventory/adjustments', roles: STOCK_VIEW_ROLES },
  { label: 'Counts', href: '/staff/inventory/counts', roles: STOCK_VIEW_ROLES },
  { label: 'Ledger', href: '/staff/inventory/ledger', roles: STOCK_VIEW_ROLES },
  { label: 'Alerts', href: '/staff/inventory/alerts', roles: STOCK_VIEW_ROLES },
  { label: 'Reports', href: '/staff/inventory/reports/near-expiry', roles: PROCUREMENT_VIEW_ROLES },
  { label: 'Recalls', href: '/staff/inventory/recalls', roles: INVENTORY_MANAGER_ROLES },
  { label: 'Storage breaches', href: '/staff/inventory/storage-breaches', roles: STOCK_VIEW_ROLES },
  { label: 'Storage profiles', href: '/staff/inventory/storage-profiles', roles: STOCK_VIEW_ROLES },
  { label: 'Reorder policies', href: '/staff/inventory/reorder-policies', roles: STOCK_VIEW_ROLES },
  { label: 'Locations', href: '/staff/inventory/locations', roles: STOCK_VIEW_ROLES },
];

export const procurementNavItems: readonly SectionNavItem[] = [
  { label: 'Suppliers', href: '/staff/procurement/suppliers', roles: PROCUREMENT_VIEW_ROLES },
  {
    label: 'Purchase orders',
    href: '/staff/procurement/purchase-orders',
    roles: PROCUREMENT_VIEW_ROLES,
  },
  { label: 'Consignments', href: '/staff/procurement/consignments', roles: PROCUREMENT_VIEW_ROLES },
  { label: 'Reorder', href: '/staff/procurement/reorder', roles: PROCUREMENT_VIEW_ROLES },
];
