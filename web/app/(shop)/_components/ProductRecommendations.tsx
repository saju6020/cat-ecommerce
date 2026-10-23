'use client';

import { RecommendationBlocks } from '@/components/recommendations/RecommendationBlocks';
import { LoadingState } from '@/components/common/LoadingState';
import { useRecommendations } from '@/lib/hooks/useShopCatalogue';

/** Fetched client side so the server's exclusions reflect the session (US-SHP-08). A failure just
 *  omits the blocks — recommendations never block the product page. */
export function ProductRecommendations({ slug, lang }: { slug: string; lang: string }) {
  const { data, isPending } = useRecommendations(slug, lang);
  if (isPending) return <LoadingState rows={2} label="Loading suggestions…" />;
  if (!data) return null;
  return (
    <RecommendationBlocks recommendations={data} lang={lang || undefined} hideFrequentlyBought />
  );
}
