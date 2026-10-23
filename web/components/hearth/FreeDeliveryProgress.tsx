import * as React from 'react';
import { cn } from '@/lib/utils';

export interface FreeDeliveryProgressProps {
  /**
   * The amount still to go, already formatted by the server's money (e.g. "৳250.00"). When it is
   * absent the card does not render: the browser never works out a threshold itself.
   */
  remaining?: string | null;
  /** Progress 0-100, supplied with the threshold. Clamped for display only. No number, no bar. */
  percent?: number | null;
  /** Zone wording; default "Dhaka". */
  zoneLabel?: string;
  /** e.g. a "Keep shopping" link. */
  children?: React.ReactNode;
  className?: string;
}

/** Warm-soft free-delivery progress card. Renders ONLY when the server sent a threshold. */
export function FreeDeliveryProgress({
  remaining,
  percent,
  zoneLabel = 'Dhaka',
  children,
  className,
}: FreeDeliveryProgressProps) {
  if (!remaining) return null;
  const value =
    typeof percent === 'number' ? Math.min(100, Math.max(0, Math.round(percent))) : null;
  return (
    <div className={cn('flex flex-col gap-2.5 rounded-card bg-warm-soft px-5 py-4', className)}>
      <p className="m-0 text-sm">
        <strong className="text-warm-ink">Add {remaining} more</strong> for free delivery in{' '}
        {zoneLabel}.
      </p>
      {value !== null && (
        <div
          role="progressbar"
          aria-label="Progress to free delivery"
          aria-valuenow={value}
          aria-valuemin={0}
          aria-valuemax={100}
          className="h-2.5 overflow-hidden rounded-pill bg-surface"
        >
          <div className="h-full rounded-pill bg-warm" style={{ width: `${value}%` }} />
        </div>
      )}
      {children}
    </div>
  );
}
