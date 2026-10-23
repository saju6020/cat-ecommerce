import { PortalHeader } from '@/components/common/portal-header';
import { CustomerSessionGuard } from '@/components/portal/CustomerSessionGuard';
import { PolicyAcceptancePrompt } from '@/components/content/PolicyAcceptancePrompt';
import { InboxBadge } from '@/components/inbox/InboxBadge';
import { CartBadge } from '@/components/cart/CartBadge';
import { AccountFrame } from '@/components/portal/AccountSidebar';

// Owner-facing shell for every /portal/** route (HO-CAT-002 §8.3). `data-theme="customer"`
// applies the warm customer design-token layer (app/globals.css) — this is the ONLY *layout* it is
// applied on; it must never reach a staff layout or a shared component (a few HO-ACC-002 `(auth)`
// pages also apply it directly, at the page level, for the customer-facing auth screens — see
// that route group's own pages).
//
// HO-ACC-002 §11.5 / ADR-FE-001: `CustomerSessionGuard` now wraps every portal PAGE's content —
// the header renders regardless (it is chrome, not "portal content"), but `{children}` (the actual
// page) never renders until a session is confirmed present.
export default function PortalLayout({ children }: { children: React.ReactNode }) {
  return (
    <div data-theme="customer" className="min-h-screen bg-surface-page">
      {/* HO-NTF-002 §11.1/§13 — InboxBadge (rendered by the header once signed in); HO-ORD-002 §3.3/§20.1 — the CartBadge beside it. */}
      <PortalHeader
        sessionActions={
          <>
            {/* Round buttons with orange counts (the approved design); on a phone Messages is in the name menu. */}
            <CartBadge appearance="icon" />
            <InboxBadge appearance="icon" className="max-sm:hidden" />
          </>
        }
      />
      {/* Option A "Account sidebar" (owner, 2026-10-17): the account pages beside every /portal page. */}
      <AccountFrame>
        <CustomerSessionGuard>
          {/* HO-ADM-002 §11.6/§13 — the one scoped edit to this file: gates the whole portal
              behind any pending policy acceptance (US-ADM-07, OQ-3 — no dismiss). */}
          <PolicyAcceptancePrompt />
          {children}
        </CustomerSessionGuard>
      </AccountFrame>
    </div>
  );
}
