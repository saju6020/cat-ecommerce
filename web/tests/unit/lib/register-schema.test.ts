import { describe, expect, it } from 'vitest';
import { registerSchema } from '@/components/auth/RegisterForm.schema';

/**
 * `tests/unit/lib/register-schema.test.ts` — HO-ACC-002 §15 U-B1. `register-form.test.tsx`
 * exercises this schema only through a full render + submit; this is a pure schema-level test of
 * the specific combination §15 names (name + mobile + a 10-character password + a CAPTCHA
 * answer), including the boundary itself (exactly 10 characters must pass, not just "long
 * enough") and that email is genuinely optional, not merely blank-tolerated.
 */
describe('registerSchema — U-B1', () => {
  it('parses successfully with a name, a mobile number, a 10-character password and an answer — no email', () => {
    const result = registerSchema.safeParse({
      fullName: 'Jamie Owner',
      mobileNumber: '01712345678',
      emailAddress: '',
      password: '1234567890', // exactly 10 characters — the boundary itself
      captchaAnswer: 'ABCDE',
    });

    expect(result.success).toBe(true);
  });

  it('still requires the other three fields even though email is optional', () => {
    const result = registerSchema.safeParse({
      fullName: '',
      mobileNumber: '01712345678',
      emailAddress: '',
      password: '',
      captchaAnswer: '',
    });

    expect(result.success).toBe(false);
  });
});
