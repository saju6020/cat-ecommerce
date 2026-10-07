'use client';

import { use } from 'react';
import { LoadingState } from '@/components/common/LoadingState';
import { RelatedProductsEditor } from '@/components/staff-shop/RelatedProductsEditor';
import { useSetRelatedProducts, useStaffProduct } from '@/lib/hooks/useStaffShop';
import { ProductPicker } from '../../../_components/Pickers';

/** `/staff/shop/products/[productId]/related` — curated links (US-SHP-08). */
export default function ProductRelatedPage({ params }: { params: Promise<{ productId: string }> }) {
  const { productId } = use(params);
  const product = useStaffProduct(productId);
  const setRelated = useSetRelatedProducts(productId);
  const p = product.data;
  if (!p) return <LoadingState rows={6} />;
  return (
    <RelatedProductsEditor
      key={p.version}
      product={p}
      renderPicker={(onPick, kind) => <ProductPicker id={`related-${kind}`} onPick={onPick} />}
      onReload={() => void product.refetch()}
      onSave={(links) => setRelated.mutateAsync({ expectedVersion: p.version, links })}
    />
  );
}
