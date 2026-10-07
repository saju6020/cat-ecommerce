GATE: SECURITY
HANDOVER: HO-FIL-002
COMMITS: 08e873e..5c0d8a3 (head 5c0d8a3, branch ho/FIL-002; second pass, focus c59b4a5..5c0d8a3: db613bf, 4bb04e2, 2d5a6ee, 53606a1, c962934, f981e57, 3934ce5)
DATE: 2026-09-28
STATUS: PASS

Second pass. It replaces the first SECURITY pass, which was a PASS at c59b4a5 with 2 MEDIUM and 2 LOW.

There are no BLOCKER or HIGH findings. All four earlier findings are fixed (details below). There is one new LOW, a residual edge of the refresh-once fix. Under the owner rule (2026-09-27) it does not block the gate. It belongs in docs/backlog/FIL.md, which this gate has not edited.

FINDINGS
- [LOW] web/components/files/FileThumb.tsx:61, web/components/files/MediaViewer.tsx:210 — The refresh-once guard is a `useRef`, so it lives only as long as one component instance.
  - The prop-update case is now bounded. The test "host-driven prop update ... does not refresh again" covers it.
  - A host that remounts the thumb or viewer on refetch still resets the guard. Two ways this happens:
    - keying it on `url`;
    - swapping the list for a skeleton while `isFetching`, then rendering it again.
  - In that case a persistently failing object (corrupt body, 404, or 429 from the FileServe per-IP limit) loops again: each cycle does one host read plus one content GET.
  - FileList itself keys on `fileId` (FileList.tsx:59), so the slice's own composition is safe. The risk is only in future consumer screens.
  - Fix: state the rule in ADR-FE-154 / the `onRefresh` doc comment: "do not remount on refetch; key on fileId". Optionally, move the attempted-fileId set to a module-level or context-scoped `Set` that survives a remount.

PREVIOUS FINDINGS — VERIFIED FIXED
- [was MEDIUM] Harness reachable in production. Fixed in db613bf.
  - app/dev/file-storage-harness/page.tsx:31-36 calls `notFound()` during render when `NODE_ENV === 'production'`. NODE_ENV is inlined at build time, and the check runs for both SSR and the client render, so the route is a 404 in production.
  - The live `<FileUploader>`, the `window.__fileUploadGates` / `__fileStorageHarness` hooks and the mock `gate.ts` / `fixtures.ts` imports all moved to HarnessContent.tsx. That file is loaded only via `React.lazy(() => import(...))`, behind the throwing branch.
  - Webpack will probably still emit HarnessContent as a separate static chunk, because `import()` is resolved at build time whether or not it is reachable. Nothing ever requests or runs that chunk. It contains no secrets: only the public `NEXT_PUBLIC_API_BASE_URL` and fixture GUIDs, and no `msw` import (gate.ts and fixtures.ts import nothing from msw). Not a finding.
  - tests/unit/files/file-storage-harness-page.test.tsx covers both branches.
- [was MEDIUM] Unbounded refresh when the host re-keys `url`. Fixed in 2d5a6ee.
  - The guard is now keyed on `fileId`, and the prop-sync effect (FileThumb.tsx:63-70, MediaViewer.tsx:213-218) never resets it.
  - Component tests prove that a second failing URL for the same fileId does not call `onRefresh` again.
  - The proactive past-`urlExpiresOn` trigger goes through the same guard, so it adds at most one attempt per fileId. An invalid date gives NaN, which never triggers.
- [was LOW] Unknown server `errorCode` shown verbatim. Fixed in 53606a1.
  - lib/api/files.ts:67-101 allow-lists codes against `KNOWN_VIOLATION_CODES`. Anything else becomes `'UnknownViolation'`, which fileErrors.ts:73-74 maps to a fixed sentence.
  - The `default` branch is now a `never` exhaustiveness check.
  - The server `errorMessage` is still carried only on `Error.message` and is never rendered.
- [was LOW] iframe / img / anchor had no referrer policy. Fixed in 2d5a6ee.
  - `referrerPolicy="no-referrer"` is now on the `<img>` (FileThumb.tsx:130, MediaViewer.tsx:285), the `<iframe>` (MediaViewer.tsx:276) and the download `<a>` (MediaViewer.tsx:311).
  - `<video>` has no such attribute in the HTML spec. The content route's own `Referrer-Policy: no-referrer` header still applies.
  - No `sandbox` attribute, correctly, because Chromium will not render a PDF in a sandboxed frame. The server's CSP `sandbox` covers that case.

CHECKED, NO FINDING (new code c59b4a5..HEAD)
- Session handling (client.ts:165-167, files.ts:261-274):
  - `notifySessionError` is a thin pass-through, and `sessionErrorHandler` itself stays module-private.
  - The upload 401 body forwarded to the handler is narrowed to exactly `session_invalid` or `session_idle_timeout`, defaulting to `session_invalid`. A malformed or hostile body cannot inject other shapes into the IdleLock / CustomerSessionGuard handlers.
  - `apiFetch` behaviour is unchanged. It still skips `invalid_credentials` and `skipSessionErrorHandling`.
- CSRF (files.ts:280-299):
  - A 403 `csrf_token_invalid` becomes `FileUploadForbiddenError`.
  - A 403 carrying a ValidationResponse (`FilePurposeNotAllowedForCaller`) keeps its mapped sentence.
  - Anything else on a 403 is Forbidden.
  - The CSRF echo and `withCredentials` are unchanged, and they match `apiFetch`.
- SVG (lib/files/fileRules.ts:524-526): `image/svg+xml` is excluded from inline rendering. A parameterised or mixed-case variant would still reach `<img>`, but that is harmless:
  - SVG in an `<img>` context never runs script;
  - no purpose's allow-list admits SVG;
  - the content route serves `CSP sandbox` plus `nosniff`.
- XSS and URL schemes:
  - There is still no `dangerouslySetInnerHTML` / `innerHTML` / `console.*` / web storage anywhere in the slice.
  - `originalFileName` and `fileName` are rendered only as React text or attributes.
  - `url` is server-issued (the client never constructs it) and appears only in `src` / `href` attributes. React 19.3 blocks `javascript:` URLs in `href`.
  - No redirects are built from response data.
- Error text: every user-visible sentence comes from the fileErrors.ts table. Session and CSRF refusals show only the generic "did not finish uploading" row.
- Dependencies: none added. The package-lock churn was reverted (c962934).

SCOPE CHECK
- files in allowed scope: 14/33 (web/components/files/* 4, web/mocks/handlers/files/* 4, web/docs/decisions.md, plus tests, which are treated as accompanying the slice)
- files outside allowed scope:
  - Recorded in HO-FIL-002 §1 Amended:
    - web/lib/api/files.ts
    - web/lib/hooks/useFileUpload.ts
    - web/lib/files/fileRules.ts
    - web/lib/files/fileErrors.ts
    - web/lib/api/client.ts (the `notifySessionError` export)
    - web/mocks/handlers/areas/file-storage.ts
  - Owner-approved on 2026-09-28 as dev-only, to be deleted once a real consumer exists:
    - web/app/dev/file-storage-harness/page.tsx
    - web/app/dev/file-storage-harness/HarnessContent.tsx
  - Documentation:
    - docs/handover/HO-FIL-002.md (Amended rows only)
    - docs/gates/*
    - docs/backlog/FIL.md
