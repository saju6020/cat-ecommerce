GATE: CODE_REVIEW (combined follow-up: architecture + security + code)
HANDOVER: HO-CAT-002-PHOTOS (frontend follow-up, branch ho/CAT-002-photos)
COMMITS: be4f26ef (range 4a4acb88..be4f26ef)
DATE: 2026-10-09
STATUS: PASS

CHECKS (run in web/ at be4f26ef)
- npx tsc --noEmit: clean
- npm run lint (--max-warnings 0): clean
- vitest run --maxWorkers=3: 181 files, 1140 tests passed
- next build: succeeds; no /dev/file-storage-harness route remains

FINDINGS (this change)
- [MEDIUM] web/tests/unit/cat/photos-page.test.tsx — no test for the 10-photo cap (uploader hidden, message shown), the expired-URL re-read via onRefresh/refetch, or a `//host` server url (anchored to the API base, not protocol-relative). Line 60 asserts only `stringContaining('/api/files/')`, which passes for relative AND absolute urls, so it does not prove anchoring (line 87 does, for the attached photo only).
- [LOW] web/app/(staff)/staff/cats/[catId]/photos/page.tsx:22 — `startsWith('/')` also matches `//evil`; safe only because getApiUrl prefixes the (mandatory, non-empty) NEXT_PUBLIC_API_BASE_URL, giving `https://api//evil`. Correct but accidental; a shared helper with an explicit single-slash rule and a test would make it intentional. Absolute urls pass through unchecked (needed for GCS, ADR-318).
- [LOW] web/docs/decisions.md:1324 — ADR-FE-164 consumed, but docs/handover/README.md:106 still says "next new area starts at ADR-FE-164"; update the counter or a later area will collide. No existing 164 on master or other ho/* branches (ADM-002-FU1 uses 170-172).
- [LOW] page.tsx — attach failure keeps the chip (ADR-FE-164 intent) but a 30-minute-window refusal then offers only retry-of-the-same-dead-file until the user removes it; acceptable, message tells them to upload again.

VERIFIED OK
- Contract: AddCatPhotoRequest {FileId, IsPrimary} matches the new client body; no storageKey on the wire. Handler returns PhotoFileNotEligible (one code for all ineligible reasons), PhotoLimitExceeded, deceased (EnsureAlive) as 400 ValidationResult; describeApiError shows the server message. Primary logic: server forces primary when the cat has none, so isPrimary = photos.length===0 is consistent; page never demotes the existing primary.
- Security: attach goes through the existing client (CSRF, X-Branch-Id); storage key/type/size/name never supplied by the browser; url anchoring cannot escape the API origin.
- Deletions: no remaining references to PhotoField (only a comment in FileThumb.tsx:121), the harness page, or __fileUploadGates in web/.
- Scope: web/**, docs/handover/HO-CAT-002.md, docs/test-cases/TC-HO-CAT-002.md only. No src/, .ai/ or migration change.

WIDER CHECK — server-signed urls are API-relative (`/api/files/{id}/content?...`, HmacFileUrlSigner.cs:25) and are NOT anchored elsewhere. Against the real API each of these resolves against the web origin (404). Recorded in docs/backlog/FIL.md row 25 (HIGH, outside this diff):
- web/components/files/FileThumb.tsx:161 (img src) — used by FileList, HistoryItem.tsx:104, PhotoThumb, this page
- web/components/files/MediaViewer.tsx:124,139,152,165 (iframe/img/video src, download href)
- web/components/files/FileList.tsx:80 (href)
- web/components/visit/PhotoThumb.tsx:17 (photo.url straight into StoredFileViewModel; EMR visit photos)
- web/components/visit/panels/AttachmentList.tsx:45,55 (diagnostic result href)
- web/components/consent/ConsentDetail.tsx:154 (consent.file.url href); signature at :145 is a blob fetch via the client, fine
- Upload response url (lib/api/files.ts StoredFileViewModel.url) is also relative, so any preview of a just-uploaded file has the same problem
- Not affected: shop/catalogue images (ProductImageRef.Url is null server-side today, placeholder shown); portal cat pages render no photos (CatProfileHeader placeholder icon); CaptchaField/CageCard images use their own paths.
Right fix: one shared helper (e.g. `resolveFileUrl(url)` in lib/api/files.ts using getApiUrl, single leading `/` only) applied where the API response is normalised (files.ts mapper / clinical-history / consents / diagnostics / visits readers), or inside FileThumb/MediaViewer/FileList; then drop the local toFileViewModel anchoring in this page.

SCOPE CHECK
- files in allowed scope: 22/22
- files outside allowed scope: none
