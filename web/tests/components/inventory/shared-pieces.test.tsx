import * as React from 'react';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { ScanField } from '@/components/common/ScanField';
import { QuantityInput } from '@/components/inventory/QuantityInput';
import { LocationSelect } from '@/components/inventory/LocationSelect';
import { ExpiryBadge } from '@/components/stock/ExpiryBadge';
import { StockFigures } from '@/components/stock/StockFigures';
import { BatchStateBadge } from '@/components/stock/BatchStateBadge';
import {
  formatExpiry,
  formatQuantity,
  formatSignedQuantity,
  formatUnitCost,
  trimDecimalString,
} from '@/lib/format/inventory';
import type { StockLocation } from '@/lib/api/inventory-lookup';

vi.mock('next/navigation', async () => {
  const { navigation } = await import('./testUtils');
  return {
    useRouter: () => ({ push: navigation.push, replace: navigation.replace }),
    usePathname: () => navigation.pathname,
    useSearchParams: () => navigation.search,
  };
});

afterEach(() => vi.useRealTimers());

describe('U-A1 — formatQuantity always carries the unit (UI-8, ADR-FE-041)', () => {
  it('trims trailing zeros and appends the unit', () => {
    expect(formatQuantity('12.000', 'bags')).toBe('12 bags');
    expect(formatQuantity('0.500', 'ml')).toBe('0.5 ml');
    expect(formatQuantity('3', 'each')).toBe('3 each');
  });

  it('keeps the value a string — no float rounding', () => {
    expect(trimDecimalString('0.1230')).toBe('0.123');
    expect(formatQuantity('1234567.125', 'ml')).toBe('1234567.125 ml');
    expect(formatSignedQuantity('-3.000', 'bags')).toBe('-3 bags');
    expect(formatSignedQuantity('3', 'bags')).toBe('+3 bags');
  });

  it('shows a unit cost to four decimals', () => {
    expect(formatUnitCost({ amount: '12.3456', currencyCode: 'USD' })).toContain('12.3456');
    expect(formatUnitCost({ amount: '450.0000', currencyCode: 'BDT' })).toContain('450.0000');
  });
});

describe('U-A2 — QuantityInput refuses what the item cannot hold', () => {
  function Harness({ allowsFractional, initial }: { allowsFractional: boolean; initial: string }) {
    const [value, setValue] = React.useState(initial);
    return (
      <QuantityInput
        id="q"
        label="Accepted"
        value={value}
        onValueChange={setValue}
        stockUnit="bags"
        allowsFractional={allowsFractional}
      />
    );
  }

  it('whole-only SKU: 1.5 is refused with a unit-aware message', async () => {
    render(<Harness allowsFractional={false} initial="1.5" />);
    expect(screen.getByRole('alert')).toHaveTextContent(/whole bags/i);
    expect(screen.getByLabelText(/accepted/i)).toHaveAttribute('aria-invalid', 'true');
  });

  it('fractional SKU: 0.125 accepted, 0.1234 refused (3 dp)', async () => {
    const { rerender } = render(<Harness allowsFractional initial="0.125" />);
    expect(screen.queryByRole('alert')).not.toBeInTheDocument();
    rerender(<Harness key="b" allowsFractional initial="0.1234" />);
    expect(screen.getByRole('alert')).toHaveTextContent(/at most 3 decimal places/i);
  });

  it('shows the unit in the label and beside the field', () => {
    render(<Harness allowsFractional initial="" />);
    expect(screen.getByLabelText(/accepted \(bags\)/i)).toBeInTheDocument();
  });
});

describe('U-A3 — ScanField (keyboard-wedge, ADR-FE-042)', () => {
  it('a fast burst ending in Enter fires one lookup and announces the match', async () => {
    const onScan = vi.fn().mockResolvedValue('Kitten dry food — FD-KDC-2KG');
    const user = userEvent.setup({ delay: 0 });
    render(<ScanField id="scan" onScan={onScan} />);
    await user.type(screen.getByLabelText(/scan a barcode/i), '4006381333931{Enter}');
    await waitFor(() => expect(onScan).toHaveBeenCalledWith('4006381333931'));
    expect(onScan).toHaveBeenCalledTimes(1);
    expect(await screen.findByText(/added: kitten dry food — fd-kdc-2kg/i)).toBeInTheDocument();
  });

  it('slow typing is not a scan', async () => {
    const onScan = vi.fn();
    const user = userEvent.setup({ delay: 80 });
    render(<ScanField id="scan" onScan={onScan} />);
    await user.type(screen.getByLabelText(/scan a barcode/i), '4006381{Enter}');
    expect(onScan).not.toHaveBeenCalled();
  });

  it('an unknown barcode says so and keeps focus', async () => {
    const onScan = vi.fn().mockResolvedValue(null);
    const user = userEvent.setup({ delay: 0 });
    render(<ScanField id="scan" onScan={onScan} />);
    const input = screen.getByLabelText(/scan a barcode/i);
    await user.type(input, '9999999999999{Enter}');
    expect(await screen.findByText('No SKU has barcode 9999999999999.')).toBeInTheDocument();
    expect(input).toHaveFocus();
  });

  it('a second scan after a miss replaces the unknown barcode instead of being appended', async () => {
    const onScan = vi.fn().mockResolvedValueOnce(null).mockResolvedValueOnce('Kitten — FD-1');
    const user = userEvent.setup({ delay: 0 });
    render(<ScanField id="scan" onScan={onScan} />);
    const input = screen.getByLabelText(/scan a barcode/i) as HTMLInputElement;
    await user.type(input, '9999999999999{Enter}');
    expect(await screen.findByText('No SKU has barcode 9999999999999.')).toBeInTheDocument();
    expect(input.selectionStart).toBe(0);
    expect(input.selectionEnd).toBe(input.value.length);
    await user.keyboard('4006381333931{Enter}');
    await waitFor(() => expect(onScan).toHaveBeenLastCalledWith('4006381333931'));
    expect(onScan).toHaveBeenCalledTimes(2);
  });

  it('a typed barcode can still be looked up with the Look up button', async () => {
    const onScan = vi.fn().mockResolvedValue('X — Y');
    const user = userEvent.setup({ delay: 80 });
    render(<ScanField id="scan" onScan={onScan} />);
    await user.type(screen.getByLabelText(/scan a barcode/i), '12345678');
    await user.click(screen.getByRole('button', { name: /look up/i }));
    await waitFor(() => expect(onScan).toHaveBeenCalledWith('12345678'));
  });
});

describe('U-A4 — ExpiryBadge is text first', () => {
  const stamp = (offset: number) => {
    const d = new Date();
    d.setDate(d.getDate() + offset);
    return d.toISOString().slice(0, 10);
  };

  it('47 days, today, expired 3 days ago, no expiry', () => {
    expect(formatExpiry(stamp(47), false, 47).text).toMatch(/· 47 days$/);
    expect(formatExpiry(stamp(0), false, 0).text).toMatch(/expires today$/);
    expect(formatExpiry(stamp(-3), true, -3).text).toMatch(/expired 3 days ago$/);
    expect(formatExpiry(null, false, null).text).toBe('No expiry date');
  });

  it('the expired variant repeats that it cannot be sold, dispensed or fed', () => {
    render(<ExpiryBadge expiresOn={stamp(-3)} isExpired daysToExpiry={-3} />);
    expect(screen.getByText(/expired 3 days ago/i)).toBeInTheDocument();
    expect(screen.getByText(/cannot be sold, dispensed or fed/i)).toBeInTheDocument();
  });
});

describe('U-A5 — StockFigures', () => {
  it('lists every figure with its unit and explains what sellable excludes', () => {
    render(
      <StockFigures
        stockUnit="bags"
        figures={{
          onHand: '37',
          reserved: '2',
          usable: '30',
          sellable: '24',
          heldQuantity: '5',
          expiredQuantity: '4',
        }}
      />,
    );
    expect(screen.getByText('37 bags')).toBeInTheDocument();
    expect(screen.getByText('24 bags')).toBeInTheDocument();
    expect(
      screen.getByText(/sellable excludes expired, quarantined, damaged and reserved stock/i),
    ).toBeInTheDocument();
  });
});

describe('U-A6 — LocationSelect disables what the server would refuse, with the reason in text', () => {
  const locations: StockLocation[] = [
    {
      id: 'w',
      code: 'WH1',
      name: 'Warehouse',
      kind: 'Warehouse',
      isActive: true,
      permittedMovements: ['Sale', 'Receipt'],
    },
    {
      id: 'd',
      code: 'DISP',
      name: 'Dispensary',
      kind: 'ClinicDispensary',
      isActive: true,
      permittedMovements: ['Dispense', 'Receipt'],
    },
    {
      id: 'x',
      code: 'OLD',
      name: 'Old shelf',
      kind: 'Warehouse',
      isActive: false,
      permittedMovements: ['Sale'],
    },
  ];

  it('for a sale, the dispensary option is disabled and says why', () => {
    render(
      <LocationSelect
        id="loc"
        label="Location"
        locations={locations}
        value=""
        onChange={() => undefined}
        movement="Sale"
      />,
    );
    const dispensary = screen.getByRole('option', { name: /dispensary/i });
    expect(dispensary).toBeDisabled();
    expect(dispensary).toHaveTextContent(/doesn't take sales/i);
    expect(screen.getByRole('option', { name: /warehouse \(warehouse\)$/i })).toBeEnabled();
    expect(screen.getByRole('option', { name: /old shelf/i })).toBeDisabled();
  });

  it('extra reasons (the other end of a transfer) are shown in the option text', () => {
    render(
      <LocationSelect
        id="loc"
        label="To"
        locations={locations}
        value=""
        onChange={() => undefined}
        disabledReasons={{ w: 'it is the source' }}
      />,
    );
    expect(screen.getByRole('option', { name: /warehouse.*it is the source/i })).toBeDisabled();
  });
});

describe('BatchStateBadge — ADR-FE-047', () => {
  it('says why in words for every unusable state', () => {
    const { rerender } = render(<BatchStateBadge status="Recalled" showWhy />);
    expect(screen.getByText('Recalled — held')).toBeInTheDocument();
    rerender(<BatchStateBadge status="Quarantined" statusReason="Damaged" showWhy />);
    expect(screen.getByText('Quarantined')).toBeInTheDocument();
    rerender(<BatchStateBadge status="Available" isExpired showWhy />);
    expect(screen.getByText('Expired')).toBeInTheDocument();
    rerender(<BatchStateBadge status="Available" inQuarantineLocation showWhy />);
    expect(screen.getByText('In quarantine location')).toBeInTheDocument();
    rerender(<BatchStateBadge status="WrittenOff" showWhy />);
    expect(screen.getAllByText(/written off/i).length).toBeGreaterThan(0);
  });
});
