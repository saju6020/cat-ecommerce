import { redirect } from 'next/navigation';

// HO-ACC-002 §8.3 — /portal/account redirects to the profile tab.
export default function AccountIndexPage() {
  redirect('/portal/account/profile');
}
