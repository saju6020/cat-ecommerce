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
| | `CatalogueMobile360.dc.html` | Catalogue at 360px | *Filters (2)* opens a sheet with *Show results*; a two-column grid sits behind it (UI-1) |
| | `ShopByCountry.dc.html` | Shop by country | Flag + name + notes excerpt + count; “No sourcing notes yet.” written out; a globe for a country with no flag file |
| | `OriginPage.dc.html` | Origin: Thailand | The AC-5 path: the country is fixed by the page, and every other filter is still available |
| | `MaintenanceNotice.dc.html` | Shop closed | Replaces every storefront page; the admin's message, then booking and account links |
| Product page — US-SHP-01, 02, 03, 07, 08, 09, 10, 12, 14 | `ProductPage.dc.html` | Product page | ⭐ thumbnail-button gallery, a variant radio group with stock per size, the best-before notice, the **empty action slot** that HO-ORD-002 fills, and the analysis `<table>` with % in every cell |
| | `ProductOutOfStock.dc.html` | Out-of-stock size | The 4 kg size is out of stock and the others stay in stock (US-SHP-02 AC2). Also shows the three stock-alert outcomes, including OQ-7's “you won't hear from us”, and the guest `?next=` redirect |
| | `PrescriptionGate.dc.html` | Prescription gate | ⭐ guest / no authorised cat / two authorised cats side by side; *Book a consultation*; `PrescriptionCatPicker` |
| | `KitProduct.dc.html` | Kit | “This kit contains” with quantity and availability; one item out of stock makes the kit out of stock (US-SHP-12 AC2) |
| | `ProductReviewsAndRecs.dc.html` | Recommendations & reviews | Related blocks, the empty up-sell block not drawn, “for your cats”, reviews as literal text, and each review-eligibility variant |
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

**Customer layer** (added 25 Sep 2026). Customer screens add the warmer PurrCare customer tokens
on top of the usual set: `--surface-page:#fbf6ef`, `--warm:#e8894a` (fill and decoration only,
never text), `--warm-ink:#8a3f14` (7.5:1 on white), `--warm-soft:#fdeee2`, and `--radius-md` of
14px. Teal stays the colour for actions, and staff screens are unchanged. `ShopHome` has a warm
announcement band with the line-art cat and warm category icons, and `CatalogueNoResults` shows
the cat beside the message. `PrescriptionGate` and `MaintenanceNotice` keep the neutral tokens.

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
9. **No payment step anywhere.** The buy box ends at an empty, labelled action slot for HO-ORD-002.
   No card-entry form is drawn.

## Handover ambiguities resolved here

- **Cart link in the storefront header.** §11.1's `ShopHeader` doesn't list one, but the brief asked
  for a storefront header with a cart count. The count is drawn for context, and Add to cart itself
  is left as HO-ORD-002's slot.
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
