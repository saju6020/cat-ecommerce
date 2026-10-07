import { ShieldAlert } from 'lucide-react';

/**
 * `components/admin/PermissionDeniedState.tsx` — HO-ACC-002 §11.10, ADR-FE-009. Rendered on a
 * server `403 ActionNotPermitted`. Names no role and no action — UI role-gating is presentation
 * only; this is the one "not permitted" the UI recognises.
 */
export function PermissionDeniedState() {
  return (
    <div
      role="alert"
      className="flex items-start gap-3 rounded-[var(--radius-md)] border border-border bg-surface p-6"
    >
      <ShieldAlert size={20} className="mt-0.5 flex-none text-ink-muted" aria-hidden="true" />
      <div>
        <p className="text-[15px] font-semibold text-ink">
          You don&rsquo;t have permission to do this.
        </p>
        <p className="mt-1 text-sm text-ink-muted">Ask an administrator if you need access.</p>
      </div>
    </div>
  );
}
