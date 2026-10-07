/**
 * `lib/shop/helpers.ts` — HO-SHP-002. Small pure helpers for storefront presentation. Nothing here
 * decides stock, price or eligibility (ADR-FE-032): those come from the API.
 */

/** A plain-text excerpt of LimitedMarkup (headings, bullets, bold, links reduced to their text). */
export function plainExcerpt(markup: string | null | undefined, max = 160): string {
  if (!markup) return '';
  const text = markup
    .replace(/^#{2,3}\s+/gm, '')
    .replace(/^-\s+/gm, '')
    .replace(/\[([^\]]+)\]\([^)]+\)/g, '$1')
    .replace(/\*\*([^*]+)\*\*/g, '$1')
    .replace(/\s+/g, ' ')
    .trim();
  if (text.length <= max) return text;
  return `${text.slice(0, max).replace(/\s+\S*$/, '')}…`;
}

/** `Rahim K.`-style first name for greetings. */
export function firstName(fullName: string | null | undefined): string {
  return (fullName ?? '').trim().split(/\s+/)[0] ?? '';
}

/**
 * Only a site-relative path or an `https:` URL may become a link target (banner links). A path
 * must start with a single `/` (not `//` or `/\`, which browsers read as off-site) and hold no
 * tab, CR or LF (browsers strip them, so `/<TAB>/host` becomes `//host`) - S5.
 */
export function isSafeLink(url: string | null | undefined): url is string {
  if (!url) return false;
  if (/[\t\r\n]/.test(url)) return false;
  if (url.startsWith('/')) return /^\/(?![/\\])/.test(url);
  try {
    return new URL(url).protocol === 'https:';
  } catch {
    return false;
  }
}

/** `/shop/products/{slug}` with the optional variant and language parameters. */
export function productHref(slug: string, options: { sku?: string; lang?: string } = {}): string {
  const query = new URLSearchParams();
  if (options.sku) query.set('sku', options.sku);
  if (options.lang) query.set('lang', options.lang);
  const text = query.toString();
  return `/shop/products/${encodeURIComponent(slug)}${text ? `?${text}` : ''}`;
}

/** Joins channel names for the stock-alert confirmation: `in-app and SMS`. */
export function describeChannels(channels: string[]): string {
  const names = channels.map((channel) =>
    channel === 'InApp' ? 'in-app notification' : channel === 'Sms' ? 'SMS' : channel.toLowerCase(),
  );
  if (names.length <= 1) return names[0] ?? 'notification';
  return `${names.slice(0, -1).join(', ')} and ${names[names.length - 1]}`;
}

export type Settled<T> = { ok: true; value: T } | { ok: false; error: unknown };

/** Awaits a server read without letting one failing optional read (banners, nav) break the page. */
export async function settle<T>(promise: Promise<T>): Promise<Settled<T>> {
  try {
    return { ok: true, value: await promise };
  } catch (error) {
    return { ok: false, error };
  }
}

/** A storefront call answering `503` is the maintenance refusal (US-ADM-08). */
export function isMaintenanceError(error: unknown): boolean {
  return (
    typeof error === 'object' && error !== null && (error as { status?: number }).status === 503
  );
}

/** Default variant (HO-SHP-002 §8.4): the requested one, else the first in-stock SKU, else the first. */
export function defaultSku<T extends { skuId: string; availability: string }>(
  skus: T[],
  requested?: string | null,
): T | undefined {
  return (
    skus.find((s) => s.skuId === requested) ??
    skus.find((s) => s.availability === 'InStock') ??
    skus[0]
  );
}
