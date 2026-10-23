import Link from 'next/link';
import { unstable_noStore as noStore } from 'next/cache';
import { ErrorState } from '@/components/common/ErrorState';
import { buttonVariants } from '@/components/ui/button';

/**
 * The storefront's server-rendered "couldn't load" state (S19). A Server Component that fails to
 * read the API used to `throw`, and `next start` then answered 500; this renders a real page in the
 * shell instead. A plain link retries (no JavaScript needed).
 */
export function ShopLoadError({ retryHref, error }: { retryHref: string; error: unknown }) {
  // The failure is ours to see in the server log; the visitor gets plain words only.
  console.error('Storefront read failed:', error);
  // A failure must not be cached or indexed: it is not the page's content.
  noStore();
  return (
    <div className="flex flex-col gap-3">
      <meta name="robots" content="noindex" />
      <ErrorState
        title="We couldn't load the shop just now."
        description="This does not mean the product is gone — please try again."
      />
      <Link
        href={retryHref}
        className={buttonVariants({
          variant: 'pillSecondary',
          size: 'pillMd',
          className: 'self-start',
        })}
      >
        Try again
      </Link>
    </div>
  );
}
