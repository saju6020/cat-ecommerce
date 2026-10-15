import AxeBuilder from '@axe-core/playwright';
import { test, expect, signInAsCustomer, signInAsStaff } from '../e2e/fixtures';
import { loadInventory } from '../perf/inventory';

/**
 * HO-XCT-002 section 3.4 / 11.6, E-X1, US-XCT-03 (UI-6) and US-XCT-04 (NFR-USA-4/5) -- one app-wide
 * axe sweep over every route in the generated inventory: zero serious or critical findings, and no
 * error code, stack trace or GUID in any rendered alert. Findings in another slice's route are
 * reported to its owning handover, never fixed or excluded here (ADR-FE-148). Runs under the MSW
 * mocks (`next dev`).
 */
test.describe.configure({ timeout: 120_000 });

const GUID = /[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}/i;
const TECHNICAL = /(Exception|StackTrace|ErrorCode|errorCode|\bat\s+\S+\s+\(.*:\d+:\d+\))/;

for (const entry of loadInventory()) {
  test(`E-X1 ${entry.audience} ${entry.route}`, async ({ page }) => {
    if (entry.audience === 'staff') await signInAsStaff(page);
    if (entry.audience === 'customer') await signInAsCustomer(page);

    await page.goto(entry.sampleUrl, { waitUntil: 'load' });
    await page.waitForTimeout(600);

    const results = await new AxeBuilder({ page })
      .withTags(['wcag2a', 'wcag2aa', 'wcag21a', 'wcag21aa'])
      .analyze();
    const serious = results.violations
      .filter((v) => v.impact === 'serious' || v.impact === 'critical')
      .map((v) => `${v.id} (${v.impact}): ${v.nodes.length} node(s), e.g. ${v.nodes[0]?.target.join(' ')}`);
    expect(serious, `${entry.route}: serious or critical axe findings`).toEqual([]);

    // US-XCT-04: what an error says never carries a code, a stack trace or an id.
    const alertTexts = await page.locator('[role="alert"]').allInnerTexts();
    for (const text of alertTexts) {
      expect(text, `${entry.route}: alert text`).not.toMatch(GUID);
      expect(text, `${entry.route}: alert text`).not.toMatch(TECHNICAL);
    }
  });
}
