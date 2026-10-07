'use client';

import * as React from 'react';
import { Button } from '@/components/ui/button';
import { inputClass } from '@/components/staff-shop/SectionFrame';
import type { PickedSku } from '@/components/staff-shop/BundleEditor';
import type { RelatedItem } from '@/components/staff-shop/RelatedProductsEditor';
import { useStaffProduct, useStaffProducts } from '@/lib/hooks/useStaffShop';

function useSearch() {
  const [text, setText] = React.useState('');
  const [submitted, setSubmitted] = React.useState('');
  return { text, setText, submitted, submit: () => setSubmitted(text.trim()) };
}

/** Staff product search (name, brand or SKU code), used by both pickers. Searching is the server's. */
function ProductSearch({
  id,
  label,
  onChoose,
  actionLabel,
}: {
  id: string;
  label: string;
  onChoose: (product: { productId: string; name: string }) => void;
  actionLabel: (name: string) => string;
}) {
  const search = useSearch();
  const results = useStaffProducts({ q: search.submitted, status: 'Active', pageSize: 8 });
  return (
    <div className="flex flex-col gap-2">
      <div className="flex gap-2">
        <label htmlFor={id} className="sr-only">
          {label}
        </label>
        <input
          id={id}
          type="search"
          placeholder={label}
          className={inputClass}
          value={search.text}
          onChange={(e) => search.setText(e.target.value)}
          onKeyDown={(e) => {
            if (e.key === 'Enter') {
              e.preventDefault();
              search.submit();
            }
          }}
        />
        <Button type="button" variant="secondary" onClick={search.submit}>
          Search
        </Button>
      </div>
      {search.submitted && results.data && (
        <ul aria-label="Search results" className="m-0 flex list-none flex-col gap-1 p-0">
          {results.data.items.length === 0 && (
            <li className="text-sm text-ink-muted">No active products match.</li>
          )}
          {results.data.items.map((p) => (
            <li key={p.productId}>
              <Button
                type="button"
                variant="ghost"
                size="sm"
                aria-label={actionLabel(p.name)}
                onClick={() => onChoose(p)}
              >
                {p.name}
              </Button>
            </li>
          ))}
        </ul>
      )}
    </div>
  );
}

function SkusOf({ productId, onPick }: { productId: string; onPick: (sku: PickedSku) => void }) {
  const product = useStaffProduct(productId);
  if (!product.data) return <p className="text-sm text-ink-muted">Loading SKUs…</p>;
  return (
    <ul
      aria-label={`SKUs of ${product.data.name}`}
      className="m-0 flex list-none flex-wrap gap-2 p-0"
    >
      {product.data.skus
        .filter((s) => s.status === 'Active')
        .map((s) => (
          <li key={s.skuId}>
            <Button
              type="button"
              variant="secondary"
              size="sm"
              onClick={() =>
                onPick({ skuId: s.skuId, code: s.code, label: `${product.data!.name} (${s.code})` })
              }
            >
              Add {s.code}
            </Button>
          </li>
        ))}
    </ul>
  );
}

/** Kit component picker: search a product, then choose one of its SKUs (§11.6 `BundleEditor`). */
export function SkuPicker({ onPick }: { onPick: (sku: PickedSku) => void }) {
  const [productId, setProductId] = React.useState<string | null>(null);
  return (
    <div className="flex flex-col gap-2">
      <ProductSearch
        id="sku-picker"
        label="Search products to add"
        actionLabel={(n) => `Choose ${n}`}
        onChoose={(p) => setProductId(p.productId)}
      />
      {productId && <SkusOf productId={productId} onPick={onPick} />}
    </div>
  );
}

/** Related-product picker (§11.6 `RelatedProductsEditor`). */
export function ProductPicker({ id, onPick }: { id: string; onPick: (item: RelatedItem) => void }) {
  return (
    <ProductSearch
      id={id}
      label="Search products to link"
      actionLabel={(n) => `Add ${n}`}
      onChoose={(p) => onPick({ relatedProductId: p.productId, name: p.name })}
    />
  );
}
