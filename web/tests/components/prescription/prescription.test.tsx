import { describe, expect, it, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { PrescriptionGate } from '@/components/prescription/PrescriptionGate';
import { PrescriptionCatPicker } from '@/components/prescription/PrescriptionCatPicker';
import type { SkuPurchasability } from '@/lib/api/shop-customer';

const SIGN_IN = '/sign-in?next=%2Fshop%2Fproducts%2Fhills-cd-urinary-care';

const NO_AUTH: SkuPurchasability = {
  skuId: 's1',
  purchasable: false,
  purchaseBlockReason: 'PrescriptionAuthorisationRequired',
  eligibleCats: [],
};
const TWO_CATS: SkuPurchasability = {
  skuId: 's1',
  purchasable: true,
  purchaseBlockReason: null,
  eligibleCats: [
    { catId: 'c1', name: 'Mitu', validTo: '2026-12-12' },
    { catId: 'c2', name: 'Luna', validTo: '2027-01-05' },
  ],
};

describe('PrescriptionGate — HO-SHP-002 §11.3, ADR-FE-033, US-SHP-10 AC1', () => {
  it('U-B6: a guest sees the message and a sign-in link with ?next=', () => {
    render(<PrescriptionGate audience="guest" signInHref={SIGN_IN} />);
    expect(
      screen.getByText(
        'This is a prescription diet. It can be bought only for a cat with a current authorisation from our vets.',
      ),
    ).toBeInTheDocument();
    expect(
      screen.getByRole('link', { name: 'Sign in to check whether your cat is authorised' }),
    ).toHaveAttribute('href', SIGN_IN);
    expect(screen.queryByRole('link', { name: 'Book a consultation' })).not.toBeInTheDocument();
  });

  it('U-B6: PrescriptionAuthorisationRequired shows the reason and Book a consultation', () => {
    render(<PrescriptionGate audience="customer" purchasability={NO_AUTH} signInHref={SIGN_IN} />);
    expect(
      screen.getByText('None of your cats has a current authorisation for this food.'),
    ).toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'Book a consultation' })).toHaveAttribute(
      'href',
      '/book?category=Consultation',
    );
  });

  it('U-B6: two eligible cats are listed with their authorisation end dates, nothing clinical', () => {
    render(<PrescriptionGate audience="customer" purchasability={TWO_CATS} signInHref={SIGN_IN} />);
    expect(screen.getByText('Authorised for Mitu until 12 Dec 2026')).toBeInTheDocument();
    expect(screen.getByText('Authorised for Luna until 5 Jan 2027')).toBeInTheDocument();
    expect(screen.queryByRole('link', { name: 'Book a consultation' })).not.toBeInTheDocument();
    // No clinical detail (ADR-FE-033): only the generic gate copy, names and dates.
    expect(document.body.textContent).not.toMatch(/diagnos|condition|urinary|renal/i);
  });

  it('a failed purchasability read is a neutral retry, never "no authorisation"', async () => {
    const user = userEvent.setup();
    const onRetry = vi.fn();
    render(<PrescriptionGate audience="customer" isError onRetry={onRetry} signInHref={SIGN_IN} />);
    expect(screen.getByRole('alert')).toHaveTextContent("We couldn't check right now");
    expect(screen.queryByText(/None of your cats/)).not.toBeInTheDocument();
    expect(screen.queryByRole('link', { name: 'Book a consultation' })).not.toBeInTheDocument();
    await user.click(screen.getByRole('button', { name: 'Try again' }));
    expect(onRetry).toHaveBeenCalled();
  });

  it('says it is checking while the customer purchasability loads', () => {
    render(<PrescriptionGate audience="customer" isLoading signInHref={SIGN_IN} />);
    expect(screen.getByRole('status')).toHaveTextContent('Checking your cats');
    expect(screen.queryByRole('link', { name: 'Book a consultation' })).not.toBeInTheDocument();
  });
});

describe('PrescriptionCatPicker — HO-SHP-002 §11.3, US-SHP-10 AC2 (component for HO-ORD-002)', () => {
  it('U-B7: arrow keys move, space selects, onChange fires with the cat id — and nothing else happens', async () => {
    const user = userEvent.setup();
    const onChange = vi.fn();
    render(<PrescriptionCatPicker eligibleCats={TWO_CATS.eligibleCats} onChange={onChange} />);
    const group = screen.getByRole('group', { name: 'Which cat is this for?' });
    expect(group).toBeInTheDocument();

    await user.tab();
    expect(screen.getByRole('radio', { name: /Mitu/ })).toHaveFocus();
    await user.keyboard('{ArrowDown}');
    expect(screen.getByRole('radio', { name: /Luna/ })).toHaveFocus();
    expect(onChange).toHaveBeenLastCalledWith('c2');
    await user.keyboard('{ArrowUp}');
    expect(onChange).toHaveBeenLastCalledWith('c1');
    await user.keyboard(' ');
    expect(screen.getByRole('radio', { name: /Mitu/ })).toBeChecked();
  });

  it('U-B7: the required error is announced when the host submits with no cat chosen', () => {
    render(
      <PrescriptionCatPicker
        eligibleCats={TWO_CATS.eligibleCats}
        onChange={() => {}}
        showRequiredError
      />,
    );
    expect(screen.getByRole('alert')).toHaveTextContent('Choose which cat this is for.');
    for (const radio of screen.getAllByRole('radio')) expect(radio).toBeRequired();
  });
});
