'use client';

import { Button } from '@/components/ui/button';
import { describeDroppedReason } from '@/lib/api/errors';
import type { MergeCartResult } from '@/lib/api/cart';

/**
 * `components/cart/CartMergeNotice.tsx` — HO-ORD-002 §11.1, US-ORD-01 AC1. After a guest cart was merged
 * on sign-in: *"We've added the items from your cart before you signed in."* plus any dropped lines by
 * name and reason. Dismissible; **never auto-hides**.
 */
export function CartMergeNotice({
  result,
  onDismiss,
}: {
  result: MergeCartResult;
  onDismiss: () => void;
}) {
  return (
    <div
      role="status"
      className="flex items-start justify-between gap-3 rounded-[var(--radius-sm)] border border-accent/25 border-l-4 border-l-accent bg-accent-soft p-3 text-sm text-accent-ink"
    >
      <div>
        <p className="font-bold">We&rsquo;ve added the items from your cart before you signed in.</p>
        {result.droppedLines.length > 0 && (
          <>
            <p className="mt-1">
              {result.droppedLines.length === 1
                ? "One item couldn't be added:"
                : `${result.droppedLines.length} items couldn't be added:`}
            </p>
            <ul className="m-0 mt-1 list-disc pl-5">
              {result.droppedLines.map((line) => (
                <li key={`${line.productName}-${line.reason}`}>
                  <b>{line.productName}</b> — {describeDroppedReason(line.reason)}
                </li>
              ))}
            </ul>
          </>
        )}
      </div>
      <Button variant="ghost" size="sm" onClick={onDismiss} aria-label="Dismiss this message">
        Dismiss
      </Button>
    </div>
  );
}
