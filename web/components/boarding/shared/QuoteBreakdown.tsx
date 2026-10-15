import type { StayQuote } from '@/lib/api/types';
import { formatMoney } from '@/lib/format/money';
import { cn } from '@/lib/utils';

/**
 * `components/boarding/shared/QuoteBreakdown.tsx` — HO-BRD-002 §11.1 `QuoteBreakdown`, US-BRD-03
 * AC2: the basis label (*"Weekly rate"*), each rate-unit line, additional-cat lines, the long-stay
 * **discount as its own line**, and the total — all exactly as the server sent them. Nothing is
 * added, multiplied or merged here (ADR-FE-105): the only call is `formatMoney` on a string.
 */
const BASIS_LABEL = {
  Nightly: 'Nightly rate',
  Weekly: 'Weekly rate',
  Monthly: 'Monthly rate',
} as const;

export function QuoteBreakdown({ quote, className }: { quote: StayQuote; className?: string }) {
  return (
    <div className={cn('text-sm', className)}>
      <p className="mb-1.5 text-xs font-semibold uppercase tracking-wide text-ink-muted">
        {BASIS_LABEL[quote.basis]}
      </p>
      <ul aria-label="Price breakdown" className="flex flex-col gap-1">
        {quote.units.map((unit) => (
          <li key={unit.sequence} className="flex justify-between gap-3">
            <span className="text-ink">{unit.description}</span>
            <span className="whitespace-nowrap font-semibold tabular-nums text-ink">
              {formatMoney(unit.amount)}
            </span>
          </li>
        ))}
        {quote.discountLines.map((line, index) => (
          <li key={`d${index}`} className="flex justify-between gap-3 text-accent-ink">
            <span>{line.description}</span>
            <span className="whitespace-nowrap font-semibold tabular-nums">
              {formatMoney(line.amount)}
            </span>
          </li>
        ))}
      </ul>
      <div className="mt-2 flex justify-between gap-3 border-t border-border pt-2 font-bold text-ink">
        <span>Total</span>
        <span className="tabular-nums">{formatMoney(quote.total)}</span>
      </div>
    </div>
  );
}
