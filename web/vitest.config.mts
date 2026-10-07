import { defineConfig } from 'vitest/config';
import react from '@vitejs/plugin-react';
import path from 'node:path';

export default defineConfig({
  plugins: [react()],
  resolve: {
    alias: {
      '@': path.resolve(import.meta.dirname, '.'),
    },
  },
  test: {
    environment: 'jsdom',
    setupFiles: ['./tests/setup.ts'],
    include: ['**/*.test.{ts,tsx}'],
    exclude: ['node_modules', '.next', 'tests/e2e/**'],
    css: true,
    env: {
      // lib/api/client.ts's resolveBaseUrl() throws when this is unset (HO-CAT-002 §11.2). Real
      // requests never leave the process — MSW's Node server (tests/setup.ts) intercepts every
      // one — so the value only has to be a well-formed URL, not a reachable one. Added by
      // HO-CAT-002 (Cat Profiles), the first area whose tests exercise `apiFetch` for real
      // rather than constructing an `ApiValidationError` directly.
      NEXT_PUBLIC_API_BASE_URL: 'https://localhost:7099',
    },
  },
});
