/**
 * `lib/calendar/ics.ts` — HO-APT-002 §11.1 `BookingDone`: a client-generated iCalendar (RFC 5545)
 * file from the confirmed appointment. Text only, no library, no HTML. Every field is escaped
 * (backslash, semicolon, comma, newline) and CR/LF are stripped from single-line values so user
 * text such as a cat's name can never inject another iCalendar property (HO-APT-002 gate 2).
 */

export interface IcsEventInput {
  uid: string;
  summary: string;
  startsOn: string; // ISO instant
  endsOn: string; // ISO instant
  location?: string | null;
  description?: string | null;
}

export function escapeIcsText(value: string): string {
  return (
    value
      .replace(/\\/g, '\\\\')
      .replace(/\r\n|\r|\n|\u0085|\u2028|\u2029/g, '\\n')
      // Every other control character (C0 except tab, DEL, C1) is invalid in iCalendar text.
      .replace(/[\u0000-\u0008\u000B\u000C\u000E-\u001F\u007F-\u009F]/g, '')
      .replace(/;/g, '\\;')
      .replace(/,/g, '\\,')
  );
}

/** `20261012T043000Z` — the UTC form every client understands. */
export function toIcsUtc(instant: string): string {
  const date = new Date(instant);
  if (Number.isNaN(date.getTime())) throw new Error('Invalid instant for iCalendar.');
  return date
    .toISOString()
    .replace(/[-:]/g, '')
    .replace(/\.\d{3}/, '');
}

/** RFC 5545 folds lines at 75 octets; fold by characters, which is safe for the ASCII-heavy text here. */
function fold(line: string): string {
  if (line.length <= 73) return line;
  const chunks: string[] = [];
  for (let i = 0; i < line.length; i += 73) chunks.push(line.slice(i, i + 73));
  return chunks.join('\r\n ');
}

export function buildIcs(event: IcsEventInput, now: Date = new Date()): string {
  const lines = [
    'BEGIN:VCALENDAR',
    'VERSION:2.0',
    'PRODID:-//PurrCare//Appointments//EN',
    'CALSCALE:GREGORIAN',
    'METHOD:PUBLISH',
    'BEGIN:VEVENT',
    `UID:${escapeIcsText(event.uid)}@purrcare`,
    `DTSTAMP:${toIcsUtc(now.toISOString())}`,
    `DTSTART:${toIcsUtc(event.startsOn)}`,
    `DTEND:${toIcsUtc(event.endsOn)}`,
    `SUMMARY:${escapeIcsText(event.summary)}`,
  ];
  if (event.location) lines.push(`LOCATION:${escapeIcsText(event.location)}`);
  if (event.description) lines.push(`DESCRIPTION:${escapeIcsText(event.description)}`);
  lines.push('END:VEVENT', 'END:VCALENDAR');
  return lines.map(fold).join('\r\n') + '\r\n';
}

/** A `blob:` URL for a download link; the caller revokes it. */
export function icsDownloadUrl(ics: string): string {
  return URL.createObjectURL(new Blob([ics], { type: 'text/calendar;charset=utf-8' }));
}
