'use client';

import { useState } from 'react';
import { Check, Copy } from 'lucide-react';
import { Button } from '@/components/ui/button';

/**
 * `components/orders/CopyButton.tsx` — HO-ORD-002 ADR-FE-096. A **Copy** button beside a value that is
 * always visible as text (the customer may be away from the screen when they pay, and copy is a
 * convenience, not the only way to read it). The result is announced politely: *"Copied account
 * number"*. If the clipboard is not available the value is still on screen.
 */
export function CopyButton({ value, label }: { value: string; label: string }) {
  const [copied, setCopied] = useState(false);

  async function copy() {
    try {
      await navigator.clipboard.writeText(value);
      setCopied(true);
      setTimeout(() => setCopied(false), 2500);
    } catch {
      setCopied(false);
    }
  }

  return (
    <>
      <Button
        variant="pillSecondary"
        size="pillMd"
        onClick={() => void copy()}
        aria-label={`Copy ${label}`}
        className="flex-none"
      >
        {copied ? <Check size={14} aria-hidden="true" /> : <Copy size={14} aria-hidden="true" />}
        {copied ? 'Copied' : 'Copy'}
      </Button>
      <span role="status" aria-live="polite" className="sr-only">
        {copied ? `Copied ${label}` : ''}
      </span>
    </>
  );
}
