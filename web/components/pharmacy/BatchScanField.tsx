'use client';

import * as React from 'react';
import { ScanLine } from 'lucide-react';

/**
 * `BatchScanField` — HW-2. A keyboard-wedge barcode scanner is a very fast keyboard that ends with
 * Enter; typing a lot number and pressing Enter does the same. `onScan` receives the text and answers
 * whether it matched and with the sentence to announce (a match, or why not). The match is made against the batches the
 * SERVER put in the plan — this field decides nothing about stock.
 */
export function BatchScanField({
  id,
  onScan,
  disabled,
}: {
  id: string;
  onScan: (code: string) => { matched: boolean; message: string };
  disabled?: boolean;
}) {
  const [value, setValue] = React.useState('');
  const [message, setMessage] = React.useState('');
  const [ok, setOk] = React.useState(true);

  function submit() {
    const code = value.trim();
    if (!code) return;
    const answer = onScan(code);
    setMessage(answer.message);
    setOk(answer.matched);
    setValue('');
  }

  return (
    <div className="flex flex-col gap-1.5">
      <label htmlFor={id} className="flex items-center gap-2 text-sm font-semibold text-ink">
        <ScanLine size={16} aria-hidden="true" />
        Scan batch
      </label>
      <input
        id={id}
        type="text"
        autoComplete="off"
        disabled={disabled}
        value={value}
        placeholder="Scan the batch barcode, or type the lot number and press Enter"
        aria-describedby={`${id}-live`}
        onChange={(event) => setValue(event.target.value)}
        onKeyDown={(event) => {
          if (event.key === 'Enter') {
            event.preventDefault();
            submit();
          }
        }}
        className="w-full rounded-[var(--radius-sm)] border border-border-strong bg-surface px-3 py-2 text-sm text-ink focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-accent"
      />
      <p
        id={`${id}-live`}
        role="status"
        aria-live="polite"
        className={
          ok
            ? 'min-h-5 text-sm font-semibold text-accent-ink'
            : 'min-h-5 text-sm font-semibold text-danger-ink'
        }
      >
        {message}
      </p>
    </div>
  );
}
