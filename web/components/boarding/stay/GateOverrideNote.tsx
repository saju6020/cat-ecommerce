import { formatBranchInstant } from '@/lib/format/boarding';

/**
 * `components/boarding/stay/GateOverrideNote.tsx` — HO-BRD-002 §11.8: who overrode a cat's vaccination gate,
 * why and when. The built wire has the reason and the instant but no `overriddenBy`; each is shown only when sent.
 */
export function GateOverrideNote({
  by,
  reason,
  on,
  zone,
}: {
  by?: string | null;
  reason?: string | null;
  on?: string | null;
  zone: string;
}) {
  if (!by && !reason) return null;
  return (
    <span className="text-ink-muted">
      {by ? `by ${by}: ` : 'Override reason: '}
      {reason}
      {on ? ` (${formatBranchInstant(on, zone)})` : ''}
    </span>
  );
}
