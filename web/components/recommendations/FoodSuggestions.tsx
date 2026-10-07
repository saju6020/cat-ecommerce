import Link from 'next/link';
import { EmptyCatIllustration, EmptyState } from '@/components/common/EmptyState';
import { ProductCard } from '@/components/catalogue/ProductCard';
import { lifeStageLabel } from '@/lib/shop/labels';
import type { FoodRecommendations } from '@/lib/api/shop-customer';

function reasonText(
  reason: { code: string; values: Record<string, string> },
  catName: string,
): string {
  const v = reason.values;
  switch (reason.code) {
    case 'LifeStageMatch':
      return `Suits ${v.about === 'true' ? 'about ' : ''}${(v.lifeStage ? lifeStageLabel(v.lifeStage) : 'your cat').toLowerCase()} cats`;
    case 'Sterilised':
      return 'For neutered cats';
    case 'WeightManagement':
      return `Weight-management recipe — ${catName} weighs ${Number(v.weightKg).toFixed(1)} kg`;
    case 'ConditionMatch':
      return `May suit ${catName}'s recorded ${(v.focus ?? 'health').toLowerCase()} condition`;
    default:
      return 'Matches what we know about your cat';
  }
}

/**
 * HO-SHP-002 §11.5 `FoodSuggestions` — US-SHP-11. Each suggestion shows its REASONS as text; the
 * allergy exclusion is stated in a sentence (AC2); an `AddDateOfBirth` hint links to the cat's
 * profile; and the footnote says this is general guidance. Everything shown was decided by the API.
 */
export function FoodSuggestions({ data, lang }: { data: FoodRecommendations; lang?: string }) {
  const { cat } = data;
  return (
    <div className="flex flex-col gap-5">
      <header className="flex flex-col gap-1">
        <h1 className="font-display text-2xl font-semibold text-ink">
          Food suggestions for {cat.name}
        </h1>
        {cat.lifeStage && (
          <p className="text-sm text-ink-muted">
            Life stage: {cat.lifeStageIsEstimated ? 'about ' : ''}
            {lifeStageLabel(cat.lifeStage).toLowerCase()}
          </p>
        )}
      </header>

      {data.hints.includes('AddDateOfBirth') && (
        <p className="rounded-[var(--radius-sm)] bg-warm-soft p-3 text-sm text-warm-ink">
          Add {cat.name}&apos;s date of birth for suggestions that suit their age.{' '}
          <Link href={`/portal/cats/${cat.catId}`} className="font-semibold underline">
            Go to {cat.name}&apos;s profile
          </Link>
        </p>
      )}

      {data.excludedForAllergy.map((e) => (
        <p
          key={e.allergen}
          className="rounded-[var(--radius-sm)] bg-surface-sunken p-3 text-sm text-ink"
        >
          We left out {e.count} {e.count === 1 ? 'product' : 'products'} containing{' '}
          {e.allergen.toLowerCase()} (recorded allergy).
        </p>
      ))}

      {data.items.length === 0 ? (
        <EmptyState
          icon={<EmptyCatIllustration />}
          title="No suggestions right now"
          description="Nothing in stock suits this cat at the moment. Try again later."
        />
      ) : (
        <ul className="m-0 grid list-none grid-cols-1 gap-3 p-0 sm:grid-cols-2 lg:grid-cols-3">
          {data.items.map(({ item, reasons }) => (
            <li key={item.productId} className="flex flex-col gap-2">
              <ProductCard item={item} lang={lang} headingLevel="h2" />
              {reasons.length > 0 && (
                <ul aria-label={`Why ${item.name}`} className="m-0 list-disc pl-5 text-sm text-ink">
                  {reasons.map((reason) => (
                    <li key={reason.code}>{reasonText(reason, cat.name)}</li>
                  ))}
                </ul>
              )}
            </li>
          ))}
        </ul>
      )}
      <p className="text-xs text-ink-muted">
        General guidance — ask our vets for advice for your cat.
      </p>
    </div>
  );
}
