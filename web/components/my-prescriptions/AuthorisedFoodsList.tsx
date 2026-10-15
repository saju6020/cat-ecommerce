'use client';

import Link from 'next/link';
import { Ban, CheckCircle2, Clock } from 'lucide-react';
import { Badge } from '@/components/ui/badge';
import { EmptyCatIllustration, EmptyState } from '@/components/common/EmptyState';
import { ErrorState } from '@/components/common/ErrorState';
import { LoadingState } from '@/components/common/LoadingState';
import type { MyDietAuthorisation } from '@/lib/api/my-prescriptions';
import { formatDay } from '@/lib/format/pharmacy';

/**
 * `AuthorisedFoodsList` — HO-PHM-002 §11.8 (US-PHM-08). The foods a vet has authorised, per cat: the
 * product, who it is for, the last day and the effective status in words. An active row says
 * *"You can buy this for {cat} in the shop."* (plain text until HO-SHP-002 gives a route to link to); an
 * expired or revoked row says when it ended and links to book a consultation. The shop's own gate is
 * HO-SHP-002 / HO-ORD-002's (the storefront half of A13).
 */
export function AuthorisedFoodsList({
  rows,
  isLoading,
  isError,
  onRetry,
}: {
  rows: MyDietAuthorisation[] | undefined;
  isLoading: boolean;
  isError: boolean;
  onRetry: () => void;
}) {
  if (isLoading) return <LoadingState variant="card" rows={2} label="Loading authorised foods…" />;
  if (isError || !rows)
    return (
      <ErrorState title="We couldn’t load your authorised foods just now." onRetry={onRetry} />
    );
  if (rows.length === 0) {
    return (
      <EmptyState
        icon={<EmptyCatIllustration />}
        title="No authorised foods."
        description="When a vet authorises a therapeutic diet for one of your cats, it appears here."
      />
    );
  }
  return (
    <ul aria-label="Authorised foods" className="m-0 flex list-none flex-col gap-3 p-0">
      {rows.map((row) => (
        <li key={row.id} className="rounded-[var(--radius-md)] border border-border bg-surface p-4">
          <p className="text-xs font-semibold uppercase tracking-wide text-ink-muted">
            {row.catName}
          </p>
          <h2 className="mt-0.5 text-base font-semibold text-ink">{row.productName}</h2>
          <p className="mt-1 flex flex-wrap items-center gap-2 text-sm text-ink">
            {row.status === 'Active' ? (
              <Badge variant="success" icon={<CheckCircle2 size={12} aria-hidden="true" />}>
                Active
              </Badge>
            ) : row.status === 'Expired' ? (
              <Badge variant="neutral" icon={<Clock size={12} aria-hidden="true" />}>
                Expired
              </Badge>
            ) : (
              <Badge variant="danger" icon={<Ban size={12} aria-hidden="true" />}>
                Revoked
              </Badge>
            )}
            <span>
              {row.status === 'Active'
                ? `Authorised until ${formatDay(row.validTo)}`
                : row.status === 'Expired'
                  ? `Ended on ${formatDay(row.validTo)}`
                  : 'Withdrawn by your vet'}
            </span>
          </p>
          <p className="mt-1 text-sm text-ink-muted">
            {row.status === 'Active' ? (
              `You can buy this for ${row.catName} in the shop.`
            ) : (
              <>
                To keep buying it, ask your vet to authorise it again.{' '}
                <Link
                  href={`/book?catId=${row.catId}`}
                  className="font-semibold text-accent-ink underline"
                >
                  Book a consultation
                </Link>
              </>
            )}
          </p>
        </li>
      ))}
    </ul>
  );
}
