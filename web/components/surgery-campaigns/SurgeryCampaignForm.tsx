'use client';

import { useState } from 'react';
import { Button } from '@/components/ui/button';
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog';
import { Banner } from '@/components/inventory/fields';
import { toast } from '@/components/ui/use-toast';
import { describeSurgeryError, surgeryViolations } from '@/lib/api/errors';
import { useAdminBranches } from '@/lib/hooks/useAdminBranches';
import { useCreateCampaign, useUpdateCampaign } from '@/lib/hooks/useSurgeryCatalogue';
import { useSurgeryServices } from '@/lib/hooks/useSurgeries';
import { SURGERY_LIMITS } from '@/lib/validation/surgery';
import type { SurgeryCampaign } from '@/lib/api/surgery-campaigns';
import { TextAreaBox } from '@/components/surgery/TextAreaBox';
import { errorClass, hintClass, inputClass, labelClass } from '@/components/surgery/styles';

/**
 * `SurgeryCampaignForm` — HO-SRG-002 §11.7, US-SRG-11. Code (fixed once created), name, description, the
 * dates, the branches, a price for each spay/neuter service (currency shown beside each) and an optional
 * maximum number of cases. When the server says prices are locked (a surgery has been booked under the
 * campaign) the price fields are read-only with the reason in words, and `CampaignPriceLocked` on save is
 * *"Prices can’t change once a surgery has been booked under this campaign."*
 */
/** A plain amount: digits, at most two decimals. No sign, exponent, comma or spaces; blank is not a price. */
const PRICE_PATTERN = /^[0-9]+([.][0-9]{1,2})?$/;

export function SurgeryCampaignForm({
  campaign,
  onClose,
}: {
  campaign: SurgeryCampaign | null;
  onClose: () => void;
}) {
  const create = useCreateCampaign();
  const update = useUpdateCampaign(campaign?.id ?? '');
  const services = useSurgeryServices();
  const branches = useAdminBranches();
  const sterilisations = (services.data ?? []).filter((s) => s.isSterilisation);
  const [code, setCode] = useState(campaign?.code ?? '');
  const [name, setName] = useState(campaign?.name ?? '');
  const [description, setDescription] = useState(campaign?.description ?? '');
  const [validFrom, setValidFrom] = useState(campaign?.validFrom ?? '');
  const [validTo, setValidTo] = useState(campaign?.validTo ?? '');
  const [branchIds, setBranchIds] = useState<string[]>(campaign?.branchIds ?? []);
  const [prices, setPrices] = useState<Record<string, string>>(
    Object.fromEntries((campaign?.prices ?? []).map((p) => [p.serviceId, p.price.amount])),
  );
  const [maxCases, setMaxCases] = useState(campaign?.maxCases ? String(campaign.maxCases) : '');
  const [showErrors, setShowErrors] = useState(false);
  const [serverErrors, setServerErrors] = useState<string[]>([]);
  const locked = campaign?.pricesLocked ?? false;
  const pending = create.isPending || update.isPending;

  const errors = {
    code: !campaign && !code.trim() ? 'Give the campaign a code.' : undefined,
    name: !name.trim() ? 'Give the campaign a name.' : undefined,
    dates:
      !validFrom || !validTo
        ? 'Choose the first and last day.'
        : validTo < validFrom
          ? 'The last day cannot be before the first day.'
          : undefined,
    prices: Object.values(prices).some((v) => v !== '' && !PRICE_PATTERN.test(v))
      ? 'Enter prices as plain amounts such as 800 or 800.50.'
      : !Object.values(prices).some((v) => v !== '')
        ? 'Set a price for at least one procedure.'
        : undefined,
    maxCases:
      maxCases !== '' && !(Number.isInteger(Number(maxCases)) && Number(maxCases) > 0)
        ? 'Use a whole number above 0, or leave it empty for no limit.'
        : undefined,
  };

  async function submit(event: React.FormEvent) {
    event.preventDefault();
    setShowErrors(true);
    if (Object.values(errors).some(Boolean)) return;
    setServerErrors([]);
    const body = {
      name: name.trim(),
      description: description.trim() || undefined,
      validFrom,
      validTo,
      branchIds,
      prices: Object.entries(prices)
        .filter(([, amount]) => amount !== '')
        .map(([serviceId, amount]) => ({ serviceId, amount: Number(amount) })),
      maxCases: maxCases === '' ? undefined : Number(maxCases),
    };
    try {
      if (campaign) await update.mutateAsync({ ...body, expectedVersion: campaign.version });
      else await create.mutateAsync({ ...body, code: code.trim() });
      toast({ title: campaign ? 'Campaign saved' : 'Campaign created' });
      onClose();
    } catch (error) {
      const messages = surgeryViolations(error).map((v) => v.message);
      setServerErrors(messages.length > 0 ? messages : [describeSurgeryError(error)]);
    }
  }

  const err = (message: string | undefined) =>
    showErrors && message ? (
      <p role="alert" className={errorClass}>
        {message}
      </p>
    ) : null;

  return (
    <Dialog open onOpenChange={(open) => !open && !pending && onClose()}>
      <DialogContent className="max-h-[90vh] max-w-2xl overflow-y-auto">
        <form noValidate onSubmit={(event) => void submit(event)} className="flex flex-col gap-4">
          <DialogHeader>
            <DialogTitle>{campaign ? `Edit ${campaign.name}` : 'New campaign'}</DialogTitle>
            <DialogDescription>
              A campaign sets a reduced price for spay and neuter procedures during a period. The
              clinic still takes payment at the clinic.
            </DialogDescription>
          </DialogHeader>
          {serverErrors.length > 0 && (
            <Banner tone="danger">
              <ul className="list-disc pl-5">
                {serverErrors.map((m) => (
                  <li key={m}>{m}</li>
                ))}
              </ul>
            </Banner>
          )}
          <div className="grid gap-4 sm:grid-cols-2">
            <div className="flex flex-col gap-1.5">
              <label htmlFor="campaign-code" className={labelClass}>
                Code
              </label>
              <input
                id="campaign-code"
                className={inputClass}
                maxLength={SURGERY_LIMITS.campaignCodeMax}
                disabled={!!campaign}
                value={code}
                onChange={(event) => setCode(event.target.value)}
              />
              {err(errors.code)}
            </div>
            <div className="flex flex-col gap-1.5">
              <label htmlFor="campaign-name" className={labelClass}>
                Name
              </label>
              <input
                id="campaign-name"
                className={inputClass}
                maxLength={SURGERY_LIMITS.campaignNameMax}
                value={name}
                onChange={(event) => setName(event.target.value)}
              />
              {err(errors.name)}
            </div>
            <div className="flex flex-col gap-1.5">
              <label htmlFor="campaign-from" className={labelClass}>
                First day
              </label>
              <input
                id="campaign-from"
                type="date"
                className={inputClass}
                value={validFrom}
                onChange={(event) => setValidFrom(event.target.value)}
              />
            </div>
            <div className="flex flex-col gap-1.5">
              <label htmlFor="campaign-to" className={labelClass}>
                Last day
              </label>
              <input
                id="campaign-to"
                type="date"
                className={inputClass}
                value={validTo}
                onChange={(event) => setValidTo(event.target.value)}
              />
            </div>
          </div>
          {err(errors.dates)}
          <TextAreaBox
            id="campaign-description"
            label="Description"
            value={description}
            onChange={setDescription}
            max={SURGERY_LIMITS.campaignDescriptionMax}
            rows={2}
          />
          <fieldset className="flex flex-col gap-2">
            <legend className="text-sm font-semibold text-ink">Branches</legend>
            <p className={hintClass}>
              Leave every branch unticked to run the campaign at all of them.
            </p>
            {(branches.data ?? []).map((branch) => (
              <label key={branch.id} className="flex items-center gap-2 text-sm text-ink">
                <input
                  type="checkbox"
                  className="h-4 w-4"
                  checked={branchIds.includes(branch.id)}
                  onChange={(event) =>
                    setBranchIds((all) =>
                      event.target.checked
                        ? [...all, branch.id]
                        : all.filter((id) => id !== branch.id),
                    )
                  }
                />
                {branch.name}
              </label>
            ))}
          </fieldset>
          <fieldset className="flex flex-col gap-3">
            <legend className="text-sm font-semibold text-ink">Campaign prices</legend>
            {locked && (
              <p className={hintClass} role="status">
                Prices can’t change once a surgery has been booked under this campaign.
              </p>
            )}
            {sterilisations.map((service) => (
              <div key={service.id} className="flex flex-col gap-1.5">
                <label htmlFor={`campaign-price-${service.id}`} className={labelClass}>
                  {service.name}
                </label>
                <div className="flex max-w-56 items-stretch">
                  <input
                    id={`campaign-price-${service.id}`}
                    inputMode="decimal"
                    className={`${inputClass} rounded-r-none`}
                    disabled={locked}
                    value={prices[service.id] ?? ''}
                    onChange={(event) =>
                      setPrices((all) => ({ ...all, [service.id]: event.target.value }))
                    }
                  />
                  <span className="flex items-center rounded-r-[var(--radius-sm)] border border-l-0 border-border-strong bg-surface-sunken px-3 text-sm font-semibold text-ink">
                    {service.price.currencyCode}
                  </span>
                </div>
                <p className={hintClass}>
                  Standard price {service.price.amount} {service.price.currencyCode}
                </p>
              </div>
            ))}
            {err(errors.prices)}
          </fieldset>
          <div className="flex max-w-56 flex-col gap-1.5">
            <label htmlFor="campaign-max" className={labelClass}>
              Maximum cases (optional)
            </label>
            <input
              id="campaign-max"
              inputMode="numeric"
              className={inputClass}
              value={maxCases}
              onChange={(event) => setMaxCases(event.target.value)}
            />
            {err(errors.maxCases)}
          </div>
          <DialogFooter>
            <Button variant="secondary" onClick={onClose} disabled={pending}>
              Cancel
            </Button>
            <Button type="submit" disabled={pending}>
              {pending ? 'Saving…' : 'Save campaign'}
            </Button>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  );
}
