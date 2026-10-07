'use client';

import { use } from 'react';
import { LoadingState } from '@/components/common/LoadingState';
import { ImagesEditor } from '@/components/staff-shop/ImagesEditor';
import { useSetProductImages, useStaffProduct } from '@/lib/hooks/useStaffShop';

/** `/staff/shop/products/[productId]/images` — images by stored file id + alt text; NO upload (ADR-FE-035). */
export default function ProductImagesPage({ params }: { params: Promise<{ productId: string }> }) {
  const { productId } = use(params);
  const product = useStaffProduct(productId);
  const setImages = useSetProductImages(productId);
  const p = product.data;
  if (!p) return <LoadingState rows={6} />;
  return (
    <ImagesEditor
      key={p.version}
      product={p}
      onReload={() => void product.refetch()}
      onSave={(images) => setImages.mutateAsync({ expectedVersion: p.version, images })}
    />
  );
}
