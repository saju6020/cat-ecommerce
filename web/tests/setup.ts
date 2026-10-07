import '@testing-library/jest-dom/vitest';
import { cleanup } from '@testing-library/react';
import { afterAll, afterEach, beforeAll } from 'vitest';
import { server } from '../mocks/server';

// Shared Vitest bootstrap — jest-dom matchers plus the MSW server (HO-CAT-002 §12.5). Every
// area's component tests import nothing extra for this; `npm run test` picks this file up via
// vitest.config.ts's `setupFiles`.
//
// `vitest.config.mts` does not set `test.globals: true` (HO-CAT-002 keeps imports explicit —
// `describe`/`it`/`expect` come from 'vitest', not ambient globals), which means Testing
// Library's automatic per-test `cleanup()` (it detects a *global* afterEach) never fires. Every
// area's tests would otherwise leak DOM between `it()` blocks in the same file. Call it
// explicitly here, once, so no area needs to import `cleanup` itself.
beforeAll(() => server.listen({ onUnhandledRequest: 'error' }));
afterEach(() => {
  server.resetHandlers();
  cleanup();
});
afterAll(() => server.close());
