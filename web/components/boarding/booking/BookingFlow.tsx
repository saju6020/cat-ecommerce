'use client';

import { useEffect, useMemo, useRef, useState } from 'react';
import { useQueryClient } from '@tanstack/react-query';
import { ErrorState } from '@/components/common/ErrorState';
import { LoadingState } from '@/components/common/LoadingState';
import { FormErrorAlert } from '@/components/boarding/shared/FormBits';
import { QueryGate } from '@/components/boarding/shared/QueryGate';
import { UNIT_TYPE_LABEL } from '@/components/boarding/shared/labels';
import { ApiValidationError } from '@/lib/api/client';
import { normaliseGate } from '@/lib/api/my-boarding';
import { describeBoardingError, getViolationCode } from '@/lib/api/errors';
import type {
  AvailableUnitType,
  BoardingUnitType,
  EnquiryResult,
  MyStayDetail,
  StayQuote,
  StaySearchParams,
  VaccinationGate,
} from '@/lib/api/types';
import {
  useBoardingAvailability,
  useChangeEnquiry,
  useCreateEnquiry,
  useMyStay,
  useReserveStay,
  useSaveCareProfile,
} from '@/lib/hooks/useMyBoarding';
import { useCats } from '@/lib/hooks/useCats';
import { useUrlParams } from '@/lib/hooks/useUrlParams';
import { usePublicBranches } from '@/lib/hooks/useMyOrders';
import { nightsBetween } from '@/lib/format/boarding';
import { timeOnlyInZone } from '@/lib/format/time';
import { bookVaccinationHref, PORTAL_CATS } from '@/lib/routing/portal-links';
import { CareProfileTabs } from './CareProfileTabs';
import { CatStep } from './CatStep';
import { ReservedPanel } from './ReservedPanel';
import { ReviewAndReserve } from './ReviewAndReserve';
import { StaySearch, type StaySearchValues } from './StaySearch';
import { UnitTypeResults } from './UnitTypeResults';
import { VaccinationGatePanel } from './VaccinationGatePanel';
import { WaitlistJoinDialog } from './WaitlistJoinDialog';
import { BookingStepper, type BookingStep } from './BookingStepper';

const UNIT_TYPES: BoardingUnitType[] = ['Standard', 'Deluxe', 'Family', 'Isolation'];

function gateFromStay(stay: MyStayDetail): VaccinationGate {
  const blocked = stay.cats.filter((c) => c.gateStatus === 'Blocked');
  return {
    passed: blocked.length === 0,
    blockedCats: blocked.map((c) => ({
      catId: c.catId,
      catName: c.catName,
      reasons: c.gateReasons,
      bookingLink: bookVaccinationHref(c.catId),
    })),
  };
}

function firstStep(stay: MyStayDetail): BookingStep {
  if (stay.status !== 'Enquiry') return 'reserved';
  if (stay.cats.some((c) => c.gateStatus === 'Blocked')) return 'gate';
  if (!stay.cats.every((c) => c.careProfileComplete)) return 'profiles';
  return 'review';
}

/**
 * `components/boarding/booking/BookingFlow.tsx` — ⭐ HO-BRD-002 §11.1, US-BRD-02…07, ADR-FE-099:
 * the whole owner booking on one route. The search lives in the URL (`arrivalOn`, `departureOn`,
 * `catCount`, `unitType`, `branchId`); the enquiry lives on the server from the cats step on, with
 * `stayId` in the URL — so a vaccination-blocked booking survives a reload, a trip to the
 * vaccination booking and a return through *My stays* (**Resume**). The server prices, checks
 * availability, evaluates the gate and decides every refusal; this component only routes the owner
 * to the step the answer calls for (ADR-FE-105).
 */
export function BookingFlow() {
  const url = useUrlParams();
  const queryClient = useQueryClient();
  const branches = usePublicBranches();
  const cats = useCats({ pageNumber: 1, pageSize: 100 });

  const stayId = url.get('stayId') || null;
  const arrivalOn = url.get('arrivalOn');
  const departureOn = url.get('departureOn');
  const catCount = Math.min(6, Math.max(1, Number(url.get('catCount')) || 1));
  const unitType = (UNIT_TYPES as string[]).includes(url.get('unitType'))
    ? (url.get('unitType') as BoardingUnitType)
    : null;

  const branchList = useMemo(
    () => (branches.data ?? []).map((b) => ({ id: b.id, name: b.name, timeZoneId: b.timeZoneId })),
    [branches.data],
  );
  const branchId = url.get('branchId') || branchList[0]?.id || '';
  const branch = branchList.find((b) => b.id === branchId) ?? branchList[0];

  const stayQuery = useMyStay(stayId);
  const [step, setStep] = useState<BookingStep | null>(
    stayId ? null : unitType ? 'cats' : 'search',
  );
  const [notice, setNotice] = useState<string | null>(null);
  const [gate, setGate] = useState<VaccinationGate | null>(null);
  // The stay view's `quote` is null while the stay is an Enquiry, so the enquiry's own quote is kept.
  const [enquiryQuote, setEnquiryQuote] = useState<StayQuote | null>(null);
  const [selectedCatIds, setSelectedCatIds] = useState<string[]>([]);
  const [activeCatId, setActiveCatId] = useState<string | null>(null);
  const [catError, setCatError] = useState<string | null>(null);
  const [waitlistOpen, setWaitlistOpen] = useState(false);
  const [waitlistJoined, setWaitlistJoined] = useState(false);
  const initialised = useRef(false);
  const quoteRequested = useRef(false);
  const [quotePending, setQuotePending] = useState(false);

  const createEnquiry = useCreateEnquiry();
  const changeEnquiry = useChangeEnquiry(stayId ?? '');
  const saveProfile = useSaveCareProfile(stayId ?? '');
  const reserve = useReserveStay(stayId ?? '');

  // Land on the first step that still needs work when the booking is resumed (ADR-FE-099).
  useEffect(() => {
    if (!stayId || initialised.current || !stayQuery.data) return;
    initialised.current = true;
    const stay = stayQuery.data;
    setSelectedCatIds(stay.cats.map((c) => c.catId));
    const next = firstStep(stay);
    setStep(next);
    if (next === 'gate') setGate(gateFromStay(stay));
  }, [stayId, stayQuery.data]);

  // A reload on the review step (or Resume from My stays) has no enquiry answer in memory and the stay view's
  // quote is null for an Enquiry, so ask the server again with the stay's own values to get the quote.
  const reviewStay = stayQuery.data;
  useEffect(() => {
    if (step !== 'review' || !reviewStay || reviewStay.status !== 'Enquiry') return;
    if (reviewStay.quote || enquiryQuote || quoteRequested.current) return;
    quoteRequested.current = true;
    setQuotePending(true);
    changeEnquiry
      .mutateAsync({
        branchId: reviewStay.branchId,
        arrivalOn: reviewStay.arrivalOn,
        departureOn: reviewStay.departureOn,
        unitType: reviewStay.requestedUnitType,
        catIds: reviewStay.cats.map((c) => c.catId),
      })
      .then((result) => setEnquiryQuote(result.quote))
      .catch(() => undefined)
      .finally(() => setQuotePending(false));
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [step, reviewStay, enquiryQuote]);

  const hasSearch = !!arrivalOn && !!departureOn && nightsBetween(arrivalOn, departureOn) > 0;
  const searchParams: StaySearchParams | null =
    hasSearch && branchId ? { branchId, arrivalOn, departureOn, catCount } : null;
  const availability = useBoardingAvailability(step === 'search' ? searchParams : null);
  const capacityOf = (type: BoardingUnitType | null) =>
    type
      ? (availability.data?.unitTypes.find((t) => t.unitType === type)?.capacityCats ?? null)
      : null;

  function runSearch(values: StaySearchValues) {
    setNotice(null);
    url.set({
      arrivalOn: values.arrivalOn,
      departureOn: values.departureOn,
      catCount: String(values.catCount),
      branchId: values.branchId,
      unitType: undefined,
    });
    setStep('search');
  }

  function chooseType(type: AvailableUnitType) {
    url.set({ unitType: type.unitType });
    setCatError(null);
    setStep('cats');
  }

  function applyEnquiry(result: EnquiryResult) {
    setEnquiryQuote(result.quote);
    if (!stayId) url.set({ stayId: result.stayId });
    initialised.current = true;
    void queryClient.invalidateQueries({ queryKey: ['me', 'boarding', 'stay'] });
    if (!result.vaccinationGate.passed) {
      setGate(result.vaccinationGate);
      setStep('gate');
    } else {
      setGate(null);
      setStep('profiles');
    }
  }

  async function continueFromCats() {
    if (!arrivalOn || !departureOn || !unitType || !branchId) return;
    setCatError(null);
    const request = {
      branchId,
      arrivalOn,
      departureOn,
      unitType,
      catIds: selectedCatIds,
    };
    try {
      const result = stayId
        ? await changeEnquiry.mutateAsync(request)
        : await createEnquiry.mutateAsync(request);
      if (selectedCatIds.length !== catCount) url.set({ catCount: String(selectedCatIds.length) });
      applyEnquiry(result);
    } catch (e) {
      setCatError(describeBoardingError(e));
    }
  }

  async function resume() {
    const stay = stayQuery.data;
    if (!stay || !stayId) return;
    try {
      const result = await changeEnquiry.mutateAsync({
        branchId: stay.branchId,
        arrivalOn: stay.arrivalOn,
        departureOn: stay.departureOn,
        unitType: stay.requestedUnitType,
        catIds: stay.cats.map((c) => c.catId),
      });
      applyEnquiry(result);
    } catch (e) {
      setNotice(describeBoardingError(e));
    }
  }

  async function doReserve() {
    try {
      await reserve.mutateAsync();
      setStep('reserved');
      setNotice(null);
    } catch (e) {
      const code = getViolationCode(e);
      if (code === 'VaccinationGateBlocked' && e instanceof ApiValidationError) {
        const fresh = (e.response as unknown as { vaccinationGate?: VaccinationGate })
          .vaccinationGate;
        const stay = stayQuery.data;
        setGate(fresh ? normaliseGate(fresh) : stay ? gateFromStay(stay) : null);
        setNotice(describeBoardingError(e));
        setStep('gate');
        void stayQuery.refetch();
        return;
      }
      if (code === 'CareProfileIncomplete' && e instanceof ApiValidationError) {
        const named = (e.response as unknown as { catId?: string }).catId ?? null;
        setActiveCatId(named);
        setNotice(describeBoardingError(e));
        setStep('profiles');
        return;
      }
      if (code === 'UnitNoLongerAvailable' || code === 'CatAlreadyBoarding') {
        setNotice(describeBoardingError(e));
        setStep('search');
        void queryClient.invalidateQueries({ queryKey: ['boarding', 'availability'] });
        return;
      }
      throw e;
    }
  }

  const stay = stayQuery.data;
  const currency =
    stay?.quote?.total.currencyCode ??
    availability.data?.unitTypes[0]?.quote.total.currencyCode ??
    'BDT';
  const checkInHint = availability.data
    ? `Check-in from ${timeOnlyInZone(availability.data.occupiedFrom, availability.data.branchTimeZoneId)}.`
    : null;
  const aliveCats = (cats.data?.items ?? []).filter((c) => c.lifeStatus === 'Alive');

  if (!branch && (branches.isPending || !branches.data)) {
    return <LoadingState variant="card" rows={4} label="Loading…" />;
  }
  if (stayId && step === null) {
    return (
      <QueryGate
        query={stayQuery}
        loadingLabel="Loading your booking…"
        notFoundTitle="We can’t find that booking."
      >
        {() => <LoadingState variant="card" rows={4} label="Loading your booking…" />}
      </QueryGate>
    );
  }
  const current: BookingStep = step ?? 'search';

  return (
    <div className="flex flex-col gap-6">
      <BookingStepper current={current} showGate={!!gate || current === 'gate'} />
      {notice && (
        <div
          role="alert"
          className="rounded-[var(--radius-sm)] border border-border bg-warning-bg px-4 py-3 text-sm font-semibold text-warning-ink"
        >
          {notice}
        </div>
      )}

      {current === 'search' && (
        <>
          <StaySearch
            key={`${arrivalOn}-${departureOn}-${catCount}-${branchId}`}
            value={{
              branchId,
              arrivalOn: arrivalOn ?? '',
              departureOn: departureOn ?? '',
              catCount,
            }}
            branches={branchList}
            checkInHint={checkInHint}
            onSearch={runSearch}
          />
          {searchParams &&
            (availability.isPending ? (
              <LoadingState variant="card" rows={5} label="Checking availability…" />
            ) : availability.isError ? (
              <FormErrorAlert message={describeBoardingError(availability.error)} />
            ) : (
              <>
                {waitlistJoined && (
                  <p role="status" className="text-sm font-semibold text-accent-ink">
                    You’re on the waitlist. We’ll message you if a unit frees up.
                  </p>
                )}
                <UnitTypeResults
                  availability={availability.data}
                  catCount={catCount}
                  selectedType={unitType}
                  onChoose={chooseType}
                  onPickAlternative={(alt) =>
                    runSearch({
                      branchId,
                      arrivalOn: alt.arrivalOn,
                      departureOn: alt.departureOn,
                      catCount,
                    })
                  }
                  onJoinWaitlist={() => setWaitlistOpen(true)}
                />
                <WaitlistJoinDialog
                  open={waitlistOpen}
                  onOpenChange={setWaitlistOpen}
                  search={searchParams}
                  onUnitsAvailable={() => {
                    setNotice('Units have just become available — choose one from the results.');
                    void availability.refetch();
                  }}
                  onJoined={() => setWaitlistJoined(true)}
                />
              </>
            ))}
        </>
      )}

      {current === 'cats' &&
        (cats.isPending ? (
          <LoadingState variant="card" rows={4} label="Loading your cats…" />
        ) : cats.isError ? (
          <ErrorState onRetry={() => void cats.refetch()} />
        ) : (
          <CatStep
            cats={aliveCats}
            selectedIds={selectedCatIds}
            onToggle={(id) =>
              setSelectedCatIds((prev) =>
                prev.includes(id) ? prev.filter((x) => x !== id) : [...prev, id],
              )
            }
            capacity={
              capacityOf(unitType) ??
              stay?.cats.length ??
              (unitType === 'Standard'
                ? 1
                : unitType === 'Deluxe'
                  ? 2
                  : unitType === 'Family'
                    ? 3
                    : null)
            }
            unitLabel={unitType ? UNIT_TYPE_LABEL[unitType] : 'chosen'}
            onContinue={() => void continueFromCats()}
            onBack={() => setStep('search')}
            pending={createEnquiry.isPending || changeEnquiry.isPending}
            serverError={catError}
            addCatHref={PORTAL_CATS}
          />
        ))}

      {current === 'gate' && stay && (
        <VaccinationGatePanel
          gate={gate ?? gateFromStay(stay)}
          onResume={() => void resume()}
          resuming={changeEnquiry.isPending}
        />
      )}
      {current === 'gate' && !stay && gate && (
        <VaccinationGatePanel
          gate={gate}
          onResume={() => void resume()}
          resuming={changeEnquiry.isPending}
        />
      )}

      {current === 'profiles' && stay && (
        <CareProfileTabs
          cats={stay.cats}
          currencyCode={currency}
          activeCatId={activeCatId}
          onSave={(catId, profile) => saveProfile.mutateAsync({ catId, profile })}
          onBack={() => setStep('cats')}
          onContinue={() => setStep('review')}
        />
      )}

      {current === 'review' && stay && (
        <ReviewAndReserve
          quotePending={quotePending}
          stay={stay.quote ? stay : { ...stay, quote: enquiryQuote }}
          onBack={() => setStep('profiles')}
          onReserve={doReserve}
        />
      )}

      {current === 'reserved' && stay && <ReservedPanel stay={stay} />}
      {current !== 'search' && current !== 'cats' && !stay && stayId && stayQuery.isError && (
        <ErrorState onRetry={() => void stayQuery.refetch()} />
      )}
    </div>
  );
}
