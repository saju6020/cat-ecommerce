import { beforeEach, describe, expect, it, vi } from 'vitest';
import { render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { FormularyMedicineForm } from '@/components/formulary/FormularyMedicineForm';
import { LinkedSkuPicker } from '@/components/formulary/LinkedSkuPicker';
import { ActivationToggle, ControlledToggle } from '@/components/formulary/MedicineActions';
import FormularyPage from '@/app/(staff)/staff/admin/formulary/page';
import { ApiConflictError, ApiValidationError } from '@/lib/api/client';
import { getFormularyMedicine } from '@/lib/api/formulary';
import {
  pharmacyFixtureIds as ids,
  resetPharmacyMockData,
  setPharmacyCallerRoles,
} from '@/mocks/handlers/pharmacy';
import { navigation, renderWithClient, resetNavigation } from '../common/pharmacyTestUtils';

vi.mock('next/navigation', async () =>
  (await import('../common/pharmacyTestUtils')).navigationMock(),
);
vi.mock('@/lib/hooks/useStaffRoles', () => ({
  useStaffRoles: () => ({
    roleIds: ['Administrator'],
    ready: true,
    has: (...w: string[]) => w.includes('Administrator'),
  }),
}));
vi.setConfig({ testTimeout: 20_000 });

beforeEach(() => {
  resetPharmacyMockData();
  resetNavigation();
  setPharmacyCallerRoles(['Administrator']);
});

describe('the formulary list (UI-4)', () => {
  it('lists strengths with units and shows Controlled and Stocked as words with icons', async () => {
    renderWithClient(<FormularyPage />);
    expect(await screen.findByRole('link', { name: 'Buprenorphine injection' })).toHaveAttribute(
      'href',
      `/staff/admin/formulary/${ids.medBuprenorphineId}`,
    );
    expect(screen.getAllByText('Controlled').length).toBeGreaterThan(0);
    expect(screen.getAllByText('Stocked').length).toBeGreaterThan(0);
    expect(screen.getByText('Not stocked', { selector: 'span' })).toBeInTheDocument(); // gabapentin
    expect(
      screen.getAllByText('1.5 mg per 1 ml', { selector: '[data-clinical-value]' }).length,
    ).toBeGreaterThan(0);
    expect(screen.getByRole('link', { name: /New medicine/ })).toBeInTheDocument();
  });
});

describe('U-E1 and U-E2 — the editor', () => {
  it('a per-kg medicine without a range gets field errors on min and max', async () => {
    const user = userEvent.setup();
    renderWithClient(<FormularyMedicineForm onSubmit={vi.fn()} />);
    await user.click(screen.getByRole('button', { name: 'Create medicine' }));
    expect(await screen.findByText('Enter the minimum dose in mg/kg.')).toBeInTheDocument();
    expect(screen.getByText('Enter the maximum dose in mg/kg.')).toBeInTheDocument();
    expect(screen.getByRole('textbox', { name: /minimum dose/i })).toHaveAttribute(
      'aria-invalid',
      'true',
    );
  });

  it('a fixed-dose medicine needs no range', async () => {
    const user = userEvent.setup();
    renderWithClient(<FormularyMedicineForm onSubmit={vi.fn()} />);
    await user.selectOptions(screen.getByLabelText(/dosing basis/i), 'Fixed');
    expect(screen.queryByRole('textbox', { name: /minimum dose/i })).not.toBeInTheDocument();
  });

  it('previews the strength with its units as it is typed', async () => {
    const user = userEvent.setup();
    renderWithClient(<FormularyMedicineForm onSubmit={vi.fn()} />);
    await user.type(screen.getByRole('textbox', { name: /^strength/i }), '1.5');
    await user.selectOptions(screen.getByRole('combobox', { name: 'Strength unit' }), 'Mg');
    await user.selectOptions(screen.getByRole('combobox', { name: 'Per unit' }), 'Millilitre');
    expect(screen.getByText(/Strength preview/)).toHaveTextContent(
      'Strength preview: 1.5 mg per 1 ml',
    );
  });

  it('chips for ingredients and a repeatable interaction row group', async () => {
    const user = userEvent.setup();
    renderWithClient(<FormularyMedicineForm onSubmit={vi.fn()} />);
    await user.type(screen.getByLabelText(/^active ingredients/i), 'meloxicam{Enter}');
    expect(screen.getByRole('button', { name: 'Remove meloxicam' })).toBeInTheDocument();
    await user.click(screen.getByRole('button', { name: /Add interaction/ }));
    expect(screen.getByRole('group', { name: 'Interaction 1' })).toBeInTheDocument();
    await user.selectOptions(screen.getByLabelText('Interacts with'), 'term');
    expect(screen.getByLabelText('Term')).toBeInTheDocument();
    await user.click(screen.getByRole('button', { name: /Remove interaction 1/ }));
    expect(screen.queryByRole('group', { name: 'Interaction 1' })).not.toBeInTheDocument();
  });

  it('U-X2 — a server 400 lands on its field and no error code reaches the DOM', async () => {
    const user = userEvent.setup();
    const detail = await getFormularyMedicine(ids.medMeloxicamId);
    const onSubmit = vi.fn().mockRejectedValue(
      new ApiValidationError({
        isValid: false,
        errors: [
          {
            propertyName: 'Name',
            errorCode: 'NotEmptyValidator',
            errorMessage: 'Name is taken by another medicine.',
            resourceName: 'Pharmacy',
          },
        ],
      }),
    );
    renderWithClient(<FormularyMedicineForm initial={detail} onSubmit={onSubmit} />);
    await user.click(screen.getByRole('button', { name: 'Save changes' }));
    expect(await screen.findByText('Name is taken by another medicine.')).toBeInTheDocument();
    expect(screen.getByRole('textbox', { name: /^name/i })).toHaveAttribute('aria-invalid', 'true');
    expect(document.body).not.toHaveTextContent('NotEmptyValidator');
  });

  it('U-X3 — leaving a dirty form asks first', async () => {
    const user = userEvent.setup();
    renderWithClient(
      <>
        <a href="#back-to-list">Back to the list</a>
        <FormularyMedicineForm onSubmit={vi.fn()} />
      </>,
    );
    await user.type(screen.getByRole('textbox', { name: /^name/i }), 'Something');
    await user.click(screen.getByRole('link', { name: 'Back to the list' }));
    expect(await screen.findByText('Leave this page?')).toBeInTheDocument();
    expect(navigation.push).not.toHaveBeenCalled();
  });
});

describe('U-E4 — someone else changed the medicine', () => {
  it('a 409 says so, keeps what was typed and offers Reload', async () => {
    const user = userEvent.setup();
    const detail = await getFormularyMedicine(ids.medMeloxicamId);
    const onReload = vi.fn();
    renderWithClient(
      <FormularyMedicineForm
        initial={detail}
        onReload={onReload}
        onSubmit={vi.fn().mockRejectedValue(new ApiConflictError())}
      />,
    );
    const name = screen.getByRole('textbox', { name: /^name/i });
    await user.clear(name);
    await user.type(name, 'Meloxicam suspension (renamed)');
    await user.click(screen.getByRole('button', { name: 'Save changes' }));
    const banner = await screen.findByText(
      'Someone else changed this medicine. Reload to see their changes.',
    );
    expect(banner).toBeInTheDocument();
    expect(screen.getByRole('textbox', { name: /^name/i })).toHaveValue(
      'Meloxicam suspension (renamed)',
    );
    await user.click(screen.getByRole('button', { name: 'Reload' }));
    expect(onReload).toHaveBeenCalledTimes(1);
  });
});

describe('U-E3 — the controlled flag and activation are separate, confirmed actions', () => {
  it('turning it on needs a schedule and a confirmation', async () => {
    const user = userEvent.setup();
    const onSet = vi.fn().mockResolvedValue(undefined);
    render(
      <ControlledToggle
        medicine={{ name: 'Gabapentin capsules', isControlled: false, controlledSchedule: null }}
        onSet={onSet}
      />,
    );
    await user.click(screen.getByRole('button', { name: /Mark as controlled/ }));
    const dialog = await screen.findByRole('dialog');
    expect(dialog).toHaveTextContent('every supply of this medicine needs a witness');
    const confirm = within(dialog).getByRole('button', { name: 'Mark as controlled' });
    expect(confirm).toBeDisabled();
    await user.type(within(dialog).getByLabelText(/schedule/i), 'Schedule 4');
    expect(confirm).toBeEnabled();
    await user.click(confirm);
    await waitFor(() =>
      expect(onSet).toHaveBeenCalledWith({ isControlled: true, controlledSchedule: 'Schedule 4' }),
    );
  });

  it('turning it off reveals a reason field when the server asks for one', async () => {
    const user = userEvent.setup();
    const onSet = vi
      .fn()
      .mockRejectedValueOnce(
        new ApiValidationError({
          isValid: false,
          errors: [
            {
              propertyName: 'Reason',
              errorCode: 'ControlledFlagChangeReasonRequired',
              errorMessage:
                'This medicine has register entries — say why it is no longer controlled.',
              resourceName: 'Pharmacy',
            },
          ],
        }),
      )
      .mockResolvedValueOnce(undefined);
    render(
      <ControlledToggle
        medicine={{
          name: 'Buprenorphine injection',
          isControlled: true,
          controlledSchedule: 'Schedule 3',
        }}
        onSet={onSet}
      />,
    );
    await user.click(screen.getByRole('button', { name: /Clear the controlled flag/ }));
    const dialog = await screen.findByRole('dialog');
    expect(within(dialog).queryByLabelText(/reason/i)).not.toBeInTheDocument();
    await user.click(within(dialog).getByRole('button', { name: 'Clear the flag' }));
    expect(await within(dialog).findByLabelText(/reason/i)).toBeInTheDocument();
    expect(within(dialog).getByText(/say why it is no longer controlled/)).toBeInTheDocument();
    await user.type(within(dialog).getByLabelText(/reason/i), 'Reclassified');
    await user.click(within(dialog).getByRole('button', { name: 'Clear the flag' }));
    await waitFor(() =>
      expect(onSet).toHaveBeenLastCalledWith({ isControlled: false, reason: 'Reclassified' }),
    );
  });

  it('deactivating says existing prescriptions remain dispensable', async () => {
    const user = userEvent.setup();
    const onSet = vi.fn().mockResolvedValue(undefined);
    render(
      <ActivationToggle medicine={{ name: 'Maropitant tablets', isActive: true }} onSet={onSet} />,
    );
    await user.click(screen.getByRole('button', { name: /Deactivate…/ }));
    const dialog = await screen.findByRole('dialog');
    expect(dialog).toHaveTextContent('Prescriptions that already exist remain dispensable.');
    await user.click(within(dialog).getByRole('button', { name: 'Deactivate' }));
    await waitFor(() => expect(onSet).toHaveBeenCalledWith(false));
  });
});

describe('U-E5 — the linked-SKU picker', () => {
  it('shows a SKU that belongs to another medicine disabled, with that medicine’s name, and will not select it', async () => {
    const user = userEvent.setup();
    const onChange = vi.fn();
    renderWithClient(
      <LinkedSkuPicker medicineId={ids.medPrednisoloneId} linked={[]} onChange={onChange} />,
    );
    await user.type(screen.getByRole('combobox', { name: /link a clinic sku/i }), 'melox');
    const option = await screen.findByRole(
      'option',
      { name: /Meloxicam 1\.5 mg\/ml oral suspension — 15 ml bottle/ },
      { timeout: 4000 },
    );
    expect(option).toHaveAttribute('aria-disabled', 'true');
    expect(option).toHaveTextContent('Already linked to Meloxicam oral suspension');
    await user.click(option);
    expect(onChange).not.toHaveBeenCalled();

    const free = await screen.findByRole('option', { name: /30 ml bottle/ });
    expect(free).not.toHaveAttribute('aria-disabled');
    expect(free).toHaveTextContent('Barcode');
    await user.click(free);
    expect(onChange).toHaveBeenCalledWith([
      expect.objectContaining({ variantLabel: '30 ml bottle' }),
    ]);
  });
});
