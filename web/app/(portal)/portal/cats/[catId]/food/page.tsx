'use client';

import { use } from 'react';
import { notFound } from 'next/navigation';
import { ApiNotFoundError } from '@/lib/api/client';
import { ErrorState } from '@/components/common/ErrorState';
import { LoadingState } from '@/components/common/LoadingState';
import { FoodSuggestions } from '@/components/recommendations/FoodSuggestions';
import { isGuid } from '@/lib/validation/guid';
import { useFoodRecommendations } from '@/lib/hooks/useShopCustomer';

/** `/portal/cats/[catId]/food` — US-SHP-11: food suggestions for one cat, with reasons. */
export default function CatFoodPage({ params }: { params: Promise<{ catId: string }> }) {
  const { catId } = use(params);
  // S10: a route id that is not a GUID can never be a cat.
  if (!isGuid(catId)) notFound();
  return <CatFood catId={catId} />;
}

function CatFood({ catId }: { catId: string }) {
  const food = useFoodRecommendations(catId, '');
  if (food.isPending) return <LoadingState rows={5} label="Finding food that suits your cat…" />;
  if (food.error instanceof ApiNotFoundError) {
    return (
      <ErrorState
        title="We couldn't find that cat."
        description="Choose one of your cats from My cats."
      />
    );
  }
  if (food.isError || !food.data) return <ErrorState onRetry={() => food.refetch()} />;
  return <FoodSuggestions data={food.data} />;
}
