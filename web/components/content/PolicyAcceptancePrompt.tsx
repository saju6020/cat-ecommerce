'use client';

import { useState } from 'react';
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog';
import { Button } from '@/components/ui/button';
import { SignOutButton } from '@/components/auth/SignOutButton';
import { describeApiError } from '@/lib/api/errors';
import { usePendingPolicyAcceptances, useAcceptPolicyVersion } from '@/lib/hooks/usePublicContent';
import type { PendingPolicyAcceptance } from '@/lib/api/public-content';

/**
 * `components/content/PolicyAcceptancePrompt.tsx` — ⭐ HO-ADM-002 §11.6, US-ADM-07, OQ-2/OQ-3.
 * Mounted in `(portal)/layout.tsx` with no props — a session-adjacent gate over the whole portal,
 * the same class of component as `StaffSessionGuard`/`IdleLock` (HO-ACC-002 §11.3/§11.5/§11.9's
 * named exemption from "components take props, pages wire hooks"): it wires
 * `usePendingPolicyAcceptances()`/`useAcceptPolicyVersion()` itself so the layout stays a plain
 * server component with a one-line addition, per this handover's own scoped-edit note for that
 * file.
 *
 * Built on `components/ui/dialog.tsx` — the same non-dismissable pattern `IdleLock` already uses
 * (`onOpenChange={() => {}}`, `Escape`/outside-click/outside-pointer all prevented, `hideClose`):
 * Radix's own focus trap and background `inert`/`aria-hidden` do the rest. Sign-out stays reachable
 * (rendered inside the dialog); nothing else is. Never pre-ticked — accepting is one deliberate
 * click per policy, not a checkbox default.
 */
export function PolicyAcceptancePrompt() {
  const { data, isError, refetch, isFetching } = usePendingPolicyAcceptances();
  const acceptMutation = useAcceptPolicyVersion();
  const [error, setError] = useState<string | null>(null);
  const [acceptingKey, setAcceptingKey] = useState<string | null>(null);

  // ADM backlog 14 — fail CLOSED: a request error is not "nothing to accept". The server does not
  // block portal endpoints while an acceptance is pending, so this dialog is the only gate; on an
  // error it stays up in a retry state (sign-out remains reachable) instead of opening the portal.
  if (isError && !data) {
    return (
      <Dialog open onOpenChange={() => {}}>
        <DialogContent
          hideClose
          onEscapeKeyDown={(event) => event.preventDefault()}
          onPointerDownOutside={(event) => event.preventDefault()}
          onInteractOutside={(event) => event.preventDefault()}
        >
          <DialogHeader>
            <DialogTitle>We couldn&rsquo;t check your policies</DialogTitle>
            <DialogDescription>
              We need to confirm you are up to date with our policies before you continue. This does
              not mean there is nothing to accept &mdash; please try again.
            </DialogDescription>
          </DialogHeader>
          <div className="flex items-center gap-3">
            <Button onClick={() => void refetch()} disabled={isFetching} aria-busy={isFetching}>
              {isFetching ? 'Trying again…' : 'Try again'}
            </Button>
            <SignOutButton audience="customer" />
          </div>
        </DialogContent>
      </Dialog>
    );
  }

  const pending = data ?? [];
  if (pending.length === 0) return null;

  async function handleAccept(policy: PendingPolicyAcceptance) {
    setError(null);
    setAcceptingKey(policy.key);
    try {
      await acceptMutation.mutateAsync({
        policyKey: policy.key,
        versionNumber: policy.versionNumber,
      });
    } catch (err) {
      setError(describeApiError(err));
    } finally {
      setAcceptingKey(null);
    }
  }

  return (
    <Dialog open onOpenChange={() => {}}>
      <DialogContent
        hideClose
        onEscapeKeyDown={(event) => event.preventDefault()}
        onPointerDownOutside={(event) => event.preventDefault()}
        onInteractOutside={(event) => event.preventDefault()}
      >
        <DialogHeader>
          <DialogTitle>Updated policies</DialogTitle>
          <DialogDescription>
            We&rsquo;ve updated our policies. Please review and accept to continue using your
            account.
          </DialogDescription>
        </DialogHeader>

        {error && (
          <p role="alert" aria-live="polite" className="mb-3 text-sm font-semibold text-danger-ink">
            {error}
          </p>
        )}

        <ul className="flex flex-col gap-3">
          {pending.map((policy) => (
            <li
              key={policy.key}
              className="flex items-center justify-between gap-3 rounded-[var(--radius-md)] border border-border p-3"
            >
              <div>
                <p className="text-sm font-semibold text-ink">{policy.title}</p>
                <a
                  href={`/policies/${encodeURIComponent(policy.key)}`}
                  target="_blank"
                  rel="noopener noreferrer"
                  className="text-xs font-semibold text-accent-ink underline"
                >
                  Read the full text
                </a>
              </div>
              <Button
                size="sm"
                onClick={() => handleAccept(policy)}
                disabled={acceptingKey === policy.key}
                aria-busy={acceptingKey === policy.key}
              >
                {acceptingKey === policy.key ? 'Accepting…' : 'Accept'}
              </Button>
            </li>
          ))}
        </ul>

        <div className="mt-6 border-t border-border pt-4">
          <SignOutButton audience="customer" />
        </div>
      </DialogContent>
    </Dialog>
  );
}
