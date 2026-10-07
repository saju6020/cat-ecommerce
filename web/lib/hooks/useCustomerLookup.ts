import { useMutation } from '@tanstack/react-query';
import { findCustomers } from '@/lib/api/staff-customers';

/**
 * HO-NTF-002 §11.5 — the log filter's "customer" field reuses HO-ACC-002's lookup
 * (`GET /api/staff/customers?mobile=|email=`, US-ACC-10): staff type a mobile number or an email,
 * pick the match, and the filter stores that customer's id. A mutation rather than a query because
 * it runs only when asked (a button), exactly like `ExistingCustomerNotice`'s lookup.
 */
export function useCustomerLookup() {
  return useMutation({
    mutationFn: (term: string) => {
      const trimmed = term.trim();
      return findCustomers(trimmed.includes('@') ? { email: trimmed } : { mobile: trimmed });
    },
  });
}
