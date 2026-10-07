'use client';

import * as React from 'react';
import { notFound } from 'next/navigation';

const HarnessContent = React.lazy(() => import('./HarnessContent'));

/**
 * **Test harness — not a product page.** HO-FIL-002 §14 forbids this slice from owning any page,
 * route or layout; every real consumer (HO-SHP-002, HO-EMR-002, HO-BRD-002, HO-RPT-002) composes
 * `components/files/` into its own screens instead. None of those exist yet, and this slice's own
 * required gates (axe + overflow e2e coverage across widths and states) need a real browser
 * rendering these components on an actual route to exercise.
 *
 * **Owner decision, 2026-09-28 (HO-FIL-002 §1 Amended, ARCHITECTURE/SECURITY gate fix):** kept as a
 * development-only test page, to be deleted once a real consuming screen exists. Unreachable in a
 * production build:
 *  - `notFound()` (`next/navigation`) is called **during render**, unconditionally, the instant
 *    `NODE_ENV === 'production'` — not from an effect, so a production request never renders
 *    anything else, server-side or client-side, before the framework's own not-found handling
 *    takes over.
 *  - The actual content — a live `<FileUploader>` wired to the real `POST /api/staff/files`, plus
 *    the mock gate/fixtures modules — lives in `./HarnessContent.tsx`, loaded only via
 *    `React.lazy`'s dynamic `import()`. Since the branch that renders it is never reached in
 *    production (the call above always throws first), that `import()` is never issued and the
 *    module's own chunk is never fetched by a production client.
 *
 * Unlinked from `nav/` — reachable only by a test, or a developer, that knows this exact path.
 */
export default function FileStorageHarnessPage() {
  if (process.env.NODE_ENV === 'production') {
    notFound();
    return null; // unreachable with the real next/navigation notFound() (it always throws); kept
    // so a test that mocks notFound() as a plain spy still renders nothing instead of falling
    // through to the lazy-loaded content below.
  }

  return (
    <React.Suspense fallback={null}>
      <HarnessContent />
    </React.Suspense>
  );
}
