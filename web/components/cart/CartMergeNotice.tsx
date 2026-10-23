'use client';

import { Info, X } from 'lucide-react';
import { bannerInfoClass } from '@/components/hearth/card';
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
    <div role="status" className={bannerInfoClass}>
      <Info size={18} aria-hidden="true" className="mt-px flex-none" />
      <div className="min-w-0 flex-1">
        <p className="m-0 font-bold">
          We&rsquo;ve added the items from your cart before you signed in.
        </p>
        {result.droppedLines.length > 0 && (
          <>
            <p className="mb-0 mt-1 text-[13px]">
              {result.droppedLines.length === 1
                ? "One item couldn't be added:"
                : `${result.droppedLines.length} items couldn't be added:`}
            </p>
            <ul className="m-0 mt-1 list-disc pl-5 text-[13px]">
              {result.droppedLines.map((line) => (
                <li key={`${line.productName}-${line.reason}`}>
                  <b>{line.productName}</b> — {describeDroppedReason(line.reason)}
                </li>
              ))}
            </ul>
          </>
        )}
      </div>
      <button
        type="button"
        onClick={onDismiss}
        aria-label="Dismiss this message"
        className="-my-2.5 -mr-2 grid size-11 flex-none place-items-center rounded-pill text-accent-ink hover:bg-surface/60"
      >
        <X size={16} aria-hidden="true" />
      </button>
    </div>
  );
}
