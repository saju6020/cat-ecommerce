'use client';

import * as React from 'react';
import { Check, Circle, X } from 'lucide-react';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { ConfirmDialog } from '@/components/common/ConfirmDialog';
import type {
  ProductTransition,
  PublishChecklistItem,
  StaffProductDetail,
} from '@/lib/api/staff-shop';

const STATUS_VARIANT = { Draft: 'neutral', Active: 'success', Archived: 'neutral' } as const;
const STATUS_TEXT = {
  Draft: 'Draft',
  Active: 'Active (in the shop)',
  Archived: 'Archived',
} as const;

/** HO-SHP-002 §11.6 `PublishChecklist` — each item with its message; blocking vs warning is stated
 *  in TEXT, met/unmet in text plus an icon (UI-6). The same list the server enforces. */
export const PublishChecklist = React.forwardRef<
  HTMLUListElement,
  { items: PublishChecklistItem[] }
>(function PublishChecklist({ items }, ref) {
  return (
    <ul
      ref={ref}
      tabIndex={-1}
      aria-label="Publish checklist"
      className="m-0 flex list-none flex-col gap-1.5 p-0 text-sm focus:outline-2 focus:outline-accent"
    >
      {items.map((item) => (
        <li key={item.item} className="flex items-start gap-2">
          {item.isMet ? (
            <Check size={16} className="mt-0.5 flex-none text-accent" aria-hidden="true" />
          ) : item.isBlocking ? (
            <X size={16} className="mt-0.5 flex-none text-danger-ink" aria-hidden="true" />
          ) : (
            <Circle size={16} className="mt-0.5 flex-none text-warning-ink" aria-hidden="true" />
          )}
          <span>
            <span className="font-semibold">
              {item.isMet ? 'Done' : item.isBlocking ? 'Blocking' : 'Warning'}:
            </span>{' '}
            {item.message}
          </span>
        </li>
      ))}
    </ul>
  );
});

const ACTIONS: Record<StaffProductDetail['status'], ProductTransition[]> = {
  Draft: ['publish'],
  Active: ['unpublish', 'archive'],
  Archived: ['publish'],
};

const LABEL: Record<ProductTransition, string> = {
  publish: 'Publish',
  unpublish: 'Unpublish',
  archive: 'Archive',
};

export interface ProductEditorHeaderProps {
  product: StaffProductDetail;
  /** Runs the transition; rejects with the API error so the header can show it. */
  onTransition: (transition: ProductTransition) => Promise<unknown>;
  /** The server's refusal (`ProductNotPublishable` message) or a conflict, already worded. */
  error?: string | null;
  isConflict?: boolean;
  onReload: () => void;
}

/**
 * HO-SHP-002 §11.6 `ProductEditorHeader` — ADR-FE-034. Name, status in text, the server's publish
 * checklist and the status actions for the CURRENT status, each confirmed (UI-3) with what it
 * affects. **Publish is never disabled to express invalidity**: pressing it with an unmet blocking
 * item shows the server's refusal and moves focus to the checklist.
 */
export function ProductEditorHeader({
  product,
  onTransition,
  error,
  isConflict,
  onReload,
}: ProductEditorHeaderProps) {
  const [pending, setPending] = React.useState<ProductTransition | null>(null);
  const checklistRef = React.useRef<HTMLUListElement>(null);
  const consequence: Record<ProductTransition, string> = {
    publish: `Publish ${product.name}? It will appear in the shop.`,
    unpublish: `Unpublish ${product.name}? It will disappear from the shop and go back to draft.`,
    archive: `Archive ${product.name}? It will disappear from the shop and can't be edited until restored.`,
  };

  return (
    <header className="flex flex-col gap-3">
      <div className="flex flex-wrap items-center justify-between gap-3">
        <div className="flex flex-col gap-1">
          <h1 className="font-display text-2xl font-semibold text-ink">{product.name}</h1>
          <p className="flex items-center gap-2 text-sm text-ink-muted">
            <Badge variant={STATUS_VARIANT[product.status]}>{STATUS_TEXT[product.status]}</Badge>
            <span>{product.brand}</span>
          </p>
        </div>
        <div className="flex flex-wrap gap-2">
          {ACTIONS[product.status].map((transition) => (
            <Button
              key={transition}
              variant={transition === 'publish' ? 'primary' : 'secondary'}
              onClick={() => setPending(transition)}
            >
              {LABEL[transition]}
            </Button>
          ))}
        </div>
      </div>
      {error && (
        <div
          role="alert"
          className="flex flex-wrap items-center gap-3 rounded-[var(--radius-sm)] bg-danger-bg p-3 text-sm font-semibold text-danger-ink"
        >
          <span>{error}</span>
          {isConflict && (
            <Button variant="secondary" size="sm" onClick={onReload}>
              Reload
            </Button>
          )}
        </div>
      )}
      <PublishChecklist ref={checklistRef} items={product.publishChecklist} />
      <ConfirmDialog
        open={pending !== null}
        onOpenChange={(open) => !open && setPending(null)}
        title={pending ? `${LABEL[pending]} this product?` : ''}
        consequence={pending ? consequence[pending] : ''}
        confirmLabel={pending ? LABEL[pending] : 'Confirm'}
        variant={pending === 'archive' || pending === 'unpublish' ? 'destructive' : 'primary'}
        onConfirm={async () => {
          const transition = pending;
          setPending(null);
          if (!transition) return;
          try {
            await onTransition(transition);
          } finally {
            // The refusal (if any) is rendered above; bring the checklist into view and focus.
            window.setTimeout(() => checklistRef.current?.focus(), 0);
          }
        }}
      />
    </header>
  );
}
