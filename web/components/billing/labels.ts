/**
 * `components/billing/labels.ts` — plain-language labels for server enumeration names. Presentation
 * only: no screen decides anything from a method or kind name (ADR-FE-082) — they are only shown.
 */
export function humanise(name: string): string {
  const spaced = name.replace(/([a-z])([A-Z])/g, '$1 $2').toLowerCase();
  return spaced.charAt(0).toUpperCase() + spaced.slice(1);
}

export const methodLabel = humanise;
