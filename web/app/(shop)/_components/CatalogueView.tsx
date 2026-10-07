import { ErrorState } from '@/components/common/ErrorState';
import { NoResults } from '@/components/catalogue/NoResults';
import { Pagination } from '@/components/catalogue/Pagination';
import { ProductGrid } from '@/components/catalogue/ProductGrid';
import { ResultSummary } from '@/components/catalogue/ResultSummary';
import { MaintenanceNotice } from '@/components/shop/MaintenanceNotice';
import { ShopLoadError } from '@/components/shop/ShopLoadError';
import { ApiValidationError } from '@/lib/api/client';
import {
  getCatalogueFiltersServer,
  getCatalogueServer,
  getCategoriesServer,
} from '@/lib/api/shop-catalogue';
import { isMaintenanceError, settle } from '@/lib/shop/helpers';
import {
  clearFilters,
  toQueryString,
  withoutFixed,
  type CatalogueParams,
} from '@/lib/shop/catalogueParams';
import { CatalogueControls } from './CatalogueControls';

export interface CatalogueViewProps {
  /** The shopper's URL state (`/shop/products?…` or an origin page's query). */
  params: CatalogueParams;
  /** The page's own path — links (pages, suggestions) are built on it. */
  basePath: string;
  /** Values the page owns (an origin page's country): applied to the API call, never in the URL. */
  fixed?: Partial<CatalogueParams>;
}

/**
 * HO-SHP-002 §11.2 / §11.4 — the server-rendered catalogue: results, pagination and suggestions
 * come straight from `GET /api/shop/products` (ADR-FE-030/031/032), with real links. Only the
 * filter/sort controls are a client island. The UI never filters, sorts or counts anything itself.
 */
export async function CatalogueView({ params, basePath, fixed = {} }: CatalogueViewProps) {
  const effective: CatalogueParams = { ...params, ...fixed };
  // Links must not carry the page-owned values, and must keep everything else.
  const urlParams = withoutFixed(params, fixed);
  const retryHref = `${basePath}${toQueryString(urlParams)}`;
  const [result, facets, categories] = await Promise.all([
    settle(getCatalogueServer(effective)),
    settle(getCatalogueFiltersServer(effective.category || undefined)),
    settle(getCategoriesServer()),
  ]);

  if (!result.ok) {
    if (isMaintenanceError(result.error)) return <MaintenanceNotice />;
    if (result.error instanceof ApiValidationError) {
      return (
        <ErrorState
          title="That search or filter isn’t valid."
          description={
            result.error.response.errors[0]?.errorMessage ?? 'Clear the filters and try again.'
          }
        />
      );
    }
    return <ShopLoadError retryHref={retryHref} error={result.error} />;
  }
  if (!facets.ok) return <ShopLoadError retryHref={retryHref} error={facets.error} />;
  const href = (next: CatalogueParams) => `${basePath}${toQueryString(next)}`;
  const { items, totalCount, pageNumber, pageSize, suggestions } = result.value;
  const currencyCode = items.find((i) => i.fromPrice)?.fromPrice?.currencyCode ?? 'BDT';

  return (
    <CatalogueControls
      facets={facets.value}
      categories={categories.ok ? categories.value : []}
      currencyCode={currencyCode}
      fixed={fixed}
      summary={<ResultSummary totalCount={totalCount} q={params.q} />}
    >
      {items.length === 0 ? (
        <NoResults
          q={params.q}
          suggestions={suggestions}
          hrefForQuery={(q) => href({ ...urlParams, q, pageNumber: 1 })}
          clearHref={href(clearFilters(urlParams))}
          lang={params.lang || undefined}
        />
      ) : (
        <>
          <ProductGrid items={items} lang={params.lang || undefined} label="Products" />
          <Pagination
            pageNumber={pageNumber}
            pageSize={pageSize}
            totalCount={totalCount}
            hrefFor={(page) => href({ ...urlParams, pageNumber: page })}
          />
        </>
      )}
    </CatalogueControls>
  );
}
