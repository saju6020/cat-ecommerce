// `(auth)` route group — HO-ACC-002 §8.3/ADR-FE-001: a minimal public shell, no navigation, no
// identity. Register, sign-in, forgot/reset-password and account-deleted all render here, for
// both customer and staff variants (staff sign-in lives at `(auth)/staff/sign-in`, NOT under the
// `(staff)` guard — see that route's own comment for why).
export default function AuthLayout({ children }: { children: React.ReactNode }) {
  return (
    <div className="flex min-h-screen items-center justify-center bg-surface-page px-4 py-10">
      {children}
    </div>
  );
}
