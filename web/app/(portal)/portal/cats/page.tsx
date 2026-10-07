'use client';

import { Suspense } from 'react';
import Link from 'next/link';
import { useRouter, useSearchParams } from 'next/navigation';
import { Cat as CatIcon } from 'lucide-react';
import { Button, buttonVariants } from '@/components/ui/button';
import { LoadingState } from '@/components/common/LoadingState';
import { ErrorState } from '@/components/common/ErrorState';
import { EmptyState, EmptyCatIllustration } from '@/components/common/EmptyState';
import { HighRiskBadge } from '@/components/cat/HighRiskBadge';
import { useCats } from '@/lib/hooks/useCats';

/**
 * "My cats" — US-CAT-01 (HO-CAT-002 §8.3), the owner-facing presentation variant of the same
 * list the staff screen uses (§4.2 — "portal views as presentation variants, not
 * permission-gated ones"). Card grid, matching `docs/design/cat-profile/Portal.dc.html`'s warm
 * round avatars, rather than `DataTable`'s table — a deliberate per-screen choice, not a second
 * generic list component.
 *
 * Not scoped to a single owner: identity is one hardcoded staff user everywhere (§4.2/§20.3),
 * with no owner sign-in yet, so there is no real owner id to filter by. Showing every cat here
 * (with a visible note saying so) is more honest than filtering by a fabricated id.
 */
function MyCatsPageContent() {
  const router = useRouter();
  const searchParams = useSearchParams();
  const page = Math.max(1, Number(searchParams.get('page')) || 1);
  const pageSize = 12;
  const { data, isLoading, isError, refetch } = useCats({ pageNumber: page, pageSize });
  const hasNextPage = data ? page * pageSize < data.totalCount : false;

  function goToPage(next: number) {
    router.replace(`/portal/cats?page=${next}`, { scroll: false });
  }

  return (
    <div className="flex flex-col gap-6">
      <div className="flex flex-wrap items-start justify-between gap-3">
        <div>
          <h1 className="font-display text-2xl font-semibold text-ink">My cats</h1>
          <p className="mt-1 text-sm text-ink-muted">
            Owner sign-in isn&rsquo;t available yet, so this shows every registered cat rather than
            only yours.
          </p>
        </div>
        <Link href="/portal/cats/new" className={buttonVariants()}>
          Add a cat
        </Link>
      </div>

      {isLoading ? (
        <LoadingState variant="card" rows={4} />
      ) : isError ? (
        <ErrorState title="We couldn't load your cats just now." onRetry={() => refetch()} />
      ) : !data || data.items.length === 0 ? (
        <EmptyState
          icon={<EmptyCatIllustration />}
          title="No cats registered yet."
          description="Add your cat and it will appear here."
          action={
            <Link href="/portal/cats/new" className={buttonVariants()}>
              Add a cat
            </Link>
          }
        />
      ) : (
        <>
          <ul className="grid grid-cols-2 gap-4 sm:grid-cols-3 lg:grid-cols-4">
            {data.items.map((cat) => (
              <li key={cat.id}>
                <Link
                  href={`/portal/cats/${cat.id}`}
                  className="flex flex-col items-center gap-2 rounded-[var(--radius-md)] border border-border bg-surface p-5 text-center no-underline hover:border-border-strong"
                >
                  <span className="flex h-16 w-16 items-center justify-center rounded-full bg-warm-soft text-warm-ink">
                    <CatIcon size={28} aria-hidden="true" />
                  </span>
                  <span className="font-display text-base font-semibold text-ink">{cat.name}</span>
                  <span className="text-xs text-ink-muted">{cat.breed ?? cat.species}</span>
                  {cat.isHighRisk && <HighRiskBadge handlingNote={null} variant="compact" />}
                </Link>
              </li>
            ))}
          </ul>

          <nav
            aria-label="Pages"
            className="flex items-center justify-between text-sm text-ink-muted"
          >
            <span>Page {page}</span>
            <div className="flex gap-2">
              <Button
                variant="secondary"
                size="sm"
                disabled={page <= 1}
                onClick={() => goToPage(page - 1)}
              >
                Previous
              </Button>
              <Button
                variant="secondary"
                size="sm"
                disabled={!hasNextPage}
                onClick={() => goToPage(page + 1)}
              >
                Next
              </Button>
            </div>
          </nav>
        </>
      )}
    </div>
  );
}

export default function MyCatsPage() {
  return (
    <Suspense>
      <MyCatsPageContent />
    </Suspense>
  );
}
