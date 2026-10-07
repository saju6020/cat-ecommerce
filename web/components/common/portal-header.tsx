'use client';

import Link from 'next/link';
import { navRegistry } from '@/nav';
import { useCustomerSession } from '@/lib/hooks/useSession';
import { SignOutButton } from '@/components/auth/SignOutButton';

/**
 * The customer-portal header (HO-CAT-002 §8.3 — "(portal)/layout.tsx: owner-facing shell —
 * simpler chrome"), matching the flat `.topnav` shape docs/design/_shared/support.js leaves
 * customer-portal screens in (no sidebar). Renders portal-audience nav items from the shared
 * registry — empty until an area adds its own portal screens.
 *
 * HO-ACC-002 §11.5/§8.4: `lib/identity.ts` is gone (ADR-FE-004) — the signed-in customer's name
 * and a `SignOutButton` come from the session query instead, the same query
 * `CustomerSessionGuard` already resolves for this route (shared cache, no extra request). While
 * the session is still resolving this renders with no name/sign-out yet, which is fine — the
 * chrome itself is not "portal content" (`CustomerSessionGuard` gates that, not this header).
 */
export function PortalHeader({ sessionActions }: { sessionActions?: React.ReactNode }) {
  const { data: session } = useCustomerSession();
  // Per-item `audience` (nav/types.ts, added by HO-CAT-002) — see the matching comment in
  // staff-shell.tsx.
  const items = navRegistry.flatMap((area) =>
    area.items.filter((item) => (item.audience ?? area.audience) !== 'staff'),
  );

  return (
    <header className="flex flex-wrap items-center justify-between gap-4 border-b border-border bg-surface px-8 py-4">
      <Link href="/" className="font-display text-lg font-semibold text-accent-ink no-underline">
        PurrCare
      </Link>

      {items.length > 0 && (
        <nav aria-label="Main" className="flex flex-wrap gap-6 text-sm font-semibold">
          {items.map((item) => (
            <Link key={item.href} href={item.href} className="text-ink-muted no-underline">
              {item.label}
            </Link>
          ))}
        </nav>
      )}

      {session && (
        <div className="flex items-center gap-3">
          {/* HO-NTF-002 §11.1 — the layout passes the unread-badge link here; it renders only once
              a session exists, so a signed-out visitor never polls the unread count. */}
          {sessionActions}
          <span className="text-sm text-ink-muted">{session.fullName}</span>
          <SignOutButton audience="customer" />
        </div>
      )}
    </header>
  );
}
