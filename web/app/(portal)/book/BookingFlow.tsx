'use client';

import { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import { useSearchParams } from 'next/navigation';
import { BookingStepper, type StepperStep } from '@/components/booking/BookingStepper';
import { CatStep } from '@/components/booking/CatStep';
import { ServiceStep } from '@/components/booking/ServiceStep';
import { BranchStep } from '@/components/booking/BranchStep';
import { SlotStep } from '@/components/booking/SlotStep';
import { ConfirmStep, type BookingSummary } from '@/components/booking/ConfirmStep';
import { BookingDone } from '@/components/booking/BookingDone';
import { WaitlistJoinDialog } from '@/components/booking/WaitlistJoinDialog';
import { LeaveHoldGuard } from '@/components/booking/LeaveHoldGuard';
import { LoadingState } from '@/components/common/LoadingState';
import { ErrorState } from '@/components/common/ErrorState';
import { useCats } from '@/lib/hooks/useCats';
import {
  useBookableServices,
  useBookingPerformers,
  useBookingSlots,
  useClinicBranches,
  useConfirmAppointment,
  useNextDayWithSlots,
  usePlaceSlotHold,
  useReleaseSlotHold,
} from '@/lib/hooks/useBooking';
import { useJoinWaitlist } from '@/lib/hooks/useMyAppointments';
import { useFilterUrlUpdate } from '@/lib/hooks/useFilterUrlUpdate';
import { describeAppointmentError, getViolationCode } from '@/lib/api/errors';
import type { SlotGroup, SlotHold, SlotPerformer } from '@/lib/api/booking';
import type { MyAppointmentDetail } from '@/lib/api/types';
import { formatAppointmentWhen, formatBranchDateLong } from '@/lib/format/appointment';
import { buildAddCatHref } from '@/lib/validation/bookingReturnPath';
import { todayInZone } from '@/lib/format/time';

const HOLD_ENDED_MESSAGE = 'Your hold has ended — please choose a time again.';
const RACE_MESSAGE = 'Someone has just taken that time. Please choose another.';
const NEW_TIMES_MESSAGE = 'Times have just become available — pick one below.';

interface HeldSlot {
  hold: SlotHold;
  performerName: string | null;
}

/**
 * `app/(portal)/book/BookingFlow.tsx` — the booking flow container (HO-APT-002 §11.1, ADR-FE-024).
 * Choices (cat, category, service, branch, date, vet) live in URL search params — reload-safe and
 * deep-linkable from a reminder; the hold lives ONLY in this component's state (never the URL,
 * never storage). Leaving releases it (`DELETE`) on Back, on a confirmed in-app navigation, on
 * unmount and on `pagehide`; the server's expiry is the guarantee (OQ-3).
 */
export function BookingFlow() {
  const searchParams = useSearchParams();
  const setParam = useFilterUrlUpdate();

  const catsQuery = useCats({ pageNumber: 1, pageSize: 100 });
  const servicesQuery = useBookableServices();
  const branchesQuery = useClinicBranches();
  const placeHold = usePlaceSlotHold();
  const releaseHold = useReleaseSlotHold();
  const confirm = useConfirmAppointment();
  const joinWaitlist = useJoinWaitlist();

  const [held, setHeld] = useState<HeldSlot | null>(null);
  const [done, setDone] = useState<{ detail: MyAppointmentDetail; summary: BookingSummary } | null>(
    null,
  );
  const [notice, setNotice] = useState<string | null>(null);
  // The reason for visit lives here, not in ConfirmStep, so it survives the hold ending (NFR-USA-5).
  const [reason, setReason] = useState('');
  const [waitlistOpen, setWaitlistOpen] = useState(false);
  const listRef = useRef<HTMLDivElement>(null);
  const heldRef = useRef<HeldSlot | null>(null);
  heldRef.current = held;

  const cats = useMemo(() => catsQuery.data?.items ?? [], [catsQuery.data]);
  const services = useMemo(() => servicesQuery.data ?? [], [servicesQuery.data]);
  const branches = useMemo(() => branchesQuery.data ?? [], [branchesQuery.data]);

  const catParam = searchParams.get('catId');
  const serviceParam = searchParams.get('serviceId');
  const branchParam = searchParams.get('branchId');
  const category = searchParams.get('category');
  const performerId = searchParams.get('performerId') ?? '';

  const catKnown = catParam && (cats.length === 0 || cats.some((cat) => cat.id === catParam));
  const catId = catKnown ? catParam : cats.length === 1 ? (cats[0]?.id ?? null) : null;
  const cat = cats.find((c) => c.id === catId) ?? null;
  const service = services.find((s) => s.id === serviceParam) ?? null;
  const branch =
    branches.find((b) => b.id === branchParam) ?? (branches.length === 1 ? branches[0]! : null);
  const zone = branch?.timeZoneId ?? 'UTC';
  const todayIso = todayInZone(zone);
  const requestedDate = searchParams.get('date');
  const date = requestedDate && requestedDate >= todayIso ? requestedDate : todayIso;

  const searchQuery = searchParams.toString();
  const addCatHref = buildAddCatHref(searchQuery);

  const ready = !!catId && !!service && !!branch;
  const slotParams = {
    branchId: branch?.id,
    serviceId: service?.id,
    date: ready ? date : undefined,
    performerId: performerId || undefined,
    catId: catId ?? undefined,
  };
  const slotsQuery = useBookingSlots(ready && !held && !done ? slotParams : {});
  const performersQuery = useBookingPerformers(branch?.id ?? null, service?.id ?? null);
  const emptyDay = !!slotsQuery.data && slotsQuery.data.slots.length === 0;
  const nextDay = useNextDayWithSlots(slotParams, emptyDay);

  const focusList = useCallback(() => {
    requestAnimationFrame(() => listRef.current?.focus());
  }, []);

  const releaseNow = useCallback(
    (keepalive = false) => {
      const current = heldRef.current;
      if (!current) return;
      heldRef.current = null;
      releaseHold.mutate({ appointmentId: current.hold.appointmentId, keepalive });
    },
    // eslint-disable-next-line react-hooks/exhaustive-deps
    [releaseHold.mutate],
  );

  // OQ-3 — best-effort release when the page goes away or the flow unmounts.
  useEffect(() => {
    const onPageHide = () => releaseNow(true);
    window.addEventListener('pagehide', onPageHide);
    return () => {
      window.removeEventListener('pagehide', onPageHide);
      releaseNow(true);
    };
  }, [releaseNow]);

  async function chooseSlot(slot: SlotGroup, performer: SlotPerformer | null) {
    if (!branch || !service || !catId) return;
    setNotice(null);
    try {
      const hold = await placeHold.mutateAsync({
        branchId: branch.id,
        serviceId: service.id,
        catId,
        startsOn: slot.startsOn,
        performerId: performer?.id,
      });
      const performerName =
        slot.performers.find((p) => p.id === hold.performerId)?.name ??
        performer?.name ??
        slot.performers[0]?.name ??
        null;
      setHeld({ hold, performerName });
    } catch (error) {
      setNotice(
        getViolationCode(error) === 'SlotNoLongerAvailable'
          ? RACE_MESSAGE
          : describeAppointmentError(error),
      );
      void slotsQuery.refetch();
      focusList();
    }
  }

  // Clearing `held` re-enables the slot query under its real key, which refetches (staleTime 0) —
  // so no manual refetch here (it would run with the paused query's empty params).
  function endHold(message: string) {
    setHeld(null);
    setNotice(message);
    focusList();
  }

  // The release finishes BEFORE the slot list is asked for again, or the customer's own hold would
  // still be counted as busy and the time they just gave back would not be offered.
  async function goBackFromConfirm() {
    const current = heldRef.current;
    heldRef.current = null;
    if (current) {
      await releaseHold
        .mutateAsync({ appointmentId: current.hold.appointmentId })
        .catch(() => undefined);
    }
    setHeld(null);
    setNotice(null);
    setReason(''); // an explicit Back starts the booking over; only an expired hold keeps it
  }

  const summary: BookingSummary | null =
    held && cat && service && branch
      ? {
          catName: cat.name,
          service: {
            name: service.name,
            category: service.category,
            durationMinutes: service.durationMinutes,
            priceAmount: Number(service.currentPrice.amount),
            priceCurrencyCode: service.currentPrice.currencyCode,
          },
          whenLabel: formatAppointmentWhen(held.hold.startsOn, zone),
          performerName: held.performerName,
          roomName: null,
          branchName: branch.name,
          zone,
        }
      : null;

  const steps: StepperStep[] = [];
  const stage = done
    ? 'done'
    : held
      ? 'confirm'
      : !catId
        ? 'cat'
        : !service
          ? 'service'
          : !branch
            ? 'branch'
            : 'time';
  const order = ['cat', 'service', 'branch', 'time', 'confirm'];
  const stateOf = (key: string): StepperStep['state'] =>
    stage === 'done' || order.indexOf(key) < order.indexOf(stage)
      ? 'done'
      : key === stage
        ? 'current'
        : 'upcoming';
  if (cats.length !== 1) {
    steps.push({
      key: 'cat',
      label: 'Cat',
      summary: cat?.name,
      state: stateOf('cat'),
      onChange: () => {
        setReason('');
        setParam('catId', '');
      },
    });
  }
  steps.push({
    key: 'service',
    label: 'Service',
    summary: service?.name,
    state: stateOf('service'),
    onChange: () => {
      setReason('');
      setParam('serviceId', '');
    },
  });
  if (branches.length !== 1) {
    steps.push({
      key: 'branch',
      label: 'Clinic',
      summary: branch?.name,
      state: stateOf('branch'),
      onChange: () => setParam('branchId', ''),
    });
  }
  steps.push(
    { key: 'time', label: 'Date & time', state: stateOf('time') },
    { key: 'confirm', label: 'Confirm', state: stateOf('confirm') },
  );

  if (catsQuery.isLoading || servicesQuery.isLoading || branchesQuery.isLoading) {
    return <LoadingState variant="card" rows={5} label="Loading…" />;
  }
  if (catsQuery.isError || servicesQuery.isError || branchesQuery.isError) {
    return (
      <ErrorState
        title="We couldn’t start the booking just now."
        onRetry={() => {
          void catsQuery.refetch();
          void servicesQuery.refetch();
          void branchesQuery.refetch();
        }}
      />
    );
  }

  if (done) {
    const cutoffHours =
      done.detail.startsOn && done.detail.changeCutoffOn
        ? Math.round(
            (new Date(done.detail.startsOn).getTime() -
              new Date(done.detail.changeCutoffOn).getTime()) /
              3_600_000,
          )
        : null;
    return (
      <BookingDone
        appointment={done.detail}
        summary={done.summary}
        branchAddress={branch?.address ?? null}
        cutoffHours={cutoffHours}
      />
    );
  }

  return (
    <div className="flex flex-col gap-2">
      <LeaveHoldGuard
        when={!!held}
        consequence={
          held && summary
            ? `Leaving now releases ${summary.whenLabel} with ${summary.performerName ?? 'the clinic'} so someone else can take it.`
            : 'Leaving now releases your held time.'
        }
        onLeave={() => releaseNow()}
      />
      <BookingStepper steps={steps} />

      {stage === 'cat' && (
        <CatStep cats={cats} onSelect={(id) => setParam('catId', id)} addCatHref={addCatHref} />
      )}
      {stage === 'service' && (
        <ServiceStep
          services={services}
          category={category}
          onClearCategory={() => setParam('category', '')}
          onSelect={(id) => setParam('serviceId', id)}
        />
      )}
      {stage === 'branch' && (
        <BranchStep branches={branches} onSelect={(id) => setParam('branchId', id)} />
      )}
      {stage === 'time' && branch && service && cat && (
        <>
          <SlotStep
            ref={listRef}
            zone={zone}
            todayIso={todayIso}
            date={date}
            onDateChange={(next) => {
              setNotice(null);
              setParam('date', next);
            }}
            openWeekdays={branch.openWeekdays}
            performers={performersQuery.data ?? []}
            performerId={performerId}
            onPerformerChange={(id) => {
              setNotice(null);
              setParam('performerId', id);
            }}
            slots={slotsQuery.data}
            isLoading={slotsQuery.isLoading}
            isError={slotsQuery.isError}
            onRetry={() => void slotsQuery.refetch()}
            onSelectSlot={chooseSlot}
            isSelecting={placeHold.isPending}
            notice={notice}
            nextDayWithSlots={nextDay.data ?? null}
            nextDayLoading={nextDay.isLoading && emptyDay}
            onJoinWaitlist={() => setWaitlistOpen(true)}
            helpText="Choosing a time holds it for you for a few minutes while you confirm."
          />
          <WaitlistJoinDialog
            open={waitlistOpen}
            onOpenChange={setWaitlistOpen}
            catName={cat.name}
            serviceName={service.name}
            dateLabel={formatBranchDateLong(date)}
            performers={performersQuery.data ?? []}
            defaultPerformerId={performerId}
            onJoin={(preferredPerformerId) =>
              joinWaitlist.mutateAsync({
                branchId: branch.id,
                serviceId: service.id,
                catId: cat.id,
                localDate: date,
                preferredPerformerId,
              })
            }
            onSlotsAvailable={() => {
              setNotice(NEW_TIMES_MESSAGE);
              void slotsQuery.refetch();
              focusList();
            }}
          />
        </>
      )}
      {stage === 'confirm' && held && summary && (
        <ConfirmStep
          summary={summary}
          holdExpiresOn={held.hold.holdExpiresOn}
          onHoldEnded={() => endHold(HOLD_ENDED_MESSAGE)}
          onBack={goBackFromConfirm}
          initialReason={reason}
          onReasonChange={setReason}
          onConfirm={async (reasonForVisit) => {
            const detail = await confirm.mutateAsync({
              appointmentId: held.hold.appointmentId,
              reasonForVisit,
            });
            heldRef.current = null;
            setHeld(null);
            setReason('');
            setDone({ detail, summary });
          }}
        />
      )}
    </div>
  );
}
