GATE: SECURITY
HANDOVER: HO-ACC-001
COMMITS: 53fb683 328622e d8b0869 2ff0a6d (fixes on top of the pass-2 head c36d1d0)
DATE: 2026-09-25
STATUS: PASS

HISTORY
- Pass 1 (81cf928): FAIL.
  - 1 HIGH: X-Forwarded-For bypassed every per-IP limit.
  - 3 MEDIUM: sign-in timing oracle; CORS reflected any origin with credentials; walk-in hijack via
    additional numbers.
  - 3 LOW: CAPTCHA not single-use under concurrency; exception handler logged body and query;
    email change without re-authentication.
- Pass 2 (c36d1d0, head a5d9a62): FAIL. All pass-1 findings were closed. The findings were:
  - HIGH: CSRF key held per process, so every user was locked out after any restart.
  - MEDIUM: the email step-up was an unthrottled password oracle.
  - LOW: a customer could claim another customer's primary mobile as an additional number.
- Pass 3 (this file, head 2ff0a6d): PASS. All three pass-2 findings are closed. There are no new
  findings and no regressions.

HOW IT WAS CHECKED
- Fresh `git archive` of 2ff0a6d in the scratchpad (probe3/), run against local Postgres.
  - Committed suites: Domain 156, Infrastructure 28, Application 64, Integration 100. All pass:
    348/348.
  - Probes, none added to the worktree:
    - scratchpad/probe3/…/SecurityGateProbe3Tests.cs and SecurityGateProbe3bTests.cs (new);
    - the pass-1 and pass-2 probe files, re-run unchanged.
- A process restart was simulated with a second `WebApplicationFactory` host against the same
  database.
- Legacy rows from before the migration were simulated with
  `UPDATE … SET csrf_token_hash = ''`.

FINDINGS
- None.

CLOSED (pass-2 findings, verified by probe)
- HIGH, CSRF lockout after restart: closed.
  - Customer: sign in on host A, then on host B with the same cookies:
    - GET /me → 200, with no Set-Cookie, because the token is still valid;
    - PUT /me → 204;
    - PUT with no header → 403;
    - sign-out → 204;
    - re-sign-in with old cookies and no header → 200.
  - Staff on host B: walk-in → 201, sign-out → 204, sign-in with the old cookie → 200.
  - The pass-2 probe unchanged now reports 204/204/200/200 where it reported 403s.
- MEDIUM, email step-up oracle: closed.
  - 12 wrong step-ups with the rate limit relaxed: 3 × `ReauthenticationFailed`, then
    `AccountLocked`.
  - The correct password while locked → 400 `AccountLocked`. The lock check runs before the
    verify, so the lockout window cannot be probed.
  - Fresh sign-in while locked → 401. A name-only edit is still 204.
  - With `AccountSensitive` at 5 per 60 minutes, 8 step-up PUTs gave `400×5, 429×3`. The pass-2
    probe unchanged gives `400×5, 429×7`.
  - A no-password account burns a dummy Argon2 verify, so timing is uniform.
- LOW, squatted additional number: closed.
  - Claiming another active customer's primary is refused with 400
    `AdditionalPhoneNumberMatchesAnotherCustomer`. Refused forms: raw, `+88`, `88`, hyphenated,
    whitespace-padded, and mixed into a list.
  - Squatting before the victim registers still returns 204 (by design). The walk-in then returns
    201, and Q-H2 labels the squatter `"Additional"`. The duplicates view carries
    `mobileMatchedOn` (committed test `…AreBothLabelledAdditionalNotPrimary`).
  - The refusal is an existence signal, but registration already discloses mobile existence
    (ADR-028/030). This one needs a signed-in account and is capped at 5 per hour per IP, so it
    adds no new oracle.

THIRD-PASS ASSESSMENTS
1. Legacy `csrf_token_hash = ''` rows fail closed.
   - The middleware rejects a null or empty header before comparing. Otherwise it compares
     SHA-256-hex(header), always 64 characters, with the stored value using `FixedTimeEquals`.
     Against `''` the lengths differ, so it is always false.
   - Probed, all 403:
     - no header and no `pc_csrf`;
     - an empty header, a whitespace header, and a header of `''` with an empty cookie;
     - the original sign-in token;
     - `hash('')` and the literal empty-string SHA-256 as the header;
     - POST, DELETE and PATCH without a header;
     - staff walk-in with no header or an empty header.
   - A GET re-issues `pc_csrf`, and the stored hash is then `hash(fresh)`. A PUT with the fresh
     token → 204; the pre-reissue token → 403. The staff path works the same way.
2. Skipping session resolution on sign-in loses nothing security-relevant.
   - Fixation: the session token is always server-generated. A planted `pc_session` is never
     adopted (issued ≠ planted), and a fresh CSRF token is issued alongside it.
   - Old session on re-sign-in as someone else: the server-side row stays alive (ended_on NULL,
     still usable by a holder of that token). This is unchanged; neither the pass-2 code nor the
     controller ever ended it. The browser's cookie is overwritten, so only someone who already
     holds the token can use it.
   - A failed sign-in has no side effects on a valid existing session: 401, no Set-Cookie, and the
     old session still returns 200.
   - Staff idle-expiry audit: sign-in with an idle-expired cookie → 200, and the old row is not
     ended at that moment. The row is permanently idle-expired and unusable: presenting it later
     → 401 `session_idle_timeout`, which ends and audits it. The audit is written only on
     detection, exactly like an abandoned or cleared cookie, so this is not a new class of gap.
   - Rate limiting is partitioned by `Connection.RemoteIpAddress` only, never by session. With
     SignIn at 3/min, 6 attempts with varying cookies gave `401×3, 429×3`, and staff sign-in from
     the same IP gave 429. CAPTCHA and the account lockout are unaffected; they sit in the
     handler and need no session context.
3. GET re-issue:
   - `pc_csrf` is `Secure; SameSite=Lax; Path=/`, not HttpOnly (by design), and not readable
     cross-site.
   - A cross-site top-level GET sends the victim's valid `pc_csrf`, so there is no rotation (a GET
     with a valid cookie gets no Set-Cookie). Even if a rotation is forced, the new value goes only
     into a cookie.
   - The token and its hash appear in no response body:
     - the sign-in body;
     - GET /me;
     - GET /api/auth/session;
     - GET /api/staff/session, with no "csrf" key.
     `[JsonIgnore]` on both view models is effective.
   - Logs: 0 lines containing the session token, the CSRF token or either hash.
   - Cookie tossing: a planted `pc_csrf` triggers a reissue to a random value, never the planted
     one, and the planted value used as header plus cookie → 403.
   - Anonymous and ended sessions get no reissue (401, no `pc_csrf`).
   - Concurrency:
     - the `xmin` row version turns racing refreshes into a caught `DbUpdateConcurrencyException`;
     - 16 parallel GETs → all 200;
     - the last-arrived cookie works (204), and the next GET does not rotate again;
     - an out-of-order arrival at worst causes one 403, which the next GET heals.
4. Crypto:
   - The token is 32 bytes from `RandomNumberGenerator`, base64url-encoded (43 characters,
     256 bits). It is independent of the session token (≠ session, ≠ hash(session)).
   - Only SHA-256-hex is stored.
   - The comparison is `CryptographicOperations.FixedTimeEquals` over fixed-length hex digests.
5. Regressions: none.
   - X-Forwarded-For rotation → `401,401,429×6`; a fixed XFF → 429.
   - Timing: unknown identifier 581 ms against locked account 589 ms (median).
   - CAPTCHA: 200/200 successes, no challenge accepted twice.
   - Exception handler: the password leaks neither before nor after binding.
   - CORS:
     - the matrix is identical to pass 2 (empty means fail-closed; exact origin only);
     - `*` with credentials still throws when the host starts.
     - `HostPipelineExtensions` is a verbatim move, and the Program.cs pipeline order is unchanged.
   - The email-variant step-up bypass probes still refuse: ZWSP, Cyrillic, blank and new
     addresses → 400; case and whitespace variants leave the stored value unchanged.

CONDITIONS RECORDED, NOT FINDINGS
- `AccountSensitive` (5 per 60 minutes per IP) now covers every `PUT /api/customers/me`, including
  name-only and phone-only edits. Behind carrier-grade NAT or shared clinic Wi-Fi, unrelated
  customers share one bucket.
  - This is an availability trade-off the developer accepted in §1 Amended (18).
  - The per-account lockout now carries the oracle defence. If the limit becomes a support
    problem, either:
    - partition the limit by the resolved customer id (the session middleware runs before the
      limiter); or
    - split the email change onto its own endpoint.
- Carried forward:
  - audit rows keep personal data after anonymisation;
  - the application role must not own `audit.*`;
  - ADR-028 and ADR-030 accept enumeration and lockout DoS;
  - never set `ASPNETCORE_FORWARDEDHEADERS_ENABLED=true`.

SCOPE CHECK
- files in allowed scope: 43/47 in the fix range (src/, tests/)
- files outside allowed scope:
  - `.ai/decisions/adr.md` (ADR-342 rewrite);
  - `docs/handover/HO-ACC-001.md`, `HO-ACC-002.md` and `HO-CAT-002.md` (§1 Amended (18) and
    downstream contract notes).
  - None of them adds security surface.
