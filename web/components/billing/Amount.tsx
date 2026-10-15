import { formatMoney, type Money } from '@/lib/format/money';
import { a11yMoney } from '@/lib/money';
import { cn } from '@/lib/utils';

/**
 * `components/billing/Amount.tsx` — HO-BIL-002 §7 UI-6, U-X4. The one way a Billing screen shows an
 * amount: the visible text is `formatMoney` (the build's only money formatter, HO-ADM-002), and the
 * text a screen reader gets is `a11yMoney` — *"BDT 1,500.00"*, never a bare number. The amount stays a
 * string end to end (ADR-FE-080); nothing here parses or compares it.
 */
export function Amount({ money, className }: { money: Money; className?: string }) {
  return (
    <span className={cn('tabular-nums', className)}>
      <span aria-hidden="true">{formatMoney(money)}</span>
      <span className="sr-only">{a11yMoney(money)}</span>
    </span>
  );
}

/** A right-aligned money table cell's content (money columns are right-aligned from 768 px). */
export function AmountCell({ money }: { money: Money | null | undefined }) {
  if (!money) return <span aria-hidden="true">—</span>;
  return <Amount money={money} className="block text-right" />;
}
