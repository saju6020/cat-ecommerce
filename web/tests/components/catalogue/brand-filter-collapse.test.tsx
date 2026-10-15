import { describe, expect, it, vi } from 'vitest';
import { render, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { CatalogueFilters } from '@/components/catalogue/CatalogueFilters';
import { EMPTY_CATALOGUE_PARAMS } from '@/lib/shop/catalogueParams';
import { CATEGORIES, FACETS } from './catalogue-fixtures';

// Issues.md #3: a long brand list collapses to the top eight, with "Show all" and the search box.
const MANY_BRANDS = Array.from({ length: 20 }, (_, i) => ({
  name: `Brand ${i + 1}`,
  count: 40 - i,
}));

function setup(brand: string[] = []) {
  render(
    <CatalogueFilters
      value={{ ...EMPTY_CATALOGUE_PARAMS, brand }}
      facets={{ ...FACETS, brands: MANY_BRANDS }}
      categories={CATEGORIES}
      currencyCode="BDT"
      priceMode="button"
      onChange={vi.fn()}
    />,
  );
}

const brandBoxes = () =>
  within(screen.getByRole('group', { name: 'Brand' })).getAllByRole('checkbox');

describe('CatalogueFilters brand list — Issues.md #3', () => {
  it('shows the top eight brands and a "Show all" button that expands and collapses', async () => {
    setup();
    expect(brandBoxes()).toHaveLength(8);
    expect(screen.queryByLabelText(/Brand 9\b/)).not.toBeInTheDocument();

    const toggle = screen.getByRole('button', { name: 'Show all 20 brands' });
    expect(toggle).toHaveAttribute('aria-expanded', 'false');
    await userEvent.click(toggle);
    expect(brandBoxes()).toHaveLength(20);

    await userEvent.click(screen.getByRole('button', { name: 'Show fewer brands' }));
    expect(brandBoxes()).toHaveLength(8);
  });

  it('keeps a ticked brand visible while collapsed', () => {
    setup(['Brand 15']);
    expect(brandBoxes()).toHaveLength(9);
    expect(screen.getByLabelText(/Brand 15\b/)).toBeChecked();
  });

  it('searching lists every match and hides the toggle', async () => {
    setup();
    await userEvent.type(screen.getByLabelText('Search brands'), 'Brand 1');
    // Brand 1 and Brand 10..19
    expect(brandBoxes()).toHaveLength(11);
    expect(screen.queryByRole('button', { name: /Show all/ })).not.toBeInTheDocument();
  });
});
