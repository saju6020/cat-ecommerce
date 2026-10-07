import Link from 'next/link';
import { Button, buttonVariants } from '@/components/ui/button';
import { EmptyState, EmptyCatIllustration } from '@/components/common/EmptyState';

export interface BookingCat {
  id: string;
  name: string;
  breed?: string | null;
}

/**
 * `components/booking/CatStep.tsx` — HO-APT-002 §11.1 step 1. The customer's alive cats, each with a
 * **Choose** button (not a radio group: choosing advances the flow, and arrow-keying through radios
 * would navigate away on every key press). The container skips this step entirely when there is one
 * cat or `catId` is given. An empty list links to the owner portal's add-a-cat screen (HO-CAT-002-ADDCAT),
 * which returns here through `addCatHref`'s `next` parameter.
 */
export function CatStep({
  cats,
  onSelect,
  addCatHref,
}: {
  cats: BookingCat[];
  onSelect: (catId: string) => void;
  /** `/portal/cats/new?next=/book…` — keeps the booking parameters across the detour. */
  addCatHref: string;
}) {
  if (cats.length === 0) {
    return (
      <EmptyState
        icon={<EmptyCatIllustration />}
        title="You don’t have a cat registered yet."
        description="Add your cat to book a visit. You will come straight back here."
        action={
          <Link href={addCatHref} className={buttonVariants()}>
            Add a cat
          </Link>
        }
      />
    );
  }
  return (
    <section aria-labelledby="cat-step-heading" className="flex flex-col gap-3">
      <h2 id="cat-step-heading" className="text-sm font-semibold text-ink">
        Which cat is this for?
      </h2>
      <ul className="m-0 flex list-none flex-col gap-2 p-0">
        {cats.map((cat) => (
          <li
            key={cat.id}
            className="flex flex-wrap items-center justify-between gap-3 rounded-[var(--radius-md)] border border-border bg-surface px-4 py-3"
          >
            <div className="text-sm">
              <p className="font-semibold text-ink">{cat.name}</p>
              {cat.breed && <p className="text-ink-muted">{cat.breed}</p>}
            </div>
            <Button aria-label={`Choose ${cat.name}`} onClick={() => onSelect(cat.id)}>
              Choose
            </Button>
          </li>
        ))}
      </ul>
    </section>
  );
}
