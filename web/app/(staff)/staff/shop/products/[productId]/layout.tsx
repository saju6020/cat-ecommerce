'use client';

import * as React from 'react';
import Link from 'next/link';
import { notFound, usePathname, useRouter } from 'next/navigation';
import { isGuid } from '@/lib/validation/guid';
import { PermissionDeniedState } from '@/components/admin/PermissionDeniedState';
import { ErrorState } from '@/components/common/ErrorState';
import { LoadingState } from '@/components/common/LoadingState';
import { ProductEditorHeader } from '@/components/staff-shop/ProductEditorHeader';
import {
  ApiConflictError,
  ApiForbiddenError,
  ApiNotFoundError,
  ApiValidationError,
} from '@/lib/api/client';
import { describeApiError, SHOP_CODE_MESSAGES } from '@/lib/api/errors';
import type { ProductTransition } from '@/lib/api/staff-shop';
import { useProductTransition, useShopLocales, useStaffProduct } from '@/lib/hooks/useStaffShop';
import { CONFLICT_MESSAGE } from '@/lib/shop/submitSection';
import { cn } from '@/lib/utils';

/**
 * `/staff/shop/products/[productId]/**` — HO-SHP-002 §8.1, ADR-FE-034: route-based sections under one
 * layout that carries the status, the server's publish checklist and the status actions. On small
 * screens the section links become a select (§7).
 */
export default function ProductEditorLayout({
  children,
  params,
}: {
  children: React.ReactNode;
  params: Promise<{ productId: string }>;
}) {
  const { productId } = React.use(params);
  // S10: an id that is not a GUID can never be a product; answer 404 before any request is built.
  if (!isGuid(productId)) notFound();
  return <ProductEditorShell productId={productId}>{children}</ProductEditorShell>;
}

function ProductEditorShell({
  children,
  productId,
}: {
  children: React.ReactNode;
  productId: string;
}) {
  const router = useRouter();
  const pathname = usePathname();
  const product = useStaffProduct(productId);
  const locales = useShopLocales();
  const transition = useProductTransition(productId);
  const [error, setError] = React.useState<{ message: string; conflict: boolean } | null>(null);

  if (product.error instanceof ApiForbiddenError) return <PermissionDeniedState />;
  if (product.error instanceof ApiNotFoundError) {
    return (
      <ErrorState title="We couldn't find that product." description="It may have been removed." />
    );
  }
  if (product.isError) return <ErrorState onRetry={() => product.refetch()} />;
  if (!product.data) return <LoadingState rows={6} />;

  const p = product.data;
  const base = `/staff/shop/products/${productId}`;
  const sections = [
    { label: 'Details', href: base },
    ...(p.category === 'CatFood' && p.kind === 'Standard'
      ? [{ label: 'Food & labelling', href: `${base}/food` }]
      : []),
    { label: 'Variants & prices', href: `${base}/variants` },
    { label: 'Images', href: `${base}/images` },
    ...(p.kind === 'Bundle' ? [{ label: 'Kit components', href: `${base}/bundle` }] : []),
    { label: 'Related', href: `${base}/related` },
    ...((locales.data?.locales.length ?? 1) > 1
      ? [{ label: 'Translations', href: `${base}/translations` }]
      : []),
  ];

  async function run(next: ProductTransition) {
    setError(null);
    try {
      await transition.mutateAsync({ transition: next, expectedVersion: p.version });
    } catch (e) {
      if (e instanceof ApiConflictError) setError({ message: CONFLICT_MESSAGE, conflict: true });
      else if (e instanceof ApiValidationError)
        setError({ message: describeApiError(e, SHOP_CODE_MESSAGES), conflict: false });
      else setError({ message: describeApiError(e, SHOP_CODE_MESSAGES), conflict: false });
    }
  }

  return (
    <div className="flex flex-col gap-6">
      <ProductEditorHeader
        product={p}
        onTransition={run}
        error={error?.message}
        isConflict={error?.conflict}
        onReload={() => {
          void product.refetch().then((result) => {
            if (result.isSuccess) setError(null);
          });
        }}
      />
      <nav aria-label="Product sections" className="border-b border-border pb-2">
        <div className="md:hidden">
          <label htmlFor="section-select" className="sr-only">
            Section
          </label>
          <select
            id="section-select"
            value={sections.find((s) => s.href === pathname)?.href ?? base}
            onChange={(event) => router.push(event.target.value)}
            className="w-full rounded-[var(--radius-sm)] border border-border-strong bg-surface px-3 py-2 text-sm text-ink"
          >
            {sections.map((s) => (
              <option key={s.href} value={s.href}>
                {s.label}
              </option>
            ))}
          </select>
        </div>
        <div className="hidden flex-wrap gap-1 md:flex">
          {sections.map((s) => (
            <Link
              key={s.href}
              href={s.href}
              aria-current={pathname === s.href ? 'page' : undefined}
              className={cn(
                'rounded-[var(--radius-sm)] px-3 py-1.5 text-sm font-semibold no-underline',
                pathname === s.href
                  ? 'bg-accent-soft text-accent-ink'
                  : 'text-ink-muted hover:bg-surface-sunken',
              )}
            >
              {s.label}
            </Link>
          ))}
        </div>
      </nav>
      <div>{children}</div>
    </div>
  );
}
