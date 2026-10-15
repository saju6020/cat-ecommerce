import type { Metadata } from 'next';
import { Suspense } from 'react';
import { Newsreader, Public_Sans } from 'next/font/google';
import { Providers } from './providers';
import { NavigationProgress } from '@/components/common/NavigationProgress';
import { WebVitalsReporter } from '@/components/observability/WebVitalsReporter';
import { ServiceWorkerCleanup } from '@/components/offline/ServiceWorkerCleanup';
import './globals.css';

// HO-CAT-002 design tokens: Newsreader (display, 600) and Public Sans (400/600/700). The
// variable names feed app/globals.css's --font-display / --font-sans fallback chain.
const newsreader = Newsreader({
  subsets: ['latin'],
  weight: ['600'],
  variable: '--font-display-loaded',
  display: 'swap',
});

const publicSans = Public_Sans({
  subsets: ['latin'],
  weight: ['400', '600', '700'],
  variable: '--font-sans-loaded',
  display: 'swap',
});

export const metadata: Metadata = {
  title: 'PurrCare',
  description: 'PurrCare — cat clinic, boarding, pharmacy and shop.',
};

export default function RootLayout({ children }: { children: React.ReactNode }) {
  return (
    <html lang="en" className={`${newsreader.variable} ${publicSans.variable}`}>
      <body>
        {/* Issues.md #1/#6 — a top progress bar for every page change, in every area. */}
        <Suspense fallback={null}>
          <NavigationProgress />
        </Suspense>
        <Providers>{children}</Providers>
        {/* HO-XCT-002 §11.6, ADR-FE-146 — real-user web vitals; sends nothing from /staff/**. */}
        <WebVitalsReporter />
        {/* HO-XCT-002 ADR-FE-140 — removes a stale /staff/ worker in dev and mock mode, on every load. */}
        <ServiceWorkerCleanup />
      </body>
    </html>
  );
}
