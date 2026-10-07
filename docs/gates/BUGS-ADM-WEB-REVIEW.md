GATE: CODE_REVIEW (combined: architecture, security, code)
HANDOVER: BUGS-ADM-WEB
COMMITS: e7c5429 7987c4a 4b555b8 dda7fc9 5cc7a56 18fcbaf c123ad7 92e08f4 61fd9d7 c90b789 426693b 57b40ab 8d36607 100cbd3 f758bde ad9405e ac304c7 e652ff5
DATE: 2026-10-07
STATUS: PASS

RUN: tsc --noEmit clean; eslint --max-warnings 0 clean; vitest --maxWorkers=3 145 files / 762 tests passed.

SECURITY CHECKS
- sanitizeNextPath: tried //x, /\x, /<TAB>/x, /book/..//evil, /book/%2e%2e//evil, /portal/../..//evil, absolute URLs, control chars: all fall back. Callers (customer sign-in, staff sign-in '/staff/', cats/[id], bookingReturnPath.safeBookingPath on master) are unaffected: valid values are returned unchanged.
- LimitedMarkup: regex rejects //, /\, tab/CR/LF; https-only otherwise. OK.
- PolicyAcceptancePrompt fails closed (modal, no dismiss, sign-out reachable). OK.
- ADM 12: restore is keyed on the branch id only; useMyBranches discards a restored branch the user is not assigned to. No data is carried, only an id (see MEDIUM 1).
- URL hooks: dedup compares against pendingParams / last pending write, `page` removal still counts as a change, debounce clear sits only in the external-navigation branch. No contract regression found for the 8 callers on master (identical on ho/BUGS-APT-WEB and ho/BUGS-SHP-WEB).
- ADM 30: derived-primary fix is correct (pure updater, effective primary derived at render and at save).

FINDINGS
- [MEDIUM] web/lib/branch.ts:clearQueryCachePreservingBranch — after staff A signs out and staff B signs in, B's first requests (including my-branches itself) carry A's X-Branch-Id until useMyBranches re-seeds. The server must refuse an unassigned branch, but B can see spurious BranchNotAssigned 403s in that window. Suggested: drop the preserved branch on sign-in when the user id differs, or only preserve on idle-lock/redirect, not on explicit sign-out.
- [LOW] web/components/files/MediaViewer.tsx:pdfFrameRef — browsers essentially never fire `error` on an iframe for HTTP failures, so FIL 11 is largely inert; the proactive urlExpiresOn check remains the real protection (the commit comment says so).
- [LOW] web/app/(staff)/staff/admin/audit-log/page.tsx — useDebouncedValue delays every query change (selects, paging) by the typing window, not only text fields; defaultFrom/defaultTo are memoised at mount and go stale across midnight.
- [LOW] web/components/branches/StaffBranchAssignmentPanel.tsx — un-assigning then re-assigning the previously chosen primary silently restores it as primary (old state kept). ADM 30 test cannot fail on old code (known caveat).
- [LOW] web/components/staff/IdleLock.tsx — when unmounted mid-resolution the cache is not cleared at all; harmless because the next guard re-checks the session.

MERGE FORECAST
- git merge-tree against master (with BUGS-NTF and BUGS-APT-BE): clean, no conflicts.
- Against ho/BUGS-SHP-WEB, ho/BUGS-APT-WEB, ho/BUGS-NTF each separately: clean (ScheduleChangeDialog.tsx edits do not overlap S16).

SCOPE CHECK
- files in allowed scope: 55/55
- files outside allowed scope: none
