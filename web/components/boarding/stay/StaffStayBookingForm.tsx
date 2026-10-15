'use client';

import { useState } from 'react';
import Link from 'next/link';
import { CustomerPicker } from '@/components/appointments/CustomerPicker';
import { CareProfileTabs } from '@/components/boarding/booking/CareProfileTabs';
import { CatStep } from '@/components/boarding/booking/CatStep';
import { ReviewAndReserve } from '@/components/boarding/booking/ReviewAndReserve';
import { StaySearch, type StaySearchValues } from '@/components/boarding/booking/StaySearch';
import { UnitTypeResults } from '@/components/boarding/booking/UnitTypeResults';
import { VaccinationGatePanel } from '@/components/boarding/booking/VaccinationGatePanel';
import { FormErrorAlert } from '@/components/boarding/shared/FormBits';
import { LoadingState } from '@/components/common/LoadingState';
import { ReasonDialog } from '@/components/boarding/shared/ReasonDialog';
import { UNIT_TYPE_LABEL } from '@/components/boarding/shared/labels';
import { Button, buttonVariants } from '@/components/ui/button';
import type { StaffCustomerSummary } from '@/lib/api/staff-customers';
import { describeBoardingError } from '@/lib/api/errors';
import type {
  AvailableUnitType,
  BoardingUnitType,
  EnquiryResult,
  StaffStayDetail,
} from '@/lib/api/types';
import { addDaysIso } from '@/lib/format/appointment';
import { todayInZone } from '@/lib/format/time';
import { useActiveBranchId } from '@/lib/hooks/useActiveBranch';
import { useCats } from '@/lib/hooks/useCats';
import { useCustomerLookup } from '@/lib/hooks/useCustomerLookup';
import { useUrlParams } from '@/lib/hooks/useUrlParams';
import { useMyBranches } from '@/lib/hooks/useActiveBranch';
import { useAdminBranches } from '@/lib/hooks/useAdminBranches';
import {
  useChangeStaffEnquiry,
  useCreateStaffEnquiry,
  useOverrideVaccinationGate,
  useReserveStaffStay,
  useSaveStaffCareProfile,
  useStaffAvailability,
  useStaffStay,
} from '@/lib/hooks/useStaffBoarding';
import { staffStayHref } from '@/lib/routing/boarding-links';
import { cn } from '@/lib/utils';

type Step = 'customer' | 'search' | 'cats' | 'gate' | 'profiles' | 'review' | 'done';

function OverrideButton({
  stayId,
  catId,
  catName,
}: {
  stayId: string;
  catId: string;
  catName: string;
}) {
  const [open, setOpen] = useState(false);
  const override = useOverrideVaccinationGate(stayId);
  return (
    <>
      <Button variant="secondary" onClick={() => setOpen(true)}>
        Override for {catName}
      </Button>
      {open && (
        <ReasonDialog
          open
          onOpenChange={setOpen}
          title={`Override the vaccination block for ${catName}?`}
          consequence="This will be recorded against you, with your reason. The cat can be reserved, but the override does not admit it at check-in."
          reasonLabel="Reason (mandatory)"
          requiredMessage="Enter the reason for the override."
          confirmLabel="Override and record"
          variant="destructive"
          onConfirm={(reason) => override.mutateAsync({ catId, reason })}
        />
      )}
    </>
  );
}

/**
 * `components/boarding/stay/StaffStayBookingForm.tsx` — HO-BRD-002 §11.4, US-BRD-02…07: reception books
 * for a customer — customer search, that customer's cats, the same search / results / care-profile /
 * review components as the owner's flow (staff mode lists free units, and isolation units when the stay
 * requires isolation), and, for administrators, **Override vaccination** (a mandatory reason, “recorded
 * against you”) — US-BRD-04 AC3. The deposit is then recorded in Billing from the stay (ADR-FE-106).
 */
export function StaffStayBookingForm() {
  const url = useUrlParams();
  const branchId = useActiveBranchId();
  const mine = useMyBranches();
  const branches = useAdminBranches();
  const lookup = useCustomerLookup();
  const [step, setStep] = useState<Step>('customer');
  const [customer, setCustomer] = useState<StaffCustomerSummary | null>(null);
  const [searched, setSearched] = useState<string | null>(null);
  const today = todayInZone('Asia/Dhaka');
  const [search, setSearch] = useState<StaySearchValues>({
    branchId: branchId ?? mine.data?.primaryBranchId ?? '',
    arrivalOn: url.get('arrivalOn') || addDaysIso(today, 1),
    departureOn: url.get('departureOn') || addDaysIso(today, 3),
    catCount: 1,
  });
  const [unitType, setUnitType] = useState<BoardingUnitType | null>(null);
  const [catIds, setCatIds] = useState<string[]>([]);
  const [stayId, setStayId] = useState<string | null>(null);
  const [gate, setGate] = useState<EnquiryResult['vaccinationGate'] | null>(null);
  // The stay view has no quote while it is an Enquiry: the enquiry answer's quote is kept for the review.
  const [quote, setQuote] = useState<EnquiryResult['quote']>(null);
  const [error, setError] = useState<string | null>(null);

  const availabilityParams =
    step === 'search' && search.branchId
      ? { arrivalOn: search.arrivalOn, departureOn: search.departureOn, catCount: search.catCount }
      : null;
  const availability = useStaffAvailability(availabilityParams);
  const cats = useCats({ ownerId: customer?.id, pageNumber: 1, pageSize: 100 });
  const stay = useStaffStay(stayId ?? '');
  const create = useCreateStaffEnquiry();
  const change = useChangeStaffEnquiry(stayId ?? '');
  const save = useSaveStaffCareProfile(stayId ?? '');
  const reserve = useReserveStaffStay(stayId ?? '');
  const branchList = (branches.data ?? []).map((b) => ({
    id: b.id,
    name: b.name,
    timeZoneId: b.timeZoneId,
  }));

  async function submitCats() {
    if (!customer || !unitType) return;
    setError(null);
    const body = {
      branchId: search.branchId,
      arrivalOn: search.arrivalOn,
      departureOn: search.departureOn,
      unitType,
      catIds,
    };
    try {
      const result = stayId
        ? await change.mutateAsync(body)
        : await create.mutateAsync({ ...body, customerId: customer.id });
      setStayId(result.stayId);
      setQuote(result.quote);
      setGate(result.vaccinationGate.passed ? null : result.vaccinationGate);
      setStep(result.vaccinationGate.passed ? 'profiles' : 'gate');
    } catch (e) {
      setError(describeBoardingError(e));
    }
  }

  async function resume() {
    if (!stayId || !unitType) return;
    try {
      const result = await change.mutateAsync({
        branchId: search.branchId,
        arrivalOn: search.arrivalOn,
        departureOn: search.departureOn,
        unitType,
        catIds,
      });
      setQuote(result.quote);
      setGate(result.vaccinationGate.passed ? null : result.vaccinationGate);
      if (result.vaccinationGate.passed) setStep('profiles');
    } catch (e) {
      setError(describeBoardingError(e));
    }
  }

  const detail: StaffStayDetail | undefined = stay.data;
  return (
    <div className="flex flex-col gap-6">
      <FormErrorAlert message={error} />
      {step === 'customer' && (
        <section aria-labelledby="bk-customer" className="flex flex-col gap-3">
          <h2 id="bk-customer" className="font-display text-lg font-semibold text-ink">
            Customer
          </h2>
          <CustomerPicker
            selected={
              customer
                ? {
                    id: customer.id,
                    fullName: customer.fullName,
                    mobileNumber: customer.mobileNumber,
                  }
                : null
            }
            onSelect={setCustomer}
            onClear={() => setCustomer(null)}
            onSearch={(term) => {
              setSearched(term);
              lookup.mutate(term);
            }}
            results={lookup.data}
            isSearching={lookup.isPending}
            searchedTerm={searched}
          />
          <div>
            <Button disabled={!customer} onClick={() => setStep('search')}>
              Continue
            </Button>
          </div>
        </section>
      )}

      {step === 'search' && (
        <>
          <StaySearch
            value={search}
            branches={branchList}
            onSearch={(v) => {
              setSearch(v);
            }}
            submitLabel="See units"
          />
          {availability.isPending && availabilityParams && (
            <LoadingState rows={4} label="Checking availability…" />
          )}
          {availability.data && (
            <UnitTypeResults
              staff
              availability={availability.data}
              catCount={search.catCount}
              selectedType={unitType}
              onChoose={(t: AvailableUnitType) => {
                setUnitType(t.unitType);
                setStep('cats');
              }}
              onPickAlternative={(alt) =>
                setSearch({ ...search, arrivalOn: alt.arrivalOn, departureOn: alt.departureOn })
              }
              onJoinWaitlist={() => undefined}
            />
          )}
        </>
      )}

      {step === 'cats' && (
        <CatStep
          cats={(cats.data?.items ?? []).filter((c) => c.lifeStatus === 'Alive')}
          selectedIds={catIds}
          onToggle={(id) =>
            setCatIds((p) => (p.includes(id) ? p.filter((x) => x !== id) : [...p, id]))
          }
          capacity={
            availability.data?.unitTypes.find((t) => t.unitType === unitType)?.capacityCats ?? null
          }
          unitLabel={unitType ? UNIT_TYPE_LABEL[unitType] : 'chosen'}
          onContinue={() => void submitCats()}
          onBack={() => setStep('search')}
          pending={create.isPending || change.isPending}
          serverError={null}
        />
      )}

      {step === 'gate' && gate && stayId && (
        <VaccinationGatePanel
          gate={gate}
          onResume={() => void resume()}
          resuming={change.isPending}
          renderOverride={(c) => (
            <OverrideButton stayId={stayId} catId={c.catId} catName={c.catName} />
          )}
        />
      )}
      {step === 'gate' && stayId && (
        <Button variant="secondary" onClick={() => void resume()}>
          Check the gate again after an override
        </Button>
      )}

      {step === 'profiles' && detail && (
        <CareProfileTabs
          cats={detail.cats}
          currencyCode={detail.deposit.required.currencyCode}
          onSave={(catId, profile) => save.mutateAsync({ catId, profile })}
          onBack={() => setStep('cats')}
          onContinue={() => setStep('review')}
        />
      )}
      {step === 'review' && detail && (
        <ReviewAndReserve
          stay={detail.quote ? detail : { ...detail, quote }}
          onBack={() => setStep('profiles')}
          onReserve={async () => {
            await reserve.mutateAsync();
            setStep('done');
          }}
        />
      )}
      {step === 'done' && stayId && (
        <section className="flex flex-col gap-3 rounded-[var(--radius-md)] border border-border bg-surface p-5">
          <h2 className="font-display text-lg font-semibold text-ink">
            Reserved — not yet confirmed
          </h2>
          <p className="text-sm text-ink">
            Record the deposit in Billing from the stay page, then press Confirm stay.
          </p>
          <div>
            <Link
              href={staffStayHref(stayId)}
              className={cn(buttonVariants({ variant: 'primary' }))}
            >
              Open the stay
            </Link>
          </div>
        </section>
      )}
    </div>
  );
}
