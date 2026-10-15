import { PortalDashboard } from '@/components/portal/dashboard/PortalDashboard';

/**
 * `/portal` — HO-POR-002 §2 (US-POR-01): the portal home. Inside `(portal)`, so the warm customer
 * layer (`data-theme="customer"`), the header with the inbox badge and `CustomerSessionGuard`
 * come from the layout. The page itself is a thin shell; the dashboard is one client component
 * over one request.
 */
export default function PortalHomePage() {
  return <PortalDashboard />;
}
