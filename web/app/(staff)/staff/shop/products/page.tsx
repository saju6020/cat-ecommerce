'use client';

import { Suspense } from 'react';
import { useSearchParams } from 'next/navigation';
import { PermissionDeniedState } from '@/components/admin/PermissionDeniedState';
import { LoadingState } from '@/components/common/LoadingState';
import {
  StaffProductTable,
  type StaffProductFilterName,
} from '@/components/staff-shop/StaffProductTable';
import { ApiForbiddenError } from '@/lib/api/client';
import { staffProductsExportUrl, type StaffProductListParams } from '@/lib/api/staff-shop';
import { useStaffProducts } from '@/lib/hooks/useStaffShop';
import { useUrlFilterState } from '@/lib/hooks/useUrlFilterState';

const FIELDS = ['status', 'category', 'brand', 'kind', 'listed', 'barcode', 'sort'] as const;
const PAGE_SIZE = 25;

function ProductListContent() {
  const { values, setValue, flush } = useUrlFilterState(FIELDS, {}, ['brand', 'barcode']);
  const searchParams = useSearchParams();
  const q = searchParams.get('q') ?? '';
  const pageNumber = Math.max(1, Number(searchParams.get('page')) || 1);
  const params: StaffProductListParams = {
    q,
    status: values.status,
    category: values.category,
    brand: values.brand,
    kind: values.kind,
    listed: values.listed,
    barcode: values.barcode,
    sort: values.sort,
    pageNumber,
    pageSize: PAGE_SIZE,
  };
  const products = useStaffProducts(params);
  if (products.error instanceof ApiForbiddenError) return <PermissionDeniedState />;

  return (
    <StaffProductTable
      items={products.data?.items}
      totalCount={products.data?.totalCount}
      pageSize={PAGE_SIZE}
      isLoading={products.isLoading}
      isError={products.isError}
      onRetry={() => products.refetch()}
      filters={values}
      onFilter={(name: StaffProductFilterName, value) => setValue(name, value)}
      onFlush={(name) => flush(name)}
      exportHref={staffProductsExportUrl(params)}
    />
  );
}

/** `/staff/shop/products` — US-SHP-01…03, UI-4: filter, sort, paginate, export CSV (all in the URL). */
export default function StaffProductsPage() {
  return (
    <Suspense fallback={<LoadingState rows={6} variant="table" />}>
      <ProductListContent />
    </Suspense>
  );
}
