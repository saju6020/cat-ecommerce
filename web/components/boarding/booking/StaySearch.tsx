'use client';

import { useState } from 'react';
import { Search } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { FormFieldRow } from '@/components/cat/FormFieldRow';
import { SelectRow } from '@/components/boarding/shared/FormBits';
import { MAX_CATS_PER_STAY, staySearchSchema } from '@/lib/validation/boarding';
import { nightsBetween } from '@/lib/format/boarding';
import { todayInZone } from '@/lib/format/time';

/**
 * `components/boarding/booking/StaySearch.tsx` — HO-BRD-002 §11.1 step 1, US-BRD-02. Arrival and
 * departure are branch-local `yyyy-MM-dd` strings in plain date inputs (no date-picker library —
 * §10); departure must follow arrival; the number of cats; the branch (hidden when there is only
 * one). The nights count is calendar arithmetic on the two strings, shown before the server has
 * priced anything — the price and availability are the server's (ADR-FE-105).
 */
export interface StaySearchValues {
  branchId: string;
  arrivalOn: string;
  departureOn: string;
  catCount: number;
}

export interface StaySearchBranch {
  id: string;
  name: string;
  timeZoneId: string;
}

export function StaySearch({
  value,
  branches,
  checkInHint,
  onSearch,
  submitLabel = 'See available units',
}: {
  value: StaySearchValues;
  branches: StaySearchBranch[];
  /** e.g. "Check-in from 14:00 (Asia/Dhaka)" — only what the availability answer carries. */
  checkInHint?: string | null;
  onSearch: (values: StaySearchValues) => void;
  submitLabel?: string;
}) {
  const [draft, setDraft] = useState<StaySearchValues>(value);
  const [errors, setErrors] = useState<
    Partial<Record<'arrivalOn' | 'departureOn' | 'catCount', string>>
  >({});
  const branch = branches.find((b) => b.id === draft.branchId) ?? branches[0];
  const zone = branch?.timeZoneId ?? 'Asia/Dhaka';
  const today = todayInZone(zone);
  const nights =
    draft.arrivalOn && draft.departureOn ? nightsBetween(draft.arrivalOn, draft.departureOn) : 0;

  function submit(event: React.FormEvent) {
    event.preventDefault();
    const parsed = staySearchSchema.safeParse({
      arrivalOn: draft.arrivalOn,
      departureOn: draft.departureOn,
      catCount: draft.catCount,
    });
    if (!parsed.success) {
      const next: typeof errors = {};
      for (const issue of parsed.error.issues) {
        const key = issue.path[0];
        if (key === 'arrivalOn' || key === 'departureOn' || key === 'catCount')
          next[key] ??= issue.message;
      }
      setErrors(next);
      return;
    }
    if (draft.arrivalOn < today) {
      setErrors({ arrivalOn: 'Arrival can’t be in the past.' });
      return;
    }
    setErrors({});
    onSearch({ ...draft, branchId: branch?.id ?? draft.branchId });
  }

  return (
    <form
      onSubmit={submit}
      noValidate
      aria-label="Find a stay"
      className="flex flex-col gap-4 rounded-[var(--radius-md)] border border-border bg-surface p-5"
    >
      <div className="grid gap-4 sm:grid-cols-2">
        <FormFieldRow
          id="stay-arrival"
          label="Arrival"
          type="date"
          required
          min={today}
          value={draft.arrivalOn}
          error={errors.arrivalOn}
          onChange={(e) => setDraft({ ...draft, arrivalOn: e.target.value })}
        />
        <FormFieldRow
          id="stay-departure"
          label="Departure"
          type="date"
          required
          min={draft.arrivalOn || today}
          value={draft.departureOn}
          error={errors.departureOn}
          onChange={(e) => setDraft({ ...draft, departureOn: e.target.value })}
        />
        <SelectRow
          id="stay-cat-count"
          label="Number of cats"
          value={String(draft.catCount)}
          error={errors.catCount}
          onChange={(e) => setDraft({ ...draft, catCount: Number(e.target.value) })}
        >
          {Array.from({ length: MAX_CATS_PER_STAY }, (_, i) => i + 1).map((n) => (
            <option key={n} value={n}>
              {n}
            </option>
          ))}
        </SelectRow>
        {branches.length > 1 && (
          <SelectRow
            id="stay-branch"
            label="Branch"
            value={draft.branchId}
            onChange={(e) => setDraft({ ...draft, branchId: e.target.value })}
          >
            {branches.map((b) => (
              <option key={b.id} value={b.id}>
                {b.name}
              </option>
            ))}
          </SelectRow>
        )}
      </div>
      <p className="text-sm text-ink-muted" aria-live="polite">
        {nights > 0 ? (
          <>
            <strong className="text-ink">
              {nights} night{nights === 1 ? '' : 's'}
            </strong>
            {' · '}
          </>
        ) : null}
        Dates are in {zone} time.{checkInHint ? ` ${checkInHint}` : ''}
      </p>
      <div>
        <Button type="submit">
          <Search size={16} aria-hidden="true" />
          {submitLabel}
        </Button>
      </div>
    </form>
  );
}
