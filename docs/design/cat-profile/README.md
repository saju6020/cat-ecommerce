# Cat Profile — UI design

The clickable design for backlog §2 *Cat Profiles*, reviewed before the frontend build starts.
Pairs with [`HO-CAT-002`](../../handover/HO-CAT-002.md), the frontend handover.

## Where to review it

**Clickable canvas:** <https://claude.ai/artifact/5K9wzKPZ2d7xLvhAZ5w1HJ>

That is the version to look at — the screens are linked, and the cat list's "Include deceased"
toggle actually works. It is private to the owner; others need it shared from the page's Share menu.

If the link is ever lost: `/artifacts` in the Claude Code terminal lists artifacts you own (`o`
opens, `c` copies the link), or browse <https://claude.ai/code/artifacts>.

## Where the source is

The `.dc.html` files in this folder are the **source of truth** for the design, committed so it
survives independently of the hosted link and so changes show up in `git diff` like anything else.

**They do not open standalone in a browser.** Each one loads `./support.js` from the canvas
runtime, which is not in this repo. Read them as source; view them on the canvas.

## The screens

Five rows on the canvas, fourteen artboards.

| File | Screen | Shows |
|---|---|---|
| `Main.dc.html` | Cat list | US-CAT-09 deceased filter (working), high-risk badge in a list row, pagination |
| `CatRegister.dc.html` | Register a cat | US-CAT-01 AC2 estimated DOB, **AC3 photo rejected with the real limit named** |
| `Vaccines.dc.html` | Vaccine catalogue | Core vs non-core, validity months — the inputs to next-due |
| `CatOverview.dc.html` | Profile — Mittens | ⭐ the header: high-risk banner, allergy chips, approximate age, units |
| `CatMedical.dc.html` | Medical profile editor | US-CAT-03 allergies, conditions, medications, care notes |
| `CatWeights.dc.html` | Weight trend | US-CAT-02 AC2 chart, with a hidden data table for screen readers |
| `CatVaccinations.dc.html` | Vaccinations — Mittens | US-CAT-07 AC3 Overdue + boarding warning; **US-CAT-05 AC2 expired batch rejected** |
| `CatHealthy.dc.html` | Profile — Biscuit | **US-CAT-03 AC2 "None recorded"**; US-CAT-07 AC1 Current |
| `Portal.dc.html` | Owner portal | US-CAT-07 AC2 Due soon + booking link; US-CAT-10 PDF download |
| `ConfirmDeceased.dc.html` | Confirm — mark deceased | UI-3: states exactly what happens, including that the record is kept |
| `CatTransfer.dc.html` | Confirm — transfer ownership | UI-3 + US-CAT-08: history moves, previous owner loses access, reason required |
| `CatDeceased.dc.html` | Deceased record — Ash | US-CAT-09 AC2 read-only, edit disabled **with an explanation** |
| `CageCard.dc.html` | Cage card | US-CAT-11 QR + the high-risk warning, print layout |
| `Mobile360.dc.html` | Profile at 360 px | UI-1 floor — header intact, no horizontal scroll |

## The design system

No design system existed on the account, so this one was built for the review and is open to change.

| Token | Value | Use |
|---|---|---|
| Display | **Newsreader** 600 | Cat names, page titles |
| Body | **Public Sans** 400/600/700 | Everything else |
| Ground | `#FAF9F6` | Page background |
| Surface | `#FFFFFF` | Cards |
| Ink / muted | `#1C1A17` / `#5C574F` | Text |
| Border | `#E5E1D8` | Card and row rules |
| Accent | `#15605A` | Primary actions, links, Current |
| Warning | `#6E4700` on `#FBF0DC` | Due soon, allergy chips |
| Danger | `#8F1D1D` on `#FBE9E7` | Overdue, high risk, destructive |

Every text/background pair clears 4.5:1 (UI-6). The danger fill under white text is `#8F1D1D`
rather than a brighter red for that reason.

**Customer layer** (added 25 Sep 2026). Customer screens add the warmer PurrCare customer tokens
on top of the usual set: `--surface-page:#fbf6ef`, `--warm:#e8894a` (fill and decoration only,
never text), `--warm-ink:#8a3f14` (7.5:1 on white), `--warm-soft:#fdeee2`, and `--radius-md` of
14px. Teal stays the colour for actions, and staff screens are unchanged. `Portal.dc.html` (My
cats) has the warmer page background and round cat avatars in warm colours. Its colours are inline
hex, not tokens. The other screens are unchanged, and `ConfirmDeceased` stays deliberately
neutral.

## Rules the design encodes

These are decisions, not decoration. Changing them changes whether an acceptance criterion is met.

1. **The profile header is in the layout, not the pages.** Every cat screen carries the allergy
   list and the high-risk warning, and a tab added later cannot omit them — UI-7, US-CAT-04 AC1.
2. **The high-risk banner sits above the cat's name.** A clinician sees it before anything else.
3. **"None recorded" is written out.** Blank space reads as *not checked*; the words read as
   *checked, and there are none* — US-CAT-03 AC2.
4. **Status carries an icon and a word, never colour alone** — UI-6.
5. **Overdue states the boarding consequence**, because that is the decision the status exists to
   drive — BR-1.
6. **Confirmations list what will happen**, including what will *not*. No "Are you sure?" — UI-3.
7. **Units are always visible and adjacent** — "4.2 kg", "2.5 mg" — UI-8.
8. **Disabled controls explain themselves.** The deceased record says why editing is unavailable
   rather than hiding the buttons — NFR-USA-4.

## Known gaps, deliberately visible

- **No sign-in.** The portal renders for a fixed identity and the sidebar says so. US-CAT-08 AC2
  ("previous owner can no longer view") cannot be shown in the UI — `HO-CAT-002` §20.3.
- **Photo upload does not upload.** The form validates the file and says plainly that image
  storage is not connected — `HO-CAT-002` §20.4.
- **The booking link has no destination.** Appointments (§3) are unbuilt — `HO-CAT-002` §20.5.
- **Appointments, Boarding and Orders** appear in the navigation marked "Later", to show where the
  cat record sits without implying they exist.
