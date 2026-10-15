'use client';

import { useEffect, useRef, useState } from 'react';
import { useRouter } from 'next/navigation';
import { Button } from '@/components/ui/button';
import { ConfirmDialog } from '@/components/common/ConfirmDialog';
import { UnsavedChangesGuard } from '@/components/account/UnsavedChangesGuard';
import { toast } from '@/components/ui/use-toast';
import { ApiConflictError, ApiForbiddenError } from '@/lib/api/client';
import { describeSurgeryError } from '@/lib/api/errors';
import { formatBranchDateLong } from '@/lib/format/appointment';
import { timeOnlyInZone } from '@/lib/format/time';
import { useBookSurgery } from '@/lib/hooks/useSurgeries';
import { useSurgeryBookingData } from '@/lib/hooks/useSurgeryBookingData';
import { useSurgeryBookingParams } from '@/lib/hooks/useSurgeryBookingParams';
import { mapBookingViolations, type MappedBookingErrors } from '@/lib/validation/surgery';
import { ApiValidationError } from '@/lib/api/client';
import { BookingClashSummary } from './BookingClashSummary';
import { CampaignSelect } from './CampaignSelect';
import { ProcedureSelect } from './ProcedureSelect';
import { SurgeryCustomerCatStep } from './SurgeryCustomerCatStep';
import { SurgeryResourcePicker, type ResourceErrors } from './SurgeryResourcePicker';

const RACE_MESSAGE = 'Someone has just booked that time. Availability has been refreshed.';
const EMPTY: MappedBookingErrors = { summary: [], byField: {}, alreadyNeutered: null };

/**
 * ⭐ `SurgeryBookingForm` — HO-SRG-002 §11.1, US-SRG-01 and US-SRG-11. The booking choices live in the
 * URL (reload-safe, deep-linkable from the calendar); the server decides everything else. Submit asks
 * for confirmation. A `400` shows EVERY clash beside its field plus a summary at the top with focus
 * moved to it; a `409` race says so and refetches availability. The form keeps what was typed
 * (NFR-USA-5). There is no payment step: the price is information only (ADR-170).
 */
export function SurgeryBookingForm({ zone }: { zone: string }) {
  const router = useRouter();
  const { choices, set } = useSurgeryBookingParams();
  const data = useSurgeryBookingData(choices);
  const book = useBookSurgery();

  const [start, setStart] = useState('');
  const [campaign, setCampaign] = useState({ campaignId: '', note: '' });
  const [confirming, setConfirming] = useState(false);
  const [errors, setErrors] = useState<MappedBookingErrors>(EMPTY);
  const [local, setLocal] = useState<Record<string, string>>({});
  const [neuteredConfirmed, setNeuteredConfirmed] = useState(false);
  const summaryRef = useRef<HTMLDivElement>(null);

  const customerCats = (data.customer.data?.cats ?? []).filter((cat) => !cat.isDeceased);
  const services = data.services.data ?? [];
  const service = services.find((s) => s.id === choices.serviceId);
  const cat = customerCats.find((c) => c.catId === choices.catId);
  const surgeon = data.staff.data?.surgeons.find((s) => s.id === choices.surgeonId);
  const assistant = data.staff.data?.assistants.find((s) => s.id === choices.assistantId);
  const theatre = data.theatres.find((t) => t.id === choices.theatreId);
  const chosenStart = data.availability.data?.starts.find((s) => s.startsOn === start);
  const dirty = !!(choices.customerId || choices.catId || start);

  // A deep link (or an old URL) can carry a choice that is not an option: drop what cannot be shown.
  useEffect(() => {
    const clear: Record<string, string> = {};
    if (
      data.theatres.length > 0 &&
      choices.theatreId &&
      !data.theatres.some((t) => t.id === choices.theatreId)
    ) {
      clear.theatreId = '';
    }
    if (choices.assistantId && choices.assistantId === choices.surgeonId) clear.assistantId = '';
    if (Object.keys(clear).length > 0) set(clear);
  }, [data.theatres, choices.theatreId, choices.assistantId, choices.surgeonId, set]);

  useEffect(() => {
    if (errors.summary.length === 0) return;
    // After the confirm dialog has closed and handed focus back to its button, take it for the summary.
    const timer = setTimeout(() => summaryRef.current?.focus(), 150);
    return () => clearTimeout(timer);
  }, [errors]);

  function clearServerErrors() {
    setErrors(EMPTY);
    setLocal({});
  }

  function validate(): boolean {
    const next: Record<string, string> = {};
    if (!choices.customerId || !choices.catId) next.cat = 'Choose the customer and the cat.';
    if (!choices.serviceId) next.procedure = 'Choose the procedure.';
    if (!choices.theatreId) next.theatreId = 'Choose a theatre.';
    if (!choices.surgeonId) next.surgeonId = 'Choose a surgeon.';
    if (!choices.assistantId) next.assistantId = 'Choose an assistant.';
    if (!start) next.start = 'Choose a start time.';
    if (campaign.campaignId && !campaign.note.trim()) {
      next.campaignNote = 'Note why this cat is eligible for the campaign.';
    }
    setLocal(next);
    return Object.keys(next).length === 0;
  }

  async function submit(confirmDiscrepancy = neuteredConfirmed) {
    clearServerErrors();
    try {
      const result = await book.mutateAsync({
        customerId: choices.customerId,
        catId: choices.catId,
        serviceId: choices.serviceId,
        startsOn: start,
        theatreId: choices.theatreId,
        surgeonId: choices.surgeonId,
        assistantId: choices.assistantId,
        campaignId: campaign.campaignId || undefined,
        campaignEligibilityNote: campaign.campaignId ? campaign.note.trim() : undefined,
        confirmNeuterStatusDiscrepancy: confirmDiscrepancy || undefined,
      });
      toast({ title: 'Surgery booked' });
      router.push(`/staff/surgeries/${result.surgeryId}`);
    } catch (error) {
      if (error instanceof ApiValidationError) {
        setErrors(mapBookingViolations(error));
      } else if (error instanceof ApiConflictError) {
        setStart('');
        setErrors({ ...EMPTY, summary: [RACE_MESSAGE] });
      } else if (error instanceof ApiForbiddenError) {
        setErrors({ ...EMPTY, summary: [describeSurgeryError(error)] });
      } else {
        setErrors({ ...EMPTY, summary: [describeSurgeryError(error)] });
      }
      void data.availability.refetch();
    } finally {
      setConfirming(false);
    }
  }

  const resourceErrors: ResourceErrors = {
    theatreId: [...(local.theatreId ? [local.theatreId] : []), ...(errors.byField.theatreId ?? [])],
    surgeonId: [...(local.surgeonId ? [local.surgeonId] : []), ...(errors.byField.surgeonId ?? [])],
    assistantId: [
      ...(local.assistantId ? [local.assistantId] : []),
      ...(errors.byField.assistantId ?? []),
    ],
    start: [...(local.start ? [local.start] : []), ...(errors.byField.start ?? [])],
  };
  const catError = [
    local.cat,
    ...(errors.byField.cat ?? []).filter((m) => m !== errors.alreadyNeutered),
  ]
    .filter(Boolean)
    .join(' ');
  const when = chosenStart
    ? `${formatBranchDateLong(choices.date)}, ${timeOnlyInZone(chosenStart.startsOn, zone)}–${timeOnlyInZone(chosenStart.endsOn, zone)}`
    : '';

  return (
    <form
      noValidate
      aria-label="Book a surgery"
      className="flex max-w-3xl flex-col gap-8"
      onSubmit={(event) => {
        event.preventDefault();
        if (validate()) setConfirming(true);
      }}
    >
      <UnsavedChangesGuard when={dirty} />
      <BookingClashSummary ref={summaryRef} messages={errors.summary} />

      <SurgeryCustomerCatStep
        customer={
          data.customer.data
            ? {
                id: data.customer.data.id,
                fullName: data.customer.data.fullName,
                mobileNumber: data.customer.data.mobileNumber,
              }
            : null
        }
        onSelectCustomer={(c) => set({ customerId: c.id, catId: '' })}
        onClearCustomer={() => {
          set({ customerId: '', catId: '' });
          data.setSearchedTerm(null);
        }}
        onSearch={data.setSearchedTerm}
        results={data.search.data}
        isSearching={data.search.isFetching}
        searchedTerm={data.searchedTerm}
        cats={customerCats.map((c) => ({ id: c.catId, name: c.name }))}
        catId={choices.catId}
        onCatChange={(catId) => {
          set({ catId });
          setNeuteredConfirmed(false);
          clearServerErrors();
        }}
        catError={catError || undefined}
        neuteredMessage={errors.alreadyNeutered}
        neuteredConfirmed={neuteredConfirmed}
        onBookAnyway={() => {
          setNeuteredConfirmed(true);
          void submit(true);
        }}
      />

      <ProcedureSelect
        services={services}
        serviceId={choices.serviceId}
        disabled={data.services.isPending}
        error={
          [local.procedure, ...(errors.byField.procedure ?? [])].filter(Boolean).join(' ') ||
          undefined
        }
        onChange={(serviceId) => {
          set({ serviceId, surgeonId: '', assistantId: '' });
          setStart('');
          setCampaign({ campaignId: '', note: '' });
        }}
      />

      <SurgeryResourcePicker
        choices={{
          date: choices.date,
          surgeonId: choices.surgeonId,
          assistantId: choices.assistantId,
          theatreId: choices.theatreId,
          start,
        }}
        onChange={(changes) => {
          const { start: nextStart, ...urlChanges } = changes;
          // The surgeon cannot also be the assistant: choosing one clears the other.
          if (urlChanges.surgeonId && urlChanges.surgeonId === choices.assistantId) {
            urlChanges.assistantId = '';
          }
          if (Object.keys(urlChanges).length > 0) set(urlChanges);
          if (nextStart !== undefined) setStart(nextStart);
          else if (Object.keys(urlChanges).length > 0) setStart('');
        }}
        staff={data.staff.data}
        theatres={data.theatres}
        availability={data.availability.data}
        availabilityLoading={data.availability.isFetching}
        availabilityError={data.availability.isError}
        onRetry={() => void data.availability.refetch()}
        zone={zone}
        errors={resourceErrors}
        nextFreeDay={data.nextFreeDay}
        onUseDay={(date) => {
          set({ date });
          setStart('');
        }}
        disabled={!choices.serviceId}
      />

      <CampaignSelect
        campaigns={data.campaigns.data ?? []}
        campaignId={campaign.campaignId}
        note={campaign.note}
        onChange={setCampaign}
        noteError={
          [local.campaignNote, ...(errors.byField.campaignNote ?? [])].filter(Boolean).join(' ') ||
          undefined
        }
        campaignError={(errors.byField.campaign ?? []).join(' ') || undefined}
      />

      <div className="flex flex-wrap items-center gap-3">
        <Button type="submit" disabled={book.isPending}>
          Book surgery
        </Button>
        <p className="text-sm text-ink-muted">
          Nothing is paid now. The price is paid at the clinic.
        </p>
      </div>

      <ConfirmDialog
        open={confirming}
        onOpenChange={setConfirming}
        title="Book this surgery?"
        consequence={`Book ${cat?.name ?? 'this cat'}’s ${service?.name.toLowerCase() ?? 'surgery'} on ${when}, ${theatre?.name ?? 'the theatre'}, ${surgeon?.name ?? 'the surgeon'} with ${assistant?.name ?? 'the assistant'}?`}
        confirmLabel="Book surgery"
        pendingLabel="Booking…"
        onConfirm={() => submit()}
      />
    </form>
  );
}
