# Accounts & Identity — UX

The clickable design for backlog §1 *Accounts and Identity*, built against
[`HO-ACC-002`](../../handover/HO-ACC-002.md), the frontend handover. Second area designed after
[Cat Profiles](../cat-profile/README.md) — the first to draw on the formalised
[PurrCare design system](#the-design-system) rather than inventing its own palette.

## Where to review it

**Clickable canvas:** <https://claude.ai/artifact/UbbZjiv8L5VJitNa4VANDV>

That is the version to look at — twenty artboards, five rows, in the same shape HO-ACC-002 §3.2's
screen table lists them. It is private to the owner; others need it shared from the page's Share
menu.

If the link is ever lost: `/artifacts` in the Claude Code terminal lists artifacts you own (`o`
opens, `c` copies the link), or browse <https://claude.ai/code/artifacts>.

## Where the source is

The `.dc.html` files in this folder are the **source of truth**, committed so the design survives
independently of the hosted link and changes show up in `git diff`.

**They do not open standalone in a browser.** Each one loads `./support.js` from the canvas
runtime, which is not in this repo. Read them as source; view them on the canvas.

## The screens

Five rows, twenty artboards — more than Cat Profiles' fourteen, because this area's staff-admin
surface (accounts, roles, duplicates, merge) is genuinely larger than one cat's record.

| Row | File | Screen | Shows |
|---|---|---|---|
| Register & sign in | `Register.dc.html` | Registration | US-ACC-01: mobile/email both-optional line, password requirements before typing, the CAPTCHA with its reception alternative |
| | `RegisterSuccess.dc.html` | Success | "Your account is ready" — no mention of verification, codes, or checking email |
| | `SignIn.dc.html` | Customer sign-in | US-ACC-04: the one non-disclosing error, identical for every failure |
| | `ForgotPassword.dc.html` | Forgot password | US-ACC-05: the client-owned confirmation, rendered the same whether or not an account matches |
| | `ResetPassword.dc.html` | Reset password | Valid-link form and **the invalid/expired-link state**, side by side |
| Customer account | `Profile.dc.html` | Account → Profile | US-ACC-06: read-only email/mobile **with the reason stated**, additional numbers |
| | `Addresses.dc.html` | Account → Addresses | Default badge; removing the default while others exist shows the server's reason |
| | `Notifications.dc.html` | Account → Notifications | ⭐ the preference matrix — a real `<table>`, "Always on" cells disabled with text, never colour alone |
| | `DataAndDelete.dc.html` | Your data / Delete | US-ACC-13: export card, and the deletion confirmation stating **exactly** what happens and what is kept |
| | `AccountDeleted.dc.html` | Deleted confirmation | The `(auth)` page after deletion, no identity |
| Staff sign-in & idle lock | `StaffSignIn.dc.html` | Staff sign-in | US-ACC-14, with "Set or reset password" for a new staff member's first password |
| | `IdleLock.dc.html` | Idle lock | ⭐ US-ACC-08: the modal over an **unsaved form still visible behind it**, plus the 2-minute warning state |
| | `StaffAccountsList.dc.html` | Staff accounts | US-ACC-07: paginated, searchable, "Password set" column |
| | `StaffAccountCreate.dc.html` | Add staff account | ⭐ role checkboxes + the capability preview, captioned "Indicative — from SRS Appendix D" |
| | `StaffAccountDetail.dc.html` | Staff account detail | Removing a role confirms and names the consequence (signed out) |
| Staff — customers & duplicates | `WalkIn.dc.html` | Walk-in registration | US-ACC-10: the existing-customer notice on a duplicate mobile number |
| | `CustomerDetail.dc.html` | Customer detail | The *Incomplete* badge (icon + word) and the online-setup-unavailable note |
| | `Duplicates.dc.html` | Duplicates report | US-ACC-11: each group's cats, deceased included, the not-yet-recorded notice |
| | `MergeReview.dc.html` | Merge review | ⭐ nothing pre-selected, every cat that will move named, "This cannot be undone" |
| 360px floor check | `RegisterMobile360.dc.html` | Register at 360px | UI-1 — the busiest form in the area, no horizontal scroll |

## The design system

Built from the [PurrCare design system](https://claude.ai/artifact/Y2WzXo2hnfc7a9ac7GHNvN) —
formalised from Cat Profiles' palette in this same session. This canvas hand-transcribes its
token values rather than installing it live on the canvas (a deliberate scope cut, see below); the
values below are that system's tokens, verbatim:

| Token | Value | Use |
|---|---|---|
| Display | **Newsreader** 600 | Page and dialog titles |
| Body | **Public Sans** 400/600/700 | Everything else |
| `surface-page` | `#FAF9F6` | Page background |
| `surface` | `#FFFFFF` | Cards |
| `ink` / `ink-muted` | `#1C1A17` / `#5C574F` | Text |
| `border` | `#E5E1D8` | Card and row rules — this system separates with rules, not shadows |
| `accent` | `#15605A` | Primary actions, links, Current |
| `warning-bg`/`warning-ink` | `#FBF0DC` / `#6E4700` | Due-soon |
| `danger-bg`/`danger-ink` | `#FBE9E7` / `#8F1D1D` | Overdue, high-risk, destructive |

Every text/background pair clears 4.5:1.

## Rules the design encodes

1. **The idle lock never clears an unsaved form.** `IdleLock.dc.html` shows the page still mounted
   behind the modal — ADR-FE-010's whole point is that re-authenticating restores the person to
   exactly where they were.
2. **One non-disclosing error, everywhere.** Sign-in failure, forgot-password confirmation and the
   reset-link outcome each render one message regardless of the real cause (ADR-FE-007) — a
   stranger can never learn whether an identifier is registered.
3. **Confirmations name the consequence.** Removing a role, an address or merging two customers
   each state what changes and what does not, never a bare "Are you sure?" (UI-3).
4. **Nothing pre-selected on a merge.** `MergeReview.dc.html`'s survivor radio group starts empty —
   deliberately, every time (ADR-FE-011).
5. **What isn't recorded says so.** Orders, invoices and balances appear nowhere as a blank column
   or a zero; the duplicates report and the merge review both carry the not-yet-recorded notice in
   words (ADR-FE-012).
6. **Read-only fields explain themselves.** Profile's email and mobile fields are locked with a
   reason next to them, not just greyed out (NFR-USA-4).
7. **Status is never colour alone.** The Incomplete badge, the preference matrix's "Always on"
   cells, and every confirmation icon all carry a word.

## Known gaps, deliberately visible

- **No live interaction.** Unlike some of Cat Profiles' artboards, none of these are wired up —
  every state shown is a separate artboard, not a working toggle. The screen count already covers
  most of what a click would reveal; wiring it up is a fair follow-up, not done here.
- **The design system isn't installed on the canvas.** Token values are hand-transcribed instead
  of linked live, so the canvas won't pick up a future edit to the design system automatically —
  see "The design system" above.
- **The capability matrix preview is illustrative**, not the real `GET /api/staff-roles` data —
  HO-ACC-002 §11.10 requires the real API response; no backend exists yet to call.
- **No design exists yet for what Cat Profiles' navigation gains** once accounts ships (a "Later"
  item becoming real) — that's Cat Profiles' own canvas to update, not this one's.
