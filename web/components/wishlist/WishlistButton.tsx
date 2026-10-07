import Link from 'next/link';
import { Heart } from 'lucide-react';
import { Button, buttonVariants } from '@/components/ui/button';

export interface WishlistButtonProps {
  saved: boolean;
  pending?: boolean;
  onToggle: () => void;
  /** Present for a GUEST: the control becomes a sign-in link back to this product and variant
   *  (ADR-FE-038). */
  signInHref?: string;
  error?: string;
}

/** HO-SHP-002 §11.3 `WishlistButton` — a toggle for the selected SKU whose label says the state
 *  (*Save* / *Saved*); there is no `aria-pressed` as well, which announced the state twice (S15).
 *  While saving it is `aria-disabled`, not `disabled`, so focus stays on it. A guest is sent to
 *  sign in and returned (`?next=`). */
export function WishlistButton({
  saved,
  pending,
  onToggle,
  signInHref,
  error,
}: WishlistButtonProps) {
  if (signInHref) {
    return (
      <Link href={signInHref} className={buttonVariants({ variant: 'secondary' })}>
        <Heart size={16} aria-hidden="true" />
        Save
      </Link>
    );
  }
  return (
    <div className="flex flex-col gap-1">
      <Button
        variant="secondary"
        aria-disabled={pending || undefined}
        className="aria-disabled:opacity-50"
        onClick={() => {
          if (!pending) onToggle();
        }}
      >
        <Heart size={16} fill={saved ? 'currentColor' : 'none'} aria-hidden="true" />
        {saved ? 'Saved' : 'Save'}
      </Button>
      {error && (
        <p role="alert" className="text-xs font-semibold text-danger-ink">
          {error}
        </p>
      )}
    </div>
  );
}
