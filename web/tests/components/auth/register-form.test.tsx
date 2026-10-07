import { describe, expect, it, vi, beforeEach } from 'vitest';
import { render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { http, HttpResponse } from 'msw';
import { server } from '@/mocks/server';
import { RegisterForm } from '@/components/auth/RegisterForm';
import { useCaptcha } from '@/lib/hooks/useCaptcha';
import { useRegisterCustomer } from '@/lib/hooks/useAuthMutations';
import { ApiRateLimitedError } from '@/lib/api/client';
import type { RegisterCustomerRequest } from '@/lib/api/auth';
import { accountsFixtureIds, resetAccountsMockData } from '@/mocks/handlers/areas/accounts';

vi.mock('next/navigation', () => ({
  useRouter: () => ({ push: vi.fn(), replace: vi.fn() }),
  usePathname: () => '/register',
}));

/**
 * ARCH gate HIGH 3 (first pass) — `RegisterForm` itself takes `captcha`/`onRegister`/`isPending`/
 * `rateLimitError` as props now (its own comment); this wrapper is the test-only stand-in for
 * `app/(auth)/register/page.tsx`, which is what actually calls `useCaptcha()`/
 * `useRegisterCustomer()` in the real app. Keeping it here (rather than moving every one of these
 * tests to a page-level test file) preserves this file's existing MSW-backed coverage of the real
 * hook wiring — captcha loading/error states, a real `429`/`500`, `mutateAsync` throwing the real
 * typed errors `RegisterForm` maps to fields — almost unchanged.
 */
function RegisterFormWithHooks({ onSuccess }: { onSuccess: () => void }) {
  const captcha = useCaptcha();
  const registerMutation = useRegisterCustomer();
  return (
    <RegisterForm
      captcha={captcha}
      onRegister={(request: RegisterCustomerRequest) => registerMutation.mutateAsync(request)}
      isPending={registerMutation.isPending}
      rateLimitError={
        registerMutation.error instanceof ApiRateLimitedError ? registerMutation.error : null
      }
      onSuccess={onSuccess}
    />
  );
}

function renderWithClient(children: React.ReactNode) {
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return render(<QueryClientProvider client={queryClient}>{children}</QueryClientProvider>);
}

beforeEach(() => {
  resetAccountsMockData();
});

async function fillValidForm(user: ReturnType<typeof userEvent.setup>) {
  await user.type(screen.getByLabelText(/full name/i), 'Jamie Owner');
  await user.type(screen.getByLabelText(/mobile number/i), '01712345678');
  await user.type(screen.getByLabelText(/^password/i), 'a-very-long-password');
  await screen.findByAltText(/security check/i);
  await user.type(screen.getByLabelText(/security check/i), 'ABCDE');
}

describe('RegisterForm — US-ACC-01', () => {
  it('U-B2: neither mobile nor email gives a form-level message linked to both fields', async () => {
    const user = userEvent.setup();
    renderWithClient(<RegisterFormWithHooks onSuccess={vi.fn()} />);
    await user.type(screen.getByLabelText(/full name/i), 'Jamie Owner');
    await user.type(screen.getByLabelText(/^password/i), 'a-very-long-password');
    await screen.findByAltText(/security check/i);
    await user.type(screen.getByLabelText(/security check/i), 'ABCDE');
    await user.click(screen.getByRole('button', { name: /create account/i }));

    const messages = await screen.findAllByText(/give a mobile number, an email, or both/i);
    expect(messages.length).toBeGreaterThanOrEqual(1);
  });

  it('U-B3: a 9-character password names the minimum and the actual length', async () => {
    const user = userEvent.setup();
    renderWithClient(<RegisterFormWithHooks onSuccess={vi.fn()} />);
    await user.type(screen.getByLabelText(/full name/i), 'Jamie Owner');
    await user.type(screen.getByLabelText(/mobile number/i), '01712345678');
    await user.type(screen.getByLabelText(/^password/i), '123456789');
    await screen.findByAltText(/security check/i);
    await user.type(screen.getByLabelText(/security check/i), 'ABCDE');
    await user.click(screen.getByRole('button', { name: /create account/i }));

    expect(
      await screen.findByText(/at least 10 characters \(you entered 9\)/i),
    ).toBeInTheDocument();
  });

  it('U-B4: a valid submit succeeds', async () => {
    const user = userEvent.setup();
    const onSuccess = vi.fn();
    renderWithClient(<RegisterFormWithHooks onSuccess={onSuccess} />);
    await fillValidForm(user);
    await user.click(screen.getByRole('button', { name: /create account/i }));
    await waitFor(() => expect(onSuccess).toHaveBeenCalledTimes(1));
  });

  it('U-B6: an identical message for a duplicate mobile, with and without an existing email, never naming "email"', async () => {
    const user = userEvent.setup();
    renderWithClient(<RegisterFormWithHooks onSuccess={vi.fn()} />);
    await user.type(screen.getByLabelText(/full name/i), 'Jamie Owner');
    await user.type(screen.getByLabelText(/mobile number/i), accountsFixtureIds.dupMobileNoEmail);
    await user.type(screen.getByLabelText(/^password/i), 'a-very-long-password');
    await screen.findByAltText(/security check/i);
    await user.type(screen.getByLabelText(/security check/i), 'ABCDE');
    await user.click(screen.getByRole('button', { name: /create account/i }));

    const messageA = await screen.findByText(/this mobile number is already registered/i);
    expect(messageA.textContent?.toLowerCase()).not.toContain('email');
    const mobileFieldWrapper = messageA.parentElement as HTMLElement;
    expect(
      within(mobileFieldWrapper).getByRole('link', { name: /^sign in$/i }),
    ).toBeInTheDocument();
    expect(
      within(mobileFieldWrapper).getByRole('link', { name: /reset your password/i }),
    ).toBeInTheDocument();
  });

  it('U-B7: a duplicate email is reported on the email field, never naming "mobile"/"phone"', async () => {
    const user = userEvent.setup();
    renderWithClient(<RegisterFormWithHooks onSuccess={vi.fn()} />);
    await user.type(screen.getByLabelText(/full name/i), 'Jamie Owner');
    await user.type(screen.getByLabelText(/^email/i), accountsFixtureIds.dupEmailOnly);
    await user.type(screen.getByLabelText(/^password/i), 'a-very-long-password');
    await screen.findByAltText(/security check/i);
    await user.type(screen.getByLabelText(/security check/i), 'ABCDE');
    await user.click(screen.getByRole('button', { name: /create account/i }));

    const message = await screen.findByText(/this email address is already registered/i);
    expect(message.textContent?.toLowerCase()).not.toMatch(/mobile|phone/);
  });

  it('U-B9: CaptchaInvalid puts the message on the CAPTCHA field, refetches, and clears password/answer', async () => {
    const user = userEvent.setup();
    renderWithClient(<RegisterFormWithHooks onSuccess={vi.fn()} />);
    await user.type(screen.getByLabelText(/full name/i), 'Jamie Owner');
    await user.type(screen.getByLabelText(/mobile number/i), '01712345678');
    await user.type(screen.getByLabelText(/^password/i), 'a-very-long-password');
    await screen.findByAltText(/security check/i);
    await user.type(screen.getByLabelText(/security check/i), 'WRONGCODE');
    await user.click(screen.getByRole('button', { name: /create account/i }));

    expect(await screen.findByText(/didn.t match, or the code expired/i)).toBeInTheDocument();
    expect((screen.getByLabelText(/^password/i) as HTMLInputElement).value).toBe('');
    expect((screen.getByLabelText(/security check/i) as HTMLInputElement).value).toBe('');
    expect((screen.getByLabelText(/full name/i) as HTMLInputElement).value).toBe('Jamie Owner');
    await waitFor(() => {
      const newSrc = screen.getByAltText(/security check/i).getAttribute('src');
      expect(newSrc).toBeTruthy();
    });
  });

  it('U-B11: a 429 with Retry-After: 540 reads "9 minutes", never raw seconds', async () => {
    server.use(
      http.post('*/api/customers', () =>
        HttpResponse.json(
          { isValid: false, errors: [] },
          { status: 429, headers: { 'Retry-After': '540' } },
        ),
      ),
    );
    const user = userEvent.setup();
    renderWithClient(<RegisterFormWithHooks onSuccess={vi.fn()} />);
    await fillValidForm(user);
    await user.click(screen.getByRole('button', { name: /create account/i }));

    const notice = await screen.findByText(/wait 9 minutes/i);
    expect(notice.textContent).not.toMatch(/540/);
  });

  it('U-B12: CAPTCHA a11y — alt states purpose (not the answer), help text present, "New code" is a text button', async () => {
    renderWithClient(<RegisterFormWithHooks onSuccess={vi.fn()} />);
    const image = await screen.findByAltText(/security check: an image of 5 letters and numbers/i);
    expect(image.getAttribute('alt')).not.toMatch(/ABCDE/);
    expect(screen.getByRole('button', { name: /new code/i })).toBeInTheDocument();
    expect(screen.getByText(/register in person at the clinic reception/i)).toBeInTheDocument();
  });

  it('U-B13: a CAPTCHA endpoint 500 shows the "couldn\'t load" state with a working retry', async () => {
    server.use(
      http.post('*/api/captcha', () => HttpResponse.json({ error: 'boom' }, { status: 500 })),
    );
    renderWithClient(<RegisterFormWithHooks onSuccess={vi.fn()} />);
    expect(await screen.findByText(/couldn.t load the security check/i)).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /try again/i })).toBeInTheDocument();
  });

  it('SEC gate MEDIUM (first pass): method="post", so a pre-hydration Enter/submit never puts the password/CAPTCHA answer in a GET URL', () => {
    renderWithClient(<RegisterFormWithHooks onSuccess={vi.fn()} />);
    expect(document.querySelector('form')).toHaveAttribute('method', 'post');
  });

  it('U-B8: breached and too-short passwords from the SERVER render distinct messages on the same field', async () => {
    const user = userEvent.setup();
    renderWithClient(<RegisterFormWithHooks onSuccess={vi.fn()} />);
    await user.type(screen.getByLabelText(/full name/i), 'Jamie Owner');
    await user.type(screen.getByLabelText(/mobile number/i), '01712345678');
    await user.type(
      screen.getByLabelText(/^password/i),
      accountsFixtureIds.triggerBreachedPassword,
    );
    await screen.findByAltText(/security check/i);
    await user.type(screen.getByLabelText(/security check/i), 'ABCDE');
    await user.click(screen.getByRole('button', { name: /create account/i }));
    const breachedMessage = await screen.findByText(/appeared in a data breach/i);

    // The client's own zod rule already refuses anything under 10 characters before a submit
    // ever reaches the server, so "too-short from the server" is only reachable by a server that
    // enforces a stricter/different rule than the client currently does — stubbed directly here,
    // the same way U-B11/U-B13 stub a 429/500 that the client cannot itself produce.
    server.use(
      http.post('*/api/customers', () =>
        HttpResponse.json(
          {
            isValid: false,
            errors: [
              {
                propertyName: 'Password',
                errorCode: 'PasswordTooShort',
                errorMessage: 'Your password must be at least 10 characters (you entered 10).',
                resourceName: '',
              },
            ],
          },
          { status: 400 },
        ),
      ),
    );
    await screen.findByAltText(/security check/i); // refetched after the first failed submit
    await user.clear(screen.getByLabelText(/^password/i));
    await user.type(screen.getByLabelText(/^password/i), 'shortishpw');
    await user.clear(screen.getByLabelText(/security check/i));
    await user.type(screen.getByLabelText(/security check/i), 'ABCDE');
    await user.click(screen.getByRole('button', { name: /create account/i }));
    // (not the ever-present password-hint text, which also contains "at least 10 characters")
    const tooShortMessage = await screen.findByText(/at least 10 characters \(you entered 10\)/i);

    expect(tooShortMessage.textContent).not.toEqual(breachedMessage.textContent);
  });

  it('U-B10: a corrected resubmit after a server PasswordTooShort carries a different captchaId, and succeeds', async () => {
    const capturedCaptchaIds: Array<string | undefined> = [];
    server.use(
      http.post('*/api/customers', async ({ request }) => {
        const body = (await request.json()) as { captchaId?: string | null };
        capturedCaptchaIds.push(body.captchaId ?? undefined);
        if (capturedCaptchaIds.length === 1) {
          return HttpResponse.json(
            {
              isValid: false,
              errors: [
                {
                  propertyName: 'Password',
                  errorCode: 'PasswordTooShort',
                  errorMessage: 'Your password must be at least 10 characters (you entered 10).',
                  resourceName: '',
                },
              ],
            },
            { status: 400 },
          );
        }
        return HttpResponse.json({ customerId: 'new-customer-id' }, { status: 201 });
      }),
    );
    const user = userEvent.setup();
    const onSuccess = vi.fn();
    renderWithClient(<RegisterFormWithHooks onSuccess={onSuccess} />);
    await fillValidForm(user);
    await user.click(screen.getByRole('button', { name: /create account/i }));
    // (not the ever-present password-hint text, which also contains "at least 10 characters")
    await screen.findByText(/at least 10 characters \(you entered 10\)/i);

    // ADR-FE-006: the failed submit clears the password and answer, and fetches a fresh
    // challenge/captchaId — both cleared fields need retyping for a genuine "corrected resubmit".
    await waitFor(() =>
      expect((screen.getByLabelText(/security check/i) as HTMLInputElement).value).toBe(''),
    );
    await screen.findByAltText(/security check/i);
    await user.type(screen.getByLabelText(/^password/i), 'a-very-long-password');
    await user.type(screen.getByLabelText(/security check/i), 'ABCDE');
    await user.click(screen.getByRole('button', { name: /create account/i }));
    await waitFor(() => expect(onSuccess).toHaveBeenCalledTimes(1));

    expect(capturedCaptchaIds).toHaveLength(2);
    expect(capturedCaptchaIds[0]).toBeTruthy();
    expect(capturedCaptchaIds[1]).toBeTruthy();
    expect(capturedCaptchaIds[0]).not.toBe(capturedCaptchaIds[1]);
  });

  it('U-B14: double-clicking submit while the request is slow sends exactly one request; the button announces busy', async () => {
    // ACCEPTANCE gate MEDIUM M4 — a fixed 30ms mock delay only made the second click LIKELY to
    // land while request 1 was still in flight, not provably so: on a loaded CI/dev-server run
    // (the same `node.exe` memory-growth conditions the ACCEPTANCE gate's own M3 observed), 30ms
    // of real wall-clock time between the two `user.click()` calls is not guaranteed, which is
    // exactly why this test flaked on every full-suite run since the merge. Gating the mock's own
    // response on a manually released promise (the same technique `idle-lock.test.tsx`'s U-G7
    // uses for its own race) makes request 1 provably still unsettled at the moment of the second
    // click — nothing here depends on wall-clock timing at all.
    let requestCount = 0;
    let releaseResponse: () => void = () => {};
    const responseGate = new Promise<void>((resolve) => {
      releaseResponse = resolve;
    });
    server.use(
      http.post('*/api/customers', async () => {
        requestCount += 1;
        await responseGate;
        return HttpResponse.json({ customerId: 'new-customer-id' }, { status: 201 });
      }),
    );
    const user = userEvent.setup();
    const onSuccess = vi.fn();
    renderWithClient(<RegisterFormWithHooks onSuccess={onSuccess} />);
    await fillValidForm(user);
    const button = screen.getByRole('button', { name: /create account/i });

    await user.click(button);
    expect(button).toBeDisabled();
    expect(button).toHaveAttribute('aria-busy', 'true');
    await user.click(button); // native <button disabled> ignores this — no second request

    // Request 1 is provably still in flight here — its own mock handler is blocked on
    // `responseGate`, which nothing has released yet — so this assertion cannot pass "by luck" on
    // a fast run the way the old fixed-delay version could fail on a slow one.
    expect(requestCount).toBe(1);

    releaseResponse();
    await waitFor(() => expect(onSuccess).toHaveBeenCalledTimes(1));
    expect(requestCount).toBe(1);
  });
});
