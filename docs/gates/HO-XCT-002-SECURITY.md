GATE: SECURITY
HANDOVER: HO-XCT-002
COMMITS: 3e52c323 f9cf3644 6fb9566a 61765229 a574b268 68814f65 66c58c4a (range 5a187b6b..66c58c4a, branch ho/XCT-002)
DATE: 2026-10-14
STATUS: FAIL

FINDINGS
- [HIGH] web/components/offline/OfflineBoard.tsx:78-80, web/lib/hooks/useIdleTimeout.ts:14-15, web/app/(offline)/layout.tsx:7 — the offline board's idle lock and person binding can be bypassed by reloading the page or opening it from cold. Rule: NFR-SEC-4 / C-9 and ADR-FE-144 say the board "locks itself after idleTimeoutMinutes" and is "bound to the staffAccountId that fetched it". What the code does instead:
  (a) `locked` is React state that starts `false` on every mount, so F5 (or typing `/staff/offline` again) shows the full list again for another 30 minutes. Nothing about the lock is persisted.
  (b) `mismatch` needs `knownStaffId`, which comes only from the in-memory query cache (`useKnownStaffAccountId`, `enabled: false`). On a cold load it is always `null`, so the board never refuses another person's copy.
  (c) The `(offline)` group has no session guard and the board does not check whether the server is reachable. With `status` starting at `'online'` and `recoveredAt` at `null`, it renders the saved copy even when the device is ONLINE.
  Exploit: a receptionist turns the copy on and later closes the tab or browser without pressing Sign out (lunch, end of shift). Closing the tab fires none of the wipe triggers; only sign-out, a `session_invalid` seen by a mounted `(staff)` layout, or `validUntil` (end of the branch day + 2 h) clear it. Anyone who then opens that browser profile and goes to `/staff/offline`, from history or autocomplete, sees an unlocked board with no sign-in. It shows today's appointments with customer names and phone numbers, high-risk handling notes, and boarding tasks with instructions. They can reload it whenever the lock appears. This is broader than the "power loss" residual risk that ADR-FE-141 accepted, and it also gets around the HO-ACC-002 idle lock that protects the online staff app.
  Remediation (frontend only, no new endpoint):
  1. Before rendering any data, the board pings `/api/system/ping` once (or reads the monitor's first result). If the server answers, `router.replace('/staff/front-desk')` so the session guard decides. The board then renders data only while the monitor says `offline`.
  2. Persist `lastActivityAt`, with no personal data, in the `device` store. The `(staff)` shell's activity handler and the board's own input handler write it, throttled to about once a minute. On mount, the board is `locked` when `now - lastActivityAt > idleTimeoutMinutes`, or when `lastActivityAt` is absent.
  3. Persist `locked: true` in the same store once the lock fires. Clear it only after a successful `syncSnapshot` for the same `staffAccountId`.
  Add an e2e case: lock, reload, still locked. Add another: a cold load while online goes to `/staff/front-desk`, not the board.

- [MEDIUM] web/lib/offline/snapshot.ts:84-91, web/lib/api/offline.ts:12-27 — the snapshot is stored in IndexedDB as plaintext. It holds `customerPhone` for every appointment, `handlingNote` and boarding `instructions`. That follows ADR-FE-141, and browser-side encryption with a key kept on the same device adds little, so this does not block. The phone number is still the open owner question carried over from XCT-001. Proposed backlog row: "Owner decision: keep or drop `customerPhone` in the offline snapshot (data minimisation). If kept, record the decision in ADR-FE-141. If dropped, HO-XCT-001 removes it from §12.4."

- [LOW] web/lib/offline/snapshot.ts:126 + web/components/offline/OfflineSnapshotSync.tsx:72 — person change (E-A9) is covered by unit tests only. The logic is correct while the `(staff)` layout is mounted: `removeOtherPeoplesSnapshots` runs before every fetch, and `writeSnapshot` wipes when an existing record belongs to someone else. Per the HIGH above, it does not protect a cold-loaded board. Proposed backlog row: "E-A9 e2e: person A opts in and syncs, signs in as B on the same device, A's copy is gone before B's write."

- [LOW] web/lib/observability/web-vitals.ts:42-60 — `routeTemplateOf` replaces the FIRST unclaimed segment whose value equals a parameter's value. If a dynamic value happens to equal an earlier static segment (for example a slug `products` in `/shop/products/products`), the static segment is replaced and the real value stays in the path. Static segments only, so no PII leaks today. Proposed backlog row: "Build the route template from `useSelectedLayoutSegments` or the App Router pattern instead of matching values."

- [LOW] backend, `POST /api/admin/operations/restore-tests` — it answers `500` for an instant with a non-UTC offset (§1 Amended item 19). The client works around it by converting to UTC. An unhandled 500 on admin input should be a `400`. Proposed backlog row (backend): "Normalise DateTimeOffset to UTC or reject with 400 in RecordRestoreTest."

REVIEWED AND CLEAN
- Opt-in: off by default (`DEFAULT_DEVICE_SETTINGS`). `writeSnapshot` refuses without the flag. Enabling needs a live staff session. Turning it off wipes the snapshot and the service-worker caches.
- Wipe triggers: sign-out (`forgetPreservedBranch` -> `clear()` runs in a `finally`, so it fires even when the sign-out POST fails or is refused offline), `session_invalid` (`isSuccess && data === null` only, so a network failure never wipes), person change, expiry timer, schema mismatch, `403 ActionNotPermitted`. No token, CSRF value or draft is stored. When IndexedDB is unavailable, nothing falls back to localStorage.
- `If-None-Match` is sent only for a record that belongs to the current person (`sync.ts:56`), so another person's ETag cannot turn into a `304` that keeps their copy.
- Service worker (`web/public/staff/sw.js`): scope `/staff/`, no `Service-Worker-Allowed`. GET only, same origin, never `/api/`, never `/staff/sign-in`. It caches only `/_next/static/**`, `/staff/icons/**` and the static `/staff/offline` shell, which is fetched with `credentials: 'omit'`. Navigations are network-first and never cached. The redirect `?from=` carries `pathname` only, and `safeReturnPath` keeps returns on `/staff/*` and rejects `//`. Registered only in production, not in mock mode, and only when opted in. The caches are purged on every wipe. No authenticated API response is ever cached.
- Erasure panel: the server enforces `[RequireAction(CustomerErasure.Erase)]`. The UI role check is presentation only. Channel and identityCheck are zod enums of the fixed codes, with no free text and no reason box, matching the 2026-10-13 contract. Submitting the form opens a ConfirmDialog that names the customer and says "This cannot be undone". Its confirm button is disabled while `isPending`, so a double submit is blocked. Repeat calls are idempotent server-side (`isReapplication`). CSRF: `apiFetch` still sends `X-CSRF-Token` from `pc_csrf` on every unsafe method, and no new CSRF-exempt route was added.
- Reapply tool: the server checks `CustomerErasure.Erase`. A confirmation step is shown. The client checks GUID shape, de-duplicates the ids and caps them at 500 before sending.
- Run retention now: the server checks `RequireAction`. A confirmation dialog states what the run will and will not delete.
- Backup and restore: the server checks `Operations.View` and the record permissions. The drill paste goes through `JSON.parse` with field-type checks only, never `eval` or markup. `drillOutput`, check names and details render as React text (a `<pre>` for the output). There is no `dangerouslySetInnerHTML` anywhere in the diff. Links are built only from server GUIDs.
- Web-vitals: `credentials: 'omit'`, no CSRF or branch header, and `text/plain` so it is a simple request. The payload has no user, session or URL field. The route is the pathname with dynamic params replaced; query strings are never read. Routes that fail the lower-case pattern are dropped, not lower-cased. `/staff/**` and `/api/**` are not measured. Batches are capped at 10 client-side. The server enforces the 4 KB body cap (chunked bodies included), the `Telemetry` rate-limit policy and the validator. Anyone can still post fake numbers to a public metrics endpoint; that is inherent to the design and bounded by the rate limit. The summary is read under `Telemetry.View` and rendered as text.
- StaffSessionGuard: a failed refetch is treated as signed out. That fails closed and is the correct security posture. It does not wipe the offline copy, because only `isSuccess && null` wipes, so going offline does not destroy the copy.

OWNER QUESTION 1 (expose ETag cross-origin): SAFE — approve. CORS is an explicit origin allow-list with `AllowCredentials()`. Adding `ETag` (and `Cache-Control`) to `WithExposedHeaders` only lets allow-listed origins read a header whose body they can already read. The ETag is a content hash of a per-person body and reveals nothing beyond that body. The client already scopes `If-None-Match` to the same `staffAccountId`. Do not widen the origin list or switch to `SetIsOriginAllowed` while doing it.

PROPOSED docs/backlog/XCT.md ROWS (medium/low; not blocking)
| Sev | Area | Item |
| MEDIUM | FE/Owner | Offline snapshot keeps `customerPhone` in plaintext IndexedDB: owner keeps or drops it; record in ADR-FE-141 |
| LOW | FE test | E-A9 person-change wipe as an e2e (A syncs, B signs in, A's copy gone before B's write) |
| LOW | FE | `routeTemplateOf` value-matching can replace a static segment; derive the template from the router pattern |
| LOW | BE | Restore-test POST returns 500 on a non-UTC offset; normalise or 400 |
| LOW | BE | Expose `ETag` (and `Cache-Control`) in `WithExposedHeaders` (owner Q1, security-approved above) |

SCOPE CHECK
- files in allowed scope: 100/100 reviewed for security. Scope conformance against §13 is owned by the ARCHITECTURE gate. The builder's own §1 Amended items 14, 17 and 18 note `adminNavItems.ts` and the HO-ACC-002 IndexedDB carve-out row still owed before merge.
- files outside allowed scope: none found by this gate (see ARCHITECTURE)
