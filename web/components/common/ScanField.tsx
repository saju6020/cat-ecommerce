'use client';

import * as React from 'react';
import { ScanLine, Search } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { cn } from '@/lib/utils';

/**
 * `components/common/ScanField.tsx` — HW-2, HO-IMP-002 ADR-FE-042. A barcode scanner in
 * keyboard-wedge mode is just a very fast keyboard that ends with Enter. Keystrokes that each
 * arrive within `SCAN_MAX_GAP_MS` of the last and finish with Enter are a SCAN: the value is
 * handed to `onScan` (a server lookup), and what it resolved to — or that nothing matched — is
 * announced in a live region. Slow typing is NOT a scan and never fires a lookup on its own; a
 * person who types a barcode presses **Look up**, and the SKU picker beside this field always
 * remains available. No camera, no scanning library.
 *
 * `onScan` resolves to the text to announce for a match (*"Kitten food — FD-KDC-2KG"*), or `null`
 * for no match — in which case the field announces *"No SKU has barcode {value}."* and keeps focus.
 * After a match the parent fills the SKU and moves focus to the quantity field.
 */
export const SCAN_MAX_GAP_MS = 30;
export const SCAN_MIN_LENGTH = 4;

export interface ScanFieldProps {
  id: string;
  label?: string;
  /** Resolve a barcode on the server. `null` = no SKU has it. */
  onScan: (barcode: string) => Promise<string | null>;
  disabled?: boolean;
  className?: string;
}

interface Burst {
  chars: string[];
  last: number;
}

export function ScanField({
  id,
  label = 'Scan a barcode',
  onScan,
  disabled,
  className,
}: ScanFieldProps) {
  const [value, setValue] = React.useState('');
  const [announcement, setAnnouncement] = React.useState('');
  const [isLooking, setIsLooking] = React.useState(false);
  const inputRef = React.useRef<HTMLInputElement>(null);
  const burst = React.useRef<Burst>({ chars: [], last: 0 });

  const resolve = React.useCallback(
    async (barcode: string) => {
      const code = barcode.trim();
      if (!code) return;
      setIsLooking(true);
      setAnnouncement(`Looking up ${code}…`);
      try {
        const matched = await onScan(code);
        if (matched) {
          setAnnouncement(`Added: ${matched}. Enter the quantity.`);
          setValue('');
        } else {
          setAnnouncement(`No SKU has barcode ${code}.`);
          // Select the text so the next scan overwrites it instead of being appended to it.
          inputRef.current?.focus();
          inputRef.current?.select();
        }
      } catch {
        setAnnouncement(`Could not look up barcode ${code}. Try again or pick the SKU instead.`);
        inputRef.current?.focus();
        inputRef.current?.select();
      } finally {
        setIsLooking(false);
      }
    },
    [onScan],
  );

  function handleKeyDown(event: React.KeyboardEvent<HTMLInputElement>) {
    if (event.key === 'Enter') {
      // Never submit a surrounding form because of a scan.
      event.preventDefault();
      const code = burst.current.chars.join('');
      // A scan covers EVERYTHING in the field: a burst that is only the tail of what was typed (a long
      // pause mid-way) would look up a wrong, partial barcode, so it is not treated as a scan.
      const wasBurst = code.length >= SCAN_MIN_LENGTH && code === event.currentTarget.value.trim();
      burst.current = { chars: [], last: 0 };
      if (wasBurst) {
        if (isLooking) setAnnouncement('Still looking up the last barcode. Scan again in a moment.');
        else void resolve(code);
      } else if (event.currentTarget.value.trim() !== '') {
        setAnnouncement('That was typed, not scanned. Choose Look up to search for it.');
        event.currentTarget.select();
      }
      return;
    }
    if (event.key.length !== 1 || event.ctrlKey || event.metaKey || event.altKey) return;
    const now = Date.now();
    const current = burst.current;
    if (current.chars.length > 0 && now - current.last > SCAN_MAX_GAP_MS) {
      burst.current = { chars: [event.key], last: now };
    } else {
      burst.current = { chars: [...current.chars, event.key], last: now };
    }
  }

  return (
    <div
      className={cn(
        'flex flex-col gap-2 rounded-[var(--radius-md)] border-2 border-accent bg-surface p-3',
        className,
      )}
    >
      <label htmlFor={id} className="flex items-center gap-2 text-sm font-semibold text-ink">
        <ScanLine size={16} aria-hidden="true" />
        {label}
      </label>
      <div className="flex flex-wrap gap-2">
        <input
          ref={inputRef}
          id={id}
          type="text"
          autoComplete="off"
          disabled={disabled}
          value={value}
          onChange={(event) => setValue(event.target.value)}
          onKeyDown={handleKeyDown}
          aria-describedby={`${id}-live`}
          placeholder="Scan, or type a barcode and choose Look up"
          className="min-w-0 flex-1 basis-56 rounded-[var(--radius-sm)] border border-border-strong bg-surface px-3 py-2 text-sm text-ink focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-accent"
        />
        <Button
          variant="secondary"
          size="md"
          disabled={disabled || isLooking || value.trim() === ''}
          onClick={() => void resolve(value)}
        >
          <Search size={14} aria-hidden="true" />
          Look up
        </Button>
      </div>
      <p
        id={`${id}-live`}
        role="status"
        aria-live="polite"
        className="min-h-5 text-sm font-semibold text-accent-ink"
      >
        {announcement}
      </p>
    </div>
  );
}
