'use client';

import { useRef, useState } from 'react';
import { WalkInPanel, type WalkInPanelHandle } from '@/components/front-desk/WalkInPanel';
import { WalkInCustomerForm } from '@/components/customer/WalkInCustomerForm';
import { toast } from '@/components/ui/use-toast';
import { useRegisterWalkIn } from '@/lib/hooks/useFrontDesk';
import { useAdminServices } from '@/lib/hooks/useAdminCatalogue';
import { useCustomerAppointmentSummary } from '@/lib/hooks/useStaffAppointments';
import { useFindCustomers, useStaffCustomer } from '@/lib/hooks/useStaffCustomers';
import { useRegisterWalkInCustomer } from '@/lib/hooks/useStaffCustomerMutations';
import { useRegisterCat } from '@/lib/hooks/useCatMutations';
import type { StaffCustomerSummary } from '@/lib/api/staff-customers';

/**
 * Container for the walk-in panel (HO-APT-002 §11.5, NFR-USA-2): wires customer search, HO-ACC-002's
 * inline walk-in customer form, HO-CAT-002's cat registration (quick-add) and the walk-in command.
 * The chosen customer's cats come from the staff customer record; the no-show count from the
 * appointment summary (US-CLN-08).
 */
export function WalkInSection({ onQueued }: { onQueued: (appointmentId: string) => void }) {
  const panelRef = useRef<WalkInPanelHandle>(null);
  const [searchedTerm, setSearchedTerm] = useState<string | null>(null);
  const [customerId, setCustomerId] = useState<string | null>(null);
  const [duplicateMobile, setDuplicateMobile] = useState<string | null>(null);

  const searchParams = searchedTerm
    ? searchedTerm.includes('@')
      ? { email: searchedTerm }
      : { mobile: searchedTerm }
    : {};
  const search = useFindCustomers(searchParams, !!searchedTerm);
  const customer = useStaffCustomer(customerId ?? '');
  const summary = useCustomerAppointmentSummary(customerId);
  const duplicateLookup = useFindCustomers(
    { mobile: duplicateMobile ?? undefined },
    !!duplicateMobile,
  );
  const services = useAdminServices();
  const registerCustomer = useRegisterWalkInCustomer();
  const registerCat = useRegisterCat();
  const registerWalkIn = useRegisterWalkIn();

  const selected = customer.data
    ? {
        id: customer.data.id,
        fullName: customer.data.fullName,
        mobileNumber: customer.data.mobileNumber,
        noShowCount: summary.data?.noShowCount ?? null,
      }
    : null;

  return (
    <WalkInPanel
      ref={panelRef}
      services={(services.data ?? [])
        .filter((service) => service.isActive && service.isPriced)
        .map((service) => ({ id: service.id, name: service.name }))}
      selectedCustomer={selected}
      cats={(customer.data?.cats ?? [])
        .filter((cat) => !cat.isDeceased)
        .map((cat) => ({ id: cat.catId, name: cat.name }))}
      onSelectCustomer={(picked: StaffCustomerSummary) => setCustomerId(picked.id)}
      onClearCustomer={() => {
        setCustomerId(null);
        setSearchedTerm(null);
      }}
      onSearch={setSearchedTerm}
      results={search.data}
      isSearching={search.isFetching}
      searchedTerm={searchedTerm}
      onNewCustomerCreated={(id) => {
        setCustomerId(id);
        setDuplicateMobile(null);
      }}
      renderNewCustomerForm={(onCreated) => (
        <WalkInCustomerForm
          onRegister={(request) => registerCustomer.mutateAsync(request)}
          isPending={registerCustomer.isPending}
          onCreated={onCreated}
          duplicateMobile={duplicateMobile}
          onDuplicateFound={setDuplicateMobile}
          existingCustomers={duplicateLookup.data}
        />
      )}
      onQuickAddCat={async (name, sex) => {
        const created = await registerCat.mutateAsync({
          ownerId: customerId ?? '',
          name,
          species: 'Cat',
          sex,
          dateOfBirth: null,
          dateOfBirthPrecision: 'Estimated',
          breed: null,
          colourAndMarkings: null,
        });
        return { id: created.catId, name };
      }}
      onSubmit={async (values) => {
        const result = await registerWalkIn.mutateAsync({
          customerId: values.customerId,
          catId: values.catId,
          serviceId: values.serviceId,
          triagePriority: values.triagePriority,
          presentingComplaint: values.presentingComplaint.trim() || undefined,
        });
        toast({ title: 'Added to the queue' });
        onQueued(result.appointmentId);
      }}
    />
  );
}
