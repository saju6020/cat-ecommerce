'use client';

import { Button } from '@/components/ui/button';
import { LoadingState } from '@/components/common/LoadingState';
import { ErrorState } from '@/components/common/ErrorState';
import { formatBranchDateLong } from '@/lib/format/appointment';
import { timeOnlyInZone, todayInZone } from '@/lib/format/time';
import { cn } from '@/lib/utils';
import type { SurgeryAvailability, SurgeryStaff, SurgeryStaffOption } from '@/lib/api/surgeries';
import { BusyExplainer } from './BusyExplainer';
import { errorClass, hintClass, inputClass, labelClass } from './styles';

export interface ResourceChoices {
  date: string;
  surgeonId: string;
  assistantId: string;
  theatreId: string;
  /** The chosen start (an ISO instant), or ''. */
  start: string;
}

export type ResourceErrors = Partial<
  Record<'date' | 'surgeonId' | 'assistantId' | 'theatreId' | 'start', string[]>
>;

/**
 * ⭐ `SurgeryResourcePicker` — HO-SRG-002 §11.1 part 3, ADR-FE-069. One form for a three-resource
 * booking: date, surgeon, assistant (the surgeon is removed from the assistant options) and theatre.
 * The starts are the SERVER's, shown as buttons (*"09:00–10:00"*, *"Outside opening hours"* where
 * flagged), and `BusyExplainer` says why the others are missing. With no start, it says so and offers
 * the next day that has one. Every clash the server returns sits next to its field (`errors`).
 */
export function SurgeryResourcePicker({
  choices,
  onChange,
  staff,
  theatres,
  availability,
  availabilityLoading,
  availabilityError,
  onRetry,
  zone,
  errors,
  nextFreeDay,
  onUseDay,
  disabled,
}: {
  choices: ResourceChoices;
  onChange: (changes: Partial<ResourceChoices>) => void;
  staff: SurgeryStaff | undefined;
  theatres: Array<{ id: string; name: string }>;
  availability: SurgeryAvailability | undefined;
  availabilityLoading: boolean;
  availabilityError: boolean;
  onRetry: () => void;
  zone: string;
  errors: ResourceErrors;
  nextFreeDay: string | null | undefined;
  onUseDay: (date: string) => void;
  disabled?: boolean;
}) {
  const assistants: SurgeryStaffOption[] = (staff?.assistants ?? []).filter(
    (person) => person.id !== choices.surgeonId,
  );
  const select = (
    id: string,
    label: string,
    value: string,
    options: Array<{ id: string; name: string }>,
    field: 'surgeonId' | 'assistantId' | 'theatreId',
  ) => {
    const messages = errors[field] ?? [];
    return (
      <div className="flex flex-col gap-1.5">
        <label htmlFor={id} className={labelClass}>
          {label}
        </label>
        <select
          id={id}
          className={inputClass}
          disabled={disabled}
          value={value}
          aria-invalid={messages.length > 0 ? true : undefined}
          aria-describedby={messages.length > 0 ? `${id}-error` : undefined}
          onChange={(event) => onChange({ [field]: event.target.value, start: '' })}
        >
          <option value="">Choose…</option>
          {options.map((option) => (
            <option key={option.id} value={option.id}>
              {option.name}
            </option>
          ))}
        </select>
        {messages.length > 0 && (
          <div id={`${id}-error`} role="alert">
            {messages.map((message) => (
              <p key={message} className={errorClass}>
                {message}
              </p>
            ))}
          </div>
        )}
      </div>
    );
  };

  const startErrors = errors.start ?? [];
  return (
    <section aria-labelledby="srg-step-when" className="flex flex-col gap-4">
      <h2 id="srg-step-when" className="font-display text-lg font-semibold text-ink">
        3. When, and who
      </h2>
      <div className="grid gap-4 sm:grid-cols-2">
        <div className="flex flex-col gap-1.5">
          <label htmlFor="surgery-date" className={labelClass}>
            Date ({zone})
          </label>
          <input
            id="surgery-date"
            type="date"
            className={inputClass}
            disabled={disabled}
            min={todayInZone(zone)}
            value={choices.date}
            onChange={(event) => onChange({ date: event.target.value, start: '' })}
          />
        </div>
        {select('surgery-theatre', 'Theatre', choices.theatreId, theatres, 'theatreId')}
        {select(
          'surgery-surgeon',
          'Surgeon',
          choices.surgeonId,
          staff?.surgeons ?? [],
          'surgeonId',
        )}
        {select('surgery-assistant', 'Assistant', choices.assistantId, assistants, 'assistantId')}
      </div>

      <div className="flex flex-col gap-2">
        <p id="surgery-starts-label" className={labelClass}>
          Available starts
        </p>
        {!choices.date || disabled ? (
          <p className={hintClass}>Choose a procedure and a date to see the times.</p>
        ) : availabilityError ? (
          <ErrorState title="We couldn’t load the times." onRetry={onRetry} headingLevel={3} />
        ) : availabilityLoading && !availability ? (
          <LoadingState variant="card" rows={2} label="Loading the available times…" />
        ) : availability && availability.starts.length > 0 ? (
          <div role="group" aria-labelledby="surgery-starts-label" className="flex flex-wrap gap-2">
            {availability.starts.map((start) => {
              const range = `${timeOnlyInZone(start.startsOn, zone)}–${timeOnlyInZone(start.endsOn, zone)}`;
              const selected = choices.start === start.startsOn;
              return (
                <button
                  key={start.startsOn}
                  type="button"
                  aria-pressed={selected}
                  onClick={() => onChange({ start: start.startsOn })}
                  className={cn(
                    'flex min-w-28 flex-col items-start rounded-[var(--radius-sm)] border px-3 py-2 text-sm font-semibold focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-accent',
                    selected
                      ? 'border-accent bg-accent-soft text-accent-ink'
                      : 'border-border-strong bg-surface text-ink hover:bg-surface-sunken',
                  )}
                >
                  {range}
                  {start.outsideOpeningHours && (
                    <span className="text-xs font-normal text-warning-ink">
                      Outside opening hours
                    </span>
                  )}
                </button>
              );
            })}
          </div>
        ) : availability ? (
          <div className="flex flex-col gap-2" role="status">
            <p className="text-sm font-semibold text-ink">
              No time on {formatBranchDateLong(choices.date)} when all three are free.
            </p>
            {nextFreeDay ? (
              <p className="text-sm text-ink">
                The next day with a start is {formatBranchDateLong(nextFreeDay)}.{' '}
                <Button variant="secondary" size="sm" onClick={() => onUseDay(nextFreeDay)}>
                  Use that day
                </Button>
              </p>
            ) : nextFreeDay === null ? (
              <p className={hintClass}>There is no common start in the next 7 days either.</p>
            ) : (
              <p className={hintClass}>Looking for the next day with a start…</p>
            )}
          </div>
        ) : null}
        {startErrors.length > 0 && (
          <div role="alert">
            {startErrors.map((message) => (
              <p key={message} className={errorClass}>
                {message}
              </p>
            ))}
          </div>
        )}
      </div>

      {availability && <BusyExplainer busy={availability.busy} zone={zone} />}
    </section>
  );
}
