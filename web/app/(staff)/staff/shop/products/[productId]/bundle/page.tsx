'use client';

import { use } from 'react';
import { EmptyState } from '@/components/common/EmptyState';
import { LoadingState } from '@/components/common/LoadingState';
import { BundleEditor } from '@/components/staff-shop/BundleEditor';
import { useSetBundleComponents, useStaffProduct } from '@/lib/hooks/useStaffShop';
import { SkuPicker } from '../../../_components/Pickers';

/** `/staff/shop/products/[productId]/bundle` — kit components (US-SHP-12). Kits only. */
export default function ProductBundlePage({ params }: { params: Promise<{ productId: string }> }) {
  const { productId } = use(params);
  const product = useStaffProduct(productId);
  const setBundle = useSetBundleComponents(productId);
  const p = product.data;
  if (!p) return <LoadingState rows={6} />;
  if (p.kind !== 'Bundle') {
    return (
      <EmptyState title="This product is not a kit" description="Only kits have components." />
    );
  }
  return (
    <BundleEditor
      key={p.version}
      product={p}
      renderPicker={(onPick) => <SkuPicker onPick={onPick} />}
      onReload={() => void product.refetch()}
      onSave={(components) => setBundle.mutateAsync({ expectedVersion: p.version, components })}
    />
  );
}
