import { beforeEach, describe, expect, it, vi } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { LabelPrintView } from '@/components/pharmacy/LabelPrintView';
import { MedicineLabel, labelPrintCss } from '@/components/pharmacy/MedicineLabel';
import { getDispensing, type Dispensing } from '@/lib/api/pharmacy';
import { pharmacyFixtureIds as ids, resetPharmacyMockData } from '@/mocks/handlers/pharmacy';
import { resetNavigation } from '../common/pharmacyTestUtils';

vi.mock('next/navigation', async () =>
  (await import('../common/pharmacyTestUtils')).navigationMock(),
);
vi.setConfig({ testTimeout: 20_000 });

beforeEach(() => {
  resetPharmacyMockData();
  resetNavigation();
});

async function controlledDispensing(): Promise<Dispensing> {
  return getDispensing(ids.dispensingControlledId);
}

describe('U-C9 — the label has every field of US-PHM-05 AC1', () => {
  it('cat, owner, medicine, dose, instructions, quantity, date, expiry, number, prescriber, controlled notice, clinic', async () => {
    const dispensing = await getDispensing(ids.dispensingMittensId);
    render(<MedicineLabel label={dispensing.label} />);
    const label = screen.getByRole('article', { name: /Medicine label for Mittens/ });
    expect(label).toHaveTextContent('Mittens');
    expect(label).toHaveTextContent('Sam Rahman');
    expect(label).toHaveTextContent('Meloxicam oral suspension');
    expect(label).toHaveTextContent('0.30 ml by mouth once daily');
    expect(label).toHaveTextContent('Give 0.3 ml by mouth once daily, with or after food.');
    expect(label).toHaveTextContent('10.00 ml');
    expect(label).toHaveTextContent('Dispensed:');
    expect(label).toHaveTextContent('Expires:');
    expect(label).toHaveTextContent('RX-2026-000101');
    expect(label).toHaveTextContent('Prescriber: Dr Akter');
    expect(label).toHaveTextContent('PurrCare Veterinary Clinic');
    expect(label).toHaveTextContent('Keep out of reach of children');
    expect(label).not.toHaveTextContent('Controlled drug');
  });

  it('a controlled medicine carries the notice', async () => {
    render(<MedicineLabel label={(await controlledDispensing()).label} />);
    expect(screen.getByRole('article')).toHaveTextContent('Controlled drug');
  });

  it('prints from the frozen label at the configured size, hiding everything else', () => {
    expect(labelPrintCss).toContain('@media print');
    expect(labelPrintCss).toContain('--label-width: 62mm');
    expect(labelPrintCss).toContain('--label-height: 100mm');
    expect(labelPrintCss).toContain('@page { size: 62mm 100mm; margin: 0; }');
    expect(labelPrintCss).toContain('body * { visibility: hidden !important; }');
    expect(labelPrintCss).toContain(
      '.medicine-label, .medicine-label * { visibility: visible !important; }',
    );
  });
});

describe('U-C10 — Print records once per print action', () => {
  it('calls window.print then posts one label-print per click, never per render', async () => {
    const user = userEvent.setup();
    const print = vi.fn();
    window.print = print;
    const onPrinted = vi.fn().mockResolvedValue(undefined);
    const dispensing = await controlledDispensing();
    const { rerender } = render(<LabelPrintView dispensing={dispensing} onPrinted={onPrinted} />);
    rerender(<LabelPrintView dispensing={dispensing} onPrinted={onPrinted} />);
    rerender(
      <LabelPrintView dispensing={{ ...dispensing, labelPrintCount: 3 }} onPrinted={onPrinted} />,
    );
    expect(print).not.toHaveBeenCalled();
    expect(onPrinted).not.toHaveBeenCalled();

    await user.click(screen.getByRole('button', { name: 'Print' }));
    await waitFor(() => expect(onPrinted).toHaveBeenCalledTimes(1));
    expect(print).toHaveBeenCalledTimes(1);
    expect(print.mock.invocationCallOrder[0]!).toBeLessThan(onPrinted.mock.invocationCallOrder[0]!);

    await user.click(screen.getByRole('button', { name: 'Print' }));
    await waitFor(() => expect(onPrinted).toHaveBeenCalledTimes(2));
    expect(screen.getByRole('link', { name: 'Back' })).toHaveAttribute(
      'href',
      `/staff/pharmacy/dispensings/${ids.dispensingControlledId}`,
    );
  });

  it('says so when the print could not be recorded', async () => {
    const user = userEvent.setup();
    window.print = vi.fn();
    render(
      <LabelPrintView
        dispensing={await controlledDispensing()}
        onPrinted={vi.fn().mockRejectedValue(new Error('x'))}
      />,
    );
    await user.click(screen.getByRole('button', { name: 'Print' }));
    expect(await screen.findByText(/could not be recorded/)).toBeInTheDocument();
  });
});
