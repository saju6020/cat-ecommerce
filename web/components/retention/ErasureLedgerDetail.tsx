import { CircleAlert, CircleCheck, Clock, Hourglass } from 'lucide-react';
import { Badge } from '@/components/ui/badge';
import type { ErasureRequest, ErasureScopeStatus } from '@/lib/api/retention';
import { formatDateOnly, formatInZone } from '@/lib/format/time';
import { glossErrorCode, scopeLabel, SOURCE_LABEL } from '@/lib/retention/labels';

const SCOPE_ICON: Record<ErasureScopeStatus, React.ReactNode> = {
  Pending: <Clock size={12} aria-hidden="true" />,
  Held: <Hourglass size={12} aria-hidden="true" />,
  Completed: <CircleCheck size={12} aria-hidden="true" />,
  Failing: <CircleAlert size={12} aria-hidden="true" />,
};

const SCOPE_TEXT: Record<ErasureScopeStatus, string> = {
  Pending: 'Waiting',
  Held: 'Held',
  Completed: 'Erased',
  Failing: 'Retrying',
};

/**
 * `components/retention/ErasureLedgerDetail.tsx` — HO-XCT-002 §11.5, U-B4. One ledger: a row per
 * part with its status, "Held until 12 Mar 2032 — NFR-LEG-5 acceptance evidence", attempts and a
 * glossed last error. The ledger holds no name, phone or email, so none is shown — only the account
 * id. A paragraph explains held data for a compliance officer.
 */
export function ErasureLedgerDetail({ request, zone }: { request: ErasureRequest; zone: string }) {
  return (
    <div className="flex flex-col gap-4">
      <h1 className="font-display text-xl font-semibold text-ink">Erasure request</h1>
      <p className="text-sm text-ink-muted">
        Customer account <span className="font-mono text-xs">{request.customerAccountId}</span> ·{' '}
        {SOURCE_LABEL[request.source] ?? 'Other'} · requested {formatInZone(request.requestedOn, zone)}
        {request.completedOn ? ` · completed ${formatInZone(request.completedOn, zone)}` : ''}
      </p>
      <p className="rounded-[var(--radius-md)] border border-border bg-surface p-4 text-sm text-ink">
        Personal details are erased straight away. Some records are <strong>held</strong> instead,
        because the law requires them to be kept for a set period (for example clinical and
        financial records). Held records keep only what that law requires and are anonymised or
        released when their date passes. Nothing here can be changed from this screen.
      </p>
      <ul className="flex flex-col gap-2">
        {request.scopes.map((scope) => (
          <li
            key={scope.contextKey}
            className="rounded-[var(--radius-md)] border border-border bg-surface p-4 text-sm"
          >
            <div className="flex flex-wrap items-center gap-2">
              <span className="font-semibold text-ink">{scopeLabel(scope.contextKey)}</span>
              <Badge
                variant={scope.status === 'Completed' ? 'success' : scope.status === 'Failing' ? 'danger' : 'neutral'}
                icon={SCOPE_ICON[scope.status]}
              >
                {SCOPE_TEXT[scope.status]}
              </Badge>
            </div>
            {scope.status === 'Held' && (
              <p className="mt-1 text-ink">
                {scope.heldUntil ? `Held until ${formatDateOnly(scope.heldUntil)}` : 'Held'}
                {scope.basis ? ` — ${scope.basis}` : ''}
              </p>
            )}
            <p className="mt-1 text-ink-muted">
              {scope.attempts} {scope.attempts === 1 ? 'attempt' : 'attempts'}
              {scope.completedOn ? ` · completed ${formatInZone(scope.completedOn, zone)}` : ''}
            </p>
            {scope.lastErrorCode && (
              <p className="mt-1 text-warning-ink">{glossErrorCode(scope.lastErrorCode)}</p>
            )}
          </li>
        ))}
      </ul>
    </div>
  );
}
