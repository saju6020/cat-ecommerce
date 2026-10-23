import Link from 'next/link';
import { Heart } from 'lucide-react';
import { cn } from '@/lib/utils';

export interface WishlistButtonProps {
  saved: boolean;
  pending?: boolean;
  onToggle: () => void;
  /** Present for a GUEST: the control becomes a sign-in link back to this product and variant
   *  (ADR-FE-038). */
  signInHref?: string;
  error?: string;
}

const ROUND =
  'grid size-12 flex-none place-items-center rounded-pill border border-border-strong bg-surface p-0 text-warm-ink ' +
  'hover:bg-warm-soft focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-accent';

/** HO-SHP-002 §11.3 `WishlistButton` — the round 48px heart of the buy row. A toggle for the
 *  selected SKU whose label says the state (*Save* / *Saved*, as hidden text); there is no
 *  `aria-pressed` as well, which announced the state twice (S15). While saving it is
 *  `aria-disabled`, not `disabled`, so focus stays on it. A guest is sent to sign in and returned.
 *  Used inside a flex row: the error wraps onto its own line. */
export function WishlistButton({
  saved,
  pending,
  onToggle,
  signInHref,
  error,
}: WishlistButtonProps) {
  if (signInHref) {
    return (
      <Link href={signInHref} className={ROUND}>
        <Heart size={20} aria-hidden="true" />
        <span className="sr-only">Save</span>
      </Link>
    );
  }
  return (
    <>
      <button
        type="button"
        aria-disabled={pending || undefined}
        className={cn(ROUND, 'aria-disabled:opacity-50')}
        onClick={() => {
          if (!pending) onToggle();
        }}
      >
        <Heart size={20} fill={saved ? 'currentColor' : 'none'} aria-hidden="true" />
        <span className="sr-only">{saved ? 'Saved' : 'Save'}</span>
      </button>
      {error && (
        <p role="alert" className="m-0 basis-full text-xs font-semibold text-danger-ink">
          {error}
        </p>
      )}
    </>
  );
}
