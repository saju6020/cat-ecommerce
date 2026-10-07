'use client';

import { use, useState } from 'react';
import { EmptyState } from '@/components/common/EmptyState';
import { LoadingState } from '@/components/common/LoadingState';
import { FoodDetailsForm } from '@/components/staff-shop/FoodDetailsForm';
import { setFoodDetails, setPrescriptionRequired } from '@/lib/api/staff-shop';
import { useStaffProduct, useTwoStepSave } from '@/lib/hooks/useStaffShop';

/**
 * `/staff/shop/products/[productId]/food` — food & labelling (US-SHP-03). Cat Food only (U-D2).
 * Saves the labelling, then the prescription flag if it changed; the form is keyed only by an
 * explicit Reload, so a refused second command keeps what was typed (NFR-USA-5).
 */
export default function ProductFoodPage({ params }: { params: Promise<{ productId: string }> }) {
  const { productId } = use(params);
  const product = useStaffProduct(productId);
  const twoStep = useTwoStepSave(productId);
  const [formKey, setFormKey] = useState(0);
  const p = product.data;
  if (!p) return <LoadingState rows={8} />;
  if (p.category !== 'CatFood' || p.kind !== 'Standard') {
    return (
      <EmptyState
        title="Food details don't apply to this product"
        description="Only standard Cat Food products have labelling and a prescription flag."
      />
    );
  }
  return (
    <FoodDetailsForm
      key={formKey}
      product={p}
      onReload={() => {
        // Only a successful refetch brings new values to show; after a failed one the typed
        // values stay (S13).
        void product.refetch().then((result) => {
          if (result.isSuccess) setFormKey((k) => k + 1);
        });
      }}
      onSave={(v) => {
        const analysis =
          v.proteinPercent === ''
            ? null
            : {
                proteinPercent: Number(v.proteinPercent),
                fatPercent: Number(v.fatPercent),
                fibrePercent: Number(v.fibrePercent),
                moisturePercent: Number(v.moisturePercent),
              };
        return twoStep(
          () =>
            setFoodDetails(productId, {
              expectedVersion: p.version,
              manufacturer: v.manufacturer || null,
              lifeStage: v.lifeStage || null,
              proteinSource: v.proteinSource || null,
              ingredientList: v.ingredientList || null,
              guaranteedAnalysis: analysis,
              feedingGuide: v.feedingGuide || null,
              storageInstructions: v.storageInstructions || null,
            }),
          v.prescriptionRequired !== p.prescriptionRequired
            ? () =>
                setPrescriptionRequired(productId, {
                  expectedVersion: p.version + 1,
                  prescriptionRequired: v.prescriptionRequired,
                })
            : null,
          'Labelling saved; the prescription setting was not changed',
        );
      }}
    />
  );
}
