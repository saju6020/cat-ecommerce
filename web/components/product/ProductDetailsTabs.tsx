'use client';

import * as React from 'react';
import Link from 'next/link';
import { LimitedMarkup } from '@/components/content/LimitedMarkup';
import { cn } from '@/lib/utils';
import type { ProductDetail } from '@/lib/api/shop-catalogue';

export interface ProductDetailsTabsProps {
  detail: ProductDetail;
  /** The origin's sourcing notes (LimitedMarkup), when the origin has any. */
  originNotes?: string | null;
  lang?: string;
}

interface Tab {
  id: string;
  label: string;
  panel: React.ReactNode;
}

const ANALYSIS_ROWS = [
  ['Protein', 'proteinPercent'],
  ['Fat', 'fatPercent'],
  ['Fibre', 'fibrePercent'],
  ['Moisture', 'moisturePercent'],
] as const;

function buildTabs(
  detail: ProductDetail,
  originNotes: string | null | undefined,
  lang?: string,
): Tab[] {
  const lang_ = (field: string) => detail.fieldLocales[field] ?? detail.locale;
  const food = detail.food;
  const tabs: Tab[] = [
    {
      id: 'description',
      label: 'Description',
      panel: detail.description ? (
        <LimitedMarkup body={detail.description} className="prose-sm space-y-3 text-ink" />
      ) : (
        <p className="text-ink-muted">No description yet.</p>
      ),
    },
  ];
  // Wrap each language-varying field so assistive tech reads it in the language it was served in.
  const lf = (field: string, node: React.ReactNode) => <div lang={lang_(field)}>{node}</div>;
  tabs[0]!.panel = lf('description', tabs[0]!.panel);

  if (food && (food.ingredientList || food.guaranteedAnalysis)) {
    tabs.push({
      id: 'ingredients',
      label: 'Ingredients & analysis',
      panel: (
        <div className="flex flex-col gap-4">
          {food.ingredientList && (
            <div>
              <h3 className="mb-1 text-sm font-bold text-ink">Ingredients</h3>
              <p lang={lang_('ingredientList')} className="whitespace-pre-line text-ink">
                {food.ingredientList}
              </p>
            </div>
          )}
          {food.guaranteedAnalysis && (
            <table className="w-full max-w-md border-collapse text-sm">
              <caption className="mb-1 text-left text-sm font-bold text-ink">
                Guaranteed analysis
              </caption>
              <thead className="sr-only">
                <tr>
                  <th scope="col">Nutrient</th>
                  <th scope="col">Amount</th>
                </tr>
              </thead>
              <tbody>
                {ANALYSIS_ROWS.map(([label, key]) => (
                  <tr key={key} className="border-b border-border">
                    <th scope="row" className="py-1.5 text-left font-normal text-ink-muted">
                      {label}
                    </th>
                    <td className="py-1.5 text-right font-semibold text-ink">
                      {food.guaranteedAnalysis![key]}%
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          )}
        </div>
      ),
    });
  }
  if (food?.feedingGuide) {
    tabs.push({
      id: 'feeding',
      label: 'Feeding guide',
      panel: lf(
        'feedingGuide',
        <LimitedMarkup body={food.feedingGuide} className="space-y-3 text-ink" />,
      ),
    });
  }
  if (food?.storageInstructions) {
    tabs.push({
      id: 'storage',
      label: 'Storage',
      panel: <p className="whitespace-pre-line text-ink">{food.storageInstructions}</p>,
    });
  }
  if (detail.countryOfOrigin || food?.manufacturer) {
    tabs.push({
      id: 'origin',
      label: 'Origin',
      panel: (
        <div className="flex flex-col gap-3 text-ink">
          {detail.countryOfOrigin && (
            <p>
              <span className="font-semibold">Country of origin:</span>{' '}
              {detail.countryOfOrigin.name}
            </p>
          )}
          {food?.manufacturer && (
            <p>
              <span className="font-semibold">Manufacturer:</span> {food.manufacturer}
            </p>
          )}
          {originNotes && <LimitedMarkup body={originNotes} className="space-y-3 text-sm" />}
          {detail.countryOfOrigin && (
            <p>
              <Link
                href={`/shop/countries/${detail.countryOfOrigin.code}${lang ? `?lang=${encodeURIComponent(lang)}` : ''}`}
                className="font-semibold text-accent-ink"
              >
                More from {detail.countryOfOrigin.name}
              </Link>
            </p>
          )}
        </div>
      ),
    });
  }
  return tabs;
}

/**
 * HO-SHP-002 §11.3 `ProductDetailsTabs` — an accessible tab list (arrows, Home, End). Every panel is
 * in the document (hidden when inactive) so the content is server-rendered. Free text — ingredient
 * list, storage instructions, manufacturer — is rendered AS TEXT; only description and feeding guide
 * go through `LimitedMarkup` (HO-SHP-001 backlog SHP row 22). Each language-varying field carries
 * its own `lang` from `fieldLocales` (US-SHP-14 AC2).
 */
export function ProductDetailsTabs({ detail, originNotes, lang }: ProductDetailsTabsProps) {
  const baseId = React.useId();
  const tabs = buildTabs(detail, originNotes, lang);
  const [active, setActive] = React.useState(tabs[0]!.id);
  const refs = React.useRef<Record<string, HTMLButtonElement | null>>({});

  function move(toIndex: number) {
    const next = tabs[(toIndex + tabs.length) % tabs.length]!;
    setActive(next.id);
    refs.current[next.id]?.focus();
  }

  function onKeyDown(event: React.KeyboardEvent, index: number) {
    if (event.key === 'ArrowRight') move(index + 1);
    else if (event.key === 'ArrowLeft') move(index - 1);
    else if (event.key === 'Home') move(0);
    else if (event.key === 'End') move(tabs.length - 1);
    else return;
    event.preventDefault();
  }

  return (
    <section aria-label="Product details" className="flex flex-col gap-3">
      <div
        role="tablist"
        aria-label="Product information"
        className="flex flex-wrap gap-1 border-b border-border"
      >
        {tabs.map((tab, index) => (
          <button
            key={tab.id}
            ref={(node) => {
              refs.current[tab.id] = node;
            }}
            id={`${baseId}-tab-${tab.id}`}
            role="tab"
            type="button"
            aria-selected={active === tab.id}
            aria-controls={`${baseId}-panel-${tab.id}`}
            tabIndex={active === tab.id ? 0 : -1}
            onClick={() => setActive(tab.id)}
            onKeyDown={(event) => onKeyDown(event, index)}
            className={cn(
              '-mb-px border-b-2 px-3 py-2 text-sm font-semibold',
              active === tab.id
                ? 'border-accent text-accent-ink'
                : 'border-transparent text-ink-muted hover:text-ink',
            )}
          >
            {tab.label}
          </button>
        ))}
      </div>
      {tabs.map((tab) => (
        <div
          key={tab.id}
          id={`${baseId}-panel-${tab.id}`}
          role="tabpanel"
          aria-labelledby={`${baseId}-tab-${tab.id}`}
          hidden={active !== tab.id}
          tabIndex={0}
          className="py-2"
        >
          {tab.panel}
        </div>
      ))}
    </section>
  );
}
