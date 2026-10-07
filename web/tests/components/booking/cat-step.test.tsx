import { describe, expect, it } from 'vitest';
import { render, screen } from '@testing-library/react';
import { CatStep } from '@/components/booking/CatStep';

describe('CatStep — HO-CAT-002-ADDCAT', () => {
  it('U-AC9 — with no cats it offers Add a cat, carrying the booking parameters in next', () => {
    const href = '/portal/cats/new?next=' + encodeURIComponent('/book?category=Vaccination');
    render(<CatStep cats={[]} onSelect={() => {}} addCatHref={href} />);
    expect(screen.getByRole('link', { name: 'Add a cat' })).toHaveAttribute('href', href);
    expect(screen.queryByText(/ask the clinic/i)).not.toBeInTheDocument();
  });
});
