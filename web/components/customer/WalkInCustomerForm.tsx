'use client';

import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { z } from 'zod';
import { Button } from '@/components/ui/button';
import { ExistingCustomerNotice } from './ExistingCustomerNotice';
import { ApiValidationError } from '@/lib/api/client';
import type {
  RegisterWalkInCustomerRequest,
  StaffCustomerSummary,
} from '@/lib/api/staff-customers';

const schema = z.object({
  fullName: z.string().trim().min(1, 'Enter the customer’s name.'),
  mobileNumber: z.string().trim().min(1, 'Enter a mobile number.'),
});
type FormValues = z.infer<typeof schema>;

/**
 * `components/customer/WalkInCustomerForm.tsx` — US-ACC-10. On `MobileNumberAlreadyRegistered`,
 * looks up the existing record(s) and hands them off — staff are authorised, so this is a feature,
 * not an error dead-end. The form keeps its values either way.
 *
 * ARCH gate HIGH 3 (first pass) — takes `onRegister`/`isPending`/`duplicateMobile`/
 * `onDuplicateFound`/`existingCustomers` as props; the page
 * (`app/(staff)/staff/customers/new/page.tsx`) owns `useRegisterWalkInCustomer()` and
 * `useFindCustomers()` — the latter is only ever enabled once this component reports a duplicate,
 * so "which mobile number to look up, if any" has to live at the page too, not just the mutation.
 */
export interface WalkInCustomerFormProps {
  onRegister: (request: RegisterWalkInCustomerRequest) => Promise<{ customerId: string }>;
  isPending: boolean;
  onCreated: (customerId: string) => void;
  duplicateMobile: string | null;
  onDuplicateFound: (mobileNumber: string | null) => void;
  existingCustomers: StaffCustomerSummary[] | undefined;
}

export function WalkInCustomerForm({
  onRegister,
  isPending,
  onCreated,
  duplicateMobile,
  onDuplicateFound,
  existingCustomers,
}: WalkInCustomerFormProps) {
  const {
    register,
    handleSubmit,
    setError,
    formState: { errors, isSubmitting },
  } = useForm<FormValues>({
    resolver: zodResolver(schema),
    defaultValues: { fullName: '', mobileNumber: '' },
  });

  async function onSubmit(values: FormValues) {
    onDuplicateFound(null);
    try {
      const result = await onRegister(values);
      onCreated(result.customerId);
    } catch (error) {
      if (error instanceof ApiValidationError) {
        const code = error.response.errors[0]?.errorCode;
        if (code === 'MobileNumberAlreadyRegistered') {
          onDuplicateFound(values.mobileNumber);
          return;
        }
        setError('root', {
          type: 'server',
          message: error.response.errors[0]?.errorMessage ?? 'Something went wrong.',
        });
      } else {
        setError('root', { type: 'server', message: 'Something went wrong. Please try again.' });
      }
    }
  }

  return (
    <div className="flex max-w-lg flex-col gap-5">
      <form
        method="post"
        onSubmit={handleSubmit(onSubmit)}
        noValidate
        className="flex flex-col gap-5"
      >
        {errors.root && (
          <p role="alert" aria-live="polite" className="text-sm font-semibold text-danger-ink">
            {errors.root.message}
          </p>
        )}

        <div className="flex flex-col gap-1.5">
          <label htmlFor="fullName" className="text-sm font-semibold text-ink">
            Full name{' '}
            <span aria-hidden="true" className="text-danger-ink">
              *
            </span>
          </label>
          <input
            id="fullName"
            required
            aria-invalid={!!errors.fullName}
            aria-describedby={errors.fullName ? 'fullName-error' : undefined}
            className="rounded-[var(--radius-sm)] border border-border-strong bg-surface px-3 py-2 text-sm text-ink focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-accent"
            {...register('fullName')}
          />
          {errors.fullName && (
            <p id="fullName-error" role="alert" className="text-xs font-semibold text-danger-ink">
              {errors.fullName.message}
            </p>
          )}
        </div>

        <div className="flex flex-col gap-1.5">
          <label htmlFor="mobileNumber" className="text-sm font-semibold text-ink">
            Mobile number{' '}
            <span aria-hidden="true" className="text-danger-ink">
              *
            </span>
          </label>
          <input
            id="mobileNumber"
            required
            aria-invalid={!!errors.mobileNumber}
            aria-describedby={errors.mobileNumber ? 'mobileNumber-error' : undefined}
            className="rounded-[var(--radius-sm)] border border-border-strong bg-surface px-3 py-2 text-sm text-ink focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-accent"
            {...register('mobileNumber')}
          />
          {errors.mobileNumber && (
            <p
              id="mobileNumber-error"
              role="alert"
              className="text-xs font-semibold text-danger-ink"
            >
              {errors.mobileNumber.message}
            </p>
          )}
        </div>

        <Button
          type="submit"
          disabled={isSubmitting || isPending}
          aria-busy={isSubmitting || isPending}
          className="w-fit"
        >
          {isSubmitting || isPending ? 'Registering…' : 'Register customer'}
        </Button>
      </form>

      {duplicateMobile && existingCustomers && (
        <ExistingCustomerNotice results={existingCustomers} />
      )}
    </div>
  );
}
