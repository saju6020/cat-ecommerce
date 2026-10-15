import { beforeEach, describe, expect, it, vi } from 'vitest';
import { render } from '@testing-library/react';
import { http, HttpResponse } from 'msw';
import { server } from '@/mocks/server';
import { sendTelemetry } from '@/lib/api/client';
import {
  buildSample,
  classifyViewport,
  isMeasuredPath,
  routeTemplateOf,
  toBatches,
  currentBuildId,
} from '@/lib/observability/web-vitals';

describe('U-X3 the payload carries a route template and nothing identifying', () => {
  it('/shop/abc-123 becomes /shop/[slug]', () => {
    expect(routeTemplateOf('/shop/abc-123', { slug: 'abc-123' })).toBe('/shop/[slug]');
    expect(routeTemplateOf('/shop/products/royal-canin', { slug: 'royal-canin' })).toBe('/shop/products/[slug]');
    expect(routeTemplateOf('/shop', {})).toBe('/shop');
    // matched by position: a value equal to an earlier static segment still replaces the right one
    expect(routeTemplateOf('/shop/products/products', { slug: 'products' })).toBe('/shop/products/[slug]');
    expect(routeTemplateOf('/staff/a/1/b/2', { first: '1', second: '2' })).toBe('/staff/a/[first]/b/[second]');
    expect(routeTemplateOf('/docs/a/b/c', { path: ['a', 'b', 'c'] })).toBe('/docs/[...path]');
    expect(routeTemplateOf('/portal/orders/9f1c', { orderId: '9f1c' })).toBe('/portal/orders/[orderId]');
  });

  it('the sample has exactly the specified keys: no URL, query, user or session field', () => {
    const sample = buildSample(
      { name: 'LCP', value: 2210.6, navigationType: 'back_forward' },
      { routeTemplate: '/shop/[slug]', viewportWidth: 360, effectiveConnectionType: '4g' },
    )!;
    expect(sample).toEqual({
      metric: 'LCP',
      value: 2211,
      routeTemplate: '/shop/[slug]',
      navigationType: 'back-forward',
      effectiveConnectionType: '4g',
      viewportClass: 'Narrow',
    });
    expect(Object.keys(sample).sort()).toEqual(
      ['effectiveConnectionType', 'metric', 'navigationType', 'routeTemplate', 'value', 'viewportClass'],
    );
  });

  it('drops what the API would refuse (unknown metric, bad route, bad value) and maps odd values', () => {
    const ctx = { routeTemplate: '/shop', viewportWidth: 800 };
    expect(buildSample({ name: 'Next.js-hydration', value: 5 }, ctx)).toBeNull();
    expect(buildSample({ name: 'LCP', value: 5 }, { ...ctx, routeTemplate: '/Shop?x=1' })).toBeNull();
    expect(buildSample({ name: 'LCP', value: -1 }, ctx)).toBeNull();
    expect(buildSample({ name: 'CLS', value: 0.04567 }, ctx)?.value).toBe(0.046);
    expect(buildSample({ name: 'LCP', value: 1 }, { ...ctx, effectiveConnectionType: '5g' })?.effectiveConnectionType).toBeNull();
    expect(classifyViewport(639)).toBe('Narrow');
    expect(classifyViewport(1024)).toBe('Wide');
    expect(currentBuildId()).toMatch(/^[A-Za-z0-9._-]{1,64}$/);
  });

  it('splits into batches of at most ten samples', () => {
    const sample = buildSample({ name: 'LCP', value: 1 }, { routeTemplate: '/', viewportWidth: 1200 })!;
    const batches = toBatches(Array.from({ length: 23 }, () => sample), 'b1');
    expect(batches.map((b) => b.samples.length)).toEqual([10, 10, 3]);
  });
});

describe('U-X5 staff routes are not measured', () => {
  it('only storefront-side paths are', () => {
    expect(isMeasuredPath('/staff/front-desk')).toBe(false);
    expect(isMeasuredPath('/staff/offline')).toBe(false);
    expect(isMeasuredPath('/shop')).toBe(true);
    expect(isMeasuredPath('/portal')).toBe(true);
  });
});

describe('U-X4 sendTelemetry', () => {
  beforeEach(() => {
    document.cookie = 'pc_csrf=secret-token';
  });

  it('is anonymous: credentials omit, keepalive, no CSRF header, no branch header', async () => {
    const spy = vi.spyOn(globalThis, 'fetch');
    const seen = vi.fn();
    server.use(
      http.post('*/api/telemetry/web-vitals', async ({ request }) => {
        seen({ csrf: request.headers.get('X-CSRF-Token'), branch: request.headers.get('X-Branch-Id'), type: request.headers.get('Content-Type') });
        return new HttpResponse(null, { status: 204 });
      }),
    );
    await expect(sendTelemetry('/api/telemetry/web-vitals', { buildId: 'b', samples: [] })).resolves.toBe(true);
    const init = spy.mock.calls.at(-1)?.[1] as RequestInit;
    expect(init.credentials).toBe('omit');
    expect(init.keepalive).toBe(true);
    expect(seen).toHaveBeenCalledWith({ csrf: null, branch: null, type: 'text/plain;charset=UTF-8' });
    spy.mockRestore();
  });

  it('never throws', async () => {
    server.use(http.post('*/api/telemetry/web-vitals', () => HttpResponse.error()));
    await expect(sendTelemetry('/api/telemetry/web-vitals', {})).resolves.toBe(false);
  });
});

describe('the reporter component', () => {
  it('sends nothing on a /staff route and flushes samples on a storefront route', async () => {
    vi.resetModules();
    const sent = vi.fn().mockResolvedValue(true);
    let reportCallback: ((m: { name: string; value: number; navigationType: string }) => void) | undefined;
    vi.doMock('@/lib/api/telemetry', async () => ({ ...(await vi.importActual<object>('@/lib/api/telemetry')), sendWebVitals: sent }));
    vi.doMock('next/web-vitals', () => ({ useReportWebVitals: (cb: typeof reportCallback) => { reportCallback = cb; } }));
    const path = { current: '/staff/front-desk' };
    vi.doMock('next/navigation', () => ({ usePathname: () => path.current, useParams: () => ({}) }));
    const { WebVitalsReporter } = await import('@/components/observability/WebVitalsReporter');

    const staff = render(<WebVitalsReporter />);
    reportCallback?.({ name: 'LCP', value: 1000, navigationType: 'navigate' });
    document.dispatchEvent(new Event('visibilitychange'));
    window.dispatchEvent(new Event('pagehide'));
    staff.unmount();
    expect(sent).not.toHaveBeenCalled();

    path.current = '/shop';
    const shop = render(<WebVitalsReporter />);
    reportCallback?.({ name: 'LCP', value: 1000, navigationType: 'navigate' });
    window.dispatchEvent(new Event('pagehide'));
    expect(sent).toHaveBeenCalledTimes(1);
    expect(sent.mock.calls[0]?.[0].samples[0]).toMatchObject({ routeTemplate: '/shop', metric: 'LCP' });
    // Load-time metrics reported late keep the route they were measured on; INP follows the current route.
    path.current = '/portal';
    shop.rerender(<WebVitalsReporter />);
    reportCallback?.({ name: 'CLS', value: 0.05, navigationType: 'navigate' });
    reportCallback?.({ name: 'INP', value: 120, navigationType: 'navigate' });
    window.dispatchEvent(new Event('pagehide'));
    const late = sent.mock.calls.at(-1)?.[0].samples as Array<{ metric: string; routeTemplate: string }>;
    expect(late.find((x) => x.metric === 'CLS')?.routeTemplate).toBe('/shop');
    expect(late.find((x) => x.metric === 'INP')?.routeTemplate).toBe('/portal');
    vi.doUnmock('next/web-vitals');
    vi.doUnmock('next/navigation');
    vi.doUnmock('@/lib/api/telemetry');
  });
});
