'use client';

import * as React from 'react';
import { ScanLine, Search } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { inputClass } from '@/components/inventory/fields';

/**
 * `PrescriptionLookup` — HO-PHM-002 §11.4, HW-2. One field, *Prescription number*. A keyboard-wedge
 * scanner types the number and ends with Enter, which submits the form — so does typing it and pressing
 * Enter, or choosing **Find**. Found: the caller goes to the dispense screen. Not found:
 * *"No prescription RX-… was found."* The lookup is the server's; nothing is matched here.
 */
export function PrescriptionLookup({
  onFind,
}: {
  /** Looks the number up; on `found` the caller has already navigated to the dispense screen. */
  onFind: (number: string) => Promise<'found' | 'notFound' | 'failed'>;
}) {
  const [value, setValue] = React.useState('');
  const [message, setMessage] = React.useState<string | null>(null);
  const [busy, setBusy] = React.useState(false);

  async function submit(event: React.FormEvent) {
    event.preventDefault();
    const number = value.trim().toUpperCase();
    if (!number || busy) return;
    setBusy(true);
    setMessage(null);
    const outcome = await onFind(number);
    setBusy(false);
    if (outcome === 'notFound') setMessage(`No prescription ${number} was found.`);
    else if (outcome === 'failed')
      setMessage('The prescription could not be looked up just now. Try again.');
  }

  return (
    <form
      onSubmit={submit}
      aria-label="Find a prescription"
      className="flex flex-col gap-2 rounded-[var(--radius-md)] border-2 border-accent bg-surface p-3"
    >
      <label htmlFor="rx-lookup" className="flex items-center gap-2 text-sm font-semibold text-ink">
        <ScanLine size={16} aria-hidden="true" />
        Prescription number
      </label>
      <div className="flex flex-wrap gap-2">
        <input
          id="rx-lookup"
          type="text"
          autoComplete="off"
          autoCapitalize="characters"
          spellCheck={false}
          value={value}
          placeholder="Scan the label, or type RX-2026-000123 and press Enter"
          aria-describedby="rx-lookup-message"
          onChange={(event) => setValue(event.target.value)}
          className={`${inputClass} min-w-0 flex-1 basis-56`}
        />
        <Button type="submit" variant="secondary" disabled={busy || value.trim() === ''}>
          <Search size={14} aria-hidden="true" />
          {busy ? 'Finding…' : 'Find'}
        </Button>
      </div>
      <p
        id="rx-lookup-message"
        role="status"
        aria-live="polite"
        className="min-h-5 text-sm font-semibold text-danger-ink"
      >
        {message}
      </p>
    </form>
  );
}
