'use client';

import { ScanLine, Search } from 'lucide-react';
import { forwardRef, useRef, useState } from 'react';
import { Button } from '@/components/ui/button';
import type { PosSku } from '@/lib/api/pos';
import { useScanner } from '@/lib/hooks/useScanner';
import { usePosBarcodeLookup, usePosSkuSearch } from '@/lib/hooks/usePos';
import { formatMoney } from '@/lib/format/money';

/**
 * `components/pos/PosScanField.tsx` — ⭐ HO-ORD-002 §11.11, ADR-FE-095, HW-2. The always-focused scan
 * input. A barcode scanner is a keyboard that types fast and ends with Enter (`useScanner`); Enter looks
 * the barcode up on the server and adds the row. Typing slowly never looks anything up until Enter. An
 * unknown barcode is announced in a polite live region, the text stays in the field, and a *Search by
 * name* fallback is offered. Focus returns to this field after every add (the parent focuses `ref`).
 */
export const PosScanField = forwardRef<HTMLInputElement, { onAdd: (sku: PosSku) => string }>(function PosScanField(
  { onAdd },
  ref,
) {
  const lookup = usePosBarcodeLookup();
  const [announcement, setAnnouncement] = useState('');
  const [searchOpen, setSearchOpen] = useState(false);
  const [searchText, setSearchText] = useState('');
  const search = usePosSkuSearch(searchOpen ? searchText : '');

  // Lookups run one after another (a queue), and the field is emptied the moment Enter is pressed, so a second
  // scan arriving while the first lookup is still in flight is neither appended to it nor wiped by it.
  const queue = useRef<Promise<void>>(Promise.resolve());
  const scanner = useScanner((code, kind) => {
    scanner.clear();
    queue.current = queue.current.then(() => process(code, kind));
  });

  async function process(code: string, kind: 'scan' | 'manual'): Promise<void> {
    // On a failure the text goes back into the field (unless a newer scan is already in it).
    const restore = () => scanner.setValue((current) => (current === '' ? code : current));
    try {
      const sku = await lookup.mutateAsync(code);
      if (!sku) {
        setAnnouncement(`No product has barcode ${code}. Check the number or search by name.`);
        setSearchOpen(true);
        restore();
        return;
      }
      setAnnouncement(onAdd(sku));
    } catch {
      setAnnouncement(`Could not look up ${kind === 'scan' ? 'the scanned' : 'that'} barcode. Try again or search by name.`);
      restore();
    }
  }

  return (
    <div className="flex flex-col gap-3 rounded-[var(--radius-md)] border-2 border-accent bg-surface p-3">
      <label htmlFor="pos-scan" className="flex items-center gap-2 text-sm font-semibold text-ink">
        <ScanLine size={16} aria-hidden="true" />
        Scan a barcode
      </label>
      <input
        ref={ref}
        id="pos-scan"
        type="text"
        autoComplete="off"
        autoFocus
        value={scanner.value}
        onChange={(e) => scanner.setValue(e.target.value)}
        onKeyDown={scanner.onKeyDown}
        aria-describedby="pos-scan-live"
        placeholder="Scan, or type a barcode and press Enter"
        className="w-full rounded-[var(--radius-sm)] border border-border-strong bg-surface px-3 py-2 text-base text-ink focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-accent"
      />
      <p id="pos-scan-live" role="status" aria-live="polite" className="min-h-5 text-sm font-semibold text-accent-ink">
        {announcement}
      </p>
      <div>
        <Button variant="ghost" size="sm" onClick={() => setSearchOpen((o) => !o)} aria-expanded={searchOpen}>
          <Search size={14} aria-hidden="true" />
          Search by name
        </Button>
      </div>
      {searchOpen && (
        <div className="flex flex-col gap-2">
          <label htmlFor="pos-search" className="text-sm font-semibold">Product name or code</label>
          <input
            id="pos-search"
            type="search"
            value={searchText}
            onChange={(e) => setSearchText(e.target.value)}
            className="w-full rounded-[var(--radius-sm)] border border-border-strong bg-surface px-3 py-2 text-sm text-ink focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-accent"
          />
          <ul aria-label="Search results" className="m-0 flex list-none flex-col gap-1 p-0">
            {(search.data ?? []).map((sku) => (
              <li key={sku.skuId} className="flex items-center justify-between gap-2 text-sm">
                <span className="min-w-0 [overflow-wrap:anywhere]">
                  {`${sku.productName} ${sku.variantLabel}`.trim()}
                  {sku.unitPrice ? ` — ${formatMoney(sku.unitPrice)}` : ''}
                </span>
                <Button
                  size="sm"
                  variant="secondary"
                  onClick={() => {
                    setAnnouncement(onAdd(sku));
                  }}
                >
                  Add<span className="sr-only"> {sku.productName}</span>
                </Button>
              </li>
            ))}
          </ul>
        </div>
      )}
    </div>
  );
});
