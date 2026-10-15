import { describe, expect, it, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { Quantity, QuantityInput, SignedQuantity } from '@/components/common/Quantity';
import {
  doseUnitsForForm,
  formatDoseRange,
  formatQuantity,
  formatRemaining,
  formatSignedQuantity,
} from '@/lib/format/units';
import { fromScaled, sameAmount, sumAmounts, toScaled } from '@/lib/format/pharmacy';

describe('formatQuantity (UI-8, ADR-FE-061)', () => {
  it('writes number and unit as one run with a non-breaking space, the amount exactly as sent', () => {
    expect(formatQuantity({ amount: '0.30', unit: 'Ml' })).toBe('0.30 ml');
    expect(formatQuantity({ amount: '4.10', unit: 'Kg' })).toBe('4.10 kg');
    expect(formatQuantity({ amount: '10.00', unit: 'Millilitre' })).toBe('10.00 ml');
  });

  it('pluralises countable units and never symbols', () => {
    expect(formatQuantity({ amount: '1.00', unit: 'Tablet' })).toBe('1.00 tablet');
    expect(formatQuantity({ amount: '2.00', unit: 'Tablet' })).toBe('2.00 tablets');
    expect(formatQuantity({ amount: '2.00', unit: 'Mg' })).toBe('2.00 mg');
  });

  it('shows a unit it does not know instead of hiding it', () => {
    expect(formatQuantity({ amount: '3.00', unit: 'Furlong' })).toBe('3.00 furlong');
  });

  it('signs a register movement', () => {
    expect(formatSignedQuantity({ amount: '-2.00', unit: 'Millilitre' })).toBe('−2.00 ml');
    expect(formatSignedQuantity({ amount: '10.00', unit: 'Millilitre' })).toBe('+10.00 ml');
    expect(formatSignedQuantity({ amount: '0.00', unit: 'Millilitre' })).toBe('0.00 ml');
  });

  it('assembles a range from the server figures only', () => {
    expect(
      formatDoseRange(
        {
          minMgPerKg: '0.05',
          maxMgPerKg: '0.10',
          minMg: { amount: '0.21', unit: 'Mg' },
          maxMg: { amount: '0.41', unit: 'Mg' },
        },
        { amount: '4.10', unit: 'Kg' },
      ),
    ).toBe('0.05–0.10 mg/kg (0.21–0.41 mg for 4.10 kg)');
  });

  it('words the remaining days', () => {
    expect(formatRemaining(5)).toBe('5 days left');
    expect(formatRemaining(1)).toBe('Last day today');
    expect(formatRemaining(0)).toBe('Course finished');
  });

  it('offers a tablet medicine only tablet and mass units', () => {
    expect(doseUnitsForForm('Tablet')).toEqual(['Tablet', 'Mg', 'Mcg', 'G']);
    expect(doseUnitsForForm('Tablet')).not.toContain('Ml');
  });
});

describe('Quantity components', () => {
  it('Quantity is one clinical-value element carrying the unit', () => {
    render(<Quantity value={{ amount: '0.30', unit: 'Ml' }} />);
    const element = document.querySelector('[data-clinical-value]');
    expect(element).toHaveTextContent('0.30 ml');
  });

  it('SignedQuantity keeps the sign and the unit in one element', () => {
    render(<SignedQuantity value={{ amount: '-1.00', unit: 'Millilitre' }} />);
    expect(document.querySelector('[data-clinical-value]')).toHaveTextContent('−1.00 ml');
  });

  it('QuantityInput starts with no unit chosen and reports changes', async () => {
    const user = userEvent.setup();
    const onChange = vi.fn();
    render(
      <QuantityInput
        id="dose"
        label="Dose"
        amount=""
        unit=""
        units={['Tablet', 'Mg']}
        onChange={onChange}
      />,
    );
    const select = screen.getByLabelText('Dose unit') as HTMLSelectElement;
    expect(select.value).toBe('');
    await user.selectOptions(select, 'Mg');
    expect(onChange).toHaveBeenLastCalledWith({ amount: '', unit: 'Mg' });
    await user.type(screen.getByLabelText('Dose'), '2');
    expect(onChange).toHaveBeenLastCalledWith({ amount: '2', unit: '' });
  });

  it('QuantityInput with a fixed unit writes the unit beside the number', () => {
    render(
      <QuantityInput
        id="qty"
        label="Quantity per supply"
        amount="10"
        unit="Millilitre"
        fixedUnit="Millilitre"
        onChange={() => undefined}
      />,
    );
    expect(screen.getByText(/\(ml\)/)).toBeInTheDocument();
  });
});

describe('exact decimal sums for the dispense total', () => {
  it('adds and compares without floating point', () => {
    expect(sumAmounts(['0.10', '0.20'])).toBe('0.30');
    expect(sumAmounts(['6.00', '4.00'])).toBe('10.00');
    expect(sameAmount('10', '10.00')).toBe(true);
    expect(sameAmount('9.99', '10.00')).toBe(false);
    expect(toScaled('abc')).toBeNaN();
    expect(fromScaled(3)).toBe('0.0003');
  });
});
