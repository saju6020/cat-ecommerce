import { beforeEach, describe, expect, it, vi } from 'vitest';
import { render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { PrescriptionDetail } from '@/components/prescriptions/PrescriptionDetail';
import { getPrescription, type PrescriptionDetail as Detail } from '@/lib/api/prescriptions';
import {
  pharmacyFixtureIds as ids,
  resetPharmacyMockData,
  setPharmacyCallerRoles,
} from '@/mocks/handlers/pharmacy';
import { resetNavigation } from '../common/pharmacyTestUtils';

vi.mock('next/navigation', async () =>
  (await import('../common/pharmacyTestUtils')).navigationMock(),
);
vi.setConfig({ testTimeout: 20_000 });

beforeEach(() => {
  resetPharmacyMockData();
  resetNavigation();
  setPharmacyCallerRoles(null);
});

async function load(id: string, roles: string[] = ['Vet']): Promise<Detail> {
  setPharmacyCallerRoles(roles);
  return getPrescription(id);
}

function view(
  detail: Detail,
  handlers: Partial<React.ComponentProps<typeof PrescriptionDetail>> = {},
) {
  return render(
    <PrescriptionDetail
      detail={detail}
      zone="Asia/Dhaka"
      catId={detail.catId}
      onCancel={vi.fn().mockResolvedValue(undefined)}
      onDeclineRepeat={vi.fn().mockResolvedValue(undefined)}
      {...handlers}
    />,
  );
}

describe('U-B1 — actions come only from allowedActions', () => {
  it('a vet sees Dispense, Renew and Cancel', async () => {
    view(await load(ids.rxMittensActiveId));
    expect(screen.getByRole('link', { name: /Dispense/ })).toHaveAttribute(
      'href',
      `/staff/pharmacy/dispense/${ids.rxMittensActiveId}`,
    );
    expect(screen.getByRole('link', { name: /Renew/ })).toHaveAttribute(
      'href',
      `/staff/cats/${ids.catMittensId}/prescriptions/${ids.rxMittensActiveId}/renew`,
    );
    expect(screen.getByRole('button', { name: /Cancel prescription/ })).toBeInTheDocument();
  });

  it('reception sees Dispense and nothing else — no Renew, no Cancel, no edit control', async () => {
    view(await load(ids.rxMittensActiveId, ['Reception']));
    expect(screen.getByRole('link', { name: /Dispense/ })).toBeInTheDocument();
    expect(screen.queryByRole('link', { name: /Renew/ })).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: /Cancel prescription/ })).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: /edit|delete/i })).not.toBeInTheDocument();
  });

  it('Decline appears for a vet on an open repeat request, and not for reception', async () => {
    const vet = view(await load(ids.rxMittensRequestedId));
    expect(screen.getByRole('button', { name: 'Decline request' })).toBeInTheDocument();
    vet.unmount();
    view(await load(ids.rxMittensRequestedId, ['Reception']));
    expect(screen.queryByRole('button', { name: 'Decline request' })).not.toBeInTheDocument();
  });
});

describe('U-B2 — the safety record is shown as it was when issued', () => {
  it('weight, dose per kg, range, who acknowledged and the note', async () => {
    view(await load(ids.rxMittensActiveId));
    const panel = screen.getByRole('region', { name: /safety record/i });
    expect(panel).toHaveTextContent('Issued with 3.60 kg');
    expect(panel).toHaveTextContent('0.1250');
    expect(panel).toHaveTextContent('mg/kg');
    expect(panel).toHaveTextContent('above range');
    expect(panel).toHaveTextContent('Warnings acknowledged by Dr Akter');
    expect(panel).toHaveTextContent('Dose note: “Higher loading dose on day 1 only.”');
    // The dose and quantity are clinical values with their units.
    expect(document.querySelectorAll('[data-clinical-value]').length).toBeGreaterThan(3);
    expect(screen.getByText('0.30 ml', { selector: '[data-clinical-value]' })).toBeInTheDocument();
  });
});

describe('U-B3 — cancelling', () => {
  it('the confirmation names the prescription and the open repeat request, and needs a reason', async () => {
    const user = userEvent.setup();
    const onCancel = vi.fn().mockResolvedValue(undefined);
    view(await load(ids.rxMittensRequestedId), { onCancel });
    await user.click(screen.getByRole('button', { name: /Cancel prescription/ }));
    const dialog = await screen.findByRole('dialog');
    expect(dialog).toHaveTextContent(
      'Cancel RX-2026-000095 for Mittens? Any open repeat request will be declined.',
    );
    await user.click(within(dialog).getByRole('button', { name: 'Cancel prescription' }));
    expect(await within(dialog).findByText(/Enter the reason for cancelling/i)).toBeInTheDocument();
    expect(onCancel).not.toHaveBeenCalled();
    await user.type(within(dialog).getByLabelText(/reason for cancelling/i), 'Owner changed vets');
    await user.click(within(dialog).getByRole('button', { name: 'Cancel prescription' }));
    await waitFor(() => expect(onCancel).toHaveBeenCalledWith('Owner changed vets'));
  });
});
