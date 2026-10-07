import { describe, expect, it } from 'vitest';
import { render, screen } from '@testing-library/react';
import { FieldWithUnit } from '@/components/common/FieldWithUnit';

describe('FieldWithUnit', () => {
  it('folds the unit into the label’s accessible name — never a bare number field', () => {
    render(<FieldWithUnit id="weight" label="Weight" unit="kg" defaultValue={4.2} />);
    expect(screen.getByRole('spinbutton', { name: 'Weight (kg)' })).toBeInTheDocument();
  });

  it('also shows the unit visibly, adjacent to the input', () => {
    render(<FieldWithUnit id="weight" label="Weight" unit="kg" />);
    // The visible suffix is aria-hidden (already announced via the label) but present in the DOM.
    expect(screen.getByText('kg', { selector: 'span[aria-hidden="true"]' })).toBeInTheDocument();
  });

  it('links a hint and an error via aria-describedby, both announced', () => {
    render(
      <FieldWithUnit
        id="weight"
        label="Weight"
        unit="kg"
        hint="Between 0.10 and 30.00 kg"
        error="Enter a weight between 0.10 and 30.00 kg"
      />,
    );
    const input = screen.getByRole('spinbutton', { name: 'Weight (kg)' });
    expect(input).toHaveAttribute('aria-invalid', 'true');
    const describedBy = input.getAttribute('aria-describedby');
    expect(describedBy).toContain('weight-hint');
    expect(describedBy).toContain('weight-error');
    expect(screen.getByRole('alert')).toHaveTextContent('Enter a weight between 0.10 and 30.00 kg');
  });
});
