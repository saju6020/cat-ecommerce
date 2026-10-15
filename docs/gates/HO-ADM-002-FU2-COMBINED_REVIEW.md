GATE: COMBINED (ARCHITECTURE + SECURITY + CODE_REVIEW)
HANDOVER: HO-ADM-002-FU2
COMMITS: 4b4a84bb (range 5a187b6b..4b4a84bb)
DATE: 2026-10-14
STATUS: FAIL

FINDINGS
- [HIGH] web/tests/e2e/admin-functional.spec.ts:171 — E-D1 was changed from `page.goto('/staff/leave')` to `page.goto('/staff/admin/leave')`. The request-leave form (Start date / End date / Request leave) lives only on `/staff/leave` (MyLeavePage); `/staff/admin/leave` is the approvals table. Verified by running E-D1 on this branch (chromium, --workers=1, PORT=3116): it times out at `getByLabel(/start date/i)`. The change is unrelated to the picker, is not mentioned in the handover, and breaks a previously passing test. Fix: revert that line to `/staff/leave`.
- [LOW] docs/backlog/ADM.md FU2-2 — records the other 360px overflows (services table, leave inputs, admin leave filters), which is honest, but names no owner area or follow-up handover, and the "Source" is "FU2 build". Add an owner (e.g. ADM / SHP-area FE) and, for the services table, who picks it up. Also note E-B3 itself uses `/staff/admin/leave`, which FU2-2 lists as overflowing; the test is safe only because it measures the picker row, not the document.
- [LOW] docs/handover/HO-ADM-002-FU2.md §3 — lists the files changed but does not mention the E-D1 edit (see HIGH). After the revert the statement "nothing else touched" is accurate.
- [LOW] web/tests/components/branches/active-branch-picker.test.tsx — the new test asserts class names (`toHaveClass`), so it guards against someone removing the classes but proves nothing about layout; the real regression guard is E-B3. Acceptable.

CHECKS
- Header at 360px (Q1): measured on /staff/admin/leave and /staff/admin/branches with two 40-char names via a throwaway Playwright probe (deleted, not committed). document scrollWidth = 360 = innerWidth; Sign out button right edge 336px. The only elements wider than the viewport are the admin leave table, which sits inside its own scroll container. So the header and picker do not overflow; the builder's "pushed to 361px" comes from page content on other pages (forms, services table), not the picker or header layout. No header HIGH.
- Accessibility (Q2): label still associated via htmlFor/id, native select kept, options keep the full text so the value is not hidden from screen readers (accessible name of select is the selected option text; truncation is visual only), title adds a tooltip. No issue. Title is `nameFor(selectedId)` (falls back to id while branches load; benign).
- E-B3 efficacy (Q3): passes with the fix (chromium). Builder reports it fails with the fix reverted (283px overflow); not re-reverted by me, the assertions (row right <= viewport, row scroll overflow <= 0, width <= 448) are meaningful.
- State leakage (Q4): the PUTs go to the in-browser MSW session, which is dropped on full page load (the test's own comment), so no cross-test leakage; no other e2e spec asserts on the renamed branch names.
- Security: no auth, API, data or secret surface touched; frontend class and attribute change only. No findings.
- Architecture: no ADR impact; component stays within ADR-FE-016; no backend change.

SCOPE CHECK
- files in allowed scope: 5/5 (ActiveBranchPicker.tsx, component test, admin-functional.spec.ts, handover, backlog), except the unrelated E-D1 line inside the spec
- files outside allowed scope: none. web/.env.local and the node_modules junction are not tracked (git status clean; `git ls-files` shows only .env.local.example).
