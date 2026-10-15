import { beforeEach, describe, expect, it, vi } from 'vitest';
import { screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { SurgeryProtocolTable } from '@/components/surgery/SurgeryProtocolTable';
import { followUpRuleError } from '@/components/surgery/SurgeryProtocolForm';
import { listSurgeryProtocols } from '@/lib/api/surgery-protocols';
import { renderWithClient, resetSurgeryTest, signInAs } from './helpers';

vi.mock('next/navigation', async () =>
  (await import('../common/pharmacyTestUtils')).navigationMock(),
);
vi.setConfig({ testTimeout: 40_000 });

beforeEach(() => {
  resetSurgeryTest();
  signInAs(['Vet']);
});

async function table(canEdit: boolean) {
  const protocols = await listSurgeryProtocols();
  renderWithClient(
    <SurgeryProtocolTable
      protocols={protocols}
      canEdit={canEdit}
      isLoading={false}
      isError={false}
      onRetry={vi.fn()}
    />,
  );
}

describe('protocols (US-SRG-07)', () => {
  it('marks a procedure with no protocol of its own as Using defaults', async () => {
    await table(true);
    expect(screen.getAllByText('Using defaults').length).toBeGreaterThan(0);
    expect(screen.getByText('Customised')).toBeInTheDocument();
  });

  it('shows no Edit button to someone who may not manage protocols', async () => {
    await table(false);
    expect(screen.queryByRole('button', { name: /^Edit / })).not.toBeInTheDocument();
  });

  it('a follow-up whose day falls outside its own window is a field error, units in days', async () => {
    const user = userEvent.setup();
    await table(true);
    await user.click(screen.getByRole('button', { name: 'Edit Spay' }));
    const dialog = await screen.findByRole('dialog', { name: /Spay protocol/ });
    const day = within(dialog).getAllByLabelText('Due on day')[0]!;
    await user.clear(day);
    await user.type(day, '9');
    await user.click(within(dialog).getByRole('button', { name: 'Save protocol' }));
    expect(await within(dialog).findByText(/must sit inside its window/)).toBeInTheDocument();
    expect(within(dialog).getAllByText('days').length).toBeGreaterThan(0);
  });

  it('followUpRuleError allows from <= day <= to, whole days 0 to 60', () => {
    const ok = { kind: 'WoundCheck' as const, dayOffset: 3, windowFromDay: 2, windowToDay: 5 };
    expect(followUpRuleError(ok)).toBeNull();
    expect(followUpRuleError({ ...ok, dayOffset: 6 })).toMatch(/inside its window/);
    expect(followUpRuleError({ ...ok, windowToDay: 61 })).toMatch(/0 to 60/);
  });
});
