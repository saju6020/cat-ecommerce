GATE: SECURITY
HANDOVER: HO-CAT-002
COMMITS: 2fcd392 0f6bc0e b1f380c c6cb154 a408ab1 40ea4a8 70ae99a 94a626b 824b1fb bee5be8 542477e c3a076a 1a4cac4 fdef977 ad776b0 09a01fd 820f5dc 0c5a59b 454f6f9 bb9fef1 47d8f10 06121d9
DATE: 2026-09-25
STATUS: PASS

This is the first run of this gate. It reviewed the net diff `master..06121d9` (96 files) against
HO-CAT-002 §3.1, §4.2, §11.2, §11.6–§11.8, §12.2–§12.5, §20.3–§20.4, and web/README.md. The
frontend was judged on its own behaviour against MSW mocks, because the HO-CAT-001 backend does
not exist yet. This gate did not start a dev server or run a build.

Nothing found is exploitable at a level that blocks the handover. The three LOW findings are
defence-in-depth items or forward-looking items.

FINDINGS
- [LOW] web/lib/api/cats.ts:189, :228, :247, :290, :312, :331, :345, :366, :384, :392, :412,
  :419, :441, :459, :478, :491, :495 — the route param `catId` is put into API paths without
  `encodeURIComponent` or a format check. This is client-side path traversal (CSPT).
  - Next 15.5 URL-decodes dynamic params (`route-matcher.js` calls `decodeURIComponent`).
  - So a crafted link such as `/staff/cats/..%2F..%2Fapi%2F<x>` makes `apiFetch` send requests
    to `/api/<x>...` on the API origin. Those requests carry `credentials: 'include'` and, on
    mutations, the `X-CSRF-Token` header.
  - That is the standard CSPT-to-CSRF setup. Exploitability here is low, for three reasons:
    - Every `[catId]` page and `CatProfileDataShell` renders forms only after
      `GET /api/cats/${catId}` returns a cat-shaped body.
    - The mutation path is always that same (normalised) GET path plus a fixed suffix.
    - It needs the victim to click through a confirmation dialog.
  - No concrete target endpoint exists today.
  - Fix, in the one place that covers every call: in `lib/api/cats.ts`, wrap each `catId` in
    `encodeURIComponent(catId)`. Better still, reject any `catId` that is not a GUID at the
    `[catId]/layout.tsx` boundary, via `notFound()`.
- [LOW] web/app/(staff)/staff/cats/[catId]/photos/page.tsx:34 — `storageKey` is minted by the
  browser as `dev/${crypto.randomUUID()}-${selectedFile.name}`.
  - It embeds the user-controlled file name and runs in production builds as well as dev.
  - This follows §11.6/§20.4 as written, so it is not a defect in this slice.
  - Recorded so the storage slice does not inherit it: when object storage lands, the server
    must mint the object key itself and never trust a client-supplied key or file name.
    Otherwise it risks object-key injection or overwriting another cat's object.
  - Also note: the client-side check uses `File.type`, which the browser derives from the file
    extension, not the content. That is acceptable only because §11.6 calls the check a
    courtesy. Server-side content sniffing and virus scanning (NFR-SEC-5) remain the guarantee.
- [LOW] web/lib/api/client.ts:121 — `...headers` is spread after `identityHeaders`, so any caller
  can overwrite `X-CSRF-Token` or `X-Branch-Id`.
  - No caller does this today, so it is not exploitable.
  - Fix: spread `identityHeaders` last, so the client's own identity headers cannot be
    overridden by accident.

VERIFIED CLEAN (requested checks)
- XSS sinks:
  - There is no `dangerouslySetInnerHTML`, `innerHTML`, `eval` or `new Function` in
    `app/`, `components/` or `lib/`.
  - All server strings are rendered as React text. This covers notes, allergies, handling note,
    photo `fileName`, and validation `errorMessage` joined by `describeApiError`.
  - The only `<img src>` (`CageCard.tsx:42`) and the PDF `<a href>`s are built by `getApiUrl`
    from the build-time `NEXT_PUBLIC_API_BASE_URL` plus a fixed path, never from response data.
  - No photo URL is rendered at all (the header uses a placeholder icon).
- Open redirects:
  - Every `router.push` goes to a literal path, or to `/staff/cats/${result.catId}` from the
    API's own create response.
  - `app/page.tsx` redirects to the first static nav-registry href.
  - No `returnUrl`/`next` parameter is read, and there is no `window.open` or `location.*`.
- CSRF and cookies:
  - `pc_csrf` is read only on POST/PUT/PATCH/DELETE (client.ts:101-104).
  - The client never reads or sets the HttpOnly session cookies.
  - The CSRF token is sent only to the build-time API base URL.
  - `X-Branch-Id` is a documented no-op (`lib/branch.ts` returns null).
  - The integrator reports unit tests that cover these headers (tests/unit/lib/api-client*.test.ts).
    This gate did not run them.
- Secrets:
  - The diff contains no hardcoded secrets or tokens.
  - `.env.local` is git-ignored (`.gitignore:34`). Only `.env.local.example` is tracked, and it
    holds no secret.
  - The diff adds no `console.*` calls, so nothing is logged.
- Error disclosure (NFR-USA-4):
  - `errorCode` is never rendered.
  - 5xx and network failures show fixed messages of our own, never the server's.
- Internal IDs and PII:
  - The cage card no longer prints the owner GUID. It shows "Owner on record" (09a01fd).
  - The staff booking links (`[catId]/page.tsx:62`, `vaccination-status/page.tsx:30`,
    `vaccinations/page.tsx:79`) put the owner GUID in a staff-only URL query string. §1's
    booking-route contract specifies this. It is an opaque ID, not PII. This is not a finding.
- MSW:
  - The worker starts only when `NODE_ENV === 'development' && NEXT_PUBLIC_USE_MOCKS === 'true'`
    (`app/providers.tsx:29`), and `@/mocks/browser` is imported dynamically there.
  - No other app code imports `mocks/`.
  - `public/mockServiceWorker.js` already exists on master. It is inert unless registered.
- Dependencies:
  - The branch does not change `web/package.json` or `package-lock.json`.
  - `npm audit --omit=dev` ran and reports 1 high and 1 moderate advisory, both in the PostCSS
    copy bundled inside `next`.
  - These advisories are **pre-existing on master** and not introduced by this handover.
  - The affected PostCSS runs at build time and processes only first-party CSS.
  - The only fix offered is `next@16` (breaking). It belongs to a separate dependency-upgrade
    item, not to this gate.

CONDITIONS RECORDED, NOT FINDINGS (HO-CAT-002 §4.2 / §20.3 — by design)
- This UI has no authentication, roles or access control:
  - `lib/identity.ts` is a hardcoded staff identity.
  - The `(portal)` routes render as that staff user.
  - The portal profile shows the high-risk badge and handling note, as the staff view does.
- Every client-side restriction is UX only:
  - disabled edit controls on a deceased cat;
  - the portal showing fewer tabs;
  - photo type/size limits;
  - required reasons.
  HO-CAT-001 and the Accounts slice must enforce each of these server-side. This includes
  object-level authorization on every `/api/cats/{catId}/*` endpoint, which HO-CAT-001 §20.3
  records as a query filter only.
- US-CAT-08 AC2 (the previous owner loses access) cannot be verified from the UI.
- The CORS requirement is carried over from ARCHITECTURE for the §5.1 re-run. With
  `credentials: 'include'`, the API must echo a specific origin with
  `Access-Control-Allow-Credentials: true`. It must not use `AllowAll`/`*`.

SCOPE CHECK
- files in allowed scope: 97/97. That is the net diff `master..06121d9`:
  - 95 paths under `web/**`;
  - `docs/gates/HO-CAT-002-ARCHITECTURE.md`;
  - this file.
- files outside allowed scope: none
