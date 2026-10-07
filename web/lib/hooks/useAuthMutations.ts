import { useMutation } from '@tanstack/react-query';
import {
  registerCustomer,
  signInCustomer,
  signOutCustomer,
  requestCustomerPasswordReset,
  confirmPasswordReset,
  type RegisterCustomerRequest,
  type SignInRequest,
  type PasswordResetRequestPayload,
  type ConfirmPasswordResetRequest,
} from '@/lib/api/auth';

/** Customer-facing auth mutations (HO-CAT-002 §8.2 rule 3 — only lib/hooks/ calls useMutation). */

export function useRegisterCustomer() {
  return useMutation({
    mutationFn: (request: RegisterCustomerRequest) => registerCustomer(request),
  });
}

export function useSignInCustomer() {
  return useMutation({
    mutationFn: (request: SignInRequest) => signInCustomer(request),
  });
}

export function useSignOutCustomer() {
  return useMutation({
    mutationFn: () => signOutCustomer(),
  });
}

export function useRequestCustomerPasswordReset() {
  return useMutation({
    mutationFn: (request: PasswordResetRequestPayload) => requestCustomerPasswordReset(request),
  });
}

export function useConfirmPasswordReset() {
  return useMutation({
    mutationFn: (request: ConfirmPasswordResetRequest) => confirmPasswordReset(request),
  });
}
