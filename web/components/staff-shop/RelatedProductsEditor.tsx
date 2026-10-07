'use client';

import * as React from 'react';
import { useForm } from 'react-hook-form';
import { Button } from '@/components/ui/button';
import { submitSection } from '@/lib/shop/submitSection';
import type { StaffProductDetail } from '@/lib/api/staff-shop';
import { SectionFrame } from './SectionFrame';

export const MAX_RELATED = 12;
type Kind = 'FrequentlyBoughtTogether' | 'UpSell';
export interface RelatedItem {
  relatedProductId: string;
  name: string;
}
interface Values {
  FrequentlyBoughtTogether: RelatedItem[];
  UpSell: RelatedItem[];
}

const LISTS: Array<{ kind: Kind; title: string }> = [
  { kind: 'FrequentlyBoughtTogether', title: 'Frequently bought together' },
  { kind: 'UpSell', title: 'You might also like' },
];

export interface RelatedProductsEditorProps {
  product: StaffProductDetail;
  /** The product picker for one list (wired by the page). */
  renderPicker: (onPick: (item: RelatedItem) => void, kind: Kind) => React.ReactNode;
  onSave: (
    links: Array<{ relatedProductId: string; kind: Kind; sortOrder: number }>,
  ) => Promise<unknown>;
  onReload: () => void;
}

/**
 * HO-SHP-002 §11.6 `RelatedProductsEditor` — US-SHP-08. Two ordered lists (≤ 12 each); reorder with
 * buttons, so it works from the keyboard. Curated links only — the server computes the rest.
 */
export function RelatedProductsEditor({
  product,
  renderPicker,
  onSave,
  onReload,
}: RelatedProductsEditorProps) {
  const initial = (kind: Kind): RelatedItem[] =>
    (product.relatedProducts ?? [])
      .filter((r) => r.kind === kind)
      .sort((a, b) => a.sortOrder - b.sortOrder)
      .map((r) => ({ relatedProductId: r.relatedProductId, name: r.name ?? 'Product' }));
  const { setValue, watch, handleSubmit, setError, reset, formState } = useForm<Values>({
    defaultValues: {
      FrequentlyBoughtTogether: initial('FrequentlyBoughtTogether'),
      UpSell: initial('UpSell'),
    },
  });
  const values = watch();
  const [announce, setAnnounce] = React.useState('');

  function update(kind: Kind, items: RelatedItem[], message: string) {
    setValue(kind, items, { shouldDirty: true });
    setAnnounce(message);
  }

  return (
    <SectionFrame
      title="Related products"
      errors={formState.errors}
      isDirty={formState.isDirty}
      isSubmitting={formState.isSubmitting}
      isSubmitSuccessful={formState.isSubmitSuccessful}
      onReload={onReload}
      onSubmit={handleSubmit(async (v) => {
        const links = LISTS.flatMap(({ kind }) =>
          v[kind].map((item, index) => ({
            relatedProductId: item.relatedProductId,
            kind,
            sortOrder: index + 1,
          })),
        );
        const ok = await submitSection(() => onSave(links), setError, []);
        if (ok) reset(v);
      })}
    >
      <p role="status" aria-live="polite" className="sr-only">
        {announce}
      </p>
      {LISTS.map(({ kind, title }) => {
        const items = values[kind] ?? [];
        return (
          <section key={kind} aria-label={title} className="flex flex-col gap-2">
            <h3 className="text-base font-bold text-ink">
              {title}{' '}
              <span className="text-sm font-normal text-ink-muted">
                ({items.length} of {MAX_RELATED})
              </span>
            </h3>
            <ol className="m-0 flex list-none flex-col gap-1.5 p-0">
              {items.map((item, index) => (
                <li
                  key={item.relatedProductId}
                  className="flex flex-wrap items-center gap-2 rounded-[var(--radius-sm)] border border-border bg-surface px-3 py-2 text-sm"
                >
                  <span className="min-w-0 flex-1 font-semibold text-ink">{item.name}</span>
                  <Button
                    type="button"
                    variant="ghost"
                    size="sm"
                    aria-label={`Move ${item.name} up in ${title}`}
                    disabled={index === 0}
                    onClick={() => {
                      const next = [...items];
                      [next[index - 1], next[index]] = [next[index]!, next[index - 1]!];
                      update(
                        kind,
                        next,
                        `${item.name} moved to position ${index} of ${items.length}`,
                      );
                    }}
                  >
                    Up
                  </Button>
                  <Button
                    type="button"
                    variant="ghost"
                    size="sm"
                    aria-label={`Move ${item.name} down in ${title}`}
                    disabled={index === items.length - 1}
                    onClick={() => {
                      const next = [...items];
                      [next[index + 1], next[index]] = [next[index]!, next[index + 1]!];
                      update(
                        kind,
                        next,
                        `${item.name} moved to position ${index + 2} of ${items.length}`,
                      );
                    }}
                  >
                    Down
                  </Button>
                  <Button
                    type="button"
                    variant="ghost"
                    size="sm"
                    aria-label={`Remove ${item.name} from ${title}`}
                    onClick={() =>
                      update(
                        kind,
                        items.filter((_, i) => i !== index),
                        `${item.name} removed`,
                      )
                    }
                  >
                    Remove
                  </Button>
                </li>
              ))}
              {items.length === 0 && (
                <li className="text-sm text-ink-muted">Nothing chosen yet.</li>
              )}
            </ol>
            {items.length < MAX_RELATED ? (
              renderPicker((picked) => {
                if (
                  picked.relatedProductId === product.productId ||
                  items.some((i) => i.relatedProductId === picked.relatedProductId)
                )
                  return;
                update(kind, [...items, picked], `${picked.name} added to ${title}`);
              }, kind)
            ) : (
              <p className="text-sm text-ink-muted">This list is full ({MAX_RELATED}).</p>
            )}
          </section>
        );
      })}
    </SectionFrame>
  );
}
