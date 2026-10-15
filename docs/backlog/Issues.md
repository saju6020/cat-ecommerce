Fixed Issues
-------------
1. [Shop (SHP) - all pages] No loader when I am navigating and the display is taking time
   - Fixed 2026-10-15: added a loading screen (`loading.tsx`, the shared `LoadingState` skeleton) to the shop, customer portal and staff areas. It shows inside the page frame as soon as a link is clicked, until the next page is ready.
2. [Shop (SHP) - banner] in all pages the banner is apearing multiple time with text "Marker-Shop-Banner-584bcd27 Shop banner" the page is broken
   - Cause: test data, not a page bug. Backend tests that were not given their own database fell back to `catecommerce_dev`, the database the running app reads, so every test run added more "Marker-…" banners, brands and products.
   - Fixed 2026-10-15: every test fallback now points at `catecommerce_test`, never `catecommerce_dev`.
   - Cleaned up 2026-10-15 (owner: "keep only one banner, remove rest"): 89 test banners in the dev database were unpublished; only "Marker-Shop-Banner-212a7f89" stays. They were unpublished, not deleted, because the table refuses permanent deletes.
3. [Shop (SHP) - catalogue filter] After bran fields in left filter two many checkbox is there. I am not sure this is required.. if required then it should be
   - Fixed 2026-10-15: the Brand filter now shows the top 8 brands and a "Show all N brands" button. Ticked brands always stay visible, and the search box still finds any brand. Test: `web/tests/components/catalogue/brand-filter-collapse.test.tsx`.
   - Why so many: almost all of the 1,484 brands in the dev database are test leftovers ("Starve-…", "zz Perf Fixture"). See #2 for the cause.
4. [Shop (SHP)]-No cart icon and count is showing.. when I am in shop pages the cart icon along with count should be shown somewhere may at top
   - Fixed 2026-10-15: the shop header now shows the cart (basket icon + "Cart", linking to /cart) on every shop page, with the item count beside it once the cart has something in it. It works for guests too. It reuses the portal's existing `CartBadge`.
5.  [Shop (SHP)]When I click on checkout its taking me to the login page but there is nothing for signup. there should be a signup option with keptcha
   - Fixed 2026-10-15: the sign-in page now has "New to PurrCare? Create an account", which opens the existing register page (it already has the CAPTCHA). The checkout return is kept: register's "Sign in" link carries `?next=/checkout`, so after signing in the customer goes straight back to checkout.
6. [Shop (SHP)] Still no loader is showing when its taking time. I become confused am I clicked or not after clicking since the movement is hapeening after some times 2-3 seconds[All Areas]
   - Fixed 2026-10-15: a thin progress bar across the top of the screen now starts the moment any internal link is clicked (or a search is submitted) and goes when the new page appears, in every area (shop, portal, staff, sign-in). The #1 loading screens only showed in some cases (not between pages in the same section, not while the dev server compiles a page); the bar covers those. Checked in a real browser. Test: `web/tests/components/issues-4-5-6.test.tsx`.
7. the chout page is showing a long popup. I think this is regarding privcy policy.. add only one sample policy and make page perfect. now one long poppup is showing and checkout details is blocking by that.
   - Cause found: the popup is the portal's "Updated policies" dialog (customers must accept policies before using the portal, checkout included). It listed 75 policies because the dev database is full of test policies (all 216 come from test runs, see #2), and the dialog had no height limit, so it ran off the screen.
   - Fixed 2026-10-15 (screen): the dialog is now capped at 90% of the screen height; the policy list scrolls inside it, and the heading and Sign out stay visible.
   - Fixed 2026-10-15 (data, owner approved): the 96 test policies in the dev database no longer require acceptance (switched off, not deleted). One sample "Privacy Policy" (`/policies/privacy`, version 1) was added through the admin API. It is now the only policy a customer is asked to accept, once, and then checkout is clear.
8. [Privacy Policy]-Read the full text is now showing anything.. please fix this
   - Cause: a field-name mismatch between the screens and the backend. The backend sends each policy's key as `policyKey` and sent no title; the screens read `key` and `title`. So in the popup every policy had no name and "Read the full text" opened `/policies/undefined` ("could not be found"). The policy page itself also had no heading. The screen tests used mock data with the screens' own field names, so they never caught it.
   - Fixed 2026-10-15: the backend now also sends the policy's `title`; the screens read `policyKey`; the mock data now matches the real backend. Checked against the running app: `/policies/privacy` shows "Privacy Policy" and its text. Tests: frontend content tests 24/24 (including a new one checking each link opens its own policy), backend policy tests 41/41.
9. [Check Out]-Review and place your order is not showing prices and total price along with delivery charge etc.
   - Cause: data, not the screen. At checkout every product needs a tax rate for its tax class (all 0% at launch). In the dev database none of the products had one: they were all made by tests with throwaway tax classes and no rates. So every item was left out ("Cannot be priced right now") and the summary showed only ৳0.00 rows.
   - Fixed 2026-10-15 (data, owner approved): a 0% tax rate was added to the 4,595 dev tax classes that had none. Nothing was deleted. (17 unpublished draft products point at a tax class that no longer exists; they never appear in the shop.)
   - Fixed 2026-10-15 (screen): when every item is left out, the review step now says "None of the items in your cart can be ordered right now, so there is nothing to price", with a link back to the cart, instead of a column of 0.00s. Test added to `web/tests/components/checkout/checkout.test.tsx` (20/20 pass).
   - Checked in a real browser: the review step lists the item with its price and tax, then Items subtotal, discounts, Delivery, Tax and Total (for example ৳150.00).
   - Note for testing: all stock in dev is at "Main clinic". The collection step picks the first branch by default, which is a test branch ("Audit Test Branch") with no stock, so items show as sold out there. Choose Main clinic. A delivery charge appears once a delivery zone covers the address (for example the Dhaka zone).

New Issues
-------------
