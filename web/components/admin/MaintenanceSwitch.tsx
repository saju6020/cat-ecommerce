'use client';

import { useState } from 'react';
import { Button } from '@/components/ui/button';
import { Badge } from '@/components/ui/badge';
import { ConfirmDialog } from '@/components/common/ConfirmDialog';
import { describeApiError } from '@/lib/api/errors';
import type { SystemStatus } from '@/lib/api/system';

/** `components/admin/MaintenanceSwitch.tsx` — HO-ADM-002 §11.8, US-ADM-08, ADR-053. Turning
 *  maintenance ON is confirmed (UI-3); turning it off is not destructive and needs no confirm. */
export interface MaintenanceSwitchProps {
  status: SystemStatus;
  onSetMaintenanceMode: (maintenanceMode: boolean, message: string | null) => Promise<unknown>;
  isPending: boolean;
}

export function MaintenanceSwitch({
  status,
  onSetMaintenanceMode,
  isPending,
}: MaintenanceSwitchProps) {
  const [message, setMessage] = useState(status.message ?? '');
  const [confirmOpen, setConfirmOpen] = useState(false);
  const [error, setError] = useState<string | null>(null);

  async function apply(next: boolean) {
    setError(null);
    try {
      await onSetMaintenanceMode(next, message.trim() || null);
      setConfirmOpen(false);
    } catch (err) {
      setError(describeApiError(err));
    }
  }

  return (
    <div className="flex max-w-lg flex-col gap-4">
      <div className="flex items-center gap-3">
        <h1 className="font-display text-xl font-semibold text-ink">Maintenance</h1>
        <Badge variant={status.maintenanceMode ? 'warning' : 'success'}>
          {status.maintenanceMode ? 'On' : 'Off'}
        </Badge>
      </div>

      {error && (
        <p role="alert" aria-live="polite" className="text-sm font-semibold text-danger-ink">
          {error}
        </p>
      )}

      <div className="flex flex-col gap-1.5">
        <label htmlFor="maintenance-message" className="text-sm font-semibold text-ink">
          Message shown to customers
        </label>
        <textarea
          id="maintenance-message"
          rows={3}
          value={message}
          onChange={(event) => setMessage(event.target.value)}
          className="rounded-[var(--radius-sm)] border border-border-strong bg-surface px-3 py-2 text-sm text-ink"
        />
      </div>

      {status.maintenanceMode ? (
        <Button
          type="button"
          variant="secondary"
          onClick={() => apply(false)}
          disabled={isPending}
          aria-busy={isPending}
          className="w-fit"
        >
          {isPending ? 'Working…' : 'Turn maintenance off'}
        </Button>
      ) : (
        <Button
          type="button"
          onClick={() => setConfirmOpen(true)}
          disabled={isPending}
          className="w-fit"
        >
          Turn maintenance on
        </Button>
      )}

      <ConfirmDialog
        open={confirmOpen}
        onOpenChange={setConfirmOpen}
        title="Turn maintenance mode on?"
        consequence="The online shop will show this message. The back office and sign-in are not affected."
        confirmLabel="Turn on"
        onConfirm={() => apply(true)}
      />
    </div>
  );
}
