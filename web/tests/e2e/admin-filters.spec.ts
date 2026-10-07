import { test, expect, signInAsStaff } from './fixtures';

/**
 * HO-ADM-002 §8.3 — URL-backed list filters (audit-log, content list). TEST gate HIGH, second and
 * third passes, 2026-09-28 (U-G6): real-browser, real-keystroke-timed typing into `AuditLogSearch`'s
 * text inputs — and two quick, back-to-back `<select>` changes on the content list — silently lost
 * earlier edits, because each edit was an async client-side navigation and the controlled
 * field/select read its value straight from `useSearchParams()`, snapping back to the
 * pre-navigation value on any render before that navigation committed (second pass). The second
 * pass's own fix (a single "last write" string) removed that whole-string-loss symptom but left a
 * rarer, non-deterministic single-character-loss variant: `useSearchParams()` does not necessarily
 * skip straight from one of the hook's own writes to the next — it can report an intermediate,
 * already-superseded write of its own first, which a single "last write" comparison cannot tell
 * apart from a genuine external navigation (third pass). Fixed by `lib/hooks/useUrlFilterState.ts`
 * and `lib/hooks/useFilterUrlUpdate.ts` tracking every pending write (a list), not one, plus
 * debouncing free-text fields' own URL writes (`~300ms` after the last keystroke, flushed
 * immediately on blur/Enter) so a typing burst produces far fewer navigations to race in the first
 * place — see `web/docs/decisions.md` ADR-FE-020 for the full mechanism.
 *
 * A couple of tests below add a short, explicitly-commented settle wait between two edits before
 * pressing Back twice. That is a Next.js App Router characteristic, not a workaround for this
 * fix: `router.push` is an async client-side transition, and calling it again before the previous
 * call's transition has actually committed to `history.pushState` makes the second call supersede
 * the first rather than add its own entry (confirmed directly against this app — a gap under
 * ~200ms reliably produced one merged history entry for two `push`es, not two). Every value-
 * correctness assertion in this file (every field/URL keeps every edit) uses no such wait, since
 * that is exactly the race U-G6 is about and holds with no pause at all.
 *
 * This is a permanent regression spec (unlike the gate's own temporary, uncommitted debug specs)
 * — run alongside `admin-functional.spec.ts` (`PORT=3102`, `--workers=1`, one shared `next dev`).
 */

test('audit-log: typing at realistic speed keeps every keystroke, in the field and in the URL', async ({
  page,
}) => {
  await signInAsStaff(page);
  await page.goto('/staff/admin/audit-log?from=2027-01-01&to=2027-01-07');

  const actionInput = page.getByLabel('action', { exact: true });
  await actionInput.click();
  await actionInput.pressSequentially('ServicePriceSet', { delay: 50 });
  await expect(actionInput).toHaveValue('ServicePriceSet');

  const entityIdInput = page.getByLabel('entityId', { exact: true });
  await entityIdInput.click();
  await entityIdInput.pressSequentially('svc12345', { delay: 30 });
  await expect(entityIdInput).toHaveValue('svc12345');

  // Both fields keep their exact typed value even after the second field's own edits settle …
  await expect(actionInput).toHaveValue('ServicePriceSet');
  await expect(entityIdInput).toHaveValue('svc12345');
  // … and both land in the URL — a quick edit to a second field never silently overwrote the first.
  await expect(page).toHaveURL(/action=ServicePriceSet/);
  await expect(page).toHaveURL(/entityId=svc12345/);
});

test('audit-log: typing at 150ms/key never drops or corrupts a character, repeated 5 times (TEST gate HIGH, third pass)', async ({
  page,
}) => {
  test.setTimeout(120_000);
  await signInAsStaff(page);
  await page.goto('/staff/admin/audit-log?from=2027-01-01&to=2027-01-07');

  const actionInput = page.getByLabel('action', { exact: true });
  const target = 'FirstValue';

  // TEST gate's own third-pass finding: at 150ms/key (roughly 80 WPM, an entirely ordinary typing
  // speed), the field intermittently dropped one character — a different one each run
  // ("FistValue", "FirtValue", "FirstVale", "FirsValue") — non-deterministically, on both chromium
  // and mobile-360. Five attempts in one test, each clearing and retyping the whole word, so a flaky
  // race would show up as a failure on at least one of them rather than being masked by a single try.
  for (let attempt = 0; attempt < 5; attempt += 1) {
    await actionInput.click();
    await actionInput.fill('');
    await actionInput.pressSequentially(target, { delay: 150 });

    await expect(actionInput).toHaveValue(target);
    await expect(page).toHaveURL(new RegExp(`action=${target}(&|$)`));
  }
});

test('content list: two quick, back-to-back select changes, no pause, are both kept in the URL', async ({
  page,
}) => {
  await signInAsStaff(page);
  await page.goto('/staff/admin/content');

  const kindSelect = page.getByLabel('Kind', { exact: true });
  const statusSelect = page.getByLabel('Status', { exact: true });

  // No pause between these two — the exact race U-G6 found: a user setting two filters one right
  // after the other, never waiting for the first one's navigation to land.
  await kindSelect.selectOption('Faq');
  await statusSelect.selectOption('Draft');

  await expect(kindSelect).toHaveValue('Faq');
  await expect(statusSelect).toHaveValue('Draft');
  await expect(page).toHaveURL(/kind=Faq/);
  await expect(page).toHaveURL(/status=Draft/);
});

test('audit-log: changing a date after typing, then Back restores the whole previous filter state, staying on the page', async ({
  page,
}) => {
  await signInAsStaff(page);
  await page.goto('/staff/admin/audit-log?from=2027-01-01&to=2027-01-07');

  const actionInput = page.getByLabel('action', { exact: true });
  await actionInput.click();
  await actionInput.pressSequentially('ServicePriceSet', { delay: 50 });
  await expect(page).toHaveURL(/action=ServicePriceSet/);
  // Let the action edit's own navigation actually commit (see file header) before the next, distinct
  // edit — otherwise this test could not tell "Back undoes one whole edit" from "Back undoes two".
  await page.waitForTimeout(300);

  const fromInput = page.getByLabel('From', { exact: true });
  await fromInput.fill('2027-02-01');
  await expect(page).toHaveURL(/from=2027-02-01/);
  await expect(fromInput).toHaveValue('2027-02-01');

  await page.goBack();

  // Back undoes the whole date edit in one step (§8.3's back-button-safe rule), not one character
  // or one keystroke at a time, and never leaves the audit-log page.
  await expect(page).toHaveURL(/\/staff\/admin\/audit-log/);
  await expect(page).toHaveURL(/from=2027-01-01/);
  await expect(fromInput).toHaveValue('2027-01-01');
  // The earlier `action` edit — a separate history entry — is unaffected by this one Back press.
  await expect(actionInput).toHaveValue('ServicePriceSet');
  await expect(page).toHaveURL(/action=ServicePriceSet/);
});

test('content list: Back twice restores each earlier filter state', async ({ page }) => {
  await signInAsStaff(page);
  await page.goto('/staff/admin/content');

  const kindSelect = page.getByLabel('Kind', { exact: true });
  const statusSelect = page.getByLabel('Status', { exact: true });

  await kindSelect.selectOption('Faq');
  await expect(page).toHaveURL(/kind=Faq/);
  // See file header — a real settle gap so the `status` edit below becomes its own, separately
  // Back-able history entry rather than superseding `kind`'s still-pending transition.
  await page.waitForTimeout(300);

  await statusSelect.selectOption('Draft');
  await expect(page).toHaveURL(/status=Draft/);
  await expect(kindSelect).toHaveValue('Faq');
  await expect(statusSelect).toHaveValue('Draft');

  await page.goBack();
  await expect(page).toHaveURL(/\/staff\/admin\/content/);
  await expect(page).toHaveURL(/kind=Faq/);
  await expect(page).not.toHaveURL(/status=/);
  await expect(kindSelect).toHaveValue('Faq');
  await expect(statusSelect).toHaveValue('');

  await page.goBack();
  await expect(page).toHaveURL(/\/staff\/admin\/content/);
  await expect(page).not.toHaveURL(/kind=/);
  await expect(kindSelect).toHaveValue('');
  await expect(statusSelect).toHaveValue('');
});

test('deep links pre-fill filters on both the audit log and the content list (§8.3, shareable, as E-G1)', async ({
  page,
}) => {
  await signInAsStaff(page);

  await page.goto(
    '/staff/admin/audit-log?from=2027-01-01&to=2027-01-31&action=ServicePriceSet&entityId=svc1',
  );
  await expect(page.getByLabel('From', { exact: true })).toHaveValue('2027-01-01');
  await expect(page.getByLabel('To', { exact: true })).toHaveValue('2027-01-31');
  await expect(page.getByLabel('action', { exact: true })).toHaveValue('ServicePriceSet');
  await expect(page.getByLabel('entityId', { exact: true })).toHaveValue('svc1');

  await page.goto('/staff/admin/content?kind=Banner&status=Published');
  await expect(page.getByLabel('Kind', { exact: true })).toHaveValue('Banner');
  await expect(page.getByLabel('Status', { exact: true })).toHaveValue('Published');
});
