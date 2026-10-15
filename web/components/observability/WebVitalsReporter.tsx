'use client';

import { useEffect, useRef } from 'react';
import { useParams, usePathname } from 'next/navigation';
import { useReportWebVitals } from 'next/web-vitals';
import { sendWebVitals, type WebVitalSample } from '@/lib/api/telemetry';
import {
  buildSample,
  currentBuildId,
  isMeasuredPath,
  routeTemplateOf,
  toBatches,
} from '@/lib/observability/web-vitals';

interface NetworkInformation {
  effectiveType?: string;
}

/**
 * `components/observability/WebVitalsReporter.tsx` — HO-XCT-002 §11.6, ADR-FE-146. Real-user Core
 * Web Vitals from Next.js's own `useReportWebVitals` (no `web-vitals` package). Samples are
 * buffered and flushed when the page is hidden, on pagehide and on route change, through
 * `sendTelemetry()`: no cookie, no CSRF header, `keepalive`. It reports nothing from `/staff/**`
 * (including the offline board): the requirement is the storefront, and staff pages are not
 * measured. Renders nothing.
 */
export function WebVitalsReporter() {
  const pathname = usePathname();
  const params = useParams();
  const context = useRef({ pathname, params });
  // The route the page was LOADED on: load-time metrics (LCP, CLS, FCP, TTFB) are reported late, often
  // after a soft navigation or at pagehide, and belong to this route, not to the current one.
  const loadContext = useRef({ pathname, params });
  const buffer = useRef<WebVitalSample[]>([]);

  useEffect(() => {
    context.current = { pathname, params };
  }, [pathname, params]);

  function flush() {
    if (buffer.current.length === 0) return;
    const samples = buffer.current;
    buffer.current = [];
    for (const batch of toBatches(samples, currentBuildId())) void sendWebVitals(batch);
  }

  useReportWebVitals((metric) => {
    const { pathname: path, params: routeParams } = (
      metric.name === 'INP' ? context.current : loadContext.current
    );
    if (!isMeasuredPath(path)) return;
    const connection = (navigator as Navigator & { connection?: NetworkInformation }).connection;
    const sample = buildSample(
      { name: metric.name, value: metric.value, navigationType: metric.navigationType },
      {
        routeTemplate: routeTemplateOf(path, routeParams as Record<string, string | string[]>),
        viewportWidth: window.innerWidth,
        effectiveConnectionType: connection?.effectiveType ?? null,
      },
    );
    if (sample) buffer.current.push(sample);
  });

  // Route change: send what was measured on the page being left.
  useEffect(() => flush, [pathname]);

  useEffect(() => {
    const onHidden = () => {
      if (document.visibilityState === 'hidden') flush();
    };
    document.addEventListener('visibilitychange', onHidden);
    window.addEventListener('pagehide', flush);
    return () => {
      document.removeEventListener('visibilitychange', onHidden);
      window.removeEventListener('pagehide', flush);
    };
  }, []);

  return null;
}
