/**
 * HO-SHP-002 §8.2, ADR-FE-030 — `msw/node` for server rendering in MOCK MODE ONLY. In
 * `next dev` with `NEXT_PUBLIC_USE_MOCKS=true` the storefront's Server Components fetch the public
 * API through `getPublic`; with no API running, this starts the same handlers the browser worker
 * uses (`mocks/handlers`) on the Node side. A production build, or a dev run against the real API,
 * never loads `msw` here. (The handover names `NEXT_PUBLIC_API_MOCKING`; this repo's existing flag
 * is `NEXT_PUBLIC_USE_MOCKS` — HO-SHP-002 §1 Amended 2026-10-04.)
 *
 * Next.js re-wraps `globalThis.fetch` as each route compiles in `next dev`, and a wrapper created
 * after the first request no longer sits on top of MSW's interceptor. So the intercepting `fetch`
 * is also published as `globalThis.__mswFetch` — `getPublic` uses it when present (mock mode only).
 */
export async function register(): Promise<void> {
  if (
    process.env.NEXT_RUNTIME === 'nodejs' &&
    process.env.NODE_ENV === 'development' &&
    process.env.NEXT_PUBLIC_USE_MOCKS === 'true'
  ) {
    const { server } = await import('./mocks/server');
    server.listen({ onUnhandledRequest: 'bypass' });
    (globalThis as { __mswFetch?: typeof fetch }).__mswFetch = globalThis.fetch;
  }
}
