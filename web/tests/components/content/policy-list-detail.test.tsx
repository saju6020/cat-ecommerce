import { describe, expect, it, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { PolicyList } from '@/components/content/PolicyList';

vi.mock('next/navigation', () => ({ useRouter: () => ({ push: vi.fn() }) }));
import { PolicyDetail } from '@/components/content/PolicyDetail';
import type { PolicyDocument } from '@/lib/api/admin-content';

const POLICY: PolicyDocument = {
  key: 'terms',
  title: 'Terms of service',
  requiresAcceptance: true,
  currentVersionNumber: 1,
  versions: [
    {
      versionNumber: 1,
      body: 'Body text.',
      publishedOn: '2027-01-01T00:00:00Z',
      changeSummary: 'Initial.',
    },
  ],
};

describe('PolicyList/PolicyDetail — HO-ADM-002 §11.6, owner decision 2026-09-26 (Amended (28))', () => {
  it('"New policy" is shown only to an Administrator (canManage) — a Shop Manager sees the list without it', () => {
    render(
      <PolicyList policies={[POLICY]} canManage={false} onCreate={vi.fn()} isPending={false} />,
    );
    expect(screen.getByText('Terms of service')).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: /new policy/i })).not.toBeInTheDocument();
  });

  it('"New policy" is shown to an Administrator', () => {
    render(<PolicyList policies={[POLICY]} canManage onCreate={vi.fn()} isPending={false} />);
    expect(screen.getByRole('button', { name: /new policy/i })).toBeInTheDocument();
  });

  it('"Publish new version" is shown only to an Administrator; the version list itself has no edit or delete control either way', () => {
    const { rerender } = render(
      <PolicyDetail
        policy={POLICY}
        canManage={false}
        onPublishVersion={vi.fn()}
        isPending={false}
      />,
    );
    expect(screen.queryByRole('button', { name: /publish new version/i })).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: /^edit$/i })).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: /^delete$/i })).not.toBeInTheDocument();

    rerender(
      <PolicyDetail policy={POLICY} canManage onPublishVersion={vi.fn()} isPending={false} />,
    );
    expect(screen.getByRole('button', { name: /publish new version/i })).toBeInTheDocument();
  });

  it('publishing a new version of a policy that requires acceptance confirms that customers will be asked to accept it', async () => {
    const user = userEvent.setup();
    const onPublishVersion = vi.fn().mockResolvedValue(undefined);
    render(
      <PolicyDetail
        policy={POLICY}
        canManage
        onPublishVersion={onPublishVersion}
        isPending={false}
      />,
    );

    await user.type(screen.getByLabelText(/^body$/i), 'New text.');
    await user.type(screen.getByLabelText(/change summary/i), 'Updated wording.');
    await user.click(screen.getByRole('button', { name: /publish new version/i }));

    const dialog = await screen.findByRole('dialog');
    expect(dialog).toHaveTextContent(/asked to accept/i);
  });
});
