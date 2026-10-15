GATE: CODE_REVIEW (recheck of FIL backlog row 25, HIGH)
HANDOVER: HO-CAT-002 (photos)
COMMITS: 338c36c0..816a14f9 (c9e5e702, af0fcbd4, 816a14f9)
DATE: 2026-10-09
STATUS: PASS

FINDINGS
- [LOW] web/lib/api/files.ts:resolveFileUrl — absolute `http:` is accepted; mixed-content blocked on https origin (fails closed). Backlog FIL row 26.
- [LOW] web/tests (money-guard, sandboxed-email-preview) — eslint-spawning tests time out at 60s under full-suite load, pass alone. Backlog FIL row 27.

NOTES
- resolveFileUrl: null/empty -> null; `//host` and `/\host` -> null; single leading `/` -> getApiUrl (prefix on API base, so cannot change host); other values must parse via URL() with protocol http/https, else null (javascript:, data:, file:, leading-space `//host`, relative-without-slash all rejected). Correct.
- Unusable url maps to '' and every consumer guards on it: FileThumb, MediaViewer, FileList, AttachmentList, ConsentDetail render no link/img.
- Row 25 call sites covered: FileThumb/MediaViewer/FileList (via mappers + empty guard), visit PhotoThumb (getVisit maps url+thumbnailUrl), AttachmentList (getDiagnosticOrder), ConsentDetail (getConsent), clinical-history sections, upload response (uploadFile), cat photos (getCat). Photos page's local anchoring removed in favour of the shared one. Refresh paths refetch through the same mappers.
- Tests added: resolve-file-url, file-url-mapping, unusable-url, photos-page.

CHECKS (web/)
- npx tsc --noEmit: exit 0
- npm run lint: exit 0 (needs NODE_OPTIONS=--max-old-space-size=8192; default heap crashed once)
- npx vitest run: 182/184 files, 1167/1170 tests passed; 3 failures are timeouts in the 2 lint-rule tests above; rerun alone: 2 files, 23 tests pass.
- Playwright not run (as instructed).

SCOPE CHECK
- files in allowed scope: all changed (web/lib/api, web/components/files|consent|visit, photos page, tests, docs)
- files outside allowed scope: none
