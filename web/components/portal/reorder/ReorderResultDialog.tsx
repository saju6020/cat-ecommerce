'use client';

import Link from 'next/link';
import { Check, X } from 'lucide-react';
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog';
import { Badge } from '@/components/ui/badge';
import { Button, buttonVariants } from '@/components/ui/button';
import { describeReorderError, describeReorderReason } from '@/lib/api/errors';
import type { ReorderResult } from '@/lib/api/my-orders';
import { CART } from '@/lib/routing/portal-links';

/**
 * `components/portal/reorder/ReorderResultDialog.tsx` — HO-POR-002 U-B2..U-B4, U-B6. Reports what
 * Orders answered: a count of what went into the basket and, for everything else, the product and
 * why (`unavailable[]`). Orders does not list the added lines, so neither does this (ADR-FE-123).
 * A failed request replaces both lists with one plain message; no status code or error name is
 * shown. Focus return is the caller's `returnFocusRef`.
 */
export interface ReorderResultDialogProps {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  orderNumber: string;
  result: ReorderResult | undefined;
  error: unknown;
  onRetry: () => void;
  returnFocusRef: React.RefObject<HTMLElement | null>;
}

function itemsText(count: number): string {
  return `${count} ${count === 1 ? 'item' : 'items'}`;
}

function headingFor(result: ReorderResult): string {
  if (result.addedCount === 0) {
    return result.unavailable.length > 0 ? 'Nothing could be added' : 'Nothing to add';
  }
  return result.unavailable.length > 0 ? 'Some items were added' : 'Added to your basket';
}

export function ReorderResultDialog({
  open,
  onOpenChange,
  orderNumber,
  result,
  error,
  onRetry,
  returnFocusRef,
}: ReorderResultDialogProps) {
  const failure = error ? describeReorderError(error) : null;
  const title = failure ? failure.title : result ? headingFor(result) : 'Re-order';

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent hideClose returnFocusRef={returnFocusRef} className="max-w-xl">
        <DialogHeader>
          <DialogTitle className="font-display text-xl">{title}</DialogTitle>
          <DialogDescription asChild>
            <p role="status" aria-live="polite" className="text-sm text-ink-muted">
              {failure
                ? failure.message
                : result
                  ? result.addedCount > 0
                    ? `${itemsText(result.addedCount)} from order ${orderNumber} ${
                        result.addedCount === 1 ? 'was' : 'were'
                      } added to your basket.${
                        result.unavailable.length > 0
                          ? ` ${itemsText(result.unavailable.length)} could not be added — each one says why below.`
                          : ''
                      }`
                    : result.unavailable.length > 0
                      ? `None of the items from order ${orderNumber} could be added — each one says why below. Your basket was not changed.`
                      : `Order ${orderNumber} has nothing that can be added. Your basket was not changed.`
                  : ''}
            </p>
          </DialogDescription>
        </DialogHeader>

        {result && !failure && (
          <div className="flex flex-col gap-3">
            {result.addedCount > 0 && (
              <Badge
                variant="success"
                icon={<Check size={13} aria-hidden="true" />}
                className="w-fit"
              >
                Added ({result.addedCount})
              </Badge>
            )}
            {result.unavailable.length > 0 && (
              <section aria-labelledby="reorder-not-added">
                <h3
                  id="reorder-not-added"
                  className="mb-1 flex items-center gap-2 text-[13px] font-bold"
                >
                  <Badge variant="danger" icon={<X size={13} aria-hidden="true" />}>
                    Not added ({result.unavailable.length})
                  </Badge>
                </h3>
                <ul>
                  {result.unavailable.map((line, index) => (
                    <li
                      key={`${line.productName}-${index}`}
                      className="border-b border-border py-2 text-[13.5px] last:border-b-0"
                    >
                      <p className="font-semibold text-ink">{line.productName}</p>
                      <p className="mt-0.5 text-[12.5px] leading-snug text-ink-muted">
                        {describeReorderReason(line.reason)}
                      </p>
                    </li>
                  ))}
                </ul>
              </section>
            )}
          </div>
        )}

        <DialogFooter className="mt-4">
          <Button variant="secondary" onClick={() => onOpenChange(false)}>
            Close
          </Button>
          {failure ? (
            <Button onClick={onRetry}>Try again</Button>
          ) : (
            <Link href={CART} className={buttonVariants()}>
              View basket
            </Link>
          )}
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
