import { describe, expect, it } from 'vitest';
import { buildIcs, escapeIcsText, toIcsUtc } from '@/lib/calendar/ics';

describe('lib/calendar/ics — HO-APT-002 §11.1 BookingDone', () => {
  const event = {
    uid: 'a1',
    summary: 'Vaccination for Mitu',
    startsOn: '2026-10-12T04:30:00Z',
    endsOn: '2026-10-12T05:00:00Z',
    location: 'Main clinic, 12 Gulshan Avenue, Dhaka',
  };

  it('U-A11: carries the summary, start and end in UTC and the branch address, with no HTML', () => {
    const ics = buildIcs(event, new Date('2026-10-01T00:00:00Z'));
    expect(ics).toContain('BEGIN:VCALENDAR');
    expect(ics).toContain('SUMMARY:Vaccination for Mitu');
    expect(ics).toContain('DTSTART:20261012T043000Z');
    expect(ics).toContain('DTEND:20261012T050000Z');
    expect(ics).toContain('LOCATION:Main clinic\\, 12 Gulshan Avenue\\, Dhaka');
    expect(ics).not.toMatch(/<[a-z]+/i);
    expect(ics.endsWith('\r\n')).toBe(true);
  });

  it('escapes user text so a cat name cannot inject a property', () => {
    const ics = buildIcs(
      { ...event, summary: 'Mitu\r\nATTENDEE:mailto:evil@example.com;x,y\\' },
      new Date('2026-10-01T00:00:00Z'),
    );
    expect(ics).not.toMatch(/^ATTENDEE:/m);
    expect(ics).toContain('SUMMARY:Mitu\\nATTENDEE:mailto:evil@example.com\\;x\\,y\\\\');
  });

  it('W11: strips other control characters and treats U+0085/U+2028/U+2029 as line breaks', () => {
    expect(escapeIcsText('a\u0000b\u0007c\u001Bd\u007Fe\u0090f')).toBe('abcdef');
    expect(escapeIcsText('x\u0085y\u2028z\u2029w')).toBe('x\\ny\\nz\\nw');
    expect(escapeIcsText('tab\there')).toBe('tab\there');
    const ics = buildIcs(
      { ...event, summary: 'Mitu\u2028ATTENDEE:mailto:evil@example.com\u0001' },
      new Date('2026-10-01T00:00:00Z'),
    );
    expect(ics).not.toMatch(/[\u0001\u2028]/);
    expect(ics).not.toMatch(/^ATTENDEE:/m);
  });

  it('helpers', () => {
    expect(escapeIcsText('a;b,c\nd')).toBe('a\\;b\\,c\\nd');
    expect(toIcsUtc('2026-10-12T04:30:00.000Z')).toBe('20261012T043000Z');
    expect(() => toIcsUtc('nope')).toThrow();
  });
});
