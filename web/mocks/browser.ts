import { setupWorker } from 'msw/browser';
import { handlers } from './handlers';

/**
 * Browser-side MSW (HO-CAT-002 §5.1) — the counterpart to `mocks/server.ts`'s Node server, for
 * `npm run dev`. Started from `app/providers.tsx`, only in development and only when
 * `NEXT_PUBLIC_USE_MOCKS=true` (`.env.local.example` — copy it to `.env.local` to enable it; see
 * web/README.md). Needs the worker script `public/mockServiceWorker.js`, generated once by
 * `npx msw init public --save` and committed (already run for this scaffold).
 */
export const worker = setupWorker(...handlers);
