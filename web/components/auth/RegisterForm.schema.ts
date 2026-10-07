import { z } from 'zod';

/**
 * `components/auth/RegisterForm.schema.ts` — MEDIUM/LOW gate item 18 (first pass): split out of
 * `RegisterForm.tsx` once it was still over §11's ~200-line guideline after the props/`method=post`
 * gate fixes (items 3/4) put lines back on top of the original hook-removal shrink. Validation
 * rules and field-name mapping are a natural, independently-readable seam from the form's own JSX
 * — no behaviour changed by this split.
 */
export const REGISTER_FIELD_MAP = {
  FullName: 'fullName',
  MobileNumber: 'mobileNumber',
  EmailAddress: 'emailAddress',
  Password: 'password',
  CaptchaAnswer: 'captchaAnswer',
} as const;
export const REGISTER_KNOWN_FIELDS = Object.values(REGISTER_FIELD_MAP);

const EMAIL_SHAPE = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;

export const registerSchema = z
  .object({
    fullName: z.string().trim().min(1, 'Enter your name.'),
    mobileNumber: z.string().trim().optional(),
    emailAddress: z.string().trim().optional(),
    password: z.string().min(1, 'Enter a password.'),
    captchaAnswer: z.string().trim().min(1, 'Enter the security check code.'),
  })
  .superRefine((data, ctx) => {
    if (!data.mobileNumber && !data.emailAddress) {
      const message = "Give a mobile number, an email, or both — you'll use one to sign in.";
      ctx.addIssue({ code: 'custom', message, path: ['mobileNumber'] });
      ctx.addIssue({ code: 'custom', message, path: ['emailAddress'] });
    }
    if (data.emailAddress && !EMAIL_SHAPE.test(data.emailAddress)) {
      ctx.addIssue({
        code: 'custom',
        message: 'Enter a valid email address.',
        path: ['emailAddress'],
      });
    }
    if (data.password.length > 0 && data.password.length < 10) {
      ctx.addIssue({
        code: 'custom',
        message: `Your password must be at least 10 characters (you entered ${data.password.length}).`,
        path: ['password'],
      });
    }
  });

export type RegisterFormValues = z.infer<typeof registerSchema>;
