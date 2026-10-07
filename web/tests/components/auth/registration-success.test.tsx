import { describe, expect, it } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';
import { RegistrationSuccess } from '@/components/auth/RegistrationSuccess';

/**
 * `tests/components/auth/registration-success.test.tsx` — HO-ACC-002 §15 U-B5. §4.1 (withdrawn
 * scope) forbids any verification/OTP/SMS/"check your inbox" wording on this screen; §8.6
 * requires the success heading receive focus. Neither was previously exercised by any test —
 * `register-form.test.tsx` never renders this component, only `RegisterForm` itself, and
 * `app/(auth)/register/page.tsx` (which does render it, on `onSuccess`) has no test of its own.
 */
describe('RegistrationSuccess — U-B5', () => {
  it('contains no verification/OTP/SMS/inbox wording, no password and no full mobile number', () => {
    render(<RegistrationSuccess />);
    const text = document.body.textContent ?? '';

    expect(text.toLowerCase()).not.toMatch(/verif/);
    expect(text.toLowerCase()).not.toMatch(/otp/);
    expect(text.toLowerCase()).not.toMatch(/\bcode\b/);
    expect(text.toLowerCase()).not.toMatch(/\bsms\b/);
    expect(text.toLowerCase()).not.toMatch(/inbox/);
    // A real Bangladesh mobile number shape (this component takes no props and is never given one
    // to echo, but the assertion pins the observable behaviour, not just the absence of a prop).
    expect(text).not.toMatch(/01\d{9}/);
  });

  it('focuses the success heading on mount (§8.6)', async () => {
    render(<RegistrationSuccess />);
    const heading = screen.getByRole('heading', { name: 'Your account is ready' });
    await waitFor(() => expect(heading).toHaveFocus());
  });

  it('links to sign-in, the only next step offered', () => {
    render(<RegistrationSuccess />);
    expect(screen.getByRole('link', { name: /sign in/i })).toHaveAttribute('href', '/sign-in');
  });
});
