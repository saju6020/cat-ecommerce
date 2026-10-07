GATE: SECURITY
HANDOVER: HO-ADM-001
COMMITS: 6dec5d0 0c2a617 8495e32 (fifth pass, delta a311677..8495e32, branch ho/ADM-001). Branch head is now d80a82c, which adds only the ARCHITECTURE verdict file.
DATE: 2026-09-26
STATUS: PASS

This is the hardening round: a global `NulByteRejectionFilter`, per-field `NoControlCharacters`,
`EnumerationParsing` + `MustBeValidEnumeration` + `AdministrationMapper.ParseEnum`, `\A…\z`
anchors, and a leave `pageSize` cap. It closes all three pass-4 LOWs for every value the pass-4
probes used. One residual of the enum-body LOW remains: a blank banner `placement`. It is
non-blocking on the same terms as pass 4, because the worst case is an empty 500 in production
and only content managers can reach it.

FINDINGS
- [LOW] src/Application/CatEcommerce.Application/DataMappers/AdministrationMapper.cs:125 (`ParseNullableEnum`)
  with ContentCommandValidators.cs:22, :36. On content create and edit, `placement` values of
  `""`, `" "` or `"\t"` still return 500.
  - Why: `EnumerationParsing.TryParse` treats a blank value as valid with a null result, so
    `MustBeValidEnumeration` passes it. `ParseNullableEnum` then tests only `value is null`,
    calls `ParseEnum("")`, gets a null result and throws `InvalidOperationException`.
  - Proven (P28b):
    - create `Banner` and create `Page`, each with all three blank values → 500;
    - edit with `""` and `" "` → 500;
    - a Shop Manager can reach it (create with `""` → 500).
  - This bug predates the delta: the old `FromDisplayName("")` also threw. The ARCH fifth pass
    reported the same thing.
  - Impact: an empty 500 and an Error log line in production. No data is written and nothing
    leaks.
  - Fix: in `ParseNullableEnum`, map `string.IsNullOrWhiteSpace(value)` to `null`, the same
    as the query side does. Or reject blank-but-present values in the validator. Add `""` and `" "`
    to the regression theory.

CLOSED IN THIS PASS (verified by probe)
- The NUL class. The global filter returns 400 `ContainsControlCharacter` before any handler or
  database call, on every controller.
  - Every former NUL 500 from P26/P27 is covered:
    - leave `note` (Nurse);
    - `decisionNote`;
    - roster and closure `reason`;
    - branch `phoneNumber`;
    - content `linkUrl`;
    - plus all the pass-3/4 fields.
  - **HO-ACC-001 carry closed:** `PUT /api/customers/me` `fullName` NUL now returns 400, not 500.
  - Also covered outside ADM:
    - staff customers `?mobile=` NUL → 400;
    - staff-accounts `?search=` NUL → 400;
    - nested `settings changes[].key` → 400.
  - Escaped `\u0000` → 400. A literal 0x00 byte → 400 (System.Text.Json rejects it). NUL in a JSON
    property *name* is ignored as an unknown member (204, harmless).
  - Sign-in:
    - customer and staff: a NUL in the password or identifier → 400;
    - a correct sign-in right afterwards → 200;
    - the filter logs nothing, and a password marker (`XYZZY`) appears 0 times in the captured
      host log;
    - the 400 depends only on the input, so it does not help enumerate accounts;
    - the sign-in rate limiter (endpoint middleware) still runs first.
  - Headers and route: Kestrel itself rejects NUL (raw-socket probe against the built host):
    `User-Agent` NUL → 400, any header NUL → 400, sign-in with a NUL UA → 400, path `%00` → 400.
    The User-Agent NUL → 500 seen under TestServer cannot happen in production, because Kestrel
    rejects the request first.
- Enum bodies. Wrong case is now accepted: `page`/`PAGE` → 201, `placement=home` → 201, room
  `consultation` → 201, and Nurse leave `annual` → 201. Unknown values → 400 on content kind,
  closure, service category and leave kind. The Shop Manager cases `Policy`/`Terms`/`4`/`0` → 400.
  Room `kind=" "` → 400 (NotEmpty).
- Trailing LF. `"x\n"` → 400 for the policy key, the accept `policyKey`, the content slug
  (Shop Manager), the branch code and the tax code. CRLF, leading space, Cyrillic, ZWSP, 81
  characters, `-` and `--` → 400.
- Paging.
  - Leave `pageSize`:
    - int.Max → 400, with pageNumber 1 or int.Max;
    - 101 → 400;
    - 1 000 000 × 100 → 200 (no overflow);
    - int.Min/int.Min → 200 (clamped).
  - Audit search: `pageSize` int.Max → 400, and `pageNumber` int.Max → 400.
  - Audit export over a 7-day range → 200 `text/csv`, `Cache-Control: no-store`.

FILTER ROBUSTNESS (P28a, filter called directly). These are not findings, because nothing
reachable today is affected.
- The walk is bounded to depth 8.
  - A self-referencing cycle returns in 0 ms. A 7-way self fan-out cycle returns in 4 ms.
  - A 2M-element `List<string>` is scanned in 94 ms. Kestrel's 30 MB body cap bounds the input,
    and a deserialized JSON tree has no shared nodes, so the cost is linear in the body size.
  - A throwing getter is swallowed, and the scan continues to the NUL property after it. A
    side-effecting getter is called exactly once.
- Latent blind spots, which are not reachable today:
  - `Dictionary<,>` keys and values are not scanned. `KeyValuePair` sits outside the
    `CatEcommerce` namespace, so it is skipped.
  - `JsonElement` is not scanned, and neither is an `object` property that holds one.
  - A NUL deeper than property depth 8 passes.
  - Every `[FromBody]` type is in `CatEcommerce.Api.Contracts.*`. None has a dictionary,
    `JsonElement`, `object` or `dynamic` member, and none nests close to depth 8. No action reads
    `Request.Body` or `Request.Form` directly, and there are no minimal-API endpoints.
  - The per-field `NoControlCharacters` rules remain as a second line.
  - If a future DTO adds a dictionary or a raw-JSON member, the filter must be extended to walk
    `KeyValuePair` and `JsonElement`.

REGRESSIONS: none.
- Committed suites: Domain 190, Application 68, Infrastructure 28, Integration 249, total
  **535/535**.
- P1–P25 (AdmSecurityGate2–5 and AdmSecurityGateProbe*) pass 25/25. The only 500s are the
  intended fail-closed probes: P10, where the owner query throws, and P24, where the status
  lookup throws.
- These still hold: role separation, leave IDOR, CSRF, CORS, the redaction matrix, maintenance
  failing closed, and Policies.Manage Administrator-only (P20b for every role).

CARRIED (not findings against this branch)
- HO-ACC-001: `GET /api/staff-accounts?pageNumber=2147483647` → 500 (the Skip overflow).
- Conditions from pass 3 still apply:
  - plain text must be rendered as text, and there is no `nosniff` header;
  - `ClinicalAuditEntityTypes` fails open for an unregistered type;
  - the CSV guard residuals remain;
  - public GETs are not rate-limited.

HOW IT WAS CHECKED
- Read the full source delta a311677..8495e32.
- Built a scratch `git archive 8495e32`. No git-ignored appsettings were copied.
- Built a fresh database `catecommerce_admsec5` with `dotnet dotnet-ef database update
  --connection`, using the connection details from the main repo's appsettings.Development.json.
  `CATECOMMERCE_TEST_DB` was pointed at it.
- Probes:
  - P26/P27 re-run;
  - new P28a (the filter called directly) and P28b (HTTP, including placement blanks);
  - a raw-socket NUL-header probe against the built API on Kestrel;
  - a regression sweep of P1–P25.
- Probe sources and transcribed results are in scratchpad `admsec5-probes/`, with a duplicate in
  `gate-adm001-pass5-backup/`.
  - After the session restart, the scratch tree and the first copy of `admsec5-probes/` (raw
    logs) were deleted twice by something outside this gate.
  - The sources were re-created and the results transcribed from the console output first
    observed (`RESULTS.txt`).
- The database was dropped. Nothing was committed.

HISTORY
- Pass 1 (head 7f5c284): FAIL, 3 MEDIUM and 6 LOW. The MEDIUMs were the unbounded audit page,
  clinical data through the audit log, and the active branch failing open.
- Pass 2 (head 04195cc): FAIL, 1 MEDIUM and 3 LOW. The MEDIUM was Cat BusinessEvent rows and
  `Reason` not being redacted.
- Pass 3 (head 4463196): FAIL, 2 LOW. Blocking: Shop Manager policy-key squatting. Also NUL 500s
  and the leave pageNumber overflow.
- Pass 4 (head a020bcb): PASS, 3 LOW. Owner decision: policies are Administrator-only. New:
  trailing-LF anchors, six more NUL fields, and enum-body 500s.
- Pass 5 (this file, head 8495e32): PASS, 1 LOW. All three pass-4 LOWs are closed; the one
  residual is a blank `placement` → 500. The ACC `fullName` NUL carry is closed by the global
  filter.

SCOPE CHECK
- files in allowed scope: 21/21 (the source, test and handover files in 6dec5d0, 0c2a617 and
  8495e32). `BusinessViolationCodes.Administration.cs` and `PagingRules.cs` are shared-constant
  additions within ADM's area. The filter is registered from `AdministrationModule`, not
  `Program.cs`, following the MaintenanceModeFilter precedent.
- files outside allowed scope: none.
