GATE: CODE_REVIEW (combined follow-up: architecture + security + code, round 2)
HANDOVER: HO-EMR-002
COMMITS: c5e1df81 c9e8f016 5e003b60 5afcc36d (delta ff034c1d..5afcc36d; master merge 3258ad8d not reviewed)
DATE: 2026-10-09
STATUS: PASS

Checked: `npx tsc --noEmit` clean; `vitest run tests/components/visit tests/components/consent` 12 files, 115 tests pass.

ROUND-1 HIGH STATUS
- ARCH HIGH staff binary downloads lacked X-Branch-Id: CLOSED. `web/lib/api/visits.ts:371-397` `fetchStaffBlob` sends the cookie and `X-Branch-Id`, maps 401/403/404 like apiFetch (GET, no CSRF needed). `DischargeSummaryView.tsx` fetches the PDF as a blob, object URL revoked after 1 s; `useStaffBlobUrl.ts` revokes on path change and unmount; ConsentDetail shows loading/error fallbacks. The owner portal keeps its plain link. Tests: fix-round-1.test.tsx:71-136 (header asserted, revoke asserted, failure states).
- CODE HIGH blank result sent as 0: CLOSED. `ResultEntryForm.tsx:101-113` blocks blank value/name/unit per row and passes `error` to the row. Tests :138-165 (blank not saved; real 0 still sent).
- CODE HIGH vitals wiped by failed refetch: CLOSED. `QuickVitalsDialog.tsx:122-131` renders data first, error only when there is no data. Test :167-190.
- CODE HIGH consent signature lost: CLOSED (silent loss). `ConsentCaptureForm.tsx` keeps both panels mounted (`hidden`), validates and submits against the live pad (`getStrokes`), never submits an empty signature. Kind change still rebuilds the pad (backlog 23) but the user is told. Tests :192-282.

NEW FINDINGS
- [MEDIUM] useConsultationAutosave.ts send(): keepalive send is not ordered after an older in-flight PUT (backlog 21).
- [MEDIUM] declared deviations still open, consent link broadened (backlog 22).
- [LOW] ConsentDetail.tsx signaturePath: absolute URL gives neither image nor message (backlog 24).
- [LOW] ConsentCaptureForm.tsx: kind change discards strokes; hidden-mount canvas sizing (backlog 23).

ROUND-1 MEDIUM/LOW
- Closed: autosave replay of failed payload, keepalive queued behind in-flight, note vs draft ordering (settle/generation), problem-lookup stale closure, autosave coverage, E-A5 flake, ADR-FE-049/051/055/057 status lines, consent entry point.
- Open, now backlog rows 21-27 in docs/backlog/EMR.md: VitalsForm nowInZone, HistoryItem slice, versionRef, plus the deviations.

SCOPE CHECK
- fix commits touch only web/ and docs/test-cases/TC-HO-EMR-002.md; no .ai/, src/, or backend tests changed by the branch's own commits (the master merge brings src/ and .ai/ changes, not reviewed)
- ADR-FE-* only in web/docs/decisions.md
- files outside allowed scope: none
