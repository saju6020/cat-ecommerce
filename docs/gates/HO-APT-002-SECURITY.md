GATE: SECURITY
HANDOVER: HO-APT-002
COMMITS: cffaa79..bb183b8 (code head bb183b8; b3377df, dfea47a docs only)
DATE: 2026-10-04
STATUS: PASS

FINDINGS
- [LOW] web/lib/validation/nextPath.ts:17-25 (pre-existing, HO-ACC-002; not introduced here) —
  `matchesPrefix` is a string-prefix test, not a check on the resolved path. `/book/..//evil` and
  `/portal/../..//evil` pass the guard and resolve (WHATWG URL against the site origin) to the
  SAME-origin pathname `//evil`. It is not an off-site redirect: `router.push` sees a same-origin
  URL, and a history entry of `//evil` would be rejected by the browser as cross-origin rather
  than followed. But the guard lets non-canonical paths through, and any later code that passes
  `next` to `location.href`/`<a href>` or a server `Location` header would turn it into a
  protocol-relative redirect. Fix (ACC backlog): resolve with `new URL(next, location.origin)`,
  require `url.origin === location.origin`, then run the prefix check on the normalised
  `url.pathname` and return `pathname + search + hash`. Also reject `\` and control characters.
- [LOW] web/components/portal/CustomerSessionGuard.tsx:54 — the session-error handler still builds
  `next` from `window.location.pathname` only. The initial redirect (line 47) now carries the query.
  There is no security impact, because the value is still sanitised on sign-in. It is an
  inconsistency: a mid-session 401 drops the `/book?…` parameters that §8.1 Amended preserves.
  Backlog for consistency.
- [LOW] web/lib/calendar/ics.ts:17-22 — `escapeIcsText` handles `\`, `;`, `,`, CR and LF, so no
  property injection is possible. Other C0 control characters and U+0085/U+2028 pass through
  unescaped, and RFC 5545 forbids CONTROL in TEXT. Some calendar clients may reject or garble the
  file if a cat name contains them. That is a robustness issue, not an injection. Strip
  `[\x00-\x08\x0B\x0C\x0E-\x1F\x7F]` as defence in depth.

Checked, no finding:
1. Open redirect. The guard change only appends `window.location.search` to `next`. An attacker
   could already set `next` by linking `/sign-in?next=…` directly, so this edit adds no surface.
   Tested through `URLSearchParams` and `sanitizeNextPath(…, ['/portal/','/book'], …)`:
   - Falls back to the default: `//evil`, `/\evil`, `%2F%2Fevil` (decoded once to `//evil`),
     `/book@evil`, `/bookX`, `/book#//evil`, leading space or tab, and `/book\t/evil`.
   - Allowed: `/book?x=//evil` and `/book?/\evil`, both same-origin with path `/book`.
   - `/bookX` gets no bare-prefix match, thanks to the `/`/`?`/exact boundary.
   - Only the same-origin `..` case above remains (LOW).
2. Customer data and IDOR.
   - Customer screens call only `/api/customers/me/**`. `CustomerAppointmentsController` has
     `[RequireCustomerSession]`, and its handlers resolve by caller.
   - A crafted `/book?catId=<someone else's cat>` is accepted on the client while the cat list is
     empty (`BookingFlow.tsx:83`). `PlaceSlotHoldCommandHandler` then rejects it
     (`cat.OwnerId != UserId` gives CatNotFound), as do confirm and release (`CustomerId != UserId`).
   - Staff endpoints all carry `RequireAction`: View, Book, CheckIn, Treat, RecordNoShow, and Book
     for walk-ins. The flagged-list customer and phone join, and the customer panel, go through
     `GET /api/staff/customers/{id}` under `RequireAction(Customers.View)`. A role without that
     action gets 403 and sees no name or phone.
   - UI buttons are driven by the server's `allowedTransitions`. Hiding them is UX only, and the
     server is the authority.
   - The flagged view requests the full staff customer record but renders only the name and
     mobile, to a role the server has already authorised for that record. That is not over-exposure.
3. `.ics` export. CR and LF are escaped to `\n`, so a cat, service or branch name cannot start a
   new property. DTSTART and DTEND go through `Date.toISOString()`. The filename is a fixed
   constant (`purrcare-appointment.ics`). The file is a `blob:` URL with a text/calendar MIME
   type, revoked after 1s. `ics.test.ts` passes.
4. Server clock. `useServerNow` and `HoldCountdown` are display only.
   `ConfirmAppointmentCommandHandler` checks `now >= HoldExpiresOn` against the server
   `TimeProvider` and returns `SlotHoldExpired`. A skewed or forged client clock can only
   mis-display the countdown. It cannot extend a hold.
5. `ApiConflictError` body. The 409 JSON is accepted only if `errors` is an array. The
   `errorMessage` values are joined into a plain string and rendered as React text children
   (escaped). `errorCode` is used for branching only and is never rendered.
6. Rendering. There is no `dangerouslySetInnerHTML`, `innerHTML`, `eval` or `document.write` in
   the diff. The only production `fetch` is `client.ts:259`. The other two `fetch` calls are in
   `mocks/`.
7. Waitlist claim. There is no token in the URL. The offer is
   `/portal/appointments/{offeredAppointmentId}`, behind the customer session and the `/me`
   ownership check, and it is confirmed through the normal confirm endpoint, which applies the
   same ownership and expiry checks.
8. Mocks. `apiUrl()` (`mocks/handlers/appointments/fixtures.ts:147`) is reachable only from MSW
   handlers. MSW loads via a dynamic import gated on `NODE_ENV === 'development' &&
   NEXT_PUBLIC_USE_MOCKS === 'true'` (`app/providers.tsx:112`). The sessionStorage clock-skew
   seam is mock-only.
9. N+1 detail reads (FlaggedView: one `GET /api/staff/appointments/{id}` per flagged row, plus one
   customer GET per customer). This is performance, not security. Each call is authenticated and
   authorised per object for a staff role that could issue the same requests directly. It
   amplifies nothing an anonymous caller can reach, and exposes no data beyond the caller's
   rights. A paged or joined flagged read belongs in the performance backlog, not here.

Tests run: vitest ics, errors, next-path and server-clock suites, 4 files and 19 tests, all passed.

SCOPE CHECK
- files in allowed scope: 107/107 code+docs files (as established by the ARCHITECTURE second pass;
  CustomerSessionGuard.tsx ratified by owner decision b3377df)
- files outside allowed scope: none
