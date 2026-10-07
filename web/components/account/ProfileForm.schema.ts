import { z } from 'zod';
import type { CustomerProfile } from '@/lib/api/account';

/**
 * `components/account/ProfileForm.schema.ts` — MEDIUM/LOW gate item 18 (first pass): split out of
 * `ProfileForm.tsx` once it was still over §11's ~200-line guideline after the props/`method=post`
 * gate fixes (items 3/4/14) put lines back on top of the original hook-removal shrink. Validation
 * rules and field-name mapping are a natural, independently-readable seam from the form's own JSX
 * — no behaviour changed by this split.
 */
export const MAX_ADDITIONAL_NUMBERS = 3;

const baseSchema = z.object({
  fullName: z.string().trim().min(1, 'Enter your name.'),
  emailAddress: z.string().trim().optional(),
  additionalPhoneNumbers: z
    .array(z.object({ value: z.string().trim() }))
    .max(MAX_ADDITIONAL_NUMBERS),
  currentPassword: z.string().optional(),
});
export type ProfileFormValues = z.infer<typeof baseSchema>;

/**
 * CODE_REVIEW LOW (first pass) — the conditionally-required current password used to be
 * `register('currentPassword', { required: emailChanging })`, but `useForm` was already given a
 * `zodResolver`, and native `register` validation options are never consulted once a resolver is
 * supplied — that `required` had no client-side effect at all (the server still correctly
 * rejected a blank one, so nothing was silently let through, but the intended fast client-side
 * check, matching `RegisterForm`'s own `superRefine` for "at least one identifier", never fired).
 * Folded into the schema itself instead — built per-`profile` since "is the email actually
 * changing" depends on the loaded value, not a fixed rule.
 */
export function makeProfileSchema(profile: CustomerProfile) {
  return baseSchema.superRefine((data, ctx) => {
    const emailChanging =
      profile.canEditEmail && (data.emailAddress || '') !== (profile.emailAddress ?? '');
    if (emailChanging && !data.currentPassword) {
      ctx.addIssue({
        code: 'custom',
        message: 'Enter your current password to change your email.',
        path: ['currentPassword'],
      });
    }
  });
}

export const PROFILE_FIELD_MAP = {
  FullName: 'fullName',
  EmailAddress: 'emailAddress',
  AdditionalPhoneNumbers: 'additionalPhoneNumbers',
  CurrentPassword: 'currentPassword',
} as const;
export const PROFILE_KNOWN_FIELDS = Object.values(PROFILE_FIELD_MAP);
