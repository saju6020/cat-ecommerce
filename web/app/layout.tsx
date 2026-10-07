import type { Metadata } from 'next';
import { Newsreader, Public_Sans } from 'next/font/google';
import { Providers } from './providers';
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
        <Providers>{children}</Providers>
      </body>
    </html>
  );
}
