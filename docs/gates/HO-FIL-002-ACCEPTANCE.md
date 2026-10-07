GATE: ACCEPTANCE
HANDOVER: HO-FIL-002
COMMITS: fabdc6b c59b4a5 560d5ce 62f7b5e db613bf 4bb04e2 2d5a6ee 53606a1 c962934 f981e57 3934ce5 5c0d8a3 7b345db 211661f c553405 42e6433 9846062 (branch ho/FIL-002, head 9846062)
DATE: 2026-09-28
STATUS: PASS

This is the second pass. The first pass (42e6433) was BLOCKED on two HIGH handover defects:
§18 A1 and §18 A6 could not be evaluated as written. The owner ruled on both on 2026-09-28. The
ruling is in commit 9846062 as a new HO-FIL-002 §1 Amended row, "2026-09-28 — owner decisions on
HO-FIL-002-ACCEPTANCE". The same commit amends the §18 A1 and A6 rows and changes docs/backlog/FIL.md
rows 21-24.

Code is unchanged since the first pass. `git diff --stat c553405 HEAD` touches only
docs/backlog/FIL.md, docs/gates/HO-FIL-002-ACCEPTANCE.md and docs/handover/HO-FIL-002.md.
`git diff --name-only c553405 HEAD -- web` is empty.

Owner rule (2026-09-27): only BLOCKER/HIGH fail a gate. MEDIUM/LOW items are listed below for
docs/backlog/FIL.md. This gate has not edited that file.

PRIOR GATES (read from docs/gates/ and docs/test-cases/)
- ARCHITECTURE: PASS (2nd pass).
- SECURITY: PASS (2nd pass).
- CODE_REVIEW: PASS.
- TEST: PASS. docs/test-cases/TC-HO-FIL-002.md has 29 rows: 27 PASS, 0 FAIL, 2 BLOCKED, 0 NOT RUN. The 2 BLOCKED rows are A1 and A6, and they were judged against the pre-amendment wording.
- Re-run by this gate at 9846062: `npx vitest run tests/components/files tests/unit/files` gave 9 files and 71 tests, all passed. Playwright and builds were not run, as instructed. The e2e and prod-build evidence comes from the TEST record, as on the first pass.

OWNER DECISIONS (checked)
- The decisions are recorded in HO-FIL-002 §1 Amended, dated and "confirmed 2026-09-28". The commit author is shahjahan (repo owner).
- A6 names the owner ("the project owner"), the trigger ("the first screen that renders a list of `<FileThumb>` images") and the fix owner (HO-FIL-001 OQ-5). OQ-5 exists at HO-FIL-001:240 ("Owner; Accept at launch; revisit with a resizing step in this slice").
- The owed items can each be traced to both a handover row and a backlog row:
  - HO-EMR-002 §8.2 amendment: §1 Amended item (a) and backlog #22 (MEDIUM). HO-EMR-002:165 still plans its own `lib/api/files.ts`. It is correctly marked as owed, not done.
  - PhotoField conversion: §1 Amended item (b) and backlog #12. `web/components/cat/PhotoField.tsx` still exists.
  - A6: §18 A6, §1 Amended item (2) and backlog #21, which now reads "owner-deferred 2026-09-28" and points to §1 Amended.

CRITERIA (HO-FIL-002 §18, as amended 2026-09-28)

| # | Result | Evidence |
|---|---|---|
| A1 | MET (as amended) | "One uploader exists": `<FileUploader>`/`useFileUpload` are the shared control. There is a single XHR multipart path, in `lib/api/files.ts`, per the ARCHITECTURE p2 convention check. No second HTTP client (§14). TC rows C1-C10 are all PASS (TC-HO-FIL-002 lines 94-103). "Each consuming handover's own ACCEPTANCE verifies that it adopts it": by the owner's amendment this is deliberately evaluated in each consumer's own gate, not here. The two known non-adopters are recorded as owed: HO-EMR-002 §8.2 (backlog #22) and PhotoField (backlog #12). SHP and BRD stay upload-free by owner decision. |
| A2 | MET | Unchanged from the first pass. `file-uploader.test.tsx` C1/C6/"ADR-FE-153", `use-file-upload.test.tsx` removal announcement, `media-viewer.test.tsx` C7, and e2e `files-upload.spec.ts` C1/E1 and `files-a11y.spec.ts` (40 executions, axe clean, TEST record). Unit suites re-run green today. |
| A3 | MET | Unchanged. `file-errors.test.ts` asserts the verbatim §8.2 sentences. C3 in `file-uploader.test.tsx`: the sentence is shown and the raw text is absent. `files-api-error-routing.test.ts`: 413/502 keep their code; an unknown code becomes `UnknownViolation`. `lib/files/fileErrors.ts` has an exhaustive `never` switch, and tsc was clean in TEST. |
| A4 | MET | Unchanged. `file-errors.test.ts` "FileScanUnavailable reads as a service state" (exact sentence, no "you/your"). C4 in `file-uploader.test.tsx` and `use-file-upload.test.tsx`: the row stays queued, and Retry leads to `onUploaded`. |
| A5 | MET (image/video; PDF carried) | Unchanged. C8 in `file-thumb.test.tsx` (two cases) and `media-viewer.test.tsx`. Same-`fileId` new-bad-URL regression cases. The PDF `<iframe>` gap is declared in §1 and carried as backlog #11. |
| A6 | MET BY OWNER DEFERRAL (not measured) | §18 A6 now names the owner's 2026-09-28 deferral as its evidence. That deferral exists (§1 Amended item 2) and has an owner, a trigger and a fix owner (HO-FIL-001 OQ-5). No measurement has been taken. The cost is still unmeasured until the Phase 6 pass fires on its trigger (backlog #21). |

FINDINGS
- No BLOCKER or HIGH findings. The two first-pass HIGHs (A1, A6) are resolved by the recorded owner decisions.
- [LOW] docs/test-cases/TC-HO-FIL-002.md:135,140 — the A1 and A6 rows still show BLOCKED against the pre-amendment §18 wording. The TEST gate was not re-run for the amended criteria. This needs no code change, but the sheet no longer matches §18.
- [LOW] docs/backlog/FIL.md row 12 — still says A1 "holds only once the cat photo page is converted". That wording is stale against the amended A1. It should cite the 2026-09-28 owner decision (§1 Amended item 1b) as #22 does.
- [LOW] docs/handover/HO-FIL-002.md §18 A1 — "One uploader exists" is literally true only of the shared components while `web/components/cat/PhotoField.tsx` remains. The owner accepted this knowingly and recorded the conversion as owed. It is not a gate failure. This gate notes that the claim becomes fully true only when backlog #12 closes.
- Carried, not re-litigated: backlog #11, #13-#20 and #22-#24. Rows 23 and 24 are this gate's first-pass LOWs. Row 22 is its first-pass MEDIUM.

OUT-OF-SCOPE CHECK (§4, §14)
- Unchanged from the first pass because the code is unchanged. No cropping, editing or gallery. No bucket URL construction. No SVG/HTML rendering. No second HTTP client. No new dependency. The only route is the owner-authorised dev-only harness (prod `notFound()`, 404 in the TEST prod build).
- §20: no condition was hit and worked around.

UNDECLARED DEVIATIONS
- None. The 9846062 delta is documents only. It matches exactly what the owner decision describes: §1 row, §18 A1/A6 rows, backlog 21-24.

SCOPE CHECK
- files in allowed scope: 39/39. These are the 38 files from the first pass plus this gate's own record, docs/gates/HO-FIL-002-ACCEPTANCE.md. 9846062 changed only files already counted: the handover's §1/§18 rows and backlog/FIL.md.
- files outside allowed scope: none
