import { AlertTriangle, CalendarClock, CalendarX2, Clock } from 'lucide-react';
import { Badge } from '@/components/ui/badge';
import { formatExpiry } from '@/lib/format/inventory';
import { cn } from '@/lib/utils';

/**
 * `components/stock/ExpiryBadge.tsx` — HO-IMP-002 §11.1, ADR-FE-047, UI-6. The expiry in WORDS
 * (`formatExpiry`), with an icon whose meaning is repeated in text — colour is never the only
 * signal. `isExpired` / `daysToExpiry` are the server's (derived in the branch's local date); this
 * component words them and decides nothing. The `Expired` variant repeats *"cannot be sold,
 * dispensed or fed"* (BR-3 has no override).
 */
export function ExpiryBadge({
  expiresOn,
  isExpired,
  daysToExpiry,
  className,
}: {
  expiresOn: string | null;
  isExpired: boolean;
  daysToExpiry: number | null;
  className?: string;
}) {
  const { text, severity } = formatExpiry(expiresOn, isExpired, daysToExpiry);
  const icon =
    severity === 'expired' ? (
      <CalendarX2 size={12} aria-hidden="true" />
    ) : severity === 'today' ? (
      <AlertTriangle size={12} aria-hidden="true" />
    ) : severity === 'ok' ? (
      <CalendarClock size={12} aria-hidden="true" />
    ) : (
      <Clock size={12} aria-hidden="true" />
    );
  return (
    <span className="inline-flex min-w-0 flex-col items-start gap-1">
      <Badge
        variant={severity === 'expired' ? 'danger' : severity === 'today' ? 'warning' : 'neutral'}
        icon={icon}
        className={cn('max-w-full whitespace-normal', className)}
      >
        {text}
      </Badge>
      {severity === 'expired' && (
        <span className="text-xs text-danger-ink">Cannot be sold, dispensed or fed.</span>
      )}
    </span>
  );
}
