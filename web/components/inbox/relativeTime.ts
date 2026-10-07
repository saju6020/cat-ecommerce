import { formatInZone } from '@/lib/format/time';

const relativeFormat = new Intl.RelativeTimeFormat('en', { numeric: 'auto' });

/** "just now", "5 minutes ago", "yesterday", "3 days ago"; older than 30 days falls back to a date. */
export function formatRelative(instant: string, now: number = Date.now()): string {
  const then = new Date(instant).getTime();
  if (Number.isNaN(then)) return '';
  const seconds = Math.round((then - now) / 1000);
  const abs = Math.abs(seconds);
  if (abs < 45) return 'just now';
  if (abs < 3600) return relativeFormat.format(Math.round(seconds / 60), 'minute');
  if (abs < 86_400) return relativeFormat.format(Math.round(seconds / 3600), 'hour');
  if (abs < 30 * 86_400) return relativeFormat.format(Math.round(seconds / 86_400), 'day');
  return new Intl.DateTimeFormat('en-GB', { dateStyle: 'medium' }).format(new Date(then));
}

/** The viewer's own time zone — an inbox is personal, not branch-local. */
export function viewerTimeZone(): string {
  return Intl.DateTimeFormat().resolvedOptions().timeZone || 'UTC';
}

/** The absolute time, with the zone named (lib/format, ADR-FE-015). */
export function formatAbsolute(instant: string): string {
  return formatInZone(instant, viewerTimeZone());
}
