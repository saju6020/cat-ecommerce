import { describe, expect, it, vi } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { http, HttpResponse } from 'msw';
import { server } from '@/mocks/server';
import { NotificationPreferenceMatrix } from '@/components/account/NotificationPreferenceMatrix';
import { useChangeNotificationPreferences } from '@/lib/hooks/useAccountMutations';
import { ApiRateLimitedError } from '@/lib/api/client';
import type { NotificationPreferences } from '@/lib/api/account';

// UnsavedChangesGuard (rendered by NotificationPreferenceMatrix whenever the form is dirty) calls
// useRouter() — ARCH gate MEDIUM (first pass) fix, replacing window.confirm with the shared
// ConfirmDialog; see profile-form.test.tsx's identical-purpose mock.
vi.mock('next/navigation', () => ({
  useRouter: () => ({ push: vi.fn() }),
}));

/** ARCH gate HIGH 3 (first pass) — stand-in for
 *  app/(portal)/portal/account/notifications/page.tsx; see register-form.test.tsx's own
 *  identical-purpose wrapper comment. */
function NotificationPreferenceMatrixWithHooks({
  preferences,
}: {
  preferences: NotificationPreferences;
}) {
  const mutation = useChangeNotificationPreferences();
  return (
    <NotificationPreferenceMatrix
      preferences={preferences}
      onSave={(changes) => mutation.mutateAsync(changes)}
      isPending={mutation.isPending}
      rateLimitError={mutation.error instanceof ApiRateLimitedError ? mutation.error : null}
    />
  );
}

function renderWithClient(children: React.ReactNode) {
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return render(<QueryClientProvider client={queryClient}>{children}</QueryClientProvider>);
}

const preferences: NotificationPreferences = {
  cells: [
    {
      channel: 'SMS',
      category: 'Appointments',
      isGranted: true,
      isEditable: false,
      isAlwaysOn: true,
    },
    {
      channel: 'Email',
      category: 'Appointments',
      isGranted: true,
      isEditable: false,
      isAlwaysOn: true,
    },
    {
      channel: 'SMS',
      category: 'Marketing',
      isGranted: false,
      isEditable: true,
      isAlwaysOn: false,
    },
    {
      channel: 'Email',
      category: 'Marketing',
      isGranted: false,
      isEditable: true,
      isAlwaysOn: false,
    },
  ],
};

describe('NotificationPreferenceMatrix — US-ACC-06', () => {
  it('U-E6: renders a real <table> from the API, with locked cells disabled and reading "Always on"', () => {
    renderWithClient(<NotificationPreferenceMatrixWithHooks preferences={preferences} />);
    const table = screen.getByRole('table');
    expect(table).toBeInTheDocument();
    expect(screen.getAllByText('Always on')).toHaveLength(2);
    const checkboxes = screen.getAllByRole('checkbox');
    const alwaysOnBoxes = checkboxes.filter((box) => (box as HTMLInputElement).disabled);
    expect(alwaysOnBoxes).toHaveLength(2);
  });

  it('U-E8: Save sends only the cells that actually changed', async () => {
    let capturedBody: unknown;
    server.use(
      http.put('*/api/customers/me/notification-preferences', async ({ request }) => {
        capturedBody = await request.json();
        return new HttpResponse(null, { status: 204 });
      }),
    );
    const user = userEvent.setup();
    renderWithClient(<NotificationPreferenceMatrixWithHooks preferences={preferences} />);

    const smsMarketingCheckbox = screen.getByLabelText('Marketing by SMS');
    await user.click(smsMarketingCheckbox);
    await user.click(screen.getByRole('button', { name: /save changes/i }));

    await waitFor(() => expect(capturedBody).toBeDefined());
    expect(capturedBody).toEqual({
      changes: [{ channel: 'SMS', category: 'Marketing', isGranted: true }],
    });
    expect(await screen.findByText(/preferences have been saved/i)).toBeInTheDocument();
  });

  it('never autosaves — toggling alone does not call the API', async () => {
    const putSpy = vi.fn();
    server.use(
      http.put('*/api/customers/me/notification-preferences', () => {
        putSpy();
        return new HttpResponse(null, { status: 204 });
      }),
    );
    const user = userEvent.setup();
    renderWithClient(<NotificationPreferenceMatrixWithHooks preferences={preferences} />);
    await user.click(screen.getByLabelText('Marketing by Email'));
    expect(putSpy).not.toHaveBeenCalled();
  });
});
