GATE: SECURITY
HANDOVER: HO-ACC-002
COMMITS: 13612b1 c39d491 a18e58d 4453214 381475c a548509 20fad47 ad7120f 7865ee5 aa47843 d04a509 c852ebd 55a4223 8aa2eeb 3dd9114 6e3e46a 88c2591 26fab00 ac8c519 (reviewed f90d465..ac8c519, second pass; 0465912 is a clean master merge)
DATE: 2026-09-26
STATUS: PASS

FINDINGS
- none. All four first-pass findings are closed; the evidence follows.

FIRST-PASS FINDINGS: CLOSURE
- [MEDIUM] method="post": CLOSED (ad7120f).
  - `method="post"` is now on Register, SignIn, ForgotPassword, ResetPassword, ProfileForm, DeleteAccountForm, StaffAccountForm, WalkInCustomerForm and AddressForm.
  - Server-rendered HTML on `next dev` port 3103:
    - `/register`, `/sign-in`, `/staff/sign-in`, `/forgot-password` and `/staff/forgot-password` all render `<form ... method="post">`.
    - `/reset-password`, `/portal/account/{profile,addresses,data/delete}`, `/staff/customers/new` and `/staff/admin/staff-accounts/new` render no form server-side at all. Either the guard is still pending, or the reset token comes from the hash on the client.
  - Pre-hydration probe, in real Chromium with JavaScript disabled (the worst case of "not yet hydrated"):
    - Filled the password and pressed Enter on `/register`, `/sign-in` and `/staff/sign-in`.
    - Each gave a native `POST` to the same path, and the URL stayed clean.
    - `Probe-Secret-789` appears 0 times in the dev-server log.
  - A direct form-encoded POST with the password returns 200. The log shows only `POST /register 200`.
  - The only form left without a method is the DataTable search (`role="search"`). GET is correct there, and it holds no password or CAPTCHA.
- [LOW 1] idle-lock overlay: CLOSED (7865ee5).
  - `DialogContent` has a new `overlayClassName` prop. IdleLock passes `bg-surface-page`, which is opaque in both themes (#faf9f6 / #fbf6ef).
  - `cn`/tailwind-merge resolves `bg-ink/40` + `bg-surface-page` to `bg-surface-page` (checked with node).
  - The overlay is `fixed inset-0 z-50` and portaled last. Nothing in the staff chrome sits above it: the skip link is z-50 only while focused, and the warning bar is z-40 and hidden while locked.
  - A unit test asserts that the opaque class is present and the translucent class is absent. Other dialogs are unchanged.
- [LOW 2] cache on sign-in: CLOSED (c39d491). Both `(auth)/sign-in` and `(auth)/staff/sign-in` call `queryClient.clear()` before `router.push`.
- [LOW 3] cross-subdomain CSRF cookie ("share the token cookie", owner decision): CLOSED (88c2591). Review:
  - Only `pc_csrf` ever gets a Domain:
    - `WriteCsrf` sets `Domain = CsrfCookieDomain`.
    - `WriteSessionCookie` sets no Domain.
    - `ExpireCustomerSession`/`ExpireStaffSession` pass `domain: null`.
    - The middleware's opportunistic reissue goes through `WriteCsrf`, so it gets the same Domain.
    - No raw `Cookies.Delete` is left in `src/`.
    - `CsrfCookieDomainTests` checks both customer and staff sign-in: the session cookies never get `Domain=`, and `pc_csrf` gets it only when configured.
  - Expiry uses the same Domain:
    - `ExpireCsrf` passes `CsrfCookieDomain`.
    - `CustomerProfileController` (account deletion) now uses `ExpireCustomerSession` + `ExpireCsrf` instead of a bare `Cookies.Delete`.
    - Sign-out (AuthController:80, StaffAuthController:72) already used `ExpireCsrf`.
    - A test asserts that the deletion expiry sets `Domain=.example.test` on `pc_csrf` and no Domain on `pc_session`.
  - Cookie flags are unchanged. `pc_csrf` stays `Secure; SameSite=Lax; Path=/` and not HttpOnly. The session cookies stay `HttpOnly; Secure; SameSite=Lax` and host-only.
  - The double-submit check is still bound to the session, including under cookie tossing:
    - `SessionAuthenticationMiddleware` never trusts the cookie value. On an unsafe method it hashes the `X-CSRF-Token` header and compares it with `FixedTimeEquals` against the per-session `CsrfTokenHash` stored on the session row.
    - A sibling host under the parent domain can plant its own `pc_csrf` (for example with a narrower Path, so it sorts first). It cannot know the victim session's token, so it cannot forge a request.
    - At worst the victim's client echoes the wrong value and gets 403 `csrf_token_invalid`. The next safe GET repairs this: the middleware sees a stale cookie and reissues.
    - So cookie tossing gives a nuisance DoS that needs control of a sibling subdomain. It does not give forgery.
  - A compromised sibling can read the real token (non-HttpOnly, domain-scoped). That is the accepted cost of the owner decision.
    - Using the token still needs a cross-origin request with a custom header, which triggers a CORS preflight.
    - `HostPipelineExtensions` uses an exact-origin `WithOrigins` allowlist, empty by default, with no wildcard. The preflight therefore fails unless that sibling is itself allowlisted.
    - The HttpOnly session cookies are not exposed.
  - A malformed domain value cannot widen the scope beyond what was chosen:
    - Browsers (RFC 6265 §5.3 plus the public-suffix list) reject a Domain that does not domain-match the API host, or that is a public suffix.
    - The widest possible scope is therefore the registrable domain the owner already accepted.
    - An unrelated value makes the browser drop the cookie, which fails closed as a 403.
    - An empty value means no Domain attribute, so the cookie is host-only.
  - Optional hardening (not a finding): `AccountOptions.CsrfCookieDomain` has no validation. A `ValidateOnStart` rule (no `;`, `,` or whitespace, and it must be a dot-suffix of the API host) would catch operator typos at start-up instead of as production 403s.
- `app/page.tsx`: it no longer reads cookies server-side. It asks the API from the client (`useStaffSession`/`useCustomerSession`) and fails closed:
  - It redirects somewhere other than `/sign-in` only on resolved session data, or on a staff `session_idle_timeout`.
  - Errors and network failures go to `/sign-in`.
  - The targets are fixed, guarded paths, so a wrong answer here grants nothing. The destination's guard makes the real decision.

ALSO REVIEWED (no finding)
- `StaffCustomerViewModel.Cats`, and `RecordStatus`/`RegistrationChannel` on duplicate members:
  - Who can call them is unchanged. `GET /api/staff/customers/{id}` needs `[RequireAction(Customers.View)]` (Reception/Nurse/Vet/Administrator). The duplicates endpoint needs `[RequireAction(Customers.Merge)]`.
  - The new data is cat id, name and an isDeceased flag for cats with `OwnerId == customerId`. This is the same shape Q-I1 already exposed.
  - Deceased cats are included on purpose (HO-ACC-001 A27/U-I4: they move on merge).
  - Cats carry no branch, so there is no other-branch leak.
  - RecordStatus/RegistrationChannel already existed on Q-H1. They are derived the same way here (`PasswordCredential != null` → display name), so no hash or credential data is exposed.
- Mock password "hashing" (`mocks/handlers/areas/accounts.ts`):
  - It is a 32-bit string hash, and plaintext is no longer written to the mock's sessionStorage.
  - It is mock-only: `app/providers.tsx` loads `@/mocks/browser` by dynamic import only when `NODE_ENV==='development' && NEXT_PUBLIC_USE_MOCKS==='true'`.
  - No file in app/, components/ or lib/ imports from `mocks/`.
- `nextPath` boundary matching:
  - A path matches only as the exact prefix, `prefix/...` or `prefix?...`, and anything starting with `//` is rejected.
  - Allowed prefixes start with a fixed `/portal/`, `/book` or `/staff/` segment, so the result always resolves same-origin.
  - The first-pass browser probes (`..//evil`, `..\evil`) still apply.
- UnsavedChangesGuard replay: `router.push(href)` replays only the `href` of an `<a>` the user actually clicked in this document, and only for plain left clicks. That is the same navigation the browser would have done natively, so it adds no redirect capability. The guard takes no URL from the query string or from storage.
- Backend tests:
  - Setup: a scratch `git archive ac8c519` (no git-ignored appsettings), a fresh DB `catecommerce_acc2sec` (`dotnet-ef database update`, all migrations applied), and `CATECOMMERCE_TEST_DB` set to the full connection string.
  - Results: Domain 161/161, Application 66/66, Infrastructure 28/28, Integration 145/145 (including the 4 `CsrfCookieDomainTests`).
  - The DB was dropped and the scratch folder deleted afterwards.
- Frontend: `tsc --noEmit` OK, `eslint .` OK, `vitest` 55 files / 186 tests passed. The dev server on 3103 was stopped.

SCOPE CHECK
- files in allowed scope: 111/111
  - web/**
  - docs/handover/HO-ACC-00{1,2}.md
  - the backend files authorised for gate item 7 / CR MEDIUM 2: SessionCookieWriter.cs, AccountOptions.cs, appsettings.json, CustomerProfileController.cs, the GetStaffCustomer/GetDuplicateCustomerGroups handlers and view models, and the integration-test factory, base and new tests
- files outside allowed scope: none

---- FIRST PASS (superseded, kept for the record) ----
GATE: SECURITY
HANDOVER: HO-ACC-002
COMMITS: 8768736 454b756 95e09bc 624c018 60f78af 9ddb094 a316d9a 04f70ee 3dafb59 93acb8a 9d2a8dc 255f6b0 5e1f120 (reviewed master 7abc9e4...5e1f120, first pass)
DATE: 2026-09-26
STATUS: FAIL

FINDINGS
- [MEDIUM] web/components/auth/RegisterForm.tsx:160, web/components/auth/SignInForm.tsx (the `<form>`), web/components/auth/ForgotPasswordForm.tsx:60 — The rule is "passwords never in a URL or log" (focus list, A26). What the code does: these forms are `<form onSubmit={handleSubmit(...)}>` with no `method`, and `register()` gives every input a `name`. They are server-rendered into the initial HTML. Confirmed on the dev server (port 3103): `/register` renders `<form>` with `name="password"` and `name="captchaAnswer"`, and in dev so do `/sign-in` and `/staff/sign-in`. If the user presses Enter or Submit before hydration finishes (slow mobile network, or a JS chunk that fails to load), the browser does a native GET to the current URL, for example `/register?fullName=..&mobileNumber=..&password=<plaintext>&captchaAnswer=..`. The password then lands in browser history and URL autocomplete, and in the web server's request log. Probe: `GET /register?...&password=Probe-Secret-123` was written verbatim to the `next dev` log. In production the same happens in any reverse-proxy, CDN or access log. The production static build probably client-renders the sign-in pages because of the `useSearchParams` Suspense bail-out, but `/register` has no such boundary and is always affected. Fix: put `method="post"` on every form that holds a password, CAPTCHA answer or PII (Register, SignIn, ForgotPassword, ResetPassword, ProfileForm, DeleteAccountForm, StaffAccountForm, WalkInCustomerForm). Optionally also keep the submit button disabled until mounted. Add a unit test that asserts `form.method === 'post'`.
- [LOW] web/components/staff/IdleLock.tsx:163 with web/components/ui/dialog.tsx:27 — The idle-lock dialog uses the shared overlay, `bg-ink/40` (40% tint). The locked staff page stays mounted (by design, ADR-FE-010) and stays readable behind the lock. On an unattended reception terminal, anyone walking past can still read the customer's name, phone numbers, addresses and cat medical data on screen after the session has timed out. Keeping the page mounted is an owner decision. Keeping it visible is not required by §11.9. Fix: give the lock's `DialogContent`/overlay an opaque background (or a strong `backdrop-blur` plus an opaque fill). This is for the idle lock only. The DOM and form state stay intact.
- [LOW] web/app/(auth)/sign-in/page.tsx:35, web/app/(auth)/staff/sign-in/page.tsx:32 — After a successful stand-alone sign-in, only the `['session', …]` query is removed. The rest of the cache is kept. Sign-out, delete, person-change-in-lock and `session_invalid` all call `queryClient.clear()`. Sign-in does not. If person B signs in on a tab where person A is still signed in (they go to `/staff/sign-in` directly), B is shown A's cached query data until it goes stale (30 s), including admin-only lists that B's own permissions would refuse. Impact is small: A's session was open on that browser anyway. But this is a "person change" that §827 says must clear the cache. Fix: call `queryClient.clear()` (not only the session key) in both `onSuccess` handlers before `router.push`.
- [LOW] web/lib/api/client.ts:188 (and web/app/page.tsx:39-45) — Deployment note. It is not exploitable and it fails closed. `pc_csrf` and `pc_session`/`pc_staff_session` are host-only cookies with no `Domain` set (`SessionCookieWriter.cs`). In the owner's production topology (`www.example.com` calling `api.example.com`), `document.cookie` on `www` cannot see `pc_csrf`. So no `X-CSRF-Token` is sent and every authenticated POST/PUT/DELETE will get `403 csrf_token_invalid`. The server-side `cookies().has('pc_session')` in `app/page.tsx` will also never see the session. This works in dev only because cookies ignore the port on `localhost`. There are two fixes. One: the API returns the CSRF token in a response body (`SignInResult` already carries `CsrfToken`) and the client holds it in memory. Two: set `Domain=.example.com` on `pc_csrf` only, never on the HttpOnly session cookies. The integrator/owner must pick one deliberately; do not widen the session cookies' scope.

VERIFIED (no finding)
- No token, password or CAPTCHA answer is written to localStorage or sessionStorage in app code. The only sessionStorage use is the MSW mock DB in `mocks/handlers/areas/accounts.ts`, which runs in dev mocks only.
- No `dangerouslySetInnerHTML`/`innerHTML`/`eval`. All user content renders as React text. Dynamic hrefs are app-built paths with ids encoded via `encodeURIComponent` in `lib/api/*`.
- CSRF: `X-CSRF-Token` is added on every POST/PUT/PATCH/DELETE, after the caller's headers so a caller cannot override it. `credentials: 'include'` is only ever sent to `NEXT_PUBLIC_API_BASE_URL`.
- `?next=` guard (`lib/validation/nextPath.ts`): no off-site redirect. Tested in a browser with `/portal/..//evil.example`, `/portal/../\evil.example` and `/book/..//evil.example`. All stayed on the same origin. `..` can leave the area prefix, but only for a same-origin path that is still guarded, so there is no impact.
- Reset token: read from `location.hash` once, stripped with `history.replaceState`, and kept in component state only. The page emits `<meta name="referrer" content="no-referrer">`.
- The password fields use `autocomplete` values `current-password`/`new-password`, and the identifier uses `username`. Failed sign-in shows one generic message, and the password is cleared.
- Session guards fail closed: when pending, children are not rendered; `null` or an error redirects. A 401 `session_invalid` clears the whole cache and routes to sign-in. The staff guard is authorised by `GET /api/staff/session` on the server, so a customer cookie cannot render staff UI.
- MSW starts only when `NODE_ENV === 'development' && NEXT_PUBLIC_USE_MOCKS === 'true'`, via a dynamic import, so it is dead code in a production build. There is no secret in `NEXT_PUBLIC_*`, and the CAPTCHA challenge carries no answer.
- The personal-data export goes to a Blob and the object URL is revoked; it is not put in the query cache.
- Dependencies: `package.json`/`package-lock.json` are unchanged by this branch. `npm audit --omit=dev` reports the postcss advisory that already exists (through `next`, build-time only), which is out of scope here.
- Static checks: `tsc --noEmit` OK, `eslint .` OK, `vitest` 53 files / 163 tests passed.
- `next.config.ts` has no security headers (CSP/frame-ancestors). This predates the branch and is unchanged by it.

SCOPE CHECK
- files in allowed scope: 116/116 (web/** plus docs/handover/HO-ACC-002.md; the shared-file edits to app/page.tsx, components/common/DataTable.tsx, portal-header.tsx, staff-shell.tsx and lib/api/client.ts are the integrator-authorised or listed modifications in §1 Amended / the file list)
- files outside allowed scope: none
