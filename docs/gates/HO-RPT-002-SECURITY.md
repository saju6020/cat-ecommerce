GATE: SECURITY
HANDOVER: HO-RPT-002
COMMITS: 2f6d1d8a 60518928 e0f17f0e 5bd83b64 09556784 (range 5a187b6b..09556784)
DATE: 2026-10-14
STATUS: PASS

FINDINGS
- [LOW] web/lib/download/reportExport.ts:58-60 — `decodeURIComponent(match[1])` runs unguarded
  inside the async `onload`. A malformed percent sequence in `Content-Disposition` throws, the
  promise is never settled, and the export button stays "Preparing…" until the page is reloaded.
  Nothing is exposed, but this code becomes live as soon as CORS exposes the header (ruling below),
  and it is already live in a same-origin deployment. Fix: wrap the decode in try/catch, fall back
  to the raw value or `null`, and keep only the part after the last `/` or `\`.
- [LOW] web/components/reports/ReportViewer.tsx:93 — the fallback filename is built from the URL's
  `from`/`to` (`state.filters`), not from the server-normalised `result.filters`. A crafted link
  can put arbitrary text in a saved file's name (the extension is always `.csv`/`.pdf`, and browsers
  clean path characters in `download`). Fix: build the fallback from `result.filters.from/to`, or
  restrict it to `[A-Za-z0-9._-]`.
- [LOW] web/lib/api/report-schedules.ts:178,191,198,202,213 — `scheduleId` from `useParams` is put
  into the API path without `encodeURIComponent`, unlike `reportKey` (reports.ts:157,173). This is
  the same pattern used across the codebase (for example clinical-catalogue.ts). It is low impact
  because the routes are Administrator-only, take a GUID id, and the GET must return a schedule
  before Save is shown. Fix: `encodeURIComponent(id)` here.

No BLOCKER or HIGH findings.

CHECKED, NO FINDING
- 403 handling: every Reporting read (catalogue, viewer and probe, dashboard, schedules list,
  detail and runs, exports) goes through `ReportErrorView`/`ExportButtons`, which render
  `PermissionDeniedState` for a 403. The nav entry is presentation only. A key outside the
  caller's catalogue is still asked of the server (ADR-FE-137), so the server's refusal decides,
  not a hidden link. A dashboard drill-down appears only when the server sends `drillDown`. Admin
  "Report schedules" relies on the server's 403. The UI never relies on hiding for security.
- Exports: fetched by XHR as a blob, with credentials and `X-Branch-Id`. The object URL is revoked
  in `finally` straight after `click()`, and the blob is never stored. Error bodies are parsed as
  JSON and mapped to typed errors. A 429 reads `Retry-After` and treats NaN or 0 as 60. The server
  sends `Cache-Control: no-store` on report reads, exports and the dashboard
  (StaffReportsController.cs:54,67,111; StaffDashboardController.cs:31). Its filename pattern uses
  only the key and dates.
- Cell rendering: `formatCell` returns strings only, and `CellValue` renders them as React text
  nodes. There is no `dangerouslySetInnerHTML`, `innerHTML`, `eval` or `window.open` in the area
  (source-scan.test.ts enforces the first). Server notes, titles and error messages are text nodes.
  `errorCode` is never rendered. Chart: recharts SVG text, and the tooltip uses the `formatCell`
  text.
- Links: catalogue, drill-down and "Schedule this report" hrefs are all internal
  `/staff/...` paths with `encodeURIComponent`/`URLSearchParams`. No server URL is used as an
  href. Sign-in `next` still goes through `sanitizeNextPath`; only the default changed.
- Schedule prefill from the query string carries report, branch and parameters, but never
  recipients, so a crafted link cannot pre-select who gets a report. Creating a schedule still
  needs an explicit Administrator submit with a CSRF header (apiFetch POST).
- Recipient picker: renders only `name`, `roles` and the boolean `emailPresent` from
  `…/recipients`. No email address or other staff PII is fetched or shown beyond the endpoint's
  data. Run history shows `recipientName` and status only.
- Clinical R3 in the browser: there is no TanStack persister, no `localStorage`/`sessionStorage`/
  IndexedDB use in the area, and no service worker in production (MSW is dev plus a flag, and is
  dynamically imported; the mockServiceWorker.js diff only reformats it). Report data lives only
  in the in-memory query cache. Sign-out runs `forgetPreservedBranch` -> `queryClient.clear()`,
  and both sign-in pages clear the cache before a new session. Nothing is left behind after
  sign-out. The 500 from the clinical access log failing closed renders "nothing was shown", and
  no partial data is displayed.
- No secrets, tokens or report data are logged (no `console.*` in the area).
- The dashboard poll has a 10 s floor whatever `refreshSeconds` says, and 4xx responses are not
  retried.

RULING — builder question 3 (CORS `WithExposedHeaders("Content-Disposition", "Retry-After")`)
- SAFE to add. `Access-Control-Expose-Headers` only lets origins that are ALREADY allowed (the
  explicit `WithOrigins` allow-list with credentials, HostPipelineExtensions.cs:97-108) read those
  two headers. Other origins still read nothing. Neither header carries a secret: the filename is
  `purrcare-{key}-{from}-to-{to}.{ext}`, which the caller already chose, and `Retry-After` is a
  number of seconds.
- Conditions: (a) keep the origin list explicit. Never go back to `SetIsOriginAllowed(_ => true)`.
  (b) Keep the server filename free of PII: no cat, client or staff names. (c) Fix the LOW
  `decodeURIComponent` finding above when the header is exposed, because the parsing becomes live
  then. This is a backend follow-up and is not part of this gate's scope.

PROPOSED BACKLOG ROWS (docs/backlog/RPT.md, after 66)
- 67 — LOW — reportExport.ts: guard `decodeURIComponent` on the Content-Disposition filename and
  strip it to a basename. Without the guard, a malformed header leaves the export promise unsettled.
- 68 — LOW — ReportViewer.tsx: build the export fallback filename from the server-normalised
  `result.filters`, or a sanitised charset, not from raw URL `from`/`to`.
- 69 — LOW — report-schedules.ts: `encodeURIComponent` the `scheduleId` path segment, to match
  `reportKey`. A codebase-wide sweep could follow.
- 70 — LOW (backend follow-up) — add `WithExposedHeaders("Content-Disposition", "Retry-After")`
  next to `Date`, under the conditions in the ruling above.

SCOPE CHECK
- files in allowed scope: 71/71 (web/ area files, docs/handover/HO-RPT-002.md,
  docs/test-cases/TC-HO-RPT-002.md)
- files outside allowed scope: none with security impact. The builder declares deviations from
  §13's literal list in Amended (12): nav/areas/reporting.ts, tests/e2e/reports-helpers.ts,
  tests/e2e/reports-real-api.spec.ts, lib/hooks/useBranchOptions.ts. Also changed: app/page.tsx,
  (auth)/staff/sign-in/page.tsx (default landing only) and public/mockServiceWorker.js
  (formatting only). There are no backend changes.
