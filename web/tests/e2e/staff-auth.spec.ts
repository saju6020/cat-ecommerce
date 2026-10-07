import { test, expect, signInAsStaff } from './fixtures';
import { accountsFixtureIds } from '../../mocks/handlers/areas/accounts';
import { catProfilesFixtureIds } from '../../mocks/handlers/areas/cat-profiles';

/**
 * `tests/e2e/staff-auth.spec.ts` — US-ACC-08/US-ACC-14, HO-ACC-002 §16. §13 names this file. The
 * unit tests (`tests/components/staff/idle-lock.test.tsx`, "ARCH gate HIGH 1"/"ARCH gate HIGH
 * (second pass)") isolate the same bugs at the component level with a directly-seeded query cache
 * and a simulated `refetchQueries` call; these are the real-browser, end-to-end counterparts,
 * against real cookies and a real MSW-backed session lifecycle (idle-timeout detection, then the
 * session actually ending). `__invalidateQueries` (`app/providers.tsx`'s own test-only hook) is
 * what triggers `useStaffSession()`'s refetch deterministically — a real tab-refocus's own
 * `visibilitychange` event fires the identical `refetchOnWindowFocus` code path, just later and
 * non-deterministically; only the trigger differs, not what is being exercised.
 */

test('ARCH gate HIGH 1 (first pass): a window-refocus-triggered session_idle_timeout locks in place, in a real browser, without redirecting or losing unsaved input', async ({
  page,
}) => {
  test.setTimeout(45_000);

  await signInAsStaff(page);
  await page.goto('/staff/customers/new');

  const fullNameField = page.getByLabel(/full name/i);
  await fullNameField.fill('Unsaved draft customer');

  const cookies = await page.context().cookies();
  const staffSessionCookie = cookies.find((c) => c.name === 'pc_staff_session');
  expect(
    staffSessionCookie,
    'pc_staff_session cookie must exist after signing in as staff',
  ).toBeTruthy();
  const token = staffSessionCookie!.value;

  // mocks/handlers/areas/accounts.ts's own test-only escape hatch (its own comment names this
  // spec) — forces the NEXT `GET /api/staff/session` read to report `session_idle_timeout`,
  // without needing to fast-forward a real 30-minute idle window.
  await page.evaluate((forcedToken) => {
    (
      window as unknown as { __forceStaffIdleExpired?: (t: string) => void }
    ).__forceStaffIdleExpired?.(forcedToken);
  }, token);

  // CODE_REVIEW suggestion (second pass) — this used to wait out `useStaffSession()`'s real 10s
  // `staleTime` with `page.waitForTimeout(11_000)` before dispatching a real tab-refocus
  // `visibilitychange` event (`@tanstack/query-core`'s own `FocusManager` listens for that
  // specifically, on `window`, not `document`, and not a `focus` event — confirmed by reading
  // `node_modules/@tanstack/query-core/build/modern/focusManager.js` directly). `__invalidateQueries`
  // (`app/providers.tsx`'s own test-only hook, gated the same way `__apiBaseUrl` is) marks the
  // query stale and refetches it immediately instead — deterministic, no real wait needed. Still
  // exercises the exact same `useStaffSession()`/`refetchOnWindowFocus` code path, not a shortcut
  // around it — only the trigger that makes the data "stale enough to refetch" is different.
  await page.evaluate(() => {
    (
      window as unknown as { __invalidateQueries?: (queryKey: readonly unknown[]) => void }
    ).__invalidateQueries?.(['session', 'staff']);
  });

  const dialog = page.getByRole('dialog');
  await expect(dialog).toBeVisible();
  await expect(dialog.getByRole('heading')).toContainText(/signed out after/i);

  // Never redirected to /staff/sign-in — StaffSessionGuard kept the page (and IdleLock, and this
  // form) mounted throughout, per ARCH gate HIGH 1's own fix.
  await expect(page).toHaveURL(/\/staff\/customers\/new/);
  await expect(fullNameField).toHaveValue('Unsaved draft customer');

  // Re-authenticate as the SAME staff member — the dialog closes and the draft is still there.
  await dialog.getByLabel(/mobile number or email/i).fill(accountsFixtureIds.adminIdentifier);
  await dialog.getByLabel(/^password/i).fill(accountsFixtureIds.adminPassword);
  await dialog.getByRole('button', { name: /^sign in$/i }).click();

  await expect(dialog).toBeHidden();
  await expect(page).toHaveURL(/\/staff\/customers\/new/);
  await expect(fullNameField).toHaveValue('Unsaved draft customer');
});

test('ARCH gate HIGH (second pass): once locked, a later session_invalid (the real API behaviour after it ends the idle session) still never redirects or unmounts the form — in a real browser', async ({
  page,
}) => {
  test.setTimeout(45_000);

  await signInAsStaff(page);
  await page.goto('/staff/customers/new');

  const fullNameField = page.getByLabel(/full name/i);
  await fullNameField.fill('Unsaved across the whole cycle');

  const cookies = await page.context().cookies();
  const staffSessionCookie = cookies.find((c) => c.name === 'pc_staff_session');
  expect(staffSessionCookie).toBeTruthy();
  const token = staffSessionCookie!.value;

  // Step 1: lock, the same way as the ARCH gate HIGH 1 (first pass) test above. The mock now ends
  // the session on THIS detection (mirroring SessionAuthenticationMiddleware's
  // EndForIdleTimeout + ExpireStaffSession) — every later call reports session_invalid, not
  // session_idle_timeout again.
  await page.evaluate((forcedToken) => {
    (
      window as unknown as { __forceStaffIdleExpired?: (t: string) => void }
    ).__forceStaffIdleExpired?.(forcedToken);
  }, token);
  // CODE_REVIEW suggestion (second pass) — `__invalidateQueries` (`app/providers.tsx`'s own
  // test-only hook) replaces the real `page.waitForTimeout(11_000)` + `visibilitychange` pair the
  // ARCH gate HIGH 1 (first pass) test above still explains in full; see that test's own comment.
  await page.evaluate(() => {
    (
      window as unknown as { __invalidateQueries?: (queryKey: readonly unknown[]) => void }
    ).__invalidateQueries?.(['session', 'staff']);
  });

  const dialog = page.getByRole('dialog');
  await expect(dialog).toBeVisible();
  await expect(dialog.getByRole('heading')).toContainText(/signed out after/i);

  // Step 2: "tab away, then back" a second time — another focus-triggered refetch of the SAME
  // ['session','staff'] query, now resolving to session_invalid (the mock already deleted the
  // session and expired the cookie on step 1's response). Must not redirect or unmount.
  await page.evaluate(() => {
    (
      window as unknown as { __invalidateQueries?: (queryKey: readonly unknown[]) => void }
    ).__invalidateQueries?.(['session', 'staff']);
  });
  await expect(dialog).toBeVisible();
  await expect(page).toHaveURL(/\/staff\/customers\/new/);
  await expect(fullNameField).toHaveValue('Unsaved across the whole cycle');

  // Step 3: a real activity heartbeat (POST /api/staff/session/activity) is the OTHER path into
  // IdleLock's own client.ts handler — it too now 401s session_invalid, since the session is gone.
  // Must also stay locked. Dispatched directly on the still-mounted, still-locked-behind field
  // (rather than a Locator interaction, which Playwright's own actionability check would refuse —
  // the lock overlay sits on top of it, `fixed inset-0 z-50`) — the listener only cares that an
  // `input`/`keydown`/`pointerdown` event reached `document`, not how it got there.
  await page.evaluate(() => {
    document.getElementById('fullName')?.dispatchEvent(new Event('input', { bubbles: true }));
  });
  await expect(dialog).toBeVisible();
  await expect(page).toHaveURL(/\/staff\/customers\/new/);
  await expect(fullNameField).toHaveValue('Unsaved across the whole cycle');

  // Step 4: only re-authenticating as the SAME staff member unlocks it — the draft is still there.
  await dialog.getByLabel(/mobile number or email/i).fill(accountsFixtureIds.adminIdentifier);
  await dialog.getByLabel(/^password/i).fill(accountsFixtureIds.adminPassword);
  await dialog.getByRole('button', { name: /^sign in$/i }).click();

  await expect(dialog).toBeHidden();
  await expect(page).toHaveURL(/\/staff\/customers\/new/);
  await expect(fullNameField).toHaveValue('Unsaved across the whole cycle');
});

test('ARCHITECTURE gate third pass, HIGH: on a page WITH its own page query (staff account detail), lock -> refocus -> that page query 401s too -> the changed-role draft survives -> unlock -> draft intact — in a real browser', async ({
  page,
}) => {
  test.setTimeout(45_000);

  await signInAsStaff(page);
  await page.goto(`/staff/admin/staff-accounts/${accountsFixtureIds.nurseStaffId}`);

  // A changed role selection — the unsaved draft this test protects. The nurse's own detail page
  // (not the signed-in administrator's own account) so nothing about "editing your own roles" is
  // special-cased into this assertion.
  const administratorRole = page.getByRole('checkbox', { name: 'Administrator' });
  await administratorRole.check();
  await expect(administratorRole).toBeChecked();

  const cookies = await page.context().cookies();
  const staffSessionCookie = cookies.find((c) => c.name === 'pc_staff_session');
  expect(staffSessionCookie).toBeTruthy();
  const token = staffSessionCookie!.value;

  // Forces the session to end (mirroring the real API's own EndForIdleTimeout on first detection —
  // every later call, including this page's own GET /api/staff-accounts/:id, 401s session_invalid
  // from here on, through the exact same `requireAction` check every staff-audience endpoint uses).
  await page.evaluate((forcedToken) => {
    (
      window as unknown as { __forceStaffIdleExpired?: (t: string) => void }
    ).__forceStaffIdleExpired?.(forcedToken);
  }, token);

  // A real window-focus event refetches every stale active query at once — the session query AND
  // this page's own `['staff-admin','staff-account',id]` query. `__invalidateQueries` triggers both
  // deterministically, the same way the tests above trigger the session query alone.
  await page.evaluate(() => {
    const invalidate = (
      window as unknown as { __invalidateQueries?: (queryKey: readonly unknown[]) => void }
    ).__invalidateQueries;
    invalidate?.(['session', 'staff']);
    invalidate?.(['staff-admin', 'staff-account']);
  });

  const dialog = page.getByRole('dialog');
  await expect(dialog).toBeVisible();
  await expect(dialog.getByRole('heading')).toContainText(/signed out after/i);

  // The page never swapped to ErrorState — `getByRole` (unlike `getByLabel`) respects
  // `aria-hidden`, which Radix's own modal Dialog correctly applies to the rest of the page while
  // open, so the checkbox's checked state is read directly rather than through a role query here.
  await expect(page.getByLabel('Administrator')).toBeChecked();
  await expect(page.getByText(/couldn't load this account/i)).not.toBeVisible();

  // Unlock via re-authentication as the same person — the draft is still there.
  await dialog.getByLabel(/mobile number or email/i).fill(accountsFixtureIds.adminIdentifier);
  await dialog.getByLabel(/^password/i).fill(accountsFixtureIds.adminPassword);
  await dialog.getByRole('button', { name: /^sign in$/i }).click();

  await expect(dialog).toBeHidden();
  await expect(page.getByRole('checkbox', { name: 'Administrator' })).toBeChecked();
});

test('E-G2 (ARCHITECTURE gate fourth pass, HIGH): on a cat form (CatProfileDataShell, a HO-CAT-002 shared layout), lock -> refocus -> the cat query 401s too -> the edited draft survives -> unlock -> draft intact — in a real browser', async ({
  page,
}) => {
  test.setTimeout(45_000);

  await signInAsStaff(page);
  const catId = catProfilesFixtureIds.catWithAllergiesExactDob; // Whiskers
  await page.goto(`/staff/cats/${catId}/edit`);

  const nameField = page.getByLabel(/^Name/);
  await expect(nameField).toBeVisible();
  await nameField.fill('Whiskers The Edited');

  const cookies = await page.context().cookies();
  const staffSessionCookie = cookies.find((c) => c.name === 'pc_staff_session');
  expect(staffSessionCookie).toBeTruthy();
  const token = staffSessionCookie!.value;

  // Forces the session to end (mirroring the real API's own EndForIdleTimeout on first
  // detection). mocks/handlers/areas/cat-profiles.ts's own endpoints never checked staff-session
  // state at all (HO-CAT-001 predates HO-ACC-001's session system), so this alone would not make
  // the cat query itself 401 — `__forceCatQuerySessionInvalid` (that file's own dedicated,
  // one-shot test hook) is the mock-side equivalent for this one endpoint, modelling what a real
  // API's `RequireActionFilter` would do once the staff session it depends on has actually ended.
  await page.evaluate((forcedToken) => {
    (
      window as unknown as { __forceStaffIdleExpired?: (t: string) => void }
    ).__forceStaffIdleExpired?.(forcedToken);
    (
      window as unknown as { __forceCatQuerySessionInvalid?: () => void }
    ).__forceCatQuerySessionInvalid?.();
  }, token);

  // A real window-focus event refetches every stale active query at once — the session query AND
  // this page's own cat-profile query (`CatProfileDataShell`'s `useCat(catId)`).
  // `__invalidateQueries` triggers both deterministically, the same way the tests above trigger
  // the session query alone.
  await page.evaluate(() => {
    const invalidate = (
      window as unknown as { __invalidateQueries?: (queryKey: readonly unknown[]) => void }
    ).__invalidateQueries;
    invalidate?.(['session', 'staff']);
    invalidate?.(['cat']);
  });

  const dialog = page.getByRole('dialog');
  await expect(dialog).toBeVisible();
  await expect(dialog.getByRole('heading')).toContainText(/signed out after/i);

  // The shared cat-profile layout never swapped to ErrorState — `getByRole` (unlike `getByLabel`)
  // respects `aria-hidden`, which Radix's own modal Dialog correctly applies to the rest of the
  // page while open, so the field's value is read directly rather than through a role query here.
  await expect(page.getByLabel(/^Name/)).toHaveValue('Whiskers The Edited');
  await expect(page.getByText(/couldn't load this cat's profile/i)).not.toBeVisible();

  // Unlock via re-authentication as the same person — the draft is still there.
  await dialog.getByLabel(/mobile number or email/i).fill(accountsFixtureIds.adminIdentifier);
  await dialog.getByLabel(/^password/i).fill(accountsFixtureIds.adminPassword);
  await dialog.getByRole('button', { name: /^sign in$/i }).click();

  await expect(dialog).toBeHidden();
  await expect(page.getByLabel(/^Name/)).toHaveValue('Whiskers The Edited');
});

test('E-G3 (CODE_REVIEW gate fifth pass, LOW): once locked, a real window-focus event does not even attempt to refetch the cat query — the pause itself, not only the isError && !data fallback, in a real browser', async ({
  page,
}) => {
  test.setTimeout(45_000);

  // CODE_REVIEW gate fourth pass, LOW — E-G2 above triggers both refetches through
  // `__invalidateQueries`, which calls `queryClient.invalidateQueries()` directly and so bypasses
  // `focusManager` entirely; it proves the `isError && !data` fallback, never the pause mechanism
  // the ARCHITECTURE gate fourth-pass HIGH fix actually is (detaching `focusManager`'s real
  // `window` listener while locked). This test dispatches a REAL `visibilitychange` event on
  // `window` — the exact `EventTarget` `@tanstack/query-core`'s own real `focusManager` listens to,
  // in this real, unmocked browser bundle, not a jsdom simulation — AFTER the lock has already
  // fully engaged, closing that gap: if a future change ever regressed the pause (e.g. reverting to
  // `queryClient.setDefaultOptions`, or the listener-swap itself breaking), this test would fail,
  // where E-G2 alone would not. `idle-lock-context.test.tsx`'s `FetchCountingQuery` tests prove the
  // identical mechanism at the component level; this is their real-browser counterpart.
  await signInAsStaff(page);
  const catId = catProfilesFixtureIds.catWithAllergiesExactDob; // Whiskers
  await page.goto(`/staff/cats/${catId}/edit`);

  const nameField = page.getByLabel(/^Name/);
  await expect(nameField).toBeVisible();
  await nameField.fill('Whiskers Untouched By The Pause');

  const cookies = await page.context().cookies();
  const staffSessionCookie = cookies.find((c) => c.name === 'pc_staff_session');
  expect(staffSessionCookie).toBeTruthy();
  const token = staffSessionCookie!.value;

  // Reach the locked state deterministically (same trigger every other lock test in this file
  // uses) — the real focus event below is the only non-deterministic-trigger step in this test,
  // isolating exactly what it is meant to prove.
  await page.evaluate((forcedToken) => {
    (
      window as unknown as { __forceStaffIdleExpired?: (t: string) => void }
    ).__forceStaffIdleExpired?.(forcedToken);
  }, token);
  await page.evaluate(() => {
    (
      window as unknown as { __invalidateQueries?: (queryKey: readonly unknown[]) => void }
    ).__invalidateQueries?.(['session', 'staff']);
  });

  const dialog = page.getByRole('dialog');
  await expect(dialog).toBeVisible();

  // Arms the cat query's own next read to 401 too — if the pause below were broken (a future
  // regression), this is what would surface as the ErrorState the assertions below check for; if
  // the pause holds, this armed 401 is never even reached, because no refetch is attempted at all.
  await page.evaluate(() => {
    (
      window as unknown as { __forceCatQuerySessionInvalid?: () => void }
    ).__forceCatQuerySessionInvalid?.();
  });

  // `useCat`'s own `staleTime` is 0 (always considered stale), so this would refetch immediately —
  // and hit the armed 401 above — if the pause were not in effect.
  await page.evaluate(() => window.dispatchEvent(new Event('visibilitychange')));
  await page.waitForTimeout(500); // lets any (incorrect) refetch attempt settle, if one occurred

  // Still locked, still no ErrorState, still the same draft — the cat query never even attempted to
  // refetch, so there was nothing here for the isError && !data fallback to need to catch.
  await expect(dialog).toBeVisible();
  await expect(page.getByLabel(/^Name/)).toHaveValue('Whiskers Untouched By The Pause');
  await expect(page.getByText(/couldn't load this cat's profile/i)).not.toBeVisible();

  // Unlock via re-authentication as the same person — the draft is still there.
  await dialog.getByLabel(/mobile number or email/i).fill(accountsFixtureIds.adminIdentifier);
  await dialog.getByLabel(/^password/i).fill(accountsFixtureIds.adminPassword);
  await dialog.getByRole('button', { name: /^sign in$/i }).click();

  await expect(dialog).toBeHidden();
  await expect(page.getByLabel(/^Name/)).toHaveValue('Whiskers Untouched By The Pause');
});

test('ARCHITECTURE gate fifth pass, MEDIUM: locking while genuinely offline does not deadlock re-sign-in, in a real browser', async ({
  page,
}) => {
  test.setTimeout(45_000);

  // Real-browser counterpart to `idle-lock.test.tsx`'s "locking while already offline does not
  // deadlock the re-sign-in mutation" (jsdom-level, `onlineManager.setOnline(false)` directly).
  // `context.setOffline(true)` fires a REAL `offline` event on `window` and flips
  // `navigator.onLine` in this actual browser — the exact signal `onlineManager`'s own default
  // listener reads, not a simulated one — closing the ARCHITECTURE gate fifth pass's own request
  // for this variant.
  await signInAsStaff(page);
  await page.goto('/staff/customers/new');

  const fullNameField = page.getByLabel(/full name/i);
  await fullNameField.fill('Unsaved draft while offline');

  const cookies = await page.context().cookies();
  const staffSessionCookie = cookies.find((c) => c.name === 'pc_staff_session');
  expect(staffSessionCookie).toBeTruthy();
  const token = staffSessionCookie!.value;

  // Offline BEFORE the lock engages — the terminal was already disconnected the instant idle
  // detection fired (Wi-Fi dropped, lid closed), the scenario the fifth-pass fix targets.
  await page.context().setOffline(true);

  await page.evaluate((forcedToken) => {
    (
      window as unknown as { __forceStaffIdleExpired?: (t: string) => void }
    ).__forceStaffIdleExpired?.(forcedToken);
  }, token);
  // NOT `__invalidateQueries` (used by every OTHER lock test in this file): that drives
  // `useStaffSession()`'s own refetch, a `useQuery` observer with the default
  // `networkMode: 'online'` — correctly paused while `onlineManager` reports offline (the real
  // `offline` event above already flipped it), so it would never even attempt the request and
  // this test would hang waiting for a dialog that never appears, regardless of the fix under
  // test. `__apiFetch` (`app/providers.tsx`) is a raw call, bypassing that gate entirely, the
  // same way the local idle-countdown timer (also network-independent) reaches `lockNow()` in
  // production — see that hook's own comment. `/api/staff-roles` is an arbitrary
  // already-idle-checked staff endpoint, standing in for "any staff-audience call", exactly like
  // `idle-lock.test.tsx`'s own `/api/some-unrelated-staff-endpoint`.
  await page.evaluate(() => {
    (window as unknown as { __apiFetch?: (path: string) => Promise<unknown> })
      .__apiFetch?.('/api/staff-roles')
      .catch(() => {});
  });

  const dialog = page.getByRole('dialog');
  await expect(dialog).toBeVisible();
  await expect(fullNameField).toHaveValue('Unsaved draft while offline');

  // Reconnect, then re-authenticate — without `networkMode: 'always'` on the re-sign-in mutation
  // and the resync this round added, `onlineManager.isOnline()` would stay frozen `false` for the
  // whole lock and this mutation would never even start (a genuine deadlock).
  await page.context().setOffline(false);

  await dialog.getByLabel(/mobile number or email/i).fill(accountsFixtureIds.adminIdentifier);
  await dialog.getByLabel(/^password/i).fill(accountsFixtureIds.adminPassword);
  await dialog.getByRole('button', { name: /^sign in$/i }).click();

  await expect(dialog).toBeHidden();
  await expect(fullNameField).toHaveValue('Unsaved draft while offline');
});
