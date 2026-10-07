import { AccountShell } from '@/components/account/AccountShell';

// US-ACC-06 — ADR-FE-008: a route-based tab shell.
export default function AccountLayout({ children }: { children: React.ReactNode }) {
  return <AccountShell>{children}</AccountShell>;
}
