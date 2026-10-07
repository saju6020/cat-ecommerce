/**
 * Shared navigation registry types.
 *
 * See web/README.md "Adding a functional area" — the registry is split one file per area
 * specifically so that up to four areas can be built in parallel, in separate worktrees,
 * without ever editing the same file. Only `nav/index.ts` imports every area, and its content
 * is fixed by this scaffold (one import + one array entry per area, all 18 already present) —
 * no future area should need to touch it.
 */

/** One of the 18 functional areas this product is split into (docs handover set). */
export type NavAreaKey =
  | 'accounts'
  | 'cat-profiles'
  | 'administration'
  | 'notifications'
  | 'appointments'
  | 'file-storage'
  | 'shop'
  | 'inventory'
  | 'consultation'
  | 'pharmacy'
  | 'surgery'
  | 'billing'
  | 'orders'
  | 'boarding'
  | 'subscriptions'
  | 'portal'
  | 'reporting'
  | 'cross-cutting';

export interface NavItem {
  /** Visible label shown in the sidebar / header nav. */
  label: string;
  /** App Router path, e.g. "/staff/cats". Must exist once the area's routes are built. */
  href: string;
  /**
   * Optional icon name from `lucide-react` (kept as a string, not a component, so this file
   * has no React/JSX dependency and can be imported from server or client code alike).
   */
  icon?: string;
  /**
   * Per-item override of the owning `NavArea.audience` — added by HO-CAT-002 for areas (like
   * Cat Profiles) whose single `audience: 'both'` area mixes staff-only and portal-only screens
   * in one `items` array. Omit to inherit the area's `audience`, which is exactly today's
   * behaviour for every area that doesn't set this — so this is additive, not a breaking change.
   * `StaffShell`/`PortalHeader` (`components/common/`) read this with that same fallback.
   */
  audience?: 'staff' | 'portal' | 'both';
}

export interface NavArea {
  /** Stable key — matches this file's name under nav/areas/. */
  key: NavAreaKey;
  /** Section heading shown above this area's items. */
  label: string;
  /** Which shell renders this area's items. Adjust when the area adds real items. */
  audience: 'staff' | 'portal' | 'both';
  /** Empty until the owning area adds its screens — see web/README.md. */
  items: NavItem[];
}
