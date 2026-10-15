import { Ban, CheckCircle2, Lock, ShieldAlert, Trash2, Warehouse } from 'lucide-react';
import { Badge } from '@/components/ui/badge';
import type { BatchStatus } from '@/lib/api/inventory-stock';

/**
 * `components/stock/BatchStateBadge.tsx` — HO-IMP-002 §11.1, ADR-FE-047. Says plainly why a batch
 * can or cannot be used: *Available*, *Quarantined*, *Recalled — held*, *Written off*,
 * *In quarantine location*, *Expired*. Text first; the icon is decorative. Precedence mirrors the
 * server's: a written-off, recalled or quarantined batch is that regardless of its date.
 */
export interface BatchStateDescription {
  label: string;
  /** Why it cannot be used — absent for an available batch. */
  why: string | null;
  usable: boolean;
  tone: 'success' | 'warning' | 'danger' | 'neutral';
}

export function describeBatchState(input: {
  status: BatchStatus;
  isExpired?: boolean;
  inQuarantineLocation?: boolean;
  statusReason?: string | null;
}): BatchStateDescription {
  const reason = input.statusReason ? ` — ${input.statusReason}` : '';
  if (input.status === 'WrittenOff') {
    return { label: 'Written off', why: `Written off${reason}`, usable: false, tone: 'neutral' };
  }
  if (input.status === 'Recalled') {
    return {
      label: 'Recalled — held',
      why: 'Recalled: held everywhere and cannot be sold, dispensed or fed.',
      usable: false,
      tone: 'danger',
    };
  }
  if (input.status === 'Quarantined') {
    return {
      label: 'Quarantined',
      why: `Quarantined${reason}. Not available until released.`,
      usable: false,
      tone: 'warning',
    };
  }
  if (input.isExpired) {
    return {
      label: 'Expired',
      why: 'Expired — cannot be sold, dispensed or fed.',
      usable: false,
      tone: 'danger',
    };
  }
  if (input.inQuarantineLocation) {
    return {
      label: 'In quarantine location',
      why: 'Held in a quarantine location — not available for use.',
      usable: false,
      tone: 'warning',
    };
  }
  return { label: 'Available', why: null, usable: true, tone: 'success' };
}

const ICONS = {
  success: <CheckCircle2 size={12} aria-hidden="true" />,
  warning: <Lock size={12} aria-hidden="true" />,
  danger: <ShieldAlert size={12} aria-hidden="true" />,
  neutral: <Trash2 size={12} aria-hidden="true" />,
} as const;

export function BatchStateBadge({
  status,
  isExpired,
  inQuarantineLocation,
  statusReason,
  showWhy = false,
}: {
  status: BatchStatus;
  isExpired?: boolean;
  inQuarantineLocation?: boolean;
  statusReason?: string | null;
  /** Also print the sentence explaining why it cannot be used. */
  showWhy?: boolean;
}) {
  const state = describeBatchState({ status, isExpired, inQuarantineLocation, statusReason });
  const icon =
    state.label === 'In quarantine location' ? (
      <Warehouse size={12} aria-hidden="true" />
    ) : state.label === 'Expired' ? (
      <Ban size={12} aria-hidden="true" />
    ) : (
      ICONS[state.tone]
    );
  return (
    <span className="inline-flex min-w-0 flex-col items-start gap-1">
      <Badge variant={state.tone} icon={icon} className="max-w-full whitespace-normal">
        {state.label}
      </Badge>
      {showWhy && state.why && <span className="text-xs text-ink-muted">{state.why}</span>}
    </span>
  );
}
