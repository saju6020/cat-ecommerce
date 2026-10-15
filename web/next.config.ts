import type { NextConfig } from 'next';

const nextConfig: NextConfig = {
  reactStrictMode: true,
  // HO-XCT-002 §12.2 — the offline service worker must always be revalidated so a new build's
  // worker replaces the old one. No `Service-Worker-Allowed` header: its scope stays `/staff/`
  // (ADR-FE-139). `/_next/static/*` keeps Next's own immutable caching.
  async headers() {
    return [
      {
        source: '/staff/sw.js',
        headers: [
          { key: 'Cache-Control', value: 'no-cache' },
          { key: 'Content-Type', value: 'application/javascript; charset=utf-8' },
        ],
      },
    ];
  },
};

export default nextConfig;
