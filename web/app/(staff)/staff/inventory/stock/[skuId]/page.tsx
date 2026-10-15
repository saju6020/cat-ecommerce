'use client';

import { use, useState } from 'react';
import Link from 'next/link';
import { PageHeading, Panel } from '@/components/inventory/fields';
import { QueryBoundary } from '@/components/inventory/QueryBoundary';
import { StorageChip } from '@/components/inventory/StorageChip';
import { FefoPreviewPanel, type FefoRequest } from '@/components/stock/FefoPreviewPanel';
import { SkuStockMatrix } from '@/components/stock/SkuStockMatrix';
import { StockFigures } from '@/components/stock/StockFigures';
import { useFefoPreview, useSkuStockMatrix, useStockSummary } from '@/lib/hooks/useInventory';
import { useLocations, useSkuSearch } from '@/lib/hooks/useInventoryLookup';

/** `/staff/inventory/stock/[skuId]` — US-IMP-06/07/08: the batch × location matrix, the item's
 *  figures, and the FEFO preview. */
export default function SkuStockPage({ params }: { params: Promise<{ skuId: string }> }) {
  const { skuId } = use(params);
  const matrix = useSkuStockMatrix(skuId);
  const code = matrix.data?.skuCode ?? '';
  // The matrix has no totals of its own: the item's figures come from the stock summary (exact code).
  const summary = useStockSummary({ search: code, pageSize: 25 });
  const lookup = useSkuSearch(code);
  const locations = useLocations();
  const [request, setRequest] = useState<FefoRequest | null>(null);
  const preview = useFefoPreview(request ? { skuId, ...request } : null);
  const figures = summary.data?.items.find((s) => s.skuId === skuId);
  const sku = lookup.data?.find((s) => s.skuId === skuId);

  return (
    <QueryBoundary query={matrix} notFoundTitle="This item could not be found">
      {(data) => (
        <div className="flex flex-col gap-5">
          <PageHeading
            title={data.displayName}
            description={
              <span className="flex flex-wrap items-center gap-2">
                <span>{data.skuCode}</span>
                <span>counted in {data.stockUnit}</span>
                <StorageChip storage={data.storage} />
              </span>
            }
            actions={
              <Link href="/staff/inventory/stock" className="text-sm font-semibold text-accent-ink">
                All stock
              </Link>
            }
          />
          <Panel title="Figures">
            {figures ? (
              <StockFigures figures={figures} stockUnit={data.stockUnit} />
            ) : (
              <p className="text-sm text-ink-muted">{summary.isLoading ? 'Loading figures…' : 'No stock of this item is held at this branch.'}</p>
            )}
          </Panel>
          <FefoPreviewPanel
            stockUnit={data.stockUnit}
            allowsFractional={sku?.allowsFractionalQuantity ?? true}
            locations={locations.data}
            preview={preview.data}
            isLoading={preview.isFetching}
            error={preview.error}
            onPreview={setRequest}
          />
          <Panel title="Batches by location">
            <SkuStockMatrix matrix={data} />
          </Panel>
        </div>
      )}
    </QueryBoundary>
  );
}
