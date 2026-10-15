'use client';

import { formatMoney } from '@/lib/format/money';
import type { ApplicableCampaign } from '@/lib/api/surgery-campaigns';
import { errorClass, hintClass, inputClass, labelClass } from './styles';

export const ELIGIBILITY_NOTE_MAX = 500;

/**
 * `CampaignSelect` — HO-SRG-002 §11.1 part 4. Campaigns applicable to this procedure and date (the
 * server's list), with *"Standard price"* the default. Choosing one shows its price and the places
 * left, and REQUIRES the eligibility note — a field error blocks the booking until it is written (U-A6).
 */
export function CampaignSelect({
  campaigns,
  campaignId,
  note,
  onChange,
  noteError,
  campaignError,
}: {
  campaigns: ApplicableCampaign[];
  campaignId: string;
  note: string;
  onChange: (next: { campaignId: string; note: string }) => void;
  noteError?: string;
  campaignError?: string;
}) {
  if (campaigns.length === 0) return null;
  const chosen = campaigns.find((campaign) => campaign.id === campaignId);
  return (
    <section aria-labelledby="srg-step-campaign" className="flex flex-col gap-3">
      <h2 id="srg-step-campaign" className="font-display text-lg font-semibold text-ink">
        4. Campaign (optional)
      </h2>
      <div className="flex flex-col gap-1.5">
        <label htmlFor="surgery-campaign" className={labelClass}>
          Price
        </label>
        <select
          id="surgery-campaign"
          className={inputClass}
          value={campaignId}
          aria-invalid={campaignError ? true : undefined}
          onChange={(event) => onChange({ campaignId: event.target.value, note })}
        >
          <option value="">Standard price</option>
          {campaigns.map((campaign) => (
            <option key={campaign.id} value={campaign.id}>
              {campaign.name} ({campaign.code})
            </option>
          ))}
        </select>
        {campaignError && (
          <p role="alert" className={errorClass}>
            {campaignError}
          </p>
        )}
      </div>
      {chosen && (
        <>
          <p className="text-sm text-ink">
            Campaign price <strong>{formatMoney(chosen.price)}</strong>
            {chosen.casesRemaining !== null && (
              <>
                {' '}
                · <strong>{chosen.casesRemaining}</strong>{' '}
                {chosen.casesRemaining === 1 ? 'place' : 'places'} left
              </>
            )}{' '}
            <span className={hintClass}>for information — paid at the clinic</span>
          </p>
          <div className="flex flex-col gap-1.5">
            <label htmlFor="surgery-eligibility" className={labelClass}>
              Why is this cat eligible?
            </label>
            <textarea
              id="surgery-eligibility"
              rows={2}
              maxLength={ELIGIBILITY_NOTE_MAX}
              className={inputClass}
              value={note}
              aria-invalid={noteError ? true : undefined}
              aria-describedby={noteError ? 'surgery-eligibility-error' : undefined}
              onChange={(event) => onChange({ campaignId, note: event.target.value })}
            />
            {noteError && (
              <p id="surgery-eligibility-error" role="alert" className={errorClass}>
                {noteError}
              </p>
            )}
            <p className={hintClass}>
              {note.length}/{ELIGIBILITY_NOTE_MAX}
            </p>
          </div>
        </>
      )}
    </section>
  );
}
