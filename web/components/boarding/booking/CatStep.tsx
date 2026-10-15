'use client';

import { Button } from '@/components/ui/button';
import { EmptyState } from '@/components/common/EmptyState';
import { FormErrorAlert } from '@/components/boarding/shared/FormBits';
import { CatHeaderCompact } from '@/components/boarding/shared/CatHeaderCompact';
import type { CatListItem } from '@/lib/api/cats';

/**
 * `components/boarding/booking/CatStep.tsx` — HO-BRD-002 §11.1 step 3, US-BRD-07. The customer's
 * living cats, selectable up to the chosen type's capacity. Over capacity is blocked here — Continue
 * is disabled **with the capacity stated** — and the server's `UnitCapacityExceeded` message is
 * shown verbatim (`serverError`) if it still happens. Each cat carries its high-risk badge and
 * handling note (UI-7).
 */
export function CatStep({
  cats,
  selectedIds,
  onToggle,
  capacity,
  unitLabel,
  onContinue,
  onBack,
  pending,
  serverError,
  addCatHref,
}: {
  cats: CatListItem[];
  selectedIds: string[];
  onToggle: (catId: string) => void;
  capacity: number | null;
  unitLabel: string;
  onContinue: () => void;
  onBack: () => void;
  pending: boolean;
  serverError: string | null;
  addCatHref?: string;
}) {
  const overCapacity = capacity !== null && selectedIds.length > capacity;
  const nothingSelected = selectedIds.length === 0;
  const reason = overCapacity
    ? `A ${unitLabel} unit holds up to ${capacity} cat${capacity === 1 ? '' : 's'}. You have chosen ${selectedIds.length}.`
    : nothingSelected
      ? 'Choose at least one cat.'
      : null;

  if (cats.length === 0) {
    return (
      <EmptyState
        title="There are no cats to book in yet."
        description="Add your cat to your account first, then come back to finish the booking."
        action={
          addCatHref ? (
            <a className="text-sm font-semibold text-accent-ink underline" href={addCatHref}>
              Add a cat
            </a>
          ) : undefined
        }
      />
    );
  }

  return (
    <section aria-labelledby="cats-heading" className="flex flex-col gap-4">
      <h2 id="cats-heading" className="font-display text-lg font-semibold text-ink">
        Which cats are staying?
      </h2>
      <fieldset className="flex flex-col gap-2">
        <legend className="sr-only">Cats staying in this booking</legend>
        {cats.map((cat) => {
          const checked = selectedIds.includes(cat.id);
          return (
            <label
              key={cat.id}
              htmlFor={`cat-${cat.id}`}
              className="flex cursor-pointer items-start gap-3 rounded-[var(--radius-md)] border border-border bg-surface p-3 has-[:checked]:border-accent has-[:checked]:bg-accent-soft"
            >
              <input
                id={`cat-${cat.id}`}
                type="checkbox"
                className="mt-1 h-5 w-5"
                checked={checked}
                onChange={() => onToggle(cat.id)}
              />
              <CatHeaderCompact
                name={cat.name}
                isHighRisk={cat.isHighRisk}
                handlingNote={cat.handlingNote}
                hideNote
              />
            </label>
          );
        })}
      </fieldset>
      <p className="text-sm text-ink-muted" aria-live="polite">
        {selectedIds.length} chosen
        {capacity !== null ? ` · a ${unitLabel} unit holds up to ${capacity}` : ''}
      </p>
      {reason && (
        <p role="alert" className="text-sm font-semibold text-danger-ink">
          {reason}
        </p>
      )}
      <FormErrorAlert message={serverError} />
      <div className="flex flex-wrap gap-3">
        <Button variant="secondary" onClick={onBack}>
          Back
        </Button>
        <Button
          onClick={onContinue}
          disabled={overCapacity || nothingSelected || pending}
          aria-busy={pending}
        >
          {pending ? 'Saving…' : 'Continue'}
        </Button>
      </div>
    </section>
  );
}
