'use client';

import { useRouter } from 'next/navigation';
import { PermissionDeniedState } from '@/components/admin/PermissionDeniedState';
import { ErrorState } from '@/components/common/ErrorState';
import { LoadingState } from '@/components/common/LoadingState';
import { NewProductForm } from '@/components/staff-shop/NewProductForm';
import { ApiForbiddenError } from '@/lib/api/client';
import { useTaxClasses } from '@/lib/hooks/useAdminCatalogue';
import { useShopCategories } from '@/lib/hooks/useShopCatalogue';
import { useCreateProduct } from '@/lib/hooks/useStaffShop';

/** `/staff/shop/products/new` — US-SHP-01: create the draft, then continue in the editor. */
export default function NewProductPage() {
  const router = useRouter();
  const categories = useShopCategories();
  const taxClasses = useTaxClasses();
  const create = useCreateProduct();
  if (taxClasses.error instanceof ApiForbiddenError) return <PermissionDeniedState />;
  if (categories.isError || taxClasses.isError) {
    return (
      <ErrorState
        onRetry={() => {
          void categories.refetch();
          void taxClasses.refetch();
        }}
      />
    );
  }
  if (!categories.data || !taxClasses.data) return <LoadingState rows={6} />;
  return (
    <NewProductForm
      categories={categories.data}
      taxClasses={taxClasses.data}
      onCreate={async (request) => {
        const { productId } = await create.mutateAsync(request);
        router.push(`/staff/shop/products/${productId}`);
      }}
    />
  );
}
