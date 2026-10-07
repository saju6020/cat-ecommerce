GATE: SECURITY
HANDOVER: HO-CAT-002-ADDCAT
COMMITS: 84b5c90 308b3ac 283ecb8 e715f06 ceb0786 (first pass, range 578dc79...ceb0786); 3796911 2a58de5 1cfffe9 (second pass, range 27bc369..1cfffe9)
DATE: 2026-10-06
STATUS: PASS (second pass, 2026-10-06; first pass FAIL)

FINDINGS
- [HIGH] web/app/(portal)/portal/cats/new/page.tsx:40-42 — open redirect: the `?next=` guard can be bypassed. `sanitizeNextPath` checks the raw string only (`/book`, `/book/…` or `/book?…`, not starting with `//`). The page then normalizes it with `new URL(next, 'http://local')` and pushes `${url.pathname}${url.search}`. WHATWG URL parsing resolves dot-segments and treats `\` as `/`, so a value that passes the check comes out as a protocol-relative path. Reproduced in Node:
  `/book/..//evil` -> `//evil?catId=…`
  `/book/%2e%2e//evil` -> `//evil?catId=…`
  `/book/.%2e//evil` -> `//evil?catId=…`
  `/book/./..//evil` -> `//evil?catId=…`
  `/book/..\/evil` and `/book/..\\evil` -> `//evil?catId=…`
  `router.push('//evil?…')` in Next 15.5 resolves against `location.href` to origin `https://evil`, which `isExternalURL` sends to a full-page navigation (app-router-instance.js dispatchNavigateAction -> navigate-reducer handleExternalUrl). Exploit: an attacker sends a customer `https://<clinic>/portal/cats/new?next=/book/..//evil.example/login`. The customer signs in, adds a cat, sees the clinic's "Cat added" toast, and lands on the attacker's page, for example a fake "session expired, sign in again" phishing page. Less severe variants: `/book/../portal/account` escapes the `/book` allow-list to any same-origin path. This slice introduced the bug: sign-in pushes the sanitized string as-is, and the browser keeps that same-origin.
  Fix: validate the string that is actually pushed, not the input. Build `dest = url.pathname + url.search` and push it only if `sanitizeNextPath(dest, '/book', '')` still accepts it (this rejects `//evil` and `/evil`); otherwise fall back to `/portal/cats/{catId}`. Better still, resolve against `window.location.origin` and require both `u.origin === location.origin` and an allowed-prefix match on `u.pathname`. Add unit cases for each payload above.
- [LOW] web/lib/validation/nextPath.ts:18 — the shared guard rejects only a leading `//`. It accepts dot-segments (`..`, `%2e`), backslashes and encoded slashes after the prefix, so it does not actually hold "limited to the prefix" for any caller that normalizes later. No current caller other than the one above can be exploited (sign-in and cats/[id] push the raw string, and the browser keeps that same-origin). Hardening for the backlog: reject `\`, any `.`/`..` segment (decoded) and `%2f`/`%5c`, or normalize with `new URL` and re-check origin and prefix inside the helper.

VERIFIED (no finding)
- Ownership (F19), on master efc107e, src/Api/CatEcommerce.Api/Controllers/CatsController.cs:45: `var ownerId = caller.Kind == ResolvedCallerKind.Customer ? caller.AccountId : request.OwnerId;`. A customer caller's request OwnerId is always discarded, so a crafted POST cannot register a cat for another owner. The empty GUID the portal sends is never used for a customer. The slice changes no backend code.
- Authorization: RegisterCat carries `[RequireAction(Actions.Cats.Register, OwnerScope.CatCollection)]`. `Cats.Register` is in CustomerActions.cs:17 plus the staff roles (Reception/Nurse/Vet/Administrator). /portal/cats/new sits under app/(portal)/layout.tsx, which wraps every page in CustomerSessionGuard. The customer form hides the staff-only "Owner customer ID" field (`audience === 'staff'`), and no staff-only route or action is linked.
- catId merge: `url.searchParams.set('catId', result.catId)` replaces any attacker-supplied catId. The value comes from the server response and URLSearchParams encodes it, so no parameter injection. Booking would also re-check the cat against the caller's own list.
- `javascript:` / `//evil` / `/\evil` / absolute URLs fall back correctly (rejected by the prefix check before normalization).
- Rendering: the cat name appears only as React text children and toast `description` string props. There is no dangerouslySetInnerHTML in the changed files, components/cat, components/booking or the toast components.
- Mocks: the MSW worker starts only when `NODE_ENV === 'development' && NEXT_PUBLIC_USE_MOCKS === 'true'` (app/providers.tsx:112). The handler change (empty-GUID -> fixture customer, 100-char name check) is mock-only and never ships to production.
- Secrets: none added. Dependencies: none added.

SCOPE CHECK
- files in allowed scope: 10/10
- files outside allowed scope: none
- note: the worktree had an uncommitted change to web/public/mockServiceWorker.js that is not part of the reviewed commits; the gate left it untouched.

SECOND PASS — 2026-10-06 (delta 27bc369..1cfffe9: 3796911, 2a58de5, 1cfffe9)
STATUS: PASS

- [HIGH, first pass] open redirect via `?next=` normalization: RESOLVED in 3796911.
  The new module web/lib/validation/bookingReturnPath.ts (`safeBookingPath` / `resolveAddCatReturn`) checks the raw string and rejects backslashes. After parsing it then requires all of the following:
  - the dummy origin;
  - a pathname of `/book` or `/book/…`;
  - no leading `//` and no `\` in the final pathname+search;
  - `sanitizeNextPath` accepting the final string as well.
  page.tsx pushes `resolveAddCatReturn(next, catId)`, and Cancel uses the same safe path (or `/portal/cats`).
  Re-fuzzed with a port of the module plus Next's resolution (`new URL(result, <clinic origin>)`). Every result stayed on the clinic origin, under `/book…` or `/portal/cats/{id}`. Payloads tried:
  - all first-pass payloads;
  - `%252e%252e` (double encoding), `%2f%2f`, `..%2f%2f`, `%5c`, `..%5c`;
  - tab, CR and LF inside `..` (stripped by the URL parser, then caught by the dot-segment re-check);
  - `\t//evil`, `/book;/..//evil`, `/book;x`, `/book%00`, `/book/%00/..//evil`, `/book/..%00//evil`;
  - fullwidth `．．` and `／／`, `‥`, ideographic `。`;
  - `/book/../../..//evil`, `/book/x/../..//evil`, `/book@evil`, `/book:evil`, `/book#//evil`, `/book//evil`;
  - `//evil` inside the query.

  The payloads that are not rejected become harmless paths on the clinic site under `/book/` (for example `/book//evil`, `/book/%EF%BC%8E…`). The URL parser does not decode them again, so the worst outcome is a 404 on the clinic's own origin. Query values are re-encoded by URLSearchParams, and `catId` is overwritten with the server-returned id. BookingFlow does not follow any `next` parameter of its own.
  Unit tests web/tests/unit/cat/booking-return-path.test.ts and add-cat-customer.test.tsx pass (22/22, run by this gate).
- 2a58de5 (OwnerId server error goes to the form's root message in customer mode): nothing security-relevant. The root message renders as React text (`{errors.root.message}`), with no innerHTML. The server already forces the owner to the caller (F19), so any CustomerNotFound/AccountNotActive message is about the caller's own account and discloses nothing about other customers.
- 1cfffe9: docs only.
- [LOW, still open, backlog] web/lib/validation/nextPath.ts:18: the shared guard still accepts dot-segments, backslashes and encoded slashes after the prefix. Not exploitable through any current caller (this slice now re-validates after normalizing). Hardening is unchanged from the first pass.

SCOPE CHECK (second pass)
- source files changed: 3 (page.tsx, CatForm.tsx, bookingReturnPath.ts) + 2 tests + docs, all in the slice's scope
- files outside allowed scope: none
