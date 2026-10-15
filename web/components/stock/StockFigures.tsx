import { formatQuantity } from '@/lib/format/inventory';

/**
 * `components/stock/StockFigures.tsx` — HO-IMP-002 §11.1, US-IMP-08. The server's figures —
 * on hand, reserved, usable, sellable, held, expired — as a labelled list, EVERY one with its unit
 * (UI-8). The disclosure makes US-IMP-08 AC1 legible: what "sellable" leaves out.
 */
export interface StockFiguresData {
  onHand: string;
  reserved: string;
  usable: string;
  sellable: string;
  heldQuantity: string;
  expiredQuantity: string;
}

export function StockFigures({
  figures,
  stockUnit,
  idPrefix = 'stock-figures',
}: {
  figures: StockFiguresData;
  stockUnit: string;
  idPrefix?: string;
}) {
  const rows: Array<[string, string, string]> = [
    ['On hand', figures.onHand, 'Everything the books say is here.'],
    ['Reserved', figures.reserved, 'Promised to orders, not yet taken.'],
    ['Usable', figures.usable, 'On hand minus reserved.'],
    ['Sellable', figures.sellable, 'What the shop may offer.'],
    ['Held', figures.heldQuantity, 'Quarantined, recalled or in a quarantine location.'],
    ['Expired', figures.expiredQuantity, 'Past its expiry date.'],
  ];
  return (
    <div className="flex flex-col gap-2">
      <dl className="grid grid-cols-2 gap-x-4 gap-y-2 text-sm sm:grid-cols-3">
        {rows.map(([label, value]) => (
          <div key={label} className="min-w-0">
            <dt className="text-ink-muted">{label}</dt>
            <dd className="font-semibold text-ink">{formatQuantity(value, stockUnit)}</dd>
          </div>
        ))}
      </dl>
      <details className="text-sm text-ink-muted">
        <summary
          className="cursor-pointer rounded-[var(--radius-sm)] font-semibold text-accent-ink focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-accent"
          id={`${idPrefix}-explain`}
        >
          What do these mean?
        </summary>
        <p className="mt-2">
          Sellable excludes expired, quarantined, damaged and reserved stock.
        </p>
        <ul className="mt-1 list-disc pl-5">
          {rows.map(([label, , help]) => (
            <li key={label}>
              <span className="font-semibold text-ink">{label}</span> — {help}
            </li>
          ))}
        </ul>
        <p className="mt-1 text-xs">These figures come from the server; nothing is calculated here.</p>
      </details>
    </div>
  );
}
