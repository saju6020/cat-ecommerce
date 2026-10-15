# HO-ADM-002-FU2 — Administration: active-branch picker overflow (frontend follow-up)

## 1. Identity

| Field | Value |
|---|---|
| Handover ID | **HO-ADM-002-FU2** (follow-up slice to [`HO-ADM-002`](HO-ADM-002.md), after [`HO-ADM-002-FU1`](HO-ADM-002-FU1.md)) |
| Title | Fix the shared `ActiveBranchPicker` overflowing on phone-width screens |
| Date | 2026-10-14 |
| Backlog | [`docs/backlog/ADM.md`](../backlog/ADM.md) row **FU2-1** (resolved), FU2-2 (new, LOW). Origin: RPT backlog row **89** on branch ho/RPT-002 |
| Origin | RPT-002 TEST gate: with branch names of about 22 characters the picker's right edge was 370 px at 360 px, so staff pages scrolled sideways. Owner decision 2026-10-14: "Yes, small ADM-002 follow-up" |
| SRS | UI-1 (no sideways scroll at 360 px), NFR-USA-4 |
| Builds on | HO-ADM-002 §11.2, ADR-FE-016 (picker behaviour unchanged) |
| Pairs with | None. **No backend, no API, no MSW handler change** |
| Baseline commit | `5a187b6b` (master) |
| Gate level | One combined review for a follow-up slice (owner's gate-scope rule); tiny fix |
| Amended | *(append rows here; never edit above)* |

## 2. Change

`web/components/branches/ActiveBranchPicker.tsx` only (markup classes plus a `title`):

- Row: `w-full min-w-0 max-w-md` (it was an unconstrained flex row).
- Label: `flex-shrink-0`, so "Branch" never wraps or collapses.
- Select: `min-w-0 flex-1 truncate`, so it shrinks to the row and cuts long text with the browser's own clipping.
- `title` on the select carries the full name of the selected branch. Each `<option>` still holds the full name, so the list and screen readers read it in full.
- Keyboard and screen-reader behaviour is unchanged: same `<label htmlFor>`, native `<select>`, same focus ring, same `onChange`.

No ADR is added: this is a class-level fix inside ADR-FE-016's component. The next free FE id was not consumed (RPT-002 129–138, XCT-002 139–148, SRG-002 069–078 are untouched).

## 3. Tests

| Test | File |
|---|---|
| Component: 40-character name appears in the option text and `title`; the row, label and select carry the shrink classes; selecting still updates the active branch and the `title`. jsdom has no layout, so the class assertions are a regression marker only and **E-B3 is the real overflow guard** | `web/tests/components/branches/active-branch-picker.test.tsx` |
| E-B3 (Playwright, MSW): 40-character names written through the admin PUT; at 360/768/1024/1440 the picker row is inside the viewport, at most `max-w-md`, and has no internal overflow. With the fix reverted (and the `title` assertion removed so it reaches the geometry check) it fails: "picker row overflow at 360, received 283" | `web/tests/e2e/admin-functional.spec.ts` |

E-B3 measures the picker row rather than `documentElement.scrollWidth`, because other staff pages overflow at 360 px for reasons of their own (backlog FU2-2). On a page free of those, the document-level check would be equivalent.

## 4. Please double-check visually

- At 360 px with a long name the select shows a clipped name (no ellipsis in some browsers; `text-overflow` on a native select varies). The full name is in the tooltip and the option list.
- At 640 px and wider the row stops at 28 rem (`max-w-md`) and sits left-aligned above page content, as before.
