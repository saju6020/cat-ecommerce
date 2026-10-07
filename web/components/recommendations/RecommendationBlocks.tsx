import { ProductGrid } from '@/components/catalogue/ProductGrid';
import { lifeStageAudience } from '@/lib/shop/labels';
import type { Recommendations } from '@/lib/api/shop-catalogue';

/**
 * HO-SHP-002 §11.3 `RecommendationBlocks` — US-SHP-08. *Frequently bought together*, *You might also
 * like* (up-sell) and *Recommended for {kittens / adult cats / senior cats}* — or *for your cats*
 * when the server's `basis` is `YourCats`. Each block is a small grid of exactly what the server
 * returned (its exclusions included); an empty block is not rendered.
 */
export function RecommendationBlocks({
  recommendations,
  lang,
}: {
  recommendations: Recommendations;
  lang?: string;
}) {
  const { frequentlyBoughtTogether, upSell, lifeStage } = recommendations;
  const lifeStageHeading =
    lifeStage.basis === 'YourCats'
      ? 'Recommended for your cats'
      : `Recommended for ${lifeStageAudience(lifeStage.lifeStages)}`;
  const blocks = [
    { id: 'rec-fbt', heading: 'Frequently bought together', items: frequentlyBoughtTogether },
    { id: 'rec-upsell', heading: 'You might also like', items: upSell },
    { id: 'rec-life', heading: lifeStageHeading, items: lifeStage.items },
  ].filter((block) => block.items.length > 0);
  if (blocks.length === 0) return null;
  return (
    <div className="flex flex-col gap-8">
      {blocks.map((block) => (
        <section key={block.id} aria-labelledby={block.id} className="flex flex-col gap-3">
          <h2 id={block.id} className="font-display text-xl font-semibold text-ink">
            {block.heading}
          </h2>
          <ProductGrid items={block.items} lang={lang} headingLevel="h3" />
        </section>
      ))}
    </div>
  );
}
