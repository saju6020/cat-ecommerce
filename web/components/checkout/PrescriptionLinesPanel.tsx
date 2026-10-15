'use client';

import { useEffect, useRef, useState } from 'react';
import Link from 'next/link';
import { Button } from '@/components/ui/button';
import type { CartLine } from '@/lib/api/cart';

/**
 * `components/checkout/PrescriptionLinesPanel.tsx` — HO-ORD-002 §11.2 step 4, US-ORD-03 AC2. Each
 * prescription line's cat is shown; a line **without a cat blocks placing** and offers a picker, which
 * takes focus. The panel lists the customer's cats and sends the choice to the cart line — whether
 * that cat is authorised is the server's answer, shown here as its message (never decided here).
 */
export function PrescriptionLinesPanel({
  lines,
  cats,
  pendingLineId,
  error,
  onChoose,
}: {
  lines: CartLine[];
  cats: Array<{ id: string; name: string }>;
  pendingLineId: string | null;
  error: string | null;
  onChoose: (lineId: string, catId: string) => void;
}) {
  const prescription = lines.filter((line) => line.requiresPrescription);
  const needsCat = prescription.some((line) => line.prescriptionStatus === 'CatRequired');
  const firstPicker = useRef<HTMLInputElement | null>(null);
  const [selection, setSelection] = useState<Record<string, string>>({});

  useEffect(() => {
    if (needsCat) firstPicker.current?.focus();
  }, [needsCat]);

  if (prescription.length === 0) return null;

  let assignedFocus = false;
  return (
    <section aria-labelledby="rx-lines-heading" className="flex flex-col gap-3 rounded-[var(--radius-md)] border border-border bg-surface p-4">
      <h3 id="rx-lines-heading" className="font-display text-base font-semibold text-ink">
        Prescription food
      </h3>
      {prescription.map((line) => {
        const missing = line.prescriptionStatus === 'CatRequired';
        return (
          <div key={line.lineId} className="text-sm">
            <p className="font-semibold text-ink">{`${line.productName} ${line.variantLabel}`.trim()}</p>
            {missing ? (
              <fieldset className="m-0 mt-2 flex min-w-0 flex-col gap-2 border-0 p-0">
                <legend className="mb-1 font-semibold text-ink">Which cat is this for?</legend>
                {cats.length === 0 && (
                  <p className="text-ink-muted">
                    You haven&rsquo;t added a cat yet.{' '}
                    <Link href="/portal/cats/new" className="font-semibold text-accent-ink underline">
                      Add your cat
                    </Link>
                  </p>
                )}
                {cats.map((cat) => {
                  const attach = !assignedFocus;
                  assignedFocus = true;
                  return (
                    <label key={cat.id} className="flex items-center gap-2">
                      <input
                        ref={attach ? firstPicker : undefined}
                        type="radio"
                        name={`rx-${line.lineId}`}
                        className="h-4 w-4 accent-[var(--accent)]"
                        checked={selection[line.lineId] === cat.id}
                        onChange={() => setSelection((current) => ({ ...current, [line.lineId]: cat.id }))}
                      />
                      {cat.name}
                    </label>
                  );
                })}
                <div>
                  <Button
                    size="sm"
                    disabled={!selection[line.lineId] || pendingLineId === line.lineId}
                    onClick={() => onChoose(line.lineId, selection[line.lineId]!)}
                  >
                    {pendingLineId === line.lineId ? 'Saving…' : 'Use this cat'}
                  </Button>
                </div>
              </fieldset>
            ) : (
              <p className="text-ink-muted">
                {line.cat ? `For ${line.cat.name}` : 'Cat chosen'}
                {line.prescriptionStatus === 'NotAuthorised'
                  ? ' — there is no current vet authorisation for this cat.'
                  : ''}
              </p>
            )}
          </div>
        );
      })}
      {error && (
        <p role="alert" className="text-sm font-semibold text-danger-ink">
          {error}
        </p>
      )}
    </section>
  );
}
