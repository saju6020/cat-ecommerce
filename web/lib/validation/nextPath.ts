/**
 * HO-ACC-002 §11.5 — the `?next=` open-redirect guard. Only a same-origin path starting with the
 * given prefix is honoured; anything else (an absolute URL, `//evil.example`, a path outside the
 * caller's own area) is ignored in favour of the default. `next` must start with a single `/` and
 * never `//` (protocol-relative — still an off-site redirect in a browser).
 *
 * ARCH gate LOW (first pass): a bare `startsWith` let a prefix match a longer, unrelated path
 * segment too — `/book` also "matched" `/bookXYZ`, a path outside the intended `/book/**` area. A
 * prefix now only matches at a `/`-boundary (or the exact path), the same rule a router would use.
 *
 * A-APT1 hardening: the value is also resolved with `new URL(next, origin)`; it must stay on that
 * origin and the NORMALISED pathname (dot-segments and `%2e` collapsed) is what the prefix is
 * checked against, so `/book/..//evil` (normalises to `//evil`) and `/portal/../..//evil` fall
 * back. A backslash or any control character (tab, CR, LF, …) is rejected outright — browsers
 * strip or rewrite them, turning `/\evil` or `/<TAB>/evil` into an off-site URL. A valid value is
 * returned unchanged (query string included).
 */
const PARSE_ORIGIN = 'http://next-path.invalid';
const FORBIDDEN_CHARS = /[\\\u0000-\u001f\u007f]/;

export function sanitizeNextPath(
  next: string | null | undefined,
  allowedPrefixes: string | string[],
  fallback: string,
): string {
  if (!next) return fallback;
  if (!next.startsWith('/') || next.startsWith('//')) return fallback;
  if (FORBIDDEN_CHARS.test(next)) return fallback;
  let resolved: URL;
  try {
    resolved = new URL(next, PARSE_ORIGIN);
  } catch {
    return fallback;
  }
  if (resolved.origin !== PARSE_ORIGIN) return fallback;
  const pathname = resolved.pathname;
  if (!pathname.startsWith('/') || pathname.startsWith('//')) return fallback;
  const prefixes = Array.isArray(allowedPrefixes) ? allowedPrefixes : [allowedPrefixes];
  return prefixes.some((prefix) => matchesPrefix(pathname, prefix)) ? next : fallback;
}

function matchesPrefix(pathname: string, prefix: string): boolean {
  const trimmed = prefix.endsWith('/') ? prefix.slice(0, -1) : prefix;
  return pathname === trimmed || pathname.startsWith(`${trimmed}/`);
}
