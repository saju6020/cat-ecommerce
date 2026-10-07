'use client';

import { use, useState } from 'react';
import { LoadingState } from '@/components/common/LoadingState';
import { ProductDetailsForm } from '@/components/staff-shop/ProductDetailsForm';
import { changeProductSlug, updateProductDetails } from '@/lib/api/staff-shop';
import { useTaxClasses } from '@/lib/hooks/useAdminCatalogue';
import { useShopCategories } from '@/lib/hooks/useShopCatalogue';
import { useStaffCountries, useStaffProduct, useTwoStepSave } from '@/lib/hooks/useStaffShop';

/**
 * `/staff/shop/products/[productId]` — details (US-SHP-01). Saves the details, then the slug if it
 * changed. The form is keyed only by an explicit Reload (never by `version`), so it stays mounted
 * while the two commands run and when the second is refused (NFR-USA-5).
 */
export default function ProductDetailsPage({ params }: { params: Promise<{ productId: string }> }) {
  const { productId } = use(params);
  const product = useStaffProduct(productId);
  const categories = useShopCategories();
  const taxClasses = useTaxClasses();
  const countries = useStaffCountries();
  const twoStep = useTwoStepSave(productId);
  const [formKey, setFormKey] = useState(0);
  const p = product.data;
  if (!p || !categories.data || !taxClasses.data || !countries.data)
    return <LoadingState rows={8} />;

  return (
    <ProductDetailsForm
      key={formKey}
      product={p}
      categories={categories.data}
      taxClasses={taxClasses.data}
      countries={countries.data}
      onReload={() => {
        // Only a successful refetch brings new values to show; after a failed one the typed
        // values stay (S13).
        void product.refetch().then((result) => {
          if (result.isSuccess) setFormKey((k) => k + 1);
        });
      }}
      onSave={(values) =>
        twoStep(
          () =>
            updateProductDetails(productId, {
              expectedVersion: p.version,
              name: values.name,
              brand: values.brand,
              description: values.description,
              category: p.category,
              subcategory: values.subcategory || null,
              tags: values.tagList,
              taxClassId: values.taxClassId,
              countryOfOriginCode: values.countryOfOriginCode || null,
              listedOnStorefront: values.listedOnStorefront,
              isExpiryTracked: values.isExpiryTracked,
              healthFocus: values.healthFocus,
            }),
          !p.firstPublishedOn && values.slug !== p.slug
            ? () =>
                changeProductSlug(productId, { expectedVersion: p.version + 1, slug: values.slug })
            : null,
          'Details saved; the web address was not changed',
        )
      }
    />
  );
}
