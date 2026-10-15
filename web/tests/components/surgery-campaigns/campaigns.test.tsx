import { beforeEach, describe, expect, it, vi } from 'vitest';
import { screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { http, HttpResponse } from 'msw';
import { CampaignCasesTable } from '@/components/surgery-campaigns/CampaignCasesTable';
import { SurgeryCampaignForm } from '@/components/surgery-campaigns/SurgeryCampaignForm';
import { SurgeryCampaignTable } from '@/components/surgery-campaigns/SurgeryCampaignTable';
import { getCampaignCases, listSurgeryCampaigns } from '@/lib/api/surgery-campaigns';
import { surgeryFixtureIds as ids } from '@/mocks/handlers/surgeries';
import { server } from '@/mocks/server';
import { renderWithClient, resetSurgeryTest, signInAs } from '../surgery/helpers';

vi.mock('next/navigation', async () =>
  (await import('../common/pharmacyTestUtils')).navigationMock(),
);
vi.setConfig({ testTimeout: 40_000 });

beforeEach(() => {
  resetSurgeryTest();
  signInAs(['Administrator']);
});

describe('campaign cases (US-SRG-11 — reportable separately)', () => {
  it('lists the cases with both prices and a totals row from the server', async () => {
    const data = await getCampaignCases(ids.campaignOutreachId, {});
    renderWithClient(
      <CampaignCasesTable
        data={data}
        zone="Asia/Dhaka"
        isLoading={false}
        isError={false}
        onRetry={vi.fn()}
        onPageChange={vi.fn()}
      />,
    );
    expect(screen.getAllByText('Shadow').length).toBeGreaterThan(0);
    expect(screen.getByLabelText('Totals')).toHaveTextContent(
      /1 case · .*800.* campaign · .*2,000.* standard/,
    );
    expect(
      screen.getByText('Community cat, trapped by the neighbourhood group.'),
    ).toBeInTheDocument();
  });
});

describe('campaign list and editor', () => {
  async function table(roles: string[]) {
    signInAs(roles);
    const page = await listSurgeryCampaigns({});
    return renderWithClient(
      <SurgeryCampaignTable
        rows={page.items}
        canManage={roles.includes('Administrator')}
        isLoading={false}
        isError={false}
        onRetry={vi.fn()}
      />,
    );
  }

  it('an administrator can create and edit; everyone else can only read', async () => {
    await table(['Administrator']);
    expect(await screen.findByRole('button', { name: 'New campaign' })).toBeInTheDocument();
    expect(screen.getAllByRole('button', { name: /^Edit / }).length).toBe(2);
  });

  it('a vet sees the campaigns and their cases but no controls', async () => {
    await table(['Vet']);
    expect(screen.getByText('Community cat spay and neuter drive')).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'New campaign' })).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: /^Edit / })).not.toBeInTheDocument();
    expect(screen.getAllByRole('link', { name: /^Cases for / }).length).toBe(2);
  });

  it('shows locked prices as read-only with the reason in words', async () => {
    signInAs(['Administrator']);
    const page = await listSurgeryCampaigns({});
    const locked = page.items.find((c) => c.id === ids.campaignOutreachId)!;
    expect(locked.pricesLocked).toBe(true);
    renderWithClient(<SurgeryCampaignForm campaign={locked} onClose={vi.fn()} />);
    expect(
      await screen.findByText(
        'Prices can’t change once a surgery has been booked under this campaign.',
      ),
    ).toBeInTheDocument();
    expect(await screen.findByLabelText('Spay')).toBeDisabled();
  });

  it('CampaignPriceLocked from the server is written as a sentence', async () => {
    const user = userEvent.setup();
    server.use(
      http.put('*/api/staff/surgery-campaigns/:id', () =>
        HttpResponse.json(
          {
            isValid: false,
            errors: [
              {
                propertyName: 'Prices',
                errorCode: 'CampaignPriceLocked',
                errorMessage: '',
                resourceName: 'Surgery',
              },
            ],
          },
          { status: 400 },
        ),
      ),
    );
    signInAs(['Administrator']);
    const page = await listSurgeryCampaigns({});
    const open = page.items.find((c) => c.id === ids.campaignExhaustedId)!;
    renderWithClient(<SurgeryCampaignForm campaign={open} onClose={vi.fn()} />);
    await user.click(await screen.findByRole('button', { name: 'Save campaign' }));
    expect(await screen.findByRole('alert')).toHaveTextContent(
      'Prices can’t change once a surgery has been booked under this campaign.',
    );
    expect(screen.queryByText('CampaignPriceLocked')).not.toBeInTheDocument();
  });

  it('tells a duplicate code in words', async () => {
    const user = userEvent.setup();
    renderWithClient(<SurgeryCampaignForm campaign={null} onClose={vi.fn()} />);
    await user.type(await screen.findByLabelText('Code'), 'TNR-OCT');
    await user.type(screen.getByLabelText('Name'), 'Another drive');
    await user.type(screen.getByLabelText('First day'), '2026-11-01');
    await user.type(screen.getByLabelText('Last day'), '2026-11-30');
    await user.type(await screen.findByLabelText('Spay'), '900');
    await user.click(screen.getByRole('button', { name: 'Save campaign' }));
    const alert = await screen.findByRole('alert');
    await waitFor(() =>
      expect(
        within(alert).getByText('A campaign with this code already exists.'),
      ).toBeInTheDocument(),
    );
  });
});
