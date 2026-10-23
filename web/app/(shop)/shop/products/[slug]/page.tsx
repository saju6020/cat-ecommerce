import type { Metadata } from 'next';
import Link from 'next/link';
import { notFound } from 'next/navigation';
import { BundleContents } from '@/components/product/BundleContents';
import { ProductDetailsTabs } from '@/components/product/ProductDetailsTabs';
import { ProductHeading } from '@/components/product/ProductHeading';
import { MaintenanceNotice } from '@/components/shop/MaintenanceNotice';
import { ApiNotFoundError } from '@/lib/api/client';
import {
  getOriginServer,
  getProductDetailServer,
  getProductReviewsServer,
} from '@/lib/api/shop-catalogue';
import { defaultSku, isMaintenanceError, plainExcerpt, settle } from '@/lib/shop/helpers';
import { categoryLabel } from '@/lib/shop/labels';
import { ProductDetailsRow } from '../../../_components/ProductDetailsRow';
import { ProductPurchase } from '../../../_components/ProductPurchase';
import { ProductRecommendations } from '../../../_components/ProductRecommendations';
import { ProductReviews } from '../../../_components/ProductReviews';

type Props = {
  params: Promise<{ slug: string }>;
  searchParams: Promise<Record<string, string | string[] | undefined>>;
};

const one = (value: string | string[] | undefined) =>
  (Array.isArray(value) ? value[0] : value) ?? '';

export async function generateMetadata({ params, searchParams }: Props): Promise<Metadata> {
  const { slug } = await params;
  const lang = one((await searchParams).lang);
  const detail = await settle(getProductDetailServer(slug, lang || undefined));
  if (!detail.ok) return { title: 'Product | PurrCare Shop' };
  const { name, brand, description } = detail.value;
  return {
    title: `${name} — ${brand} | PurrCare Shop`,
    description: plainExcerpt(description, 155) || `${brand} ${name} at PurrCare Shop.`,
    // One canonical address per product, whichever variant or language is open (FR-SHP-2).
    alternates: { canonical: `/shop/products/${detail.value.slug}` },
  };
}

/**
 * `/shop/products/[slug]` — HO-SHP-002 §11.3, US-SHP-01…03, 07…10, 12…14. A Server Component fed by
 * the public API (NFR-PRF-1, ADR-FE-030): the gallery's first image is the LCP element, the details
 * tabs and the first page of reviews are in the HTML. The purchase island, recommendations and
 * personal state hydrate on top and never block it.
 */
export default async function ProductPage({ params, searchParams }: Props) {
  const { slug } = await params;
  const query = await searchParams;
  const lang = one(query.lang);
  const detail = await settle(getProductDetailServer(slug, lang || undefined));
  if (!detail.ok) {
    if (isMaintenanceError(detail.error)) return <MaintenanceNotice />;
    if (detail.error instanceof ApiNotFoundError) notFound();
    throw detail.error;
  }
  const product = detail.value;
  const [origin, reviews] = await Promise.all([
    product.countryOfOrigin ? settle(getOriginServer(product.countryOfOrigin.code)) : null,
    settle(getProductReviewsServer(slug)),
  ]);
  const initialSku = defaultSku(product.skus, one(query.sku));
  const nameLang = product.fieldLocales.name ?? product.locale;

  return (
    <article className="flex flex-col gap-10">
      <nav aria-label="Breadcrumb" className="flex flex-wrap gap-2 text-[13px] text-ink-muted">
        <Link href="/shop/products" className="text-accent-ink">
          Shop
        </Link>
        <span aria-hidden="true">/</span>
        <Link
          href={`/shop/products?category=${encodeURIComponent(product.category)}`}
          className="text-accent-ink"
        >
          {categoryLabel(product.category)}
        </Link>
      </nav>

      <ProductPurchase
        detail={product}
        initialSkuId={initialSku?.skuId ?? ''}
        lang={lang}
        heading={
          <ProductHeading
            name={product.name}
            nameLang={nameLang}
            brand={product.brand}
            rating={product.rating}
            country={product.countryOfOrigin}
            lang={lang || undefined}
          />
        }
      />

      {product.bundle && (
        <BundleContents components={product.bundle.components} lang={lang || undefined} />
      )}

      <ProductDetailsRow slug={slug} lang={lang}>
        <ProductDetailsTabs
          detail={product}
          originNotes={origin?.ok ? origin.value.sourcingNotes : null}
          lang={lang || undefined}
        />
      </ProductDetailsRow>

      <ProductReviews
        slug={slug}
        rating={product.rating}
        initial={reviews.ok ? reviews.value : null}
        lang={lang}
      />

      <ProductRecommendations slug={slug} lang={lang} />
    </article>
  );
}
