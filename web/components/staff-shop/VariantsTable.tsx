'use client';

import * as React from 'react';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { ConfirmDialog } from '@/components/common/ConfirmDialog';
import { LoadingState } from '@/components/common/LoadingState';
import { ScheduleChangeDialog } from '@/components/admin/ScheduleChangeDialog';
import { formatMoney } from '@/lib/format/money';
import { priceCurrencyCode } from '@/lib/shop/currency';
import { formatBestBefore } from '@/lib/format/shop';
import { formatInstantDate } from '@/lib/format/time';
import { describeApiError, SHOP_CODE_MESSAGES } from '@/lib/api/errors';
import type {
  PriceEntry,
  SkuPriceHistory,
  StaffProductDetail,
  StaffSku,
} from '@/lib/api/staff-shop';
import { AddSkuDialog, type AddSkuValues } from './AddSkuDialog';

export function skuLabel(sku: StaffSku): string {
  return [sku.code, sku.sizeOrWeight, sku.flavour, sku.colour].filter(Boolean).join(' · ');
}

/** The label repeats the attributes the row already shows; it adds the product name only when none is set. */
const hasAttributes = (sku: StaffSku) =>
  !!(sku.sizeOrWeight || sku.flavour || sku.colour || sku.packQuantity !== null);

/** ADR-FE-032: `null`/absent is "never reported", NEVER zero; a real zero says `0 sellable`. */
export function stockText(stock: StaffSku['stock']): string {
  if (stock === null || stock === undefined) return 'No stock recorded';
  const parts = [`${stock.sellableQuantity} sellable`];
  if (stock.nextAllocatableExpiryOn)
    parts.push(`best before ${formatBestBefore(stock.nextAllocatableExpiryOn)}`);
  const asOf = new Date(stock.asOf);
  if (!Number.isNaN(asOf.getTime())) {
    parts.push(`as of ${asOf.toLocaleTimeString('en-GB', { hour: '2-digit', minute: '2-digit' })}`);
  }
  return parts.join(' · ');
}

function PriceRows({ title, entries }: { title: string; entries: PriceEntry[] }) {
  return (
    <section aria-label={title} className="flex flex-col gap-1">
      <h4 className="text-sm font-bold text-ink">{title}</h4>
      {entries.length === 0 ? (
        <p className="text-sm text-ink-muted">None.</p>
      ) : (
        <ul className="m-0 list-none p-0 text-sm">
          {entries.map((e) => (
            <li key={e.skuPriceId}>
              {formatMoney(e.amount)} from {formatInstantDate(e.effectiveFrom)}
            </li>
          ))}
        </ul>
      )}
    </section>
  );
}

export interface VariantsTableProps {
  product: StaffProductDetail;
  onAddSku: (values: AddSkuValues) => Promise<unknown>;
  onSetStatus: (sku: StaffSku, active: boolean) => Promise<unknown>;
  priceSkuId: string | null;
  onSelectPrices: (skuId: string | null) => void;
  priceHistory?: SkuPriceHistory;
  priceLoading?: boolean;
  onSchedulePrice: (
    sku: StaffSku,
    amount: string,
    effectiveFromIso: string,
    currencyCode: string,
  ) => Promise<unknown>;
}

/**
 * HO-SHP-002 §11.6 `VariantsTable` — US-SHP-02. SKUs with their code, barcode, status, CURRENT price,
 * SCHEDULED price and the stock copy (read-only, from the API). **Add SKU**, **Discontinue /
 * Reactivate** (confirmed) and **Prices**: Current / Scheduled / Past (HO-ADM-002's effective-dated
 * pattern) with **Schedule a new price**.
 */
export function VariantsTable(props: VariantsTableProps) {
  const { product, priceSkuId } = props;
  const [adding, setAdding] = React.useState(false);
  const [status, setStatus] = React.useState<{ sku: StaffSku; active: boolean } | null>(null);
  const [scheduling, setScheduling] = React.useState(false);
  const [statusError, setStatusError] = React.useState<string | null>(null);
  const priceSku = product.skus.find((s) => s.skuId === priceSkuId);
  const currencyCode = priceCurrencyCode(product, props.priceHistory);

  return (
    <div className="flex flex-col gap-4">
      <div className="flex flex-wrap items-center justify-between gap-3">
        <h2 className="font-display text-xl font-semibold text-ink">Variants & prices</h2>
        <Button onClick={() => setAdding(true)}>Add SKU</Button>
      </div>
      {statusError && (
        <p role="alert" className="text-sm font-semibold text-danger-ink">
          {statusError}
        </p>
      )}
      <div className="overflow-x-auto">
        <table className="w-full min-w-[720px] border-collapse text-sm">
          <caption className="sr-only">SKUs</caption>
          <thead>
            <tr className="border-b border-border text-left text-xs text-ink-muted">
              {[
                'SKU',
                'Barcode',
                'Status',
                'Current price',
                'Scheduled price',
                'Stock',
                'Actions',
              ].map((h) => (
                <th key={h} scope="col" className="py-2 pr-3 font-semibold">
                  {h}
                </th>
              ))}
            </tr>
          </thead>
          <tbody>
            {product.skus.map((sku) => (
              <tr key={sku.skuId} className="border-b border-border align-top">
                <th scope="row" className="py-2 pr-3 text-left font-semibold text-ink">
                  {skuLabel(sku)}
                  {!hasAttributes(sku) && (
                    <span className="block text-xs font-normal text-ink-muted">{sku.label}</span>
                  )}
                </th>
                <td className="py-2 pr-3">{sku.barcode ?? '—'}</td>
                <td className="py-2 pr-3">
                  <Badge variant={sku.status === 'Active' ? 'success' : 'neutral'}>
                    {sku.status}
                  </Badge>
                </td>
                <td className="py-2 pr-3">
                  {sku.currentPrice ? formatMoney(sku.currentPrice) : 'Not priced'}
                </td>
                <td className="py-2 pr-3">
                  {sku.scheduledPrice ? formatMoney(sku.scheduledPrice) : '—'}
                </td>
                <td className="py-2 pr-3">{stockText(sku.stock)}</td>
                <td className="py-2">
                  <div className="flex flex-wrap gap-2">
                    <Button
                      variant="secondary"
                      size="sm"
                      aria-label={`Prices for ${sku.code}`}
                      onClick={() => props.onSelectPrices(sku.skuId)}
                    >
                      Prices
                    </Button>
                    <Button
                      variant="ghost"
                      size="sm"
                      onClick={() => setStatus({ sku, active: sku.status !== 'Active' })}
                    >
                      {sku.status === 'Active' ? 'Discontinue' : 'Reactivate'}
                    </Button>
                  </div>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>

      {priceSku && (
        <section
          aria-label={`Price history for ${priceSku.code}`}
          className="flex flex-col gap-3 rounded-[var(--radius-md)] border border-border bg-surface p-4"
        >
          <div className="flex flex-wrap items-center justify-between gap-2">
            <h3 className="text-base font-bold text-ink">Prices — {priceSku.code}</h3>
            <div className="flex gap-2">
              <Button size="sm" onClick={() => setScheduling(true)}>
                Schedule a new price
              </Button>
              <Button size="sm" variant="ghost" onClick={() => props.onSelectPrices(null)}>
                Close
              </Button>
            </div>
          </div>
          {props.priceLoading || !props.priceHistory ? (
            <LoadingState rows={3} label="Loading prices…" />
          ) : (
            <>
              <PriceRows
                title="Current"
                entries={props.priceHistory.current ? [props.priceHistory.current] : []}
              />
              <PriceRows title="Scheduled" entries={props.priceHistory.scheduled} />
              <PriceRows title="Past" entries={props.priceHistory.past} />
            </>
          )}
          <ScheduleChangeDialog
            open={scheduling}
            onOpenChange={setScheduling}
            title="Schedule a new price"
            valueLabel={`Amount (${currencyCode})`}
            consequence={(date) =>
              `The new price shows on the storefront from ${date}. Orders placed before then keep their price.`
            }
            codeMessages={SHOP_CODE_MESSAGES}
            onSchedule={async (value, iso) => {
              await props.onSchedulePrice(priceSku, value, iso, currencyCode);
              setScheduling(false);
            }}
          />
        </section>
      )}

      <AddSkuDialog open={adding} onOpenChange={setAdding} onAdd={props.onAddSku} />
      <ConfirmDialog
        open={status !== null}
        onOpenChange={(open) => !open && setStatus(null)}
        title={status?.active ? 'Reactivate this SKU?' : 'Discontinue this SKU?'}
        consequence={
          status
            ? status.active
              ? `${status.sku.code} will be on sale again.`
              : `${status.sku.code} will stop being sold. It stays on customers' wishlists as no longer sold.`
            : ''
        }
        confirmLabel={status?.active ? 'Reactivate' : 'Discontinue'}
        variant={status?.active ? 'primary' : 'destructive'}
        onConfirm={async () => {
          const target = status;
          setStatus(null);
          if (!target) return;
          try {
            setStatusError(null);
            await props.onSetStatus(target.sku, target.active);
          } catch (error) {
            setStatusError(describeApiError(error, SHOP_CODE_MESSAGES));
          }
        }}
      />
    </div>
  );
}
