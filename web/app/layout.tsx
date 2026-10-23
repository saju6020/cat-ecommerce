import type { Metadata } from 'next';
import { Suspense } from 'react';
import { Inter } from 'next/font/google';
import { Providers } from './providers';
import { NavigationProgress } from '@/components/common/NavigationProgress';
import { WebVitalsReporter } from '@/components/observability/WebVitalsReporter';
import { ServiceWorkerCleanup } from '@/components/offline/ServiceWorkerCleanup';
import './globals.css';

// Site font (owner, 2026-10-16, after the font preview): Inter for everything - headings, body,
// tables and numbers - as the variable font (weights 100-900, real italic, optical size). It feeds
// both app/globals.css tokens, --font-display and --font-sans. (Was Newsreader + Public Sans.)
const inter = Inter({
  subsets: ['latin'],
  style: ['normal', 'italic'],
  axes: ['opsz'],
  variable: '--font-sans-loaded',
  display: 'swap',
});

export const metadata: Metadata = {
  title: 'PurrCare',
  description: 'PurrCare — cat clinic, boarding, pharmacy and shop.',
};

export default function RootLayout({ children }: { children: React.ReactNode }) {
  return (
    <html lang="en" className={inter.variable}>
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
