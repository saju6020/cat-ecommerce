import { test, expect, signInAsCustomer, signInAsStaff } from '../e2e/fixtures';
import { loadInventory, VIEWPORT_WIDTHS } from './inventory';

/**
 * HO-XCT-002 section 11.6, E-B3, NFR-CMP-2 / UI-1 -- the 360 px sweep. Every route in the generated
 * inventory is loaded at 360, 768, 1024 and 1440 px and must have:
 *   - no horizontal page scroll (`scrollWidth <= clientWidth + 1`);
 *   - no element wider than the viewport outside a scroll container that is meant to scroll;
 *   - no control (link, button, input) clipped off either edge outside such a container.
 * A failing route is a finding against the handover that owns it (ADR-FE-148); the sweep never
 * excludes a route to go green. Runs under the MSW mocks (`next dev`).
 */
test.describe.configure({ timeout: 180_000 });

interface OverflowReport {
  scrollWidth: number;
  clientWidth: number;
  wide: string[];
  clipped: string[];
}

for (const entry of loadInventory()) {
  test(`E-B3 ${entry.audience} ${entry.route}`, async ({ page }) => {
    if (entry.audience === 'staff') await signInAsStaff(page);
    if (entry.audience === 'customer') await signInAsCustomer(page);

    for (const width of VIEWPORT_WIDTHS) {
      await page.setViewportSize({ width, height: 800 });
      await page.goto(entry.sampleUrl, { waitUntil: 'load' });
      await page.waitForTimeout(500);

      const report = await page.evaluate((): OverflowReport => {
        const inScroller = (el: Element): boolean => {
          for (let node = el.parentElement; node && node !== document.body; node = node.parentElement) {
            const overflowX = getComputedStyle(node).overflowX;
            if (overflowX === 'auto' || overflowX === 'scroll') return true;
          }
          return false;
        };
        const vw = document.documentElement.clientWidth;
        const describe = (el: Element): string => {
          const label = (el.getAttribute('aria-label') || el.textContent || '').trim().slice(0, 40);
          return el.tagName.toLowerCase() + (el.id ? '#' + el.id : '') + (label ? ' "' + label + '"' : '');
        };
        const wide: string[] = [];
        const clipped: string[] = [];
        for (const el of Array.from(document.body.querySelectorAll('*'))) {
          const rect = el.getBoundingClientRect();
          if (rect.width === 0 || rect.height === 0) continue;
          const style = getComputedStyle(el);
          if (style.position === 'fixed' || style.visibility === 'hidden' || el.closest('.sr-only')) continue;
          if (rect.right > vw + 1 && !inScroller(el)) wide.push(describe(el));
          if (/^(a|button|input|select|textarea)$/i.test(el.tagName) && (rect.left < -1 || rect.right > vw + 1) && !inScroller(el)) clipped.push(describe(el));
        }
        return { scrollWidth: document.documentElement.scrollWidth, clientWidth: vw, wide: wide.slice(0, 5), clipped: clipped.slice(0, 5) };
      });

      expect(report.scrollWidth, `${entry.route} @${width}px scrolls horizontally`).toBeLessThanOrEqual(
        report.clientWidth + 1,
      );
      expect(report.wide, `${entry.route} @${width}px has elements wider than the viewport`).toEqual([]);
      expect(report.clipped, `${entry.route} @${width}px has clipped controls`).toEqual([]);
    }
  });
}
