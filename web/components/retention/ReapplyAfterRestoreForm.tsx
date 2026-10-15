'use client';

import { useState } from 'react';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { z } from 'zod';
import { WriteButton } from '@/components/common/WriteButton';
import { ConfirmDialog } from '@/components/common/ConfirmDialog';
import { useReapplyErasures } from '@/lib/hooks/useRetention';
import { describeOperationsError } from '@/lib/api/errors';
import type { ReapplyResult } from '@/lib/api/retention';

export const MAX_REAPPLY_IDS = 500;
const GUID = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;

export interface ParsedIds {
  ids: string[];
  malformed: string[];
  count: number;
}

/** One id per line; blank lines ignored; duplicates collapsed. */
export function parseCustomerIds(text: string): ParsedIds {
  const lines = text.split(/\r?\n/).map((l) => l.trim()).filter(Boolean);
  const unique = [...new Set(lines)];
  return {
    ids: unique.filter((l) => GUID.test(l)),
    malformed: unique.filter((l) => !GUID.test(l)),
    count: unique.length,
  };
}

const schema = z.object({
  ids: z.string().superRefine((value, ctx) => {
    const parsed = parseCustomerIds(value);
    if (parsed.count === 0) {
      ctx.addIssue({ code: 'custom', message: 'Paste at least one customer id.' });
    } else if (parsed.count > MAX_REAPPLY_IDS) {
      ctx.addIssue({
        code: 'custom',
        message: `That's ${parsed.count} customers; the limit is ${MAX_REAPPLY_IDS}. Split the list.`,
      });
    } else if (parsed.malformed.length > 0) {
      ctx.addIssue({
        code: 'custom',
        message: `${parsed.malformed.length} of ${parsed.count} lines are not customer ids (for example "${parsed.malformed[0]?.slice(0, 40)}"). Fix or remove them.`,
      });
    }
  }),
  restoreReference: z.string().trim().min(1, 'Enter the restore reference.').max(200, 'Keep the reference under 200 characters.'),
});
type FormValues = z.infer<typeof schema>;

/**
 * `components/retention/ReapplyAfterRestoreForm.tsx` — HO-XCT-002 §11.5, U-B7. After a restore, the
 * erasure journal is replayed so people who were erased are erased again. Ids are validated for
 * count and shape BEFORE anything is sent; a confirmation says this re-anonymises accounts that
 * came back with the restore; the result panel shows the four counts the API returns.
 */
export function ReapplyAfterRestoreForm() {
  const { register, handleSubmit, setError, formState: { errors } } = useForm<FormValues>({
    resolver: zodResolver(schema),
    defaultValues: { ids: '', restoreReference: '' },
  });
  const reapply = useReapplyErasures();
  const [pending, setPending] = useState<FormValues | null>(null);
  const [result, setResult] = useState<ReapplyResult | null>(null);

  async function confirm() {
    if (!pending) return;
    try {
      setResult(
        await reapply.mutateAsync({
          customerAccountIds: parseCustomerIds(pending.ids).ids,
          restoreReference: pending.restoreReference.trim(),
        }),
      );
    } catch (error) {
      setError('root', { message: describeOperationsError(error) });
    } finally {
      setPending(null);
    }
  }

  return (
    <div className="flex max-w-2xl flex-col gap-4">
      <h1 className="font-display text-xl font-semibold text-ink">Re-apply erasures after a restore</h1>
      <p className="text-sm text-ink-muted">
        After the database is restored from a backup, people who asked to be erased may have come
        back. Paste the customer ids from the erasure journal exported with the backup, one per line
        (up to {MAX_REAPPLY_IDS}).
      </p>
      <form onSubmit={handleSubmit((values) => setPending(values))} noValidate className="flex flex-col gap-4">
        <div className="flex flex-col gap-1">
          <label htmlFor="reapply-ids" className="text-sm font-semibold text-ink">Customer ids</label>
          <textarea
            id="reapply-ids"
            rows={8}
            aria-invalid={Boolean(errors.ids)}
            aria-describedby={errors.ids ? 'reapply-ids-error' : undefined}
            className="rounded-[var(--radius-sm)] border border-border-strong bg-surface p-2 font-mono text-xs text-ink"
            {...register('ids')}
          />
          {errors.ids && <p id="reapply-ids-error" role="alert" className="text-sm text-danger-ink">{errors.ids.message}</p>}
        </div>
        <div className="flex flex-col gap-1">
          <label htmlFor="reapply-ref" className="text-sm font-semibold text-ink">Restore reference</label>
          <input
            id="reapply-ref"
            type="text"
            aria-invalid={Boolean(errors.restoreReference)}
            aria-describedby={errors.restoreReference ? 'reapply-ref-error' : undefined}
            className="rounded-[var(--radius-sm)] border border-border-strong bg-surface px-3 py-2 text-sm text-ink"
            {...register('restoreReference')}
          />
          {errors.restoreReference && (
            <p id="reapply-ref-error" role="alert" className="text-sm text-danger-ink">{errors.restoreReference.message}</p>
          )}
        </div>
        {errors.root && <p role="alert" className="text-sm text-danger-ink">{errors.root.message}</p>}
        <div>
          <WriteButton type="submit">Re-apply erasures</WriteButton>
        </div>
      </form>

      {result && (
        <section aria-labelledby="reapply-result" className="rounded-[var(--radius-md)] border border-border bg-surface p-4">
          <h2 id="reapply-result" className="text-[15px] font-semibold text-ink">Result</h2>
          <dl className="mt-2 grid grid-cols-[auto_1fr] gap-x-4 gap-y-1 text-sm">
            <dt className="text-ink-muted">Erased again</dt><dd className="font-semibold text-ink">{result.processed}</dd>
            <dt className="text-ink-muted">Already erased</dt><dd className="font-semibold text-ink">{result.alreadyErased}</dd>
            <dt className="text-ink-muted">Not found</dt><dd className="font-semibold text-ink">{result.notFound}</dd>
            <dt className="text-ink-muted">Merged into another customer</dt><dd className="font-semibold text-ink">{result.merged}</dd>
            {result.failed ? (<><dt className="text-ink-muted">Could not be processed</dt><dd className="font-semibold text-ink">{result.failed}</dd></>) : null}
          </dl>
        </section>
      )}

      <ConfirmDialog
        open={pending !== null}
        onOpenChange={(open) => !open && setPending(null)}
        title="Re-apply these erasures?"
        consequence={`This re-anonymises ${pending ? parseCustomerIds(pending.ids).count : 0} accounts that came back with the restore. It cannot be undone.`}
        confirmLabel="Re-apply erasures"
        variant="destructive"
        onConfirm={confirm}
      />
    </div>
  );
}
