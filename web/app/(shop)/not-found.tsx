import Link from 'next/link';
import { EmptyCatIllustration, EmptyState } from '@/components/common/EmptyState';
import { buttonVariants } from '@/components/ui/button';

/** A draft, archived, unlisted or unknown slug is a not-found page on every public route. */
export default function ShopNotFound() {
  return (
    <div className="flex flex-col gap-4">
      <h1 className="font-display text-2xl font-semibold text-ink">Page not found</h1>
      <EmptyState
        icon={<EmptyCatIllustration />}
        title="We couldn't find that product."
        description="It may have been removed, or the address may be wrong."
        action={
          <Link href="/shop/products" className={buttonVariants({ variant: 'primary' })}>
            Browse all products
          </Link>
        }
      />
    </div>
  );
}
