import { differenceInMonths, differenceInYears } from 'date-fns';
import type { DateOfBirthPrecision } from '@/lib/api/cats';
import { parseDateOnly } from '@/lib/dateOnly';

/**
 * `components/cat/AgeDisplay.tsx` — US-CAT-01 AC2 (HO-CAT-002 §11.7).
 *
 * "3 years 2 months" for an exact date of birth; "~3 years (estimated)" — rounded to the
 * nearest year, prefixed with a tilde and suffixed "(estimated)" — when
 * `dateOfBirthPrecision === 'Estimated'`; "Unknown" when there is no date of birth at all.
 *
 * Parses `dateOfBirth` (a `DateOnly` string, `yyyy-MM-dd`) as a local calendar date rather than
 * through `new Date(isoString)`, which would apply UTC and could shift the day in a
 * negative-UTC-offset browser (HO-CAT-002 §9's "date moving a day" warning).
 */
export interface AgeDisplayProps {
  dateOfBirth: string | null;
  dateOfBirthPrecision: DateOfBirthPrecision;
  /** Fixed "now" for deterministic tests. Defaults to the real current date. */
  asOf?: Date;
  className?: string;
}

function formatExactAge(dob: Date, asOf: Date): string {
  const years = differenceInYears(asOf, dob);
  const months = differenceInMonths(asOf, dob) % 12;
  const parts: string[] = [];
  if (years > 0) parts.push(`${years} year${years === 1 ? '' : 's'}`);
  if (months > 0 || years === 0) parts.push(`${months} month${months === 1 ? '' : 's'}`);
  return parts.join(' ');
}

function formatEstimatedAge(dob: Date, asOf: Date): string {
  const totalMonths = differenceInMonths(asOf, dob);
  const roundedYears = Math.round(totalMonths / 12);
  return `~${roundedYears} year${roundedYears === 1 ? '' : 's'} (estimated)`;
}

export function AgeDisplay({
  dateOfBirth,
  dateOfBirthPrecision,
  asOf = new Date(),
  className,
}: AgeDisplayProps) {
  if (!dateOfBirth) {
    return <span className={className}>Unknown</span>;
  }

  const dob = parseDateOnly(dateOfBirth);
  const text =
    dateOfBirthPrecision === 'Estimated'
      ? formatEstimatedAge(dob, asOf)
      : formatExactAge(dob, asOf);

  return <span className={className}>{text}</span>;
}
