/**
 * `lib/routing/isSafeInternalPath.ts` — HO-NTF-002 §8.4, ADR-FE-162 (handover ADR-FE-022). An
 * in-app message's `linkPath` is data a producer (or, through a template, an administrator)
 * supplied; it is followed only when it is a site-relative path. It starts with a single `/`;
 * never `//` (protocol-relative, i.e. another host) and never `/\` (browsers treat a backslash as a
 * slash). Any backslash or control character anywhere is refused too — URL parsers strip tabs and
 * newlines, so a path such as "/", TAB, "/evil.example" would otherwise become `//evil.example`.
 * A path that starts with `/` cannot carry a scheme, so there is no separate scheme check to get
 * wrong.
 */
export function isSafeInternalPath(path: string | null | undefined): path is string {
  if (typeof path !== 'string' || path.length === 0) return false;
  if (!path.startsWith('/')) return false;
  if (path.startsWith('//') || path.startsWith('/\\')) return false;
  if (/[\\\u0000-\u001f\u007f]/.test(path)) return false;
  return true;
}
