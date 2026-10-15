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
      <DialogContent returnFocusRef={returnFocusRef} className="max-h-[90vh] overflow-y-auto">
        <DialogHeader>
          <DialogTitle>Which cat is this for?</DialogTitle>
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
          <fieldset className="m-0 flex min-w-0 flex-col gap-2 border-0 p-0">
            <legend className="sr-only">Choose a cat</legend>
            {cats.map((cat) => (
              <div
                key={cat.id}
                className={`rounded-[var(--radius-md)] border p-3 ${cat.authorised ? 'border-border-strong bg-surface' : 'border-border bg-surface-sunken'}`}
              >
                <label className={`flex items-center gap-3 text-sm ${cat.authorised ? 'cursor-pointer' : 'cursor-not-allowed text-ink-muted'}`}>
                  <input
                    type="radio"
                    name="rx-cat"
                    value={cat.id}
                    disabled={!cat.authorised}
                    checked={selected === cat.id}
                    onChange={() => setSelected(cat.id)}
                    aria-describedby={cat.authorised ? undefined : `rx-${cat.id}-why`}
                    className="h-4 w-4 accent-[var(--accent)]"
                  />
                  <span className="font-semibold text-ink">{cat.name}</span>
                </label>
                {!cat.authorised && (
                  <p id={`rx-${cat.id}-why`} className="mt-1 pl-7 text-xs text-ink-muted">
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
            ))}
          </fieldset>
        )}
        {error && (
          <p role="alert" className="text-sm font-semibold text-danger-ink">
            {error}
          </p>
        )}
        <DialogFooter>
          <Button variant="secondary" onClick={() => onOpenChange(false)} disabled={pending}>
            Cancel
          </Button>
          <Button disabled={!selected || pending} aria-busy={pending} onClick={() => selected && onChoose(selected)}>
            {pending ? 'Adding…' : 'Add to cart'}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
