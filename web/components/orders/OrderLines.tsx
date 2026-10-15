import { PawPrint } from 'lucide-react';
import { Amount } from '@/components/billing/Amount';
import type { Money } from '@/lib/format/money';
import { Badge } from '@/components/ui/badge';

/**
 * `components/orders/OrderLines.tsx` — HO-ORD-002 §11.4/§11.6. The lines of a quote or an order as a
 * list: product and variant, the cat chip on a prescription line, quantity, unit price, the tax rate
 * (as the server sent it) and the line total. A list, not a table, so it needs no horizontal scroll at
 * 360 px; the staff screens add batch details through `extra`.
 */
export interface DisplayLine {
  key: string;
  productName: string;
  variantLabel: string;
  cat: { id: string; name: string } | null;
  quantity: number;
  unitPrice: Money;
  lineTotal: Money;
  taxRatePercent?: number;
}

export function CatChip({ name }: { name: string }) {
  return (
    <Badge className="border border-border-strong bg-surface text-ink" icon={<PawPrint size={12} aria-hidden="true" />}>
      For {name}
    </Badge>
  );
}

export function OrderLines({
  lines,
  caption,
  extra,
}: {
  lines: DisplayLine[];
  caption: string;
  extra?: (line: DisplayLine) => React.ReactNode;
}) {
  return (
    <ul aria-label={caption} className="m-0 flex list-none flex-col p-0">
      {lines.map((line) => (
        <li key={line.key} className="flex flex-wrap items-start justify-between gap-x-4 gap-y-1 border-b border-border py-3 last:border-0">
          <div className="min-w-0 flex-1 basis-56">
            <p className="font-semibold text-ink [overflow-wrap:anywhere]">{line.productName}</p>
            <p className="text-sm text-ink-muted [overflow-wrap:anywhere]">
              {line.variantLabel}
              {line.variantLabel ? ' · ' : ''}Qty {line.quantity} ·{' '}
              <Amount money={line.unitPrice} /> each
              {line.taxRatePercent !== undefined ? ` · tax ${line.taxRatePercent}%` : ''}
            </p>
            {line.cat && (
              <p className="mt-1">
                <CatChip name={line.cat.name} />
              </p>
            )}
            {extra?.(line)}
          </div>
          <p className="whitespace-nowrap font-semibold text-ink">
            <Amount money={line.lineTotal} />
          </p>
        </li>
      ))}
    </ul>
  );
}
