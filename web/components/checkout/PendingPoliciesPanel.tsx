'use client';

import { Button } from '@/components/ui/button';
import type { PendingPolicy } from '@/lib/api/checkout';

/**
 * `components/checkout/PendingPoliciesPanel.tsx` — HO-ORD-002 §11.2 step 4. Policies the customer
 * still has to accept before an order can be placed, each with an **Accept** action (HO-ADM-002's
 * acceptance call). The quote's `PolicyAcceptanceRequired` blocking reason stays until the server
 * stops listing them.
 */
export function PendingPoliciesPanel({
  policies,
  acceptingKey,
  error,
  onAccept,
}: {
  policies: PendingPolicy[];
  acceptingKey: string | null;
  error: string | null;
  onAccept: (policy: PendingPolicy) => void;
}) {
  if (policies.length === 0) return null;
  return (
    <section aria-labelledby="policies-heading" className="flex flex-col gap-2 rounded-[var(--radius-md)] border border-border bg-surface p-4">
      <h3 id="policies-heading" className="font-display text-base font-semibold text-ink">
        Please accept our updated policies
      </h3>
      <ul className="m-0 flex list-none flex-col gap-2 p-0">
        {policies.map((policy) => (
          <li key={`${policy.key}-${policy.versionNumber}`} className="flex flex-wrap items-center justify-between gap-2 text-sm">
            <span className="font-semibold text-ink">{policy.title}</span>
            <Button
              size="sm"
              variant="secondary"
              disabled={acceptingKey === policy.key}
              onClick={() => onAccept(policy)}
              aria-label={`Accept ${policy.title}`}
            >
              {acceptingKey === policy.key ? 'Accepting…' : 'Accept'}
            </Button>
          </li>
        ))}
      </ul>
      {error && (
        <p role="alert" className="text-sm font-semibold text-danger-ink">
          {error}
        </p>
      )}
    </section>
  );
}
