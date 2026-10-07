GATE: ARCHITECTURE
HANDOVER: HO-FIL-002
COMMITS: fabdc6b c59b4a5 db613bf 4bb04e2 2d5a6ee 53606a1 c962934 f981e57 3934ce5 (reviewed as `git diff 08e873e HEAD`, focusing on the delta `c59b4a5..HEAD`, branch ho/FIL-002)
DATE: 2026-09-28
STATUS: PASS

**Second pass.** The first pass (at `c59b4a5`) failed with 1 BLOCKER, 5 MEDIUM and 3 LOW. This pass
re-read HO-FIL-002 §1 (every Amended row, including the two 2026-09-28 rows), §13 and §14. It
re-read every file changed since `c59b4a5` and re-checked the full branch diff against the
first-pass convention checks.

This gate built and ran nothing. Every runtime claim below is for TEST. That includes:
- the production `notFound()` behaviour under a real `next build`;
- the proactive `urlExpiresOn` refresh;
- the fileId-keyed one-shot guard;
- the 401/403/413/502 routing.

Owner rule (2026-09-27): only BLOCKER or HIGH fails a gate. The MEDIUM and LOW findings below belong
in `docs/backlog/FIL.md`.

RESOLUTION OF FIRST-PASS FINDINGS
- **BLOCKER (harness route in §14 forbidden scope): resolved per the recorded owner decision.**
  - §1 Amended "2026-09-28 — owner decision" authorises `web/app/dev/file-storage-harness/` as a development-only test page. It is to be deleted when the first consuming screen lands.
  - `page.tsx:31-36` calls `notFound()` during render when `NODE_ENV === 'production'`, with a `return null` fallback. There is no effect.
  - The live uploader and the `@/mocks/handlers/files/{gate,fixtures}` imports now live only in `HarnessContent.tsx`. It is reached through `React.lazy(() => import('./HarnessContent'))` (`page.tsx:6`), and `page.tsx` has no static mock import.
  - `tests/unit/files/file-storage-harness-page.test.tsx` covers both branches.
  - That matches option (a) of the first pass exactly. Playwright runs `npm run dev`, so a real production build is not exercised by any test. TEST should confirm that `/dev/file-storage-harness` returns 404 under `next build && next start`.
- **MEDIUM (`lib` → `components` inversion): fixed.** `fileRules.ts` and `fileErrors.ts` moved to `web/lib/files/`. No `@/components` import remains anywhere under `web/lib/`; the only matches are doc comments. The move is recorded in §1 Amended (gate-fix item 1).
- **MEDIUM (XHR 401/403 bypassed the HO-ACC-002 session surface): fixed.**
  - `lib/api/files.ts` now handles each status explicitly:
    - A `401` goes through `notifySessionError` into the same module-private `sessionErrorHandler` that `apiFetch` uses.
    - A `403 csrf_token_invalid` becomes a distinct `FileUploadForbiddenError`.
    - A `403` `ValidationResponse` stays a validation error.
  - No second session surface or auth path was introduced.
- **MEDIUM (`urlExpiresOn` trigger of §8.4/ADR-FE-154 not built): fixed.** `FileThumb.tsx` and `MediaViewer.tsx` both check `urlExpiresOn` in an effect before loading. The `fixtures.ts:65-67` comment is now accurate.
- **MEDIUM (ADR-FE-158 SVG not enforced by the viewer): fixed.** Both components use `isRenderableImageContentType()` (`lib/files/fileRules.ts`), which excludes `image/svg+xml`.
- **MEDIUM (PDF `<iframe>` refresh-once gap): unchanged, still recorded.** It is carried below.
- **LOW (413/502 lose their violation code): fixed.** Any status not special-cased now parses `ValidationResponse`. Unknown codes map to `UnknownViolation` and are never shown verbatim.
- **LOW (package-lock.json churn): fixed.** Commit `c962934` reverts it, and `git diff 08e873e HEAD -- web/package*.json` is empty.
- **LOW (`PhotoField.tsx` second upload control, A1): unchanged, still recorded.** It is carried below.

NEW OUT-OF-§13 FILE: `web/lib/api/client.ts` (`notifySessionError`): acceptable.
- It is recorded in §1 Amended (gate-fix item 4, "A further file outside §13").
- The change is additive. It exports a four-line function that forwards to the existing module-private `sessionErrorHandler`, and swaps `apiFetch`'s own call site to that function. `apiFetch` behaves exactly as before.
- It does not violate §14 "A second HTTP client or auth path": no new client, no new handler registration, and `sessionErrorHandler` stays private.
- It is the minimal seam needed for the first-pass MEDIUM. The file belongs to HO-ACC-002, so the integrator should know about it when merging. That is not a finding.

FINDINGS
- [MEDIUM] web/components/files/MediaViewer.tsx:105-116: the refresh-once logic of ADR-FE-154 still does not cover `<iframe>` PDFs.
  - The gap is declared in §1 Amended (2026-09-27, item 3) and in the ADR-FE-154 Status.
  - It becomes a live defect the first time HO-EMR-002 passes a lab PDF through `<MediaViewer>`. That handover must pick it up. Backlog.
- [LOW] web/components/cat/PhotoField.tsx (not in diff): a second upload control still exists, so A1 ("one uploader, every area uses it") is not met until the Cat Profiles photo page is converted. Not converting it here was correct, because that is a page under §14. Carry it to ACCEPTANCE and the backlog.
- [LOW] web/app/dev/file-storage-harness/page.tsx, HarnessContent.tsx: the owner's condition "delete once a real consuming screen exists" is recorded only in HO-FIL-002 §1 and in a code comment.
  - No consuming handover (HO-SHP-002, HO-EMR-002, HO-BRD-002, HO-RPT-002) carries the obligation to delete the route and re-point `tests/e2e/files-*.spec.ts`.
  - Record it in `docs/backlog/FIL.md` so the first consumer inherits it.

CONVENTION CHECKS (re-run on the full diff, clean)
- **One fetch site:** there is no `fetch(` in `app/`, `components/` or `lib/` additions. The one XHR is `lib/api/files.ts`, the exception that §7 and ADR-FE-150 permit.
- **Mocks in shipped code:** `@/mocks` is imported only by `HarnessContent.tsx` (the lazy, dev-only branch) and by the pre-existing `app/providers.tsx` dynamic import.
- **Forbidden content:** there is no `localStorage`, `sessionStorage`, `indexedDB`, `dangerouslySetInnerHTML` or bucket URL construction. The components render only the host-supplied signed `url`.
- **Dependencies:** there is no new dependency, and `package.json` and `package-lock.json` show no net change against `08e873e`.
- **`web/docs/decisions.md`:** the file is purely additive against the baseline (0 removed lines). The changes since `c59b4a5` touch ADR-FE-152/154/157/158 only, all within this slice's ADR-FE-149…158 range.
- **`docs/handover/HO-FIL-002.md`:** only the `Amended` rows of §1 changed. No section body was edited.
- **Contracts:** the HO-FIL-001 §12.4 contract is unchanged, and `purpose` is still a required prop.
- **Dialog:** `MediaViewer` still uses the shared `components/ui/dialog.tsx`.

SCOPE CHECK
- files in allowed scope: 34/34. That counts:
  - Literal §13 (9):
    - `web/components/files/*` (4)
    - `web/mocks/handlers/files/*` (4)
    - `web/docs/decisions.md` (1)
  - `web/tests/**` (14), following the HO-CAT-002 precedent.
  - Recorded §1 Amended exceptions (6):
    - `web/lib/api/files.ts`
    - `web/lib/hooks/useFileUpload.ts`
    - `web/lib/files/fileRules.ts`
    - `web/lib/files/fileErrors.ts`
    - `web/mocks/handlers/areas/file-storage.ts`
    - `web/lib/api/client.ts`
  - Owner-authorised 2026-09-28, development-only (2):
    - `web/app/dev/file-storage-harness/page.tsx`
    - `web/app/dev/file-storage-harness/HarnessContent.tsx`
  - `docs/handover/HO-FIL-002.md`, §1 Amended rows only (1).
  - Gate records `docs/gates/HO-FIL-002-{ARCHITECTURE,SECURITY}.md` (2).
- files outside allowed scope: none

VERDICT: PASS (second pass). There are 0 BLOCKER, 0 HIGH, 1 MEDIUM and 2 LOW findings. The first-pass BLOCKER is resolved in the form the owner recorded on 2026-09-28. Four of the five first-pass MEDIUMs and two of the three LOWs are fixed. The rest are recorded for `docs/backlog/FIL.md`.
