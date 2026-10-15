import { describe, expect, it, vi } from 'vitest';
import { act, render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MeasurementInput, sanitiseMeasurement } from '@/components/visit/MeasurementInput';
import { VitalsPanel } from '@/components/visit/vitals/VitalsPanel';
import { VitalsForm } from '@/components/visit/vitals/VitalsForm';
import { ApiValidationError } from '@/lib/api/client';
import { getVisit, recordVitals, type VisitDetail } from '@/lib/api/visits';
import { catQueryKey } from '@/lib/hooks/useCat';
import { formatMeasurement } from '@/lib/format/clinical';
import { ids, renderWithClient, freshClinicBeforeEach } from './helpers';

freshClinicBeforeEach();

describe('MeasurementInput — UI-8, ADR-FE-053', () => {
  it('U-B1: the unit is part of the accessible name and a visible suffix; no spinner', () => {
    render(
      <MeasurementInput
        id="w"
        label="Weight"
        unitName="kilograms"
        unitSymbol="kg"
        value="4.20"
        onChange={vi.fn()}
        decimalPlaces={2}
      />,
    );
    const input = screen.getByLabelText('Weight, kilograms');
    expect(input).toHaveAttribute('inputmode', 'decimal');
    expect(input).not.toHaveAttribute('type', 'number');
    expect(screen.getByText('kg')).toBeVisible();
  });

  it('filters typing to digits and the sign’s decimal places', () => {
    expect(sanitiseMeasurement('4.256x', 2)).toBe('4.25');
    expect(sanitiseMeasurement('abc', 2)).toBe('');
    expect(sanitiseMeasurement('4.5.6', 2)).toBe('4.56');
    expect(sanitiseMeasurement('38.67', 1)).toBe('38.6');
    expect(sanitiseMeasurement('188.5', 0)).toBe('1885');
  });

  it('formatMeasurement writes the server string with its unit', () => {
    expect(formatMeasurement({ value: '4.20', unit: 'kg' })).toBe('4.20 kg');
    expect(formatMeasurement({ value: '38.6', unit: '°C' })).toBe('38.6 °C');
    expect(formatMeasurement({ value: '5', unit: '/9' })).toBe('5 /9');
    expect(formatMeasurement(null)).toBe('Not recorded');
  });
});

describe('VitalsForm — US-CLN-11', () => {
  function setup(save: (r: unknown) => Promise<unknown>) {
    return renderWithClient(<VitalsForm zone="Asia/Dhaka" save={save} />);
  }

  it('shows an early hint from the server’s plausible range (and still saves by posting)', async () => {
    const user = userEvent.setup();
    setup(vi.fn());
    await user.type(await screen.findByLabelText('Temperature, degrees Celsius'), '42');
    expect(
      await screen.findByText(
        /Outside the usual range 35\.0–41\.5 °C — you’ll be asked to confirm/,
      ),
    ).toBeInTheDocument();
  });

  it('U-B2: two warned signs → both listed with ranges; Confirm and save re-posts confirmedOutOfRange for both', async () => {
    const user = userEvent.setup();
    const bodies: Array<Record<string, unknown>> = [];
    const save = async (request: unknown) => {
      bodies.push(request as Record<string, unknown>);
      return recordVitals(ids.visitEmptyId, request as never);
    };
    setup(save);
    await user.type(await screen.findByLabelText('Temperature, degrees Celsius'), '42');
    await user.type(screen.getByLabelText('Heart rate, beats per minute'), '90');
    await user.click(screen.getByRole('button', { name: 'Save vitals' }));
    const dialog = await screen.findByRole('dialog', { name: /outside the usual range/i });
    const items = within(dialog)
      .getAllByRole('listitem')
      .map((li) => li.textContent);
    expect(items).toHaveLength(2);
    expect(items.join('|')).toContain('Temperature 42.0 °C — usual range 35.0–41.5 °C');
    expect(items.join('|')).toContain('Heart rate 90 bpm — usual range 100–260 bpm');
    await user.click(within(dialog).getByRole('button', { name: 'Confirm and save' }));
    await waitFor(() => expect(bodies).toHaveLength(2));
    expect(bodies[0]).not.toHaveProperty('confirmedOutOfRange');
    expect(bodies[1]!.confirmedOutOfRange).toEqual(['Temperature', 'HeartRate']);
    await waitFor(() => expect(screen.queryByRole('dialog')).toBeNull());
  });

  it('U-B2: Correct values returns focus to the first listed field and saves nothing', async () => {
    const user = userEvent.setup();
    const save = vi.fn(async (request: unknown) =>
      recordVitals(ids.visitEmptyId, request as never),
    );
    setup(save);
    await user.type(await screen.findByLabelText('Temperature, degrees Celsius'), '42');
    await user.click(screen.getByRole('button', { name: 'Save vitals' }));
    const dialog = await screen.findByRole('dialog', { name: /outside the usual range/i });
    await user.click(within(dialog).getByRole('button', { name: 'Correct values' }));
    await waitFor(() =>
      expect(screen.getByLabelText('Temperature, degrees Celsius')).toHaveFocus(),
    );
    expect(save).toHaveBeenCalledTimes(1);
  });

  it('U-B3: VitalSignOutOfRange is a field error — no confirm is offered', async () => {
    const user = userEvent.setup();
    setup(async (request) => recordVitals(ids.visitEmptyId, request as never));
    await user.type(await screen.findByLabelText('Weight, kilograms'), '29');
    await user.clear(screen.getByLabelText('Weight, kilograms'));
    await user.type(screen.getByLabelText('Weight, kilograms'), '45');
    await user.click(screen.getByRole('button', { name: 'Save vitals' }));
    expect(
      await screen.findByText(/Weight must be between 0\.10 and 30\.00 kg/),
    ).toBeInTheDocument();
    expect(screen.queryByRole('dialog')).toBeNull();
    expect(screen.getByLabelText('Weight, kilograms')).toHaveAttribute('aria-invalid', 'true');
    expect(screen.queryByText('VitalSignOutOfRange')).toBeNull(); // U-X1: the code is never shown
  });

  it('refuses an empty form in words, before any request', async () => {
    const user = userEvent.setup();
    const save = vi.fn();
    setup(save);
    await user.click(await screen.findByRole('button', { name: 'Save vitals' }));
    expect(await screen.findByText('Enter at least one measurement.')).toBeInTheDocument();
    expect(save).not.toHaveBeenCalled();
  });

  it('sends measuredOn as an ISO instant with an offset', async () => {
    const user = userEvent.setup();
    const bodies: Array<Record<string, unknown>> = [];
    setup(async (request) => {
      bodies.push(request as Record<string, unknown>);
      return undefined;
    });
    await user.type(await screen.findByLabelText('Heart rate, beats per minute'), '150');
    await user.click(screen.getByRole('button', { name: 'Save vitals' }));
    await waitFor(() => expect(bodies).toHaveLength(1));
    expect(bodies[0]!.measuredOn).toMatch(
      /^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}(\.\d+)?(Z|[+-]\d{2}:\d{2})$/,
    );
  });
});

describe('VitalsPanel — U-B4', () => {
  it('a vitals entry with a weight invalidates the cat (header weight) and its weights', async () => {
    const user = userEvent.setup();
    const visit = await getVisit(ids.visitOpenId);
    const { client } = renderWithClient(<VitalsPanel visit={visit as VisitDetail} />);
    client.setQueryData(catQueryKey(visit.catId), { id: visit.catId });
    client.setQueryData(['cat', visit.catId, 'weights', {}], []);
    await user.click(screen.getByRole('button', { name: 'Record vitals' }));
    await user.type(await screen.findByLabelText('Weight, kilograms'), '4.30');
    await user.click(screen.getByRole('button', { name: 'Save vitals' }));
    await waitFor(() =>
      expect(client.getQueryState(catQueryKey(visit.catId))?.isInvalidated).toBe(true),
    );
    expect(client.getQueryState(['cat', visit.catId, 'weights', {}])?.isInvalidated).toBe(true);
  });

  it('lists entries newest first with units and the confirmed-outside-range words', async () => {
    const visit = await getVisit(ids.visitOpenId);
    const withWarning: VisitDetail = {
      ...visit,
      vitals: [
        ...visit.vitals,
        {
          ...visit.vitals[0]!,
          id: 'newer',
          measuredOn: new Date(Date.now() + 1000).toISOString(),
          temperature: { value: '42.0', unit: '°C' },
          confirmedOutOfRange: ['Temperature'],
        },
      ],
    };
    renderWithClient(<VitalsPanel visit={withWarning} />);
    const temperatures = screen.getAllByText(/°C/);
    expect(temperatures[0]).toHaveTextContent('42.0 °C');
    expect(screen.getByText('confirmed outside usual range')).toBeInTheDocument();
    expect(screen.getAllByText('4.20 kg', { exact: false }).length).toBeGreaterThan(0);
  });
});

describe('server errors on a form', () => {
  it('U-X1: a 400 never renders its errorCode', async () => {
    const error = new ApiValidationError({
      isValid: false,
      errors: [
        {
          propertyName: 'Weight',
          errorCode: 'VitalSignOutOfRange',
          errorMessage: '',
          resourceName: 'Visit',
        },
      ],
    });
    const user = userEvent.setup();
    renderWithClient(
      <VitalsForm
        zone="Asia/Dhaka"
        save={async () => {
          throw error;
        }}
      />,
    );
    await user.type(await screen.findByLabelText('Weight, kilograms'), '4');
    await act(async () => {
      await user.click(screen.getByRole('button', { name: 'Save vitals' }));
    });
    expect(
      await screen.findByText(
        'That value is outside what can be recorded. Check it and try again.',
      ),
    ).toBeInTheDocument();
    expect(screen.queryByText(/VitalSignOutOfRange/)).toBeNull();
  });
});
