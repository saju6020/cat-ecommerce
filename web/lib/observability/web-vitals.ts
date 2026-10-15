import type {
  ViewportClass,
  WebVitalMetricName,
  WebVitalSample,
  WebVitalsBatch,
} from '@/lib/api/telemetry';

/**
 * `lib/observability/web-vitals.ts` — HO-XCT-002 §11.6, ADR-FE-146. Pure mapping from a Next.js
 * web-vitals metric to the sample HO-XCT-001 section 12.4 accepts. A sample carries ONLY: metric,
 * value, route TEMPLATE (never the resolved URL, an id in it, or a query), navigation type,
 * effective connection type and viewport class — plus the build id on the batch. No user, session,
 * cookie or URL field exists in the payload at all.
 */

const METRICS: ReadonlySet<string> = new Set(['LCP', 'INP', 'CLS', 'FCP', 'TTFB']);

/** The API's `RoutePattern`: lower-case letters, digits and `-[]()/.` only, at most 200 chars. */
const ROUTE_PATTERN = /^\/[a-z0-9\-[\]()/.]{0,199}$/;

/** The API's accepted navigation types; Next/the browser spell one of them with an underscore. */
const NAVIGATION_TYPES: Readonly<Record<string, string>> = {
  navigate: 'navigate',
  reload: 'reload',
  'back-forward': 'back-forward',
  back_forward: 'back-forward',
  'back-forward-cache': 'back-forward-cache',
  prerender: 'prerender',
  restore: 'restore',
};

const CONNECTION_TYPES: ReadonlySet<string> = new Set(['4g', '3g', '2g', 'slow-2g']);

export const MAX_SAMPLES_PER_BATCH = 10;

/**
 * `/shop/abc-123` + `{ slug: 'abc-123' }` -> `/shop/[slug]`. Parameters are matched by POSITION: they
 * come in route order, so they are placed from the right, each one to the left of the one after it,
 * and a dynamic value that happens to equal a static segment (`/shop/products/products`) still
 * replaces the right one. (A static segment AFTER a dynamic one that shares its value cannot be told
 * apart without the route pattern; the measured storefront routes end in their parameter.)
 */
export function routeTemplateOf(
  pathname: string,
  params: Record<string, string | string[] | undefined>,
): string {
  const segments = pathname.split('/');
  const out: Array<string | null> = [...segments];
  const entries = Object.entries(params).filter(
    (entry): entry is [string, string | string[]] => entry[1] !== undefined,
  );
  let limit = segments.length; // exclusive: the next parameter to the left must end before this
  for (let e = entries.length - 1; e >= 0; e -= 1) {
    const [key, value] = entries[e] as [string, string | string[]];
    const parts = (Array.isArray(value) ? value : [value]).map(safeDecode);
    if (parts.length === 0) continue;
    for (let i = limit - parts.length; i >= 1; i -= 1) {
      if (parts.every((part, offset) => safeDecode(segments[i + offset] as string) === part)) {
        out[i] = Array.isArray(value) ? `[...${key}]` : `[${key}]`;
        for (let offset = 1; offset < parts.length; offset += 1) out[i + offset] = null;
        limit = i;
        break;
      }
    }
  }
  const joined = out.filter((segment) => segment !== null).join('/') || '/';
  return joined.length > 1 && joined.endsWith('/') ? joined.slice(0, -1) : joined;
}

function safeDecode(value: string): string {
  try {
    return decodeURIComponent(value);
  } catch {
    return value;
  }
}

export function classifyViewport(widthPx: number): ViewportClass {
  if (widthPx < 640) return 'Narrow';
  if (widthPx < 1024) return 'Medium';
  return 'Wide';
}

export interface MetricInput {
  name: string;
  value: number;
  navigationType?: string;
}

export interface SampleContext {
  routeTemplate: string;
  viewportWidth: number;
  effectiveConnectionType?: string | null;
}

/** Returns the sample, or null when the metric is not one the API takes or the route would be
 *  refused (so a bad route never poisons a whole batch). */
export function buildSample(metric: MetricInput, context: SampleContext): WebVitalSample | null {
  if (!METRICS.has(metric.name)) return null;
  if (!ROUTE_PATTERN.test(context.routeTemplate)) return null;
  if (!Number.isFinite(metric.value) || metric.value < 0) return null;
  const navigationType = NAVIGATION_TYPES[metric.navigationType ?? 'navigate'] ?? 'navigate';
  const connection = context.effectiveConnectionType ?? null;
  return {
    metric: metric.name as WebVitalMetricName,
    // CLS is unitless (keep three decimals); every other metric is milliseconds.
    value: metric.name === 'CLS' ? Math.round(metric.value * 1000) / 1000 : Math.round(metric.value),
    routeTemplate: context.routeTemplate,
    navigationType,
    effectiveConnectionType: connection && CONNECTION_TYPES.has(connection) ? connection : null,
    viewportClass: classifyViewport(context.viewportWidth),
  };
}

/** Splits the buffered samples into batches the endpoint accepts (≤ 10 samples each). */
export function toBatches(samples: WebVitalSample[], buildId: string): WebVitalsBatch[] {
  const batches: WebVitalsBatch[] = [];
  for (let i = 0; i < samples.length; i += MAX_SAMPLES_PER_BATCH) {
    batches.push({ buildId, samples: samples.slice(i, i + MAX_SAMPLES_PER_BATCH) });
  }
  return batches;
}

/** The build id the API accepts (`^[A-Za-z0-9._-]{1,64}$`); `unknown` when none was baked in. */
export function currentBuildId(): string {
  const raw = process.env.NEXT_PUBLIC_BUILD_ID ?? '';
  return /^[A-Za-z0-9._-]{1,64}$/.test(raw) ? raw : 'unknown';
}

/** Routes that are not measured: staff pages and the offline board (the requirement is the
 *  storefront), and anything under the API. */
export function isMeasuredPath(pathname: string): boolean {
  return !pathname.startsWith('/staff') && !pathname.startsWith('/api');
}
