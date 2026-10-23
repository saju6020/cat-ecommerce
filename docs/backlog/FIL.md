# Backlog — File storage (HO-FIL-001)

Medium/low gate findings not blocking the merge (owner rule 2026-09-27: only BLOCKER/HIGH block a gate).
Fix in a later batch. Source: `docs/gates/HO-FIL-001-*.md`.

| # | Severity | Item | Source |
|---|---|---|---|
| 1 | MEDIUM | No test for I2's audit entry carrying the malware signature name (source writes it). | ACCEPTANCE |
| 2 | MEDIUM | `FilePurposeAccess` is one shared dictionary; split per area (ADR-341 partial) before a second consuming area lands. | ARCHITECTURE |
| 3 | LOW | `Purge_batch_is_not_starved_by_held_rows_ordered_before_eligible_ones` leaves held rows that become purgeable after 30 days and will break the test on a long-lived test DB — clean up its own rows. | ACCEPTANCE |
| 4 | ~~LOW~~ Resolved | **Resolved 2026-10-07 in ho/BUGS-NTF.** Was — Purge source counts held files with two separate queries; may be slightly off under concurrent writes. | ACCEPTANCE |
| 5 | LOW | I10 upload memory growth is a smoke test only (three heuristics proved flaky). | CODE_REVIEW / TEST |
| 6 | LOW | U11 purge selection covered only by integration tests. | ACCEPTANCE |
| 7 | LOW | §13 scope paths in HO-FIL-001 don't match the solution's folder layout — amend the handover. | ACCEPTANCE |
| 8 | LOW | `FileStorageModule.cs` comment says `UseRateLimiter()` runs from AccountsModule — update when HO-NTF-001 merges. | ARCHITECTURE |
| 9 | LOW | Per-IP serve limit shares one bucket behind a load balancer unless trusted proxies are configured (deployment prerequisite, documented in §21). | SECURITY |
| 10 | Deploy check | Live verification still to do: real ClamAV (V3), real GCS bucket/ADC (V1/V2/V5). | TEST / ACCEPTANCE |

## HO-FIL-002 (web)

Source: `docs/gates/HO-FIL-002-*.md`.

| # | Severity | Item | Source |
|---|---|---|---|
| 11 | ~~MEDIUM~~ Resolved | **Resolved 2026-10-07 in ho/BUGS-ADM-WEB.** Was — `MediaViewer.tsx`: the refresh-once guard covers `<img>`/`<video>` only, not the PDF `<iframe>` — HO-EMR-002 must pick this up before it opens a lab PDF in `<MediaViewer>`. | ARCHITECTURE |
| 12 | LOW | `components/cat/PhotoField.tsx` is still a second upload control, so "one uploader" (A1 as amended) is only fully true once the cat photo page is converted to `<FileUploader>` — owed per the 2026-09-28 owner decision on A1 (HO-FIL-002 §1 Amended). | ARCHITECTURE / ACCEPTANCE |
| 13 | LOW | Delete `web/app/dev/file-storage-harness/*` when the first consuming screen lands (HO-SHP-002/EMR-002/BRD-002/RPT-002) and re-point `tests/e2e/files-*.spec.ts` at it — owner condition 2026-09-28, recorded only in HO-FIL-002 §1 so far. | ARCHITECTURE |
| 14 | LOW | The refresh-once guard in `FileThumb.tsx:61`/`MediaViewer.tsx:210` lives in component state, so a future host that remounts it on refetch (keying on `url`, or a skeleton swap) resets it and can refresh-loop again. `FileList` keys on `fileId`, so this slice is safe. Document "key on fileId, don't remount on refetch" in ADR-FE-154 and the `onRefresh` doc, or hoist the tried-set to module/context level. | SECURITY |
| 15 | ~~MEDIUM~~ Resolved | **Resolved 2026-10-07 in ho/BUGS-ADM-WEB.** Was — `FileUploader.tsx:164-172`: the Retry button has no per-file `aria-label`, so two failed rows give indistinguishable "Retry" controls — use `aria-label={`Retry ${item.file.name}`}`. | CODE_REVIEW |
| 16 | ~~MEDIUM~~ Resolved | **Resolved 2026-10-07 in ho/BUGS-ADM-WEB.** Was — `FileUploader.tsx:70-79` / `useFileUpload.ts:213-245`: `multiple={false}` only limits the native picker — drag-and-drop can still add several files. Cap to one in `handleFiles` before the first single-file consumer lands. | CODE_REVIEW |
| 17 | ~~MEDIUM~~ Resolved | **Resolved 2026-10-07 in ho/BUGS-ADM-WEB.** Was — `MediaViewer.tsx:51-56`, `:82-86`: the identity and proactive `urlExpiresOn` refresh effects aren't gated on `open`, so a closed viewer whose host keeps `file` set (the harness does) still calls `onRefresh`. | CODE_REVIEW |
| 18 | ~~LOW~~ Resolved | **Resolved 2026-10-07 in ho/BUGS-ADM-WEB.** Was — `FileUploadRateLimitedError.retryAfterSeconds` is parsed but unused — a `429` offers Retry immediately and reads like any transport failure. | CODE_REVIEW |
| 19 | LOW | Three 2026-09-28 gate-fix behaviours have no committed regression test (verified only by temporary tests during TEST): the proactive past-`urlExpiresOn` refresh, `isRenderableImageContentType()` excluding `image/svg+xml`, and `referrerPolicy="no-referrer"` on `<img>`/`<iframe>`/download `<a>`. | TEST |
| 20 | LOW | The `lib` → `components` import-direction fix has no automated guard (lint rule such as `import/no-restricted-paths`, or a test). | TEST |
| 21 | Deferred | A6 (known cost measured on a 3G boarding-diary profile) — **owner-deferred 2026-09-28** to the Phase 6 performance pass; trigger: the first screen rendering a list of `<FileThumb>` images. See HO-FIL-002 §1 Amended. | Owner / ACCEPTANCE |
| 22 | MEDIUM | HO-EMR-002 §8.2 plans its own `web/lib/api/files.ts` multipart upload, which already exists from HO-FIL-002 — amend HO-EMR-002 to use `useFileUpload`/`<FileUploader>` before it is built (owed per the 2026-09-28 owner decision on A1). | ACCEPTANCE |
| 23 | LOW | `file-errors.test.ts` has no direct assertion for the `FilePurposeNotAllowedForCaller`, `FilesInvalidPurpose` and `UnknownViolation` sentences (covered only by the exhaustive switch). | ACCEPTANCE |
| 24 | LOW | The C6 test checks the removal announcement is present, not that exactly one announcement fired. | ACCEPTANCE |
| 25 | ~~HIGH~~ Resolved | **Resolved 2026-10-09 in ho/CAT-002-photos (c9e5e702).** Was — Server-signed file urls are API-relative (`/api/files/{id}/content?...`, HmacFileUrlSigner.cs:25) and only the CAT photos page anchors them on the API base. Broken against the real API (resolve to the web origin): FileThumb.tsx:161, MediaViewer.tsx:124/139/152/165, FileList.tsx:80, visit/PhotoThumb.tsx:17, visit/panels/AttachmentList.tsx:45/55, consent/ConsentDetail.tsx:154, and the upload-response url. Fix once with a shared `resolveFileUrl` (single leading `/` only, via getApiUrl) where API responses are mapped. | CODE_REVIEW (ho/CAT-002-photos) |
| 26 | LOW | `resolveFileUrl` passes absolute `http:` through; on an https web origin an http file url is blocked as mixed content (fails closed, not a security hole). Consider https-only outside dev. | CODE_REVIEW recheck (ho/CAT-002-photos) |
| 27 | LOW | Under full `vitest run` load, the two eslint-spawning lint-rule tests (`money-guard.test.ts`, `sandboxed-email-preview.test.tsx`) time out at 60s; both pass in isolation. Unrelated to this diff. | CODE_REVIEW recheck (ho/CAT-002-photos) |

## ADR-370 review round (2026-10-18), non-blocking

| # | Severity | Item | Source |
|---|---|---|---|
| ADR370-1 | MEDIUM | `ImageVariantBackfillWorker` reads `CatEcommerceDbContext` directly. List the storage keys through a Read query or an Infrastructure/Files helper, like the other workers. | ARCHITECTURE gate |
| ADR370-2 | LOW | The startup guard does not try one direct signature. Today a missing IAM permission only falls back to app-served URLs, with a warning per file. Add a probe that logs one clear error at startup. | ARCHITECTURE gate |
| ADR370-3 | LOW | The signing time (X-Goog-Date) uses the system clock; the windows use `TimeProvider`. The library offers no public clock hook. | CODE_REVIEW gate |
