'use client';

import * as React from 'react';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { ConfirmDialog } from '@/components/common/ConfirmDialog';
import { EmptyState } from '@/components/common/EmptyState';
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog';
import { describeApiError, SHOP_CODE_MESSAGES } from '@/lib/api/errors';
import { formatShortInstant } from '@/lib/format/shop';
import type { ModerationItem } from '@/lib/api/staff-shop';
import { Field, inputClass } from './SectionFrame';

export const REJECTION_REASONS: Array<[string, string]> = [
  ['OffTopic', 'Off topic'],
  ['Offensive', 'Offensive'],
  ['PersonalInformation', 'Contains personal information'],
  ['Spam', 'Spam'],
  ['Other', 'Other'],
];

export type ModerationAction =
  | { kind: 'publish'; reviewId: string }
  | { kind: 'hide'; reviewId: string }
  | { kind: 'reject'; reviewId: string; reason: string; note: string | null };

const STATUS_TEXT = {
  PendingModeration: 'Awaiting approval',
  Published: 'Published',
  Rejected: 'Rejected',
  Hidden: 'Hidden',
} as const;

export interface ReviewModerationQueueProps {
  items: ModerationItem[];
  onAct: (action: ModerationAction) => Promise<unknown>;
}

/**
 * HO-SHP-002 §11.6 `ReviewModerationQueue` — US-SHP-09. Pending first (the API orders them): product,
 * variant, rating, title and body as TEXT, customer display name, submitted. **Publish**, **Reject**
 * (a reason is required, plus an optional note) and **Hide** (published ones); every action is
 * confirmed with what it affects (UI-3).
 */
export function ReviewModerationQueue({ items, onAct }: ReviewModerationQueueProps) {
  const [confirm, setConfirm] = React.useState<{
    item: ModerationItem;
    kind: 'publish' | 'hide';
  } | null>(null);
  const [rejecting, setRejecting] = React.useState<ModerationItem | null>(null);
  const [reason, setReason] = React.useState('');
  const [note, setNote] = React.useState('');
  const [reasonError, setReasonError] = React.useState('');
  const [error, setError] = React.useState<string | null>(null);

  async function run(action: ModerationAction) {
    setError(null);
    try {
      await onAct(action);
    } catch (e) {
      setError(describeApiError(e, SHOP_CODE_MESSAGES));
    }
  }

  if (items.length === 0) {
    return (
      <EmptyState
        title="No reviews to moderate"
        description="New reviews appear here as customers send them."
      />
    );
  }
  return (
    <>
      {error && (
        <p role="alert" className="mb-3 text-sm font-semibold text-danger-ink">
          {error}
        </p>
      )}
      <ul className="m-0 flex list-none flex-col gap-3 p-0">
        {items.map((item) => (
          <li key={item.reviewId}>
            <article className="flex flex-col gap-2 rounded-[var(--radius-md)] border border-border bg-surface p-4">
              <div className="flex flex-wrap items-center justify-between gap-2">
                <h2 className="text-base font-semibold text-ink">
                  {item.productName}{' '}
                  <span className="text-sm font-normal text-ink-muted">({item.skuLabel})</span>
                </h2>
                <Badge
                  variant={
                    item.status === 'PendingModeration'
                      ? 'warning'
                      : item.status === 'Published'
                        ? 'success'
                        : 'neutral'
                  }
                >
                  {STATUS_TEXT[item.status]}
                </Badge>
              </div>
              <p className="text-sm font-semibold text-ink">
                {item.rating} out of 5{item.title ? ` — ${item.title}` : ''}
              </p>
              <p className="whitespace-pre-line text-sm text-ink">{item.body}</p>
              <p className="text-xs text-ink-muted">
                {item.customerDisplayName} · {formatShortInstant(item.submittedOn)}
              </p>
              <div className="flex flex-wrap gap-2">
                {item.status !== 'Published' && item.status !== 'Hidden' && (
                  <Button
                    size="sm"
                    aria-label={`Publish review of ${item.productName}`}
                    onClick={() => setConfirm({ item, kind: 'publish' })}
                  >
                    Publish
                  </Button>
                )}
                {item.status === 'PendingModeration' && (
                  <Button
                    size="sm"
                    variant="secondary"
                    aria-label={`Reject review of ${item.productName}`}
                    onClick={() => {
                      setRejecting(item);
                      setReason('');
                      setNote('');
                      setReasonError('');
                    }}
                  >
                    Reject
                  </Button>
                )}
                {item.status === 'Published' && (
                  <Button
                    size="sm"
                    variant="secondary"
                    aria-label={`Hide review of ${item.productName}`}
                    onClick={() => setConfirm({ item, kind: 'hide' })}
                  >
                    Hide
                  </Button>
                )}
              </div>
            </article>
          </li>
        ))}
      </ul>

      <ConfirmDialog
        open={confirm !== null}
        onOpenChange={(open) => !open && setConfirm(null)}
        title={confirm?.kind === 'publish' ? 'Publish this review?' : 'Hide this review?'}
        consequence={
          confirm
            ? confirm.kind === 'publish'
              ? `It will appear on the ${confirm.item.productName} page and count toward its rating.`
              : `It will disappear from the ${confirm.item.productName} page and no longer count toward its rating.`
            : ''
        }
        confirmLabel={confirm?.kind === 'publish' ? 'Publish' : 'Hide'}
        variant={confirm?.kind === 'hide' ? 'destructive' : 'primary'}
        onConfirm={async () => {
          const target = confirm;
          setConfirm(null);
          if (target) await run({ kind: target.kind, reviewId: target.item.reviewId });
        }}
      />

      <Dialog open={rejecting !== null} onOpenChange={(open) => !open && setRejecting(null)}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>Reject this review?</DialogTitle>
            <DialogDescription>
              {rejecting
                ? `The customer will see it as not published. It will not appear on the ${rejecting.productName} page.`
                : ''}
            </DialogDescription>
          </DialogHeader>
          <form
            noValidate
            className="flex flex-col gap-3"
            onSubmit={async (event) => {
              event.preventDefault();
              if (!reason) return setReasonError('Choose a reason for rejecting this review.');
              const target = rejecting;
              setRejecting(null);
              if (target)
                await run({
                  kind: 'reject',
                  reviewId: target.reviewId,
                  reason,
                  note: note.trim() || null,
                });
            }}
          >
            <Field id="rj-reason" label="Reason" error={reasonError}>
              <select
                id="rj-reason"
                className={inputClass}
                aria-invalid={!!reasonError}
                value={reason}
                onChange={(e) => {
                  setReason(e.target.value);
                  setReasonError('');
                }}
              >
                <option value="">Choose…</option>
                {REJECTION_REASONS.map(([value, label]) => (
                  <option key={value} value={value}>
                    {label}
                  </option>
                ))}
              </select>
            </Field>
            <Field id="rj-note" label="Note (optional)">
              <textarea
                id="rj-note"
                rows={3}
                className={inputClass}
                value={note}
                onChange={(e) => setNote(e.target.value)}
              />
            </Field>
            <DialogFooter>
              <Button type="button" variant="secondary" onClick={() => setRejecting(null)}>
                Cancel
              </Button>
              <Button type="submit" variant="destructive">
                Reject review
              </Button>
            </DialogFooter>
          </form>
        </DialogContent>
      </Dialog>
    </>
  );
}
