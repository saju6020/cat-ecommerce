'use client';

import * as React from 'react';
import { use, useState } from 'react';
import { LoadingState } from '@/components/common/LoadingState';
import { VariantsTable } from '@/components/staff-shop/VariantsTable';
import {
  useAddSku,
  useScheduleSkuPrice,
  useSetSkuStatus,
  useSkuPrices,
  useStaffProduct,
} from '@/lib/hooks/useStaffShop';

/** Binds the price-scheduling mutation to the SKU whose Prices panel is open. */
function PriceScheduler({
  productId,
  skuId,
  children,
}: {
  productId: string;
  skuId: string;
  children: (
    schedule: (amount: string, iso: string, currencyCode: string) => Promise<unknown>,
  ) => React.ReactNode;
}) {
  const schedule = useScheduleSkuPrice(productId, skuId);
  return (
    <>
      {children((amount, iso, currencyCode) =>
        schedule.mutateAsync({ amount: Number(amount), currencyCode, effectiveFrom: iso }),
      )}
    </>
  );
}

/** `/staff/shop/products/[productId]/variants` — SKUs, prices and the stock copy (US-SHP-02). */
export default function ProductVariantsPage({
  params,
}: {
  params: Promise<{ productId: string }>;
}) {
  const { productId } = use(params);
  const product = useStaffProduct(productId);
  const [priceSkuId, setPriceSkuId] = useState<string | null>(null);
  const prices = useSkuPrices(priceSkuId);
  const addSku = useAddSku(productId);
  const setStatus = useSetSkuStatus(productId);
  const p = product.data;
  if (!p) return <LoadingState rows={6} />;

  const table = (
    schedule: (amount: string, iso: string, currencyCode: string) => Promise<unknown>,
  ) => (
    <VariantsTable
      product={p}
      priceSkuId={priceSkuId}
      onSelectPrices={setPriceSkuId}
      priceHistory={prices.data}
      priceLoading={prices.isPending && !!priceSkuId}
      onSchedulePrice={(_sku, amount, iso, currency) => schedule(amount, iso, currency)}
      onAddSku={(values) => addSku.mutateAsync({ expectedVersion: p.version, ...values })}
      onSetStatus={(sku, active) =>
        setStatus.mutateAsync({ skuId: sku.skuId, active, expectedVersion: p.version })
      }
    />
  );
  return priceSkuId ? (
    <PriceScheduler productId={productId} skuId={priceSkuId}>
      {table}
    </PriceScheduler>
  ) : (
    table(async () => undefined)
  );
}
