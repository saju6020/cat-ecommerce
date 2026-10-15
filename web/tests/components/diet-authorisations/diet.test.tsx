import { beforeEach, describe, expect, it, vi } from 'vitest';
import { screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { CatDietAuthorisationsTable } from '@/components/diet-authorisations/CatDietAuthorisationsTable';
import { DietAuthorisationForm } from '@/components/diet-authorisations/DietAuthorisationForm';
import { listCatDietAuthorisations } from '@/lib/api/diet-authorisations';
import { emrFixtureIds } from '@/mocks/handlers/visits';
import { pharmacyFixtureIds as ids, resetPharmacyMockData } from '@/mocks/handlers/pharmacy';
import { renderWithClient, resetNavigation } from '../common/pharmacyTestUtils';

vi.mock('next/navigation', async () =>
  (await import('../common/pharmacyTestUtils')).navigationMock(),
);
vi.mock('@/lib/hooks/useStaffRoles', () => ({
  useStaffRoles: () => ({
    roleIds: ['Vet'],
    ready: true,
    has: (...w: string[]) => w.includes('Vet'),
  }),
}));
vi.setConfig({ testTimeout: 30_000 });

beforeEach(() => {
  resetPharmacyMockData();
  resetNavigation();
});

async function authorise(user: ReturnType<typeof userEvent.setup>) {
  await user.type(await screen.findByRole('combobox', { name: /^food/i }), 'renal');
  await user.click(
    await screen.findByRole('option', { name: /Renal Care dry food/ }, { timeout: 4000 }),
  );
  await user.click(screen.getByRole('button', { name: '90 days' }));
  await user.click(screen.getByRole('button', { name: 'Authorise diet' }));
}

describe('U-G1 — authorising a diet from a visit (US-PHM-08)', () => {
  it('needs a visit', () => {
    renderWithClient(
      <DietAuthorisationForm catId={ids.catWhiskersId} visitId={null} onAuthorised={vi.fn()} />,
    );
    expect(
      screen.getByText('Prescriptions are issued from an open visit. Open the cat’s visit first.'),
    ).toBeInTheDocument();
    expect(screen.queryByRole('form', { name: 'Authorise a diet' })).not.toBeInTheDocument();
  });

  it('quick picks fill the last day; success reports the new authorisation; a second one for the same food says extend it instead', async () => {
    const user = userEvent.setup();
    const onAuthorised = vi.fn();
    const first = renderWithClient(
      <DietAuthorisationForm
        catId={ids.catWhiskersId}
        visitId={emrFixtureIds.visitOpenId}
        onAuthorised={onAuthorised}
      />,
    );
    await screen.findByText('Visit open');
    await authorise(user);
    await waitFor(() => expect(onAuthorised).toHaveBeenCalledTimes(1));
    first.unmount();

    renderWithClient(
      <DietAuthorisationForm
        catId={ids.catWhiskersId}
        visitId={emrFixtureIds.visitOpenId}
        onAuthorised={onAuthorised}
      />,
    );
    await screen.findByText('Visit open');
    await authorise(user);
    const warning = await screen.findByText(
      /already has an active authorisation for this food — extend it instead/,
    );
    expect(warning).toHaveTextContent('Whiskers already has an active authorisation');
    expect(screen.getByRole('link', { name: 'Go to the diet authorisations' })).toHaveAttribute(
      'href',
      `/staff/cats/${ids.catWhiskersId}/diet-authorisations`,
    );
    expect(onAuthorised).toHaveBeenCalledTimes(1);
  });

  it('asks for the food before anything is sent', async () => {
    const user = userEvent.setup();
    const onAuthorised = vi.fn();
    renderWithClient(
      <DietAuthorisationForm
        catId={ids.catWhiskersId}
        visitId={emrFixtureIds.visitOpenId}
        onAuthorised={onAuthorised}
      />,
    );
    await screen.findByText('Visit open');
    await user.click(screen.getByRole('button', { name: 'Authorise diet' }));
    expect(await screen.findByText('Choose the food.')).toBeInTheDocument();
    expect(screen.getByText('Enter the last day.')).toBeInTheDocument();
    expect(onAuthorised).not.toHaveBeenCalled();
  });
});

describe('the cat’s authorisations', () => {
  it('shows Active and Expired in words; a vet can extend and revoke (with a reason)', async () => {
    const user = userEvent.setup();
    const rows = await listCatDietAuthorisations(ids.catMittensId, 'all');
    const onExtend = vi.fn().mockResolvedValue(undefined);
    const onRevoke = vi.fn().mockResolvedValue(undefined);
    renderWithClient(
      <CatDietAuthorisationsTable
        catName="Mittens"
        rows={rows}
        canIssue
        isLoading={false}
        isError={false}
        onRetry={vi.fn()}
        onExtend={onExtend}
        onRevoke={onRevoke}
      />,
    );
    expect(screen.getAllByText('Active').length).toBeGreaterThan(0);
    expect(screen.getAllByText('Expired').length).toBeGreaterThan(0);

    await user.click(screen.getByRole('button', { name: /Revoke Renal Care/ }));
    const dialog = await screen.findByRole('dialog');
    expect(dialog).toHaveTextContent('will no longer be able to buy this food in the shop');
    await user.click(within(dialog).getByRole('button', { name: 'Revoke' }));
    expect(await within(dialog).findByText(/Enter the reason for revoking/i)).toBeInTheDocument();
    await user.type(within(dialog).getByLabelText(/reason for revoking/i), 'Diet no longer needed');
    await user.click(within(dialog).getByRole('button', { name: 'Revoke' }));
    await waitFor(() =>
      expect(onRevoke).toHaveBeenCalledWith(ids.dietAuthorisationActiveId, 'Diet no longer needed'),
    );

    await user.click(screen.getByRole('button', { name: /Extend Urinary Support/ }));
    const extend = await screen.findByRole('dialog');
    const confirm = within(extend).getByRole('button', { name: 'Extend' });
    expect(confirm).toBeDisabled();
    await user.type(within(extend).getByLabelText(/new last day/i), '2099-01-01');
    await user.click(confirm);
    await waitFor(() => expect(onExtend).toHaveBeenCalledWith(expect.any(String), '2099-01-01'));
  });

  it('offers no actions to someone who cannot authorise', async () => {
    renderWithClient(
      <CatDietAuthorisationsTable
        catName="Mittens"
        rows={await listCatDietAuthorisations(ids.catMittensId, 'all')}
        canIssue={false}
        isLoading={false}
        isError={false}
        onRetry={vi.fn()}
        onExtend={vi.fn()}
        onRevoke={vi.fn()}
      />,
    );
    expect(screen.queryByRole('button', { name: /Extend|Revoke/ })).not.toBeInTheDocument();
  });
});
