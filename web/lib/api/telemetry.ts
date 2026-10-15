import { sendTelemetry } from './client';

/** `lib/api/telemetry.ts` — HO-XCT-002 §8.2, ADR-FE-146. `POST /api/telemetry/web-vitals`
 *  (HO-XCT-001 section 12.4): public, CSRF-exempt, rate limited, 4 KB cap. */

export type WebVitalMetricName = 'LCP' | 'INP' | 'CLS' | 'FCP' | 'TTFB';
export type ViewportClass = 'Narrow' | 'Medium' | 'Wide';

export interface WebVitalSample {
  metric: WebVitalMetricName;
  value: number;
  routeTemplate: string;
  navigationType: string;
  effectiveConnectionType: string | null;
  viewportClass: ViewportClass;
}

export interface WebVitalsBatch {
  buildId: string;
  samples: WebVitalSample[];
}

export function sendWebVitals(batch: WebVitalsBatch): Promise<boolean> {
  return sendTelemetry('/api/telemetry/web-vitals', batch);
}
