'use client';

import Link from 'next/link';
import { useState } from 'react';
import { Button } from '@/components/ui/button';
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
  useDialogReturnFocus,
} from '@/components/ui/dialog';
import { cn } from '@/lib/utils';

export interface CatChoice {
  id: string;
  name: string;
  /** From the server's authorisation response for THIS food — never decided here. */
  authorised: boolean;
}

/**
 * `components/cart/PrescriptionCatDialog.tsx` — HO-ORD-002 §11.1. *Which cat is this for?* The signed-in
 * customer's cats with their authorisation state from the server; a cat without one is disabled with
 * *"No current authorisation for this food"* and a **Book a consultation** link
 * (`/book?catId=…&category=Consultation`). The dialog decides nothing: the cart endpoint rechecks.
 *
 * Hearth shapes (docs/design/orders/PrescriptionCatDialog.dc.html) but neutral colours: this is a
 * medical gate, so there is no warm band and no cat art. The dialog portals out of the page, so it
 * carries `data-theme="customer"` itself to get the customer pill and card tokens.
 */
export function PrescriptionCatDialog({
  open,
  onOpenChange,
  productName,
  cats,
  isLoading,
  isError,
  pending,
  error,
  onChoose,
}: {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  productName: string;
  cats: CatChoice[];
  isLoading: boolean;
  isError: boolean;
  pending: boolean;
  error: string | null;
  onChoose: (catId: string) => void;
}) {
  const returnFocusRef = useDialogReturnFocus(open);
  const [selected, setSelected] = useState<string | null>(null);

  return (
    <Dialog open={open} onOpenChange={(next) => !pending && onOpenChange(next)}>
      <DialogContent
        data-theme="customer"
        returnFocusRef={returnFocusRef}
        className="max-h-[90vh] max-w-[480px] overflow-y-auto rounded-3xl p-7"
      >
        <DialogHeader>
          <DialogTitle className="font-display text-2xl font-semibold">
            Which cat is this for?
          </DialogTitle>
          <DialogDescription>
            {`${productName} is a prescription food. It can be bought only for a cat with a current vet authorisation.`}
          </DialogDescription>
        </DialogHeader>
        {isLoading && (
          <p role="status" className="text-sm text-ink-muted">
            Checking your cats…
          </p>
        )}
        {isError && (
          <p role="alert" className="text-sm font-semibold text-danger-ink">
            We couldn&rsquo;t load your cats. Please close this and try again.
          </p>
        )}
        {!isLoading && !isError && cats.length === 0 && (
          <p className="text-sm text-ink-muted">
            You haven&rsquo;t added a cat yet.{' '}
            <Link href="/portal/cats/new" className="font-semibold text-accent-ink underline">
              Add your cat
            </Link>
          </p>
        )}
        {cats.length > 0 && (
          <fieldset className="m-0 flex min-w-0 flex-col gap-2.5 border-0 p-0">
            <legend className="sr-only">Choose a cat</legend>
            {cats.map((cat) => {
              const checked = selected === cat.id;
              return (
                <div
                  key={cat.id}
                  className={cn(
                    'rounded-card border px-[18px] py-3.5',
                    !cat.authorised
                      ? 'border-border bg-surface-sunken'
                      : checked
                        ? 'border-accent bg-surface ring-1 ring-accent'
                        : 'border-border bg-surface',
                  )}
                >
                  <label
                    className={cn(
                      'relative flex items-center gap-3.5 text-sm has-[:focus-visible]:outline-2 has-[:focus-visible]:outline-offset-2 has-[:focus-visible]:outline-accent',
                      cat.authorised ? 'cursor-pointer' : 'cursor-not-allowed text-ink-muted',
                    )}
                  >
                    <input
                      type="radio"
                      name="rx-cat"
                      value={cat.id}
                      disabled={!cat.authorised}
                      checked={checked}
                      onChange={() => setSelected(cat.id)}
                      aria-describedby={cat.authorised ? undefined : `rx-${cat.id}-why`}
                      className="absolute inset-0 size-full cursor-pointer opacity-0 disabled:cursor-not-allowed"
                    />
                    <strong
                      className={cn(
                        'flex-1 text-base',
                        cat.authorised ? 'text-ink' : 'text-ink-muted',
                      )}
                    >
                      {cat.name}
                    </strong>
                    <span
                      aria-hidden="true"
                      className={cn(
                        'size-6 flex-none rounded-pill border-2 border-border-strong bg-surface',
                        checked &&
                          'border-accent bg-accent shadow-[inset_0_0_0_4px_var(--surface)]',
                      )}
                    />
                  </label>
                  {!cat.authorised && (
                    <p
                      id={`rx-${cat.id}-why`}
                      className="m-0 mt-1 text-[13px] font-semibold text-warning-ink"
                    >
                      No current authorisation for this food.{' '}
                      <Link
                        href={`/book?catId=${encodeURIComponent(cat.id)}&category=Consultation`}
                        className="font-semibold text-accent-ink underline"
                      >
                        Book a consultation
                      </Link>
                    </p>
                  )}
                </div>
              );
            })}
          </fieldset>
        )}
        {error && (
          <p role="alert" className="text-sm font-semibold text-danger-ink">
            {error}
          </p>
        )}
        <DialogFooter>
          <Button
            variant="pillSecondary"
            size="pillMd"
            onClick={() => onOpenChange(false)}
            disabled={pending}
          >
            Cancel
          </Button>
          <Button
            variant="pill"
            size="pillMd"
            disabled={!selected || pending}
            aria-busy={pending}
            onClick={() => selected && onChoose(selected)}
          >
            {pending ? 'Adding…' : 'Add to cart'}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
