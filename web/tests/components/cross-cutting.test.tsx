import { describe, expect, it, afterEach } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { http, HttpResponse } from 'msw';
import { server } from '@/mocks/server';
import { SignInForm } from '@/components/auth/SignInForm';
import { useSignInCustomer } from '@/lib/hooks/useAuthMutations';
import {
  apiFetch,
  ApiForbiddenError,
  ApiRateLimitedError,
  ApiUnauthorizedError,
} from '@/lib/api/client';

/** ARCH gate HIGH 3 (first pass) — stand-in for `app/(auth)/sign-in/page.tsx`; see
 *  register-form.test.tsx's own identical-purpose wrapper. */
function CustomerSignInFormWithHooks({ onSuccess }: { onSuccess: () => void }) {
  const mutation = useSignInCustomer();
  return (
    <SignInForm
      audience="customer"
      onSignIn={(values) => mutation.mutateAsync(values)}
      isPending={mutation.isPending}
      error={mutation.error}
      onSuccess={onSuccess}
    />
  );
}

function renderWithClient(children: React.ReactNode) {
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return render(<QueryClientProvider client={queryClient}>{children}</QueryClientProvider>);
}

describe('Cross-cutting — HO-ACC-002 §11.2/§8.4', () => {
  afterEach(() => {
    localStorage.clear();
    sessionStorage.clear();
  });

  it('U-X2/U-X4: a 400 on sign-in never puts errorCode in the DOM, and never touches web storage', async () => {
    server.use(
      http.post('*/api/auth/sign-in', () =>
        HttpResponse.json({ error: 'invalid_credentials', message: 'nope' }, { status: 401 }),
      ),
    );
    const user = userEvent.setup();
    renderWithClient(<CustomerSignInFormWithHooks onSuccess={() => {}} />);
    await user.type(screen.getByLabelText(/mobile number or email/i), 'x@example.com');
    await user.type(screen.getByLabelText(/^password/i), 'whatever12345');
    await user.click(screen.getByRole('button', { name: /^sign in$/i }));
    await screen.findByRole('alert');

    expect(document.body.textContent).not.toMatch(/invalid_credentials/);
    expect(localStorage.length).toBe(0);
    expect(sessionStorage.length).toBe(0);
  });

  it('client.ts parses a 401 session_invalid into ApiUnauthorizedError with no message field', async () => {
    server.use(
      http.get('*/api/customers/me', () =>
        HttpResponse.json({ error: 'session_invalid' }, { status: 401 }),
      ),
    );
    await expect(apiFetch('/api/customers/me')).rejects.toSatisfy((error: unknown) => {
      expect(error).toBeInstanceOf(ApiUnauthorizedError);
      expect((error as ApiUnauthorizedError).body).toEqual({ error: 'session_invalid' });
      return true;
    });
  });

  it('client.ts parses a 403 ActionNotPermitted into ApiForbiddenError', async () => {
    server.use(
      http.get('*/api/staff-roles', () =>
        HttpResponse.json(
          { errors: [{ errorCode: 'ActionNotPermitted', errorMessage: 'nope' }] },
          { status: 403 },
        ),
      ),
    );
    await expect(apiFetch('/api/staff-roles')).rejects.toBeInstanceOf(ApiForbiddenError);
  });

  it('client.ts parses a 429 into ApiRateLimitedError with retryAfterSeconds from the header', async () => {
    server.use(
      http.post('*/api/captcha', () =>
        HttpResponse.json(
          { isValid: false, errors: [] },
          { status: 429, headers: { 'Retry-After': '120' } },
        ),
      ),
    );
    await expect(apiFetch('/api/captcha', { method: 'POST' })).rejects.toSatisfy(
      (error: unknown) => {
        expect(error).toBeInstanceOf(ApiRateLimitedError);
        expect((error as ApiRateLimitedError).retryAfterSeconds).toBe(120);
        return true;
      },
    );
  });

  it('calls the session-error handler on a non-exempt 401, but not on the session-status endpoint itself', async () => {
    const { setSessionErrorHandler } = await import('@/lib/api/client');
    const calls: unknown[] = [];
    setSessionErrorHandler((body) => calls.push(body));

    server.use(
      http.get('*/api/auth/session', () =>
        HttpResponse.json({ error: 'session_invalid' }, { status: 401 }),
      ),
    );
    await apiFetch('/api/auth/session', { skipSessionErrorHandling: true }).catch(() => {});
    expect(calls).toHaveLength(0);

    server.use(
      http.get('*/api/customers/me', () =>
        HttpResponse.json({ error: 'session_invalid' }, { status: 401 }),
      ),
    );
    await apiFetch('/api/customers/me').catch(() => {});
    await waitFor(() => expect(calls).toHaveLength(1));

    setSessionErrorHandler(null);
  });
});
