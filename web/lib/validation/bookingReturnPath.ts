import { sanitizeNextPath } from '@/lib/validation/nextPath';

const DUMMY_ORIGIN = 'http://local';
const BACKSLASH = String.fromCharCode(92);

/**
 * HO-CAT-002-ADDCAT — the `?next=` booking return path, validated on the string that is actually
 * navigated to. `sanitizeNextPath` checks the raw text, but URL parsing then resolves dot-segments
 * (`/book/..//evil`, `%2e%2e`) and backslashes into a different path, so the result is re-checked
 * here: same origin, `/book` or `/book/**` exactly, no leading `//`, no backslash.
 * Returns the normalised `pathname + search`, or null when unsafe.
 */
export function safeBookingPath(next: string | null | undefined): string | null {
  if (!next || next.includes(BACKSLASH)) return null;
  if (sanitizeNextPath(next, '/book', '') !== next) return null;
  let url: URL;
  try {
    url = new URL(next, DUMMY_ORIGIN);
  } catch {
    return null;
  }
  if (url.origin !== DUMMY_ORIGIN) return null;
  if (url.pathname !== '/book' && !url.pathname.startsWith('/book/')) return null;
  const final = `${url.pathname}${url.search}`;
  if (final.startsWith('//') || final.includes(BACKSLASH)) return null;
  if (sanitizeNextPath(final, '/book', '') !== final) return null;
  return final;
}

/** Where to go after a cat is added: the booking path with `catId` set, else the cat's page. */
export function resolveAddCatReturn(next: string | null | undefined, catId: string): string {
  const safe = safeBookingPath(next);
  if (!safe) return `/portal/cats/${catId}`;
  const url = new URL(safe, DUMMY_ORIGIN);
  url.searchParams.set('catId', catId);
  return `${url.pathname}${url.search}`;
}

/** The booking cat step's "Add a cat" link: carries every current booking parameter in `next`. */
export function buildAddCatHref(bookingQuery: string): string {
  const next = bookingQuery ? `/book?${bookingQuery}` : '/book';
  return `/portal/cats/new?next=${encodeURIComponent(next)}`;
}
