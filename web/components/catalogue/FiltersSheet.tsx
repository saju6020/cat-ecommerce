'use client';

import { useRef, useState } from 'react';
import { SlidersHorizontal } from 'lucide-react';
import { Button } from '@/components/ui/button';
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog';
import { activeFilterCount, type CatalogueParams } from '@/lib/shop/catalogueParams';
import { CatalogueFilters, type CatalogueFiltersProps } from './CatalogueFilters';

export interface FiltersSheetProps extends Omit<CatalogueFiltersProps, 'onChange' | 'priceMode'> {
  /** The number shown in *Filters (n)*; defaults to every active filter in `value`. */
  count?: number;
  /** Commits the whole draft once — one navigation, however many filters were changed. */
  onShowResults: (draft: CatalogueParams) => void;
}

/**
 * HO-SHP-002 §7 / §11.2, U-A2 — below 768 px the filter panel is a sheet opened by a
 * **Filters (n)** button. Changes are held in a draft; **Show results** applies them with a single
 * navigation, closes the sheet and returns focus to the button.
 */
export function FiltersSheet({ onShowResults, count, ...panel }: FiltersSheetProps) {
  const [open, setOpen] = useState(false);
  const [draft, setDraft] = useState<CatalogueParams>(panel.value);
  const triggerRef = useRef<HTMLButtonElement>(null);

  function openSheet() {
    setDraft(panel.value);
    setOpen(true);
  }

  return (
    <>
      <Button ref={triggerRef} variant="secondary" className="md:hidden" onClick={openSheet}>
        <SlidersHorizontal size={16} aria-hidden="true" />
        Filters ({count ?? activeFilterCount(panel.value)})
      </Button>
      <Dialog open={open} onOpenChange={setOpen}>
        <DialogContent
          onCloseAutoFocus={(event) => {
            event.preventDefault();
            triggerRef.current?.focus();
          }}
          className="inset-0 left-0 top-0 flex h-full max-h-none max-w-none translate-x-0 translate-y-0 flex-col overflow-y-auto rounded-none border-0 p-4"
        >
          <DialogHeader>
            <DialogTitle>Filters</DialogTitle>
            <DialogDescription className="sr-only">
              Choose filters, then select Show results.
            </DialogDescription>
          </DialogHeader>
          <div className="flex-1 overflow-y-auto pb-4">
            <CatalogueFilters
              {...panel}
              value={draft}
              priceMode="live"
              onChange={(patch) => setDraft((current) => ({ ...current, ...patch }))}
            />
          </div>
          <DialogFooter className="sticky bottom-0 mt-0 border-t border-border bg-surface pt-3">
            <Button
              onClick={() => {
                onShowResults({ ...draft, pageNumber: 1 });
                setOpen(false);
              }}
            >
              Show results
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </>
  );
}
