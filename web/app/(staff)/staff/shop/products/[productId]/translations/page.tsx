'use client';

import { use, useState } from 'react';
import { EmptyState } from '@/components/common/EmptyState';
import { LoadingState } from '@/components/common/LoadingState';
import { TranslationsEditor } from '@/components/staff-shop/TranslationsEditor';
import {
  useRemoveProductTranslation,
  useSetProductTranslation,
  useShopLocales,
  useStaffProduct,
} from '@/lib/hooks/useStaffShop';

/** `/staff/shop/products/[productId]/translations` — per enabled language (US-SHP-14 AC3). Hidden with one. */
export default function ProductTranslationsPage({
  params,
}: {
  params: Promise<{ productId: string }>;
}) {
  const { productId } = use(params);
  const product = useStaffProduct(productId);
  const locales = useShopLocales();
  const save = useSetProductTranslation(productId);
  const remove = useRemoveProductTranslation(productId);
  // The editor is remounted only by an explicit, successful Reload. Keying it on `p.version`
  // remounted every locale's form whenever ONE locale was saved, losing typing in the others (S9).
  const [formKey, setFormKey] = useState(0);
  const p = product.data;
  if (!p || !locales.data) return <LoadingState rows={6} />;
  if (locales.data.locales.length <= 1) {
    return (
      <EmptyState
        title="Only one language is enabled"
        description="Translations appear here when a second content language is switched on."
      />
    );
  }
  return (
    <TranslationsEditor
      key={formKey}
      product={p}
      defaultLocale={locales.data.defaultLocale}
      locales={locales.data.locales}
      onReload={() => {
        void product.refetch().then((result) => {
          if (result.isSuccess) setFormKey((k) => k + 1);
        });
      }}
      onSave={(locale, values) =>
        save.mutateAsync({ expectedVersion: p.version, locale, ...values })
      }
      onRemove={(locale) => remove.mutateAsync({ locale, expectedVersion: p.version })}
    />
  );
}
