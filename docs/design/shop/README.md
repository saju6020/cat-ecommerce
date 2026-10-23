# Shop — UX

The clickable design for backlog §8 *Shop — Catalogue and Products*, built against
[`HO-SHP-002`](../../handover/HO-SHP-002.md), the frontend handover. Fifth area, after
[Cat Profiles](../cat-profile/README.md), [Accounts & Identity](../accounts-identity/README.md),
[Administration](../administration/README.md) and [Notifications](../notifications/README.md).

## Where to review it

Review locally: http://127.0.0.1:8080/shop/index.html (served from docs/design)

There is no hosted canvas for this area yet. `index.html` lists every artboard in five rows.

## Where the source is

The `.dc.html` files in this folder are the **source of truth**. Each loads `./support.js`, the local
runtime copied unchanged from [`../_shared/`](../_shared/), which renders the artboard, wraps staff
screens in the shared sidebar and header, and makes the layout fluid. Customer-facing screens
(storefront and portal) are left exactly as designed.

## The screens

Five rows, twenty-eight artboards. This is the largest area so far, and the count follows the
handover: HO-SHP-002 covers fourteen stories across three separate surfaces. Those are a public
storefront, the customer's portal pages and a full catalogue back office with a seven-tab product
editor.

| Row | File | Screen | Shows |
|---|---|---|---|
| Storefront & catalogue — US-SHP-04, 05, 06, US-ADM-07, 08 | `ShopHome.dc.html` | Storefront home | The storefront header, banners (placement Home), categories, featured origins, new arrivals, and the language picker, which appears only when two content languages are enabled |
| | `Catalogue.dc.html` | Catalogue | ⭐ filters as `<fieldset>` groups, removable chips, “Clear all” that keeps `q`, sort, a result count announced politely, and every product-card state |
| | `CatalogueNoResults.dc.html` | No results & filter errors | “No products match “royl canan”.” with *Did you mean* and *Popular right now*; beside it the filters-only empty state and the min > max price error, which blocks navigation |
| | `CatalogueMobile360.dc.html` | Catalogue at 360px | Two stacked phone frames: the two-column card grid, then *Filters (2)* opened as a sheet with a sticky *Clear all* / *Show results* bar (UI-1) |
| | `ShopByCountry.dc.html` | Shop by country | Flag + name + notes excerpt + count; “No sourcing notes yet.” written out; a globe for a country with no flag file |
| | `OriginPage.dc.html` | Origin: Thailand | The AC-5 path: the country is fixed by the page, and every other filter is still available |
| | `MaintenanceNotice.dc.html` | Shop closed | Replaces every storefront page; the admin's message, then booking and account links |
| Product page — US-SHP-01, 02, 03, 07, 08, 09, 10, 12, 14 | `ProductPage.dc.html` | Product page | ⭐ thumbnail-button gallery, a variant radio group with stock per size, the best-before notice, the quantity stepper + *Add to cart* + wishlist row (replacing the old HO-ORD-002 action slot), the delivery-promise card, the analysis `<table>` with % in every cell, and *Often bought together* |
| | `ProductOutOfStock.dc.html` | Out-of-stock size | The 4 kg size is out of stock and the others stay in stock (US-SHP-02 AC2). *Add to cart* is drawn disabled with its reason beside it. Also shows the three stock-alert outcomes, including OQ-7's “you won't hear from us”, and the guest `?next=` redirect |
| | `PrescriptionGate.dc.html` | Prescription gate | ⭐ guest / no authorised cat / two authorised cats side by side; *Book a consultation*; `PrescriptionCatPicker` |
| | `KitProduct.dc.html` | Kit | “This kit contains” with quantity and availability; one item out of stock makes the kit out of stock (US-SHP-12 AC2) |
| | `ProductReviewsAndRecs.dc.html` | Recommendations & reviews | *Frequently bought together* as the warm card, the empty up-sell block not drawn, “for your cats” as product cards, “for your cats”, reviews as literal text, and each review-eligibility variant |
| | `ProductLanguage.dc.html` | Content in Bangla | `bn` name and description; the feeding guide falls back to English with `lang="en"` and a visible marker |
| Customer portal — US-SHP-09, 11, 13 | `Wishlist.dc.html` | Wishlist & alerts | “No longer sold”; alerts as *Waiting* / *Notified on …* / *Closed — no longer sold* |
| | `ReviewForm.dc.html` | Write a review | A rating-required error, the counter at 98 characters left, and the server refusals in plain language with the values kept |
| | `MyReviews.dc.html` | My reviews | The post-submit thanks, each status in words, and the withdraw confirmation |
| | `FoodSuggestions.dc.html` | Food suggestions | ⭐ reasons per product, “We left out 3 products containing chicken (recorded allergy).”, the date-of-birth hint and the footnote |
| Back office: products — US-SHP-01, 02, 03, 08, 12, 14 | `StaffProductList.dc.html` | Products | Barcode scan + Enter, filters, status in words, *Export CSV*, and the section's sub-navigation |
| | `NewProduct.dc.html` | New product | Standard / Kit, category → subcategory, the kit SKU code, and a web address already in use |
| | `EditorDetails.dc.html` | Editor: Details | ⭐ the editor header: Publish stays enabled, `ProductNotPublishable` is shown, and the checklist is focused, with “Required” / “Warning” in words |
| | `EditorFood.dc.html` | Editor: Food & labelling | % unit on each analysis input, the prescription-diet confirmation, and the `409` conflict banner |
| | `EditorVariants.dc.html` | Editor: Variants & prices | ⭐ “12 sellable · best before …”, “0 sellable”, “No stock recorded”; Scheduled / Current / Past; the past-date error and the “From 1 Oct 10:00 … will cost …” sentence |
| | `EditorImages.dc.html` | Editor: Images | The storage-key form with the honest “no upload yet” note, and a missing alt text error |
| | `EditorBundle.dc.html` | Editor: Kit contents | `InvalidBundleComponent` shown on the row it names |
| | `EditorRelated.dc.html` | Editor: Related + confirmations | Two ordered lists with reorder buttons; the Publish, Unpublish, Archive and Discontinue confirmations |
| | `EditorTranslations.dc.html` | Editor: Translations | English reference beside the Bangla fields, and the remove-translation confirmation |
| Back office: origins & reviews — US-SHP-05, 09 | `SourcingOrigins.dc.html` | Sourcing origins | The server's product count (read-only), and countries listed without a record |
| | `ReviewModeration.dc.html` | Review moderation | ⭐ reject with a required reason (error shown), and the hide confirmation |

## The design system

Built on the PurrCare design system tokens, hand-transcribed into each artboard as in the earlier
areas. Staff screens reuse `Table`, `Tabs` (the route-based editor tabs), `Dialog`, `Badge` (with
Administration's Scheduled / Current / Past vocabulary for prices) and `Field`.

The storefront needed a small set of new patterns, all built from the same tokens:

- **Storefront header**: logo, search, cart count, account and a category row. This is a different
  shell from the portal's tab bar and the staff sidebar.
- **Product card**: price, rating as text, availability as icon + word, and the *Vet authorisation
  needed* and *Kit* tags.
- **Availability line**: ✓ *In stock* / ⊗ *Out of stock*, used the same way on cards, variants,
  kit rows and wishlists.
- **State panel**: a small uppercase label plus a card. It shows a second state beside the main
  one wherever one artboard can't show both.

**Customer layer: Hearth (owner-approved 2026-10-16).** Customer screens add the warmer PurrCare
customer tokens on top of the usual set: `--surface-page:#fbf6ef`, `--warm:#e8894a` (fill and
decoration only, never text), `--warm-ink:#8a3f14` (7.5:1 on white), `--warm-soft:#fdeee2` and
`--radius-md` of 14px. Hearth adds `--accent-tint:#e6f0ee` (teal-soft icon tiles and image
tints), `--radius-card:20px`, `--radius-band:32px` and `--radius-pill:999px`. Each customer file
carries its Hearth rules in a block marked `/* Hearth (owner-approved 2026-10-16) */` at the end
of its `<style>`. Teal stays the colour for actions, and staff screens are unchanged.

- **Header.** A white bar. The logo is a small line-art cat head in a 40px warm-soft disc beside
  “PurrCare Shop” in Newsreader 24px. Search is a 46px pill on `--surface-sunken` with a teal pill
  *Search* button. Cart is a warm-soft pill with warm-ink text and a teal count badge. Categories
  are white pill chips with a 1px border and a small apricot dot; the current one is
  accent-tinted. Portal pages keep their own header, with the same logo disc and pill tabs.
- **Controls.** Buttons are pills at least 44px high (48px for the main product action). Primary
  is teal with white text; secondary is white with `--border-strong`. Inputs, selects, filter
  chips, pagers and tabs are pills too; a textarea takes `--radius-card`.
- **Bands.** Heroes, greetings, announcements and empty states use a `--warm-soft` fill and
  `--radius-band`, with the line-art cat on a white disc where an illustration fits (`ShopHome`,
  `CatalogueNoResults`, the `OriginPage` hero). Eyebrows are 12px uppercase 600 with .06–.08em
  tracking, in warm-ink.
- **Cards.** White, 1px `--border`, `--radius-card`. The product image area runs edge to edge
  in a warm-soft, accent-tint or sunken tint. The brand is an eyebrow above the name, with the
  availability line below (teal ✓ *In stock*) and the price at 20px bold. The *Kit* tag sits on
  the image as a white pill with warm-ink text.
- **Icon tiles.** 40–64px warm-soft discs with a warm-ink stroke icon, or 14px-radius
  accent-tint tiles with a teal icon. Inline stroke SVG only: no emoji, no paw prints, no puns.
- **Product page.** A large tinted gallery (radius 32) with thumbnail tiles. Sizes are rounded
  option cards showing price and stock, and the selected one has a 2px teal border. Tabs are pills
  and the detail panel is a white card.
- **Layout follows the approved preview.** *Owner decision 2026-10-16: shop screens follow the
  approved Hearth preview layout*, not only its colours. The approved preview is the Hearth
  concept's `Main` (shop home) and `A-Product` (product page) screens, with `A-CartPhone` for the
  phone card and sticky-bar feel. Content sits in a 1160px column under a centred header (logo,
  search, account links, then the Cart pill last). `ShopHome` is the preview's home: the Home
  banner becomes the hero (56px Newsreader headline with an italic warm-ink phrase, a white
  eyebrow pill, two pill buttons, the line-art cat on a 300px white disc and a small floating
  product card). The holiday notice is a slim warning pill under the hero, followed by six
  category icon tiles, the featured-origin cards, the new arrivals as product cards, a four-card
  promise strip and the footer. `ProductPage` is the preview's product page. Every product card
  on every screen is the preview card: a 220px tinted image area, a white pill badge (Kit, *Vet
  authorisation needed*), a round 44px wishlist button, an eyebrow (flag, brand · country), the
  name, the rating and availability lines, then a 20px price with “+ VAT” beside a teal *Add*
  pill. The 1280px catalogue results use three columns so the price and *Add* fit.
- **Added beyond the old spec. The app build must implement these** (the owner approved them
  with the preview, which overrides the earlier “look only, add nothing” rule):
  1. **Card wishlist button + Add pill** on every product card (catalogue, no-results popular
     products, origin page, home, recommendations, wishlist, the 360px grid). The heart is a
     toggle (`aria-pressed`). *Add* puts a single-size product straight in the cart. For a
     product sold in several sizes (“From ৳…”) it opens a size chooser (`aria-haspopup="dialog"`).
     For a prescription diet it opens `PrescriptionCatPicker` first. An out-of-stock card shows
     a secondary *Alert me* pill that leads to the stock alert. On the wishlist, the filled heart
     removes the item, and a no-longer-sold item offers *Remove* instead of *Add*.
  2. **Product-page quantity / Add to cart / wishlist row.** This replaces the HO-ORD-002 action
     slot: a pill quantity stepper (− / 1 / +, 48px), a full-width teal *Add to cart* and a round
     wishlist button. For an out-of-stock size or kit, the stepper and *Add to cart* are disabled,
     with the reason written next to them (`aria-describedby`), and the stock-alert card sits
     below.
  3. **Delivery-promise card** under the buy row: delivery across Dhaka (slot and fee at
     checkout), pickup at a branch, and how to pay (cash on delivery, bank transfer, cash or card
     at the counter). It holds no dates or fees of its own; those stay checkout's job. The home
     page's promise strip uses the same facts.
  4. **Often bought together**: the related-products “frequently bought together” list as a
     warm-soft card beside the product details, each row with a 44px *Add* (+) button.
  5. Smaller items: the hero needs the banner's optional eyebrow line and an optional italic
     highlight phrase in the headline (plain headline when blank). The floating hero card shows
     one featured product. The header's language picker becomes a compact globe + select pill.
- **Warmth stops at medical and money.** `PrescriptionGate`, `MaintenanceNotice` and every error
  or degraded state keep neutral colours, with no warm band and no cat. They do take the Hearth
  shapes and header. Warm is never a status: status colours (teal, warning, danger) are used only
  for status, and always with an icon and a word.

## Rules the design encodes

1. **The server decides, the page shows.** Stock, the best-before notice, eligibility, the publish
   checklist and recommendation exclusions are drawn as API output. No screen implies the page worked
   them out (ADR-FE-032). “No stock recorded” and “0 sellable” are different words for different
   facts.
2. **Status is never colour alone.** Every availability, review, product and alert status has an
   icon and a word. Ratings are “4.5 out of 5 (23 reviews)” with stars hidden from screen readers.
3. **A prescription diet explains itself.** The gate states the reason and offers the way forward
   (*Book a consultation*). It shows the owner's own cats and end dates, never a diagnosis
   (ADR-FE-033).
4. **Publish is never disabled to express invalidity.** The server refuses, and the refusal is shown
   against the checklist (ADR-FE-034). A disabled control always carries its reason, like the “main
   photo” radio on a per-size image or expiry tracking on food.
5. **Confirmations name the consequence.** This covers publish, unpublish, archive, discontinue,
   schedule a price, the prescription flag, reject, hide, remove a translation and withdraw a
   review. None of them says only “Are you sure?”.
6. **An alert that will reach nobody says so** (ADR-FE-038, OQ-7), with a link to fix it.
7. **Missing data is written out**: “No reviews yet”, “None scheduled”, “No current price”,
   “No sourcing notes yet”, “Not set”, “Photo coming soon”.
8. **Times name their zone** (Asia/Dhaka); date-only values (best before, review dates) don't.
   Units sit next to numbers (2 kg, 34.0 %, ৳2,450.00).
9. **No payment step anywhere.** The buy box ends at the quantity / *Add to cart* / wishlist row
   (owner decision 2026-10-16, replacing the HO-ORD-002 action slot). No card-entry form is drawn.
   The prescription gate still shows its picker without that row.

## Handover ambiguities resolved here

- **Cart link in the storefront header.** §11.1's `ShopHeader` doesn't list one, but the brief asked
  for a storefront header with a cart count. The count is drawn for context. Add to cart was
  first left as HO-ORD-002's slot; since 2026-10-16 it is drawn as the preview's buy row.
- **Portal nav.** §8.1 adds Shop, Wishlist and My reviews to the portal layout. The rest of that tab
  bar (My cats, Inbox, Account) is an assumption about HO-CAT-002 / HO-ACC-002's portal.
- **Tax label.** `commerce.pricesIncludeTax` defaults to `false`, so prices read “+ VAT” (OQ-5).
- **Kit editor tabs.** A kit gets a *Kit contents* tab. Because the sample kit is filed under Cat
  food, it also keeps *Food & labelling*.

## Known gaps, deliberately visible

- **No live interaction.** Every state is its own artboard or a labelled panel beside it.
- **Only one 360px artboard** (the catalogue with its filter sheet). The product page, portal and
  editor re-layouts at narrow widths are not drawn.
- **Flags are simplified drawings** standing in for the in-repo SVGs (DE, TH, TR, US, KR).
- **Product data, brands and counts are illustrative.** No backend exists yet for this area.
- **Not drawn:** the Add SKU dialog, the loading skeletons for personal islands, and
  `PermissionDeniedState`, which is unchanged from HO-ACC-002.
