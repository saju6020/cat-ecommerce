GATE: CODE_REVIEW (combined follow-up review)
HANDOVER: HEARTH-UI (branch feat/hearth-ui, bfb2d616..HEAD)
COMMITS: c125bb7c, 0d3862ba, 675c2200, 7aef7c22
DATE: 2026-10-16
STATUS: PASS

Checks run: tsc --noEmit clean; vitest on hearth, order-tracker, free-delivery, product tests 53/53 pass.
Not run: full eslint/vitest sweep (timed out), e2e, visual/responsive checks at 360/390/1280 (source review only).

FINDINGS
- [MEDIUM] web/tests/e2e/ordering-customer.spec.ts:~77-85 — signed-out cart test still clicks link name 'Checkout', but components/cart/CartScreen.tsx:106 now labels it "Sign in to check out · <amount>" when signed out ("check out" has a space, so the match fails). journey-real-api.spec.ts was updated, this one was not. Fix: match /Sign in to check out/ there.
- [MEDIUM] web/components/cart/CartScreen.tsx:157 and web/components/checkout/ReviewStep.tsx:143 — mobile sticky bottom bars (max-lg:sticky bottom-0, z-10) hold the whole summary card / total plus Place order. On 360-390px screens they take a large part of the viewport and cover content and focus targets (WCAG 2.4.11). Fix: keep the bar to total plus button, put the rest in the normal flow, and add scroll-padding-bottom.
- [MEDIUM] web/components/common/portal-header.tsx:33-52 — nav items are min-h-11 with flex-wrap and gap-x-6. The wrapped rows are 44px each, so many portal items give 3 rows at 1280 and a tall header at 390. Fix: horizontal scroll nav, or a menu button below md, with a smaller item count.
- [MEDIUM] web/components/product/PurchaseRow.tsx vs web/components/cart/AddToCartButton.tsx — the add-to-cart logic (mutation, cat dialog, message, error) is copied. Behaviour agrees today (Rx dialog, guest sign-in link, disabled reason, quantity max). One difference is that the typed number input became a +/- stepper, so a direct quantity entry is no longer possible. Duplicated logic will drift. Fix: extract a shared hook or render AddToCartButton with a "hearth" appearance.
- [LOW] web/components/product/PurchaseRow.tsx:~96 — the <output aria-label="Quantity"> sits inside a group also labelled "Quantity", so screen readers announce the name twice. Fix: label the output "Quantity, 1" or drop one label.
- [LOW] web/components/checkout/CheckoutFreeDelivery.tsx:33 — the progress bar is hidden by a [&_[role=progressbar]]:hidden selector hack, because there is no server percentage. Fix: make FreeDeliveryProgress's bar optional when percent is absent.
- [LOW] web/components/hearth/FreeDeliveryProgress.tsx:30 — Math.round/clamp on a percent is display-only and the caller gives none; acceptable. Dead path in practice.
- [LOW] web/components/hearth/ProductPackArt.tsx:6, ProductGallery.tsx:64 — text-warm (#e8894a) is used for decorative icons only, aria-hidden, not for text. OK; keep it that way. Text on accent-tint uses accent (#15605a) which has good contrast.
- [LOW] web/lib/api/checkout.ts:81-87 — freeOver / amountToFreeDelivery are typed non-optional (Money | null). An older server response would give undefined; the check `!amount` handles it, so no fix is needed.
- [LOW] tests/e2e edits — most of the diff is Prettier reformatting that hides the real changes. Heading assertions were changed to /^Thank you/ and the heading regex, and Cancel order toHaveCount(0) became toBeDisabled (an intended design change). Assertion strength is kept.

CHECKS WITH NO FINDING
- Money: no client arithmetic on prices, totals or thresholds found. The checkout free-delivery card uses the server's amountToFreeDelivery and shows nothing when it is null or zero. Pagination/quantity Math is not money.
- "Absent, not stubbed": DeliveryPromise states only true rules; OrderTracker/PillSteps take statuses from the caller; tracker.ts maps server status only; a cancelled order has no tracker.
- Staff screens: tokens (accent-tint, radius-card/band/pill) are defined only inside [data-theme='customer']. Button staff variants are unchanged (the radius moved into the sizes). OrderSummary/OrderLines default to appearance 'plain'.
- Radix dialogs: PrescriptionCatDialog, CancelOrderDialog and BankTransferInstructions carry data-theme="customer". Not checked: ReturnRequestForm and the FiltersSheet/other portal sheets in the diff.
- Backend c125bb7c: FreeOver comes from the zone service FreeOverOrderValue. The amount is max(0, threshold - goodsValue), using the same goodsValue DeliveryChargeCalculator uses. Both are null when there is no threshold. New fields have default values, so the change is additive and the one construction site compiles. A unit test covers below, reached and null-threshold cases. No convention violation.
- Hearth components: PillSteps has sr-only step text and aria-current="step"; OrderTracker has an sr-only status; CategoryChips uses aria-current="page"; the portal nav uses aria-current="page" with a 44px target. Reduced-motion and focus-visible were not exhaustively checked.

SCOPE CHECK
- files in allowed scope: 125/125 (web/ UI, one ordering read/domain change, tests, docs/decisions.md)
- files outside allowed scope: none
