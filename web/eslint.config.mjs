import { dirname } from 'node:path';
import { fileURLToPath } from 'node:url';
import { FlatCompat } from '@eslint/eslintrc';

const __filename = fileURLToPath(import.meta.url);
const __dirname = dirname(__filename);

const compat = new FlatCompat({
  baseDirectory: __dirname,
});

const eslintConfig = [
  ...compat.extends('next/core-web-vitals', 'next/typescript'),
  {
    ignores: [
      'node_modules/**',
      '.next/**',
      'playwright-report/**',
      'test-results/**',
      'coverage/**',
      'next-env.d.ts',
      'public/mockServiceWorker.js',
    ],
  },
  {
    rules: {
      // §8.2 rule 3 — only lib/hooks/ may declare useQuery/useMutation. Components take
      // props and callbacks (rule 4); enforce the shape, not just ask for it in review.
      //
      // ARCH gate BLOCKER (second pass) — integrator-authorised (recorded in HO-ACC-002 §1
      // Amended); raised from 'warn' to 'error' here and below, since `npm run lint` now runs
      // with `--max-warnings 0` and either severity fails the build the same way — 'error' says
      // what is actually meant, a real violation, not a suggestion.
      'no-restricted-imports': [
        'error',
        {
          paths: [
            {
              name: '@tanstack/react-query',
              importNames: ['useQuery', 'useMutation', 'useInfiniteQuery'],
              message:
                'Only lib/hooks/ may call useQuery/useMutation directly (HO-CAT-002 §8.2). Add or use a hook there instead.',
            },
          ],
        },
      ],
    },
  },
  {
    files: ['lib/hooks/**'],
    rules: {
      'no-restricted-imports': 'off',
    },
  },
  {
    // ARCH gate HIGH 3 (first pass) — the rule above only bans importing useQuery/useMutation
    // FROM @tanstack/react-query directly, which a component calling an area hook (e.g.
    // useCaptcha(), itself built on useQuery) never does — passing that rule while still
    // violating HO-CAT-002 §8.2 rule 4 ("components take props and callbacks; the page wires
    // hooks together"). This closes that gap for the four component folders §8.2 rule 4 governs
    // (components/cat/** already follows it without needing enforcement, being HO-CAT-002's own
    // precedent) by banning ANY import from lib/hooks/* there too.
    //
    // ARCH gate LOW (second pass) — the `@/lib/hooks/*` glob only caught the path alias; a
    // relative import (`../../lib/hooks/useCaptcha`) was not caught. `**/lib/hooks/*` and
    // `**/lib/hooks` (minimatch, not a literal `**` import) catch a relative import at any depth
    // too, alongside the alias form.
    //
    // ARCHITECTURE gate third pass, LOW — corrected: the previous version of this comment claimed
    // `**/lib/hooks/*` does NOT match an unrelated package that merely happens to have its own
    // `lib/hooks` folder (e.g. `some-pkg/lib/hooks/y`). Verified live (`eslint --stdin`) that it
    // does — minimatch's `**` matches any leading path segment, package name included, so nothing
    // about this pattern is package-aware. Left as-is anyway: no dependency in this project has a
    // `lib/hooks` path of its own today, so this is a cosmetic imprecision, not a live false
    // positive, and narrowing the pattern to somehow exclude `node_modules/**` specifically would
    // add real complexity for a risk that does not currently exist.
    files: [
      'components/account/**',
      'components/admin/**',
      'components/auth/**',
      'components/customer/**',
    ],
    rules: {
      'no-restricted-imports': [
        'error',
        {
          paths: [
            {
              name: '@tanstack/react-query',
              importNames: ['useQuery', 'useMutation', 'useInfiniteQuery'],
              message:
                'Only lib/hooks/ may call useQuery/useMutation directly (HO-CAT-002 §8.2). Add or use a hook there instead.',
            },
          ],
          patterns: [
            {
              group: ['@/lib/hooks/*', '@/lib/hooks', '**/lib/hooks/*', '**/lib/hooks'],
              message:
                'Components under components/{account,admin,auth,customer}/** take props and callbacks (HO-CAT-002 §8.2 rule 4) — the page wires lib/hooks/ together, not the component. (Exemptions: components/staff/**, components/portal/** and the session reads in components/common/{portal-header,staff-shell}.tsx are session infrastructure per HO-ACC-002 §11.3/§11.5/§11.9, and live outside these four folders already; SignOutButton.tsx has its own override below.)',
            },
          ],
        },
      ],
    },
  },
  {
    // SignOutButton is the one §11.3/§11.5/§11.9-exempted file that happens to live inside
    // components/auth/** rather than components/staff/** or components/portal/** — it calls
    // useSignOutStaff()/useSignInCustomer-equivalent sign-out mutations directly, by design,
    // the same as StaffSessionGuard/CustomerSessionGuard/IdleLock do from their own folders.
    //
    // ARCH gate LOW (second pass) — this used to turn the WHOLE `no-restricted-imports` rule off
    // for this file, which also silently re-permitted a raw `useQuery`/`useMutation` import from
    // `@tanstack/react-query` here, never audited or intended. Only the hooks-folder-import ban
    // (the block above's own `patterns`) is lifted; the `paths` ban stays, by repeating it here
    // rather than inheriting it (a later matching config's rule options replace the earlier one's
    // entirely — they do not merge).
    files: ['components/auth/SignOutButton.tsx'],
    rules: {
      'no-restricted-imports': [
        'error',
        {
          paths: [
            {
              name: '@tanstack/react-query',
              importNames: ['useQuery', 'useMutation', 'useInfiniteQuery'],
              message:
                'Only lib/hooks/ may call useQuery/useMutation directly (HO-CAT-002 §8.2). Add or use a hook there instead.',
            },
          ],
        },
      ],
    },
  },
  {
    // HO-NTF-002 §8.4 / ADR-FE-160 (handover ADR-FE-020) — `srcDoc` is the one HTML rendering path
    // in web/. Only the sandboxed email preview may set it, and that component pins
    // `sandbox=""`. Anywhere else it is a lint error.
    files: ['**/*.{ts,tsx,js,jsx,mjs}'],
    ignores: ['components/notifications/SandboxedEmailPreview.tsx'],
    rules: {
      'no-restricted-syntax': [
        'error',
        {
          selector: "JSXAttribute[name.name='srcDoc']",
          message:
            'srcDoc is allowed only in components/notifications/SandboxedEmailPreview.tsx (HO-NTF-002 §8.4, ADR-FE-160).',
        },
        {
          selector: "Property[key.name='srcDoc'], Property[key.value='srcDoc']",
          message:
            'srcDoc is allowed only in components/notifications/SandboxedEmailPreview.tsx (HO-NTF-002 §8.4, ADR-FE-160).',
        },
        {
          selector: 'AssignmentExpression > MemberExpression.left[property.name=/^srcdoc$/i]',
          message:
            'srcdoc is allowed only in components/notifications/SandboxedEmailPreview.tsx (HO-NTF-002 §8.4, ADR-FE-160).',
        },
      ],
    },
  },
  {
    // HO-BIL-002 §8.4 / ADR-FE-080 (U-X3) — money stays a string end to end: no `Number(`,
    // `parseFloat(`, `parseInt(` and no unary `+` in the Billing components, the Billing API
    // modules or `lib/money/**`. (A later `no-restricted-syntax` block replaces the earlier one's
    // options rather than merging, so the `srcDoc` selectors above are repeated here.)
    files: ['components/billing/**', 'lib/api/billing-*', 'lib/api/my-billing.ts', 'lib/money/**'],
    rules: {
      'no-restricted-syntax': [
        'error',
        {
          selector: "JSXAttribute[name.name='srcDoc']",
          message:
            'srcDoc is allowed only in components/notifications/SandboxedEmailPreview.tsx (HO-NTF-002 §8.4, ADR-FE-160).',
        },
        {
          selector: "Property[key.name='srcDoc'], Property[key.value='srcDoc']",
          message:
            'srcDoc is allowed only in components/notifications/SandboxedEmailPreview.tsx (HO-NTF-002 §8.4, ADR-FE-160).',
        },
        {
          selector: 'AssignmentExpression > MemberExpression.left[property.name=/^srcdoc$/i]',
          message:
            'srcdoc is allowed only in components/notifications/SandboxedEmailPreview.tsx (HO-NTF-002 §8.4, ADR-FE-160).',
        },
        {
          selector: "CallExpression[callee.name='Number'], NewExpression[callee.name='Number']",
          message:
            'Money stays a string (HO-BIL-002 ADR-FE-080): do not convert it with Number(). Show it with formatMoney/a11yMoney and send the string.',
        },
        {
          selector:
            "CallExpression[callee.name=/^(parseFloat|parseInt)$/], CallExpression[callee.object.name='Number'][callee.property.name=/^(parseFloat|parseInt)$/]",
          message:
            'Money stays a string (HO-BIL-002 ADR-FE-080): do not parse it with parseFloat/parseInt.',
        },
        {
          selector: "UnaryExpression[operator='+']",
          message:
            'Money stays a string (HO-BIL-002 ADR-FE-080): unary + converts a string to a number. Do no arithmetic on amounts in the browser.',
        },
      ],
    },
  },
];

export default eslintConfig;
