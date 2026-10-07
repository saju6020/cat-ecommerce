import { HighRiskBadge } from './HighRiskBadge';

/**
 * `components/cat/CageCard.tsx` — US-CAT-11 (HO-CAT-002 §11.7). Print layout with the QR from
 * the API (`GET /api/cats/{catId}/cage-card`, `image/png` — Q7) and the high-risk warning when
 * flagged. `print:` Tailwind variants replace the usual page chrome with a plain card at print
 * time.
 *
 * Takes `qrImageUrl` as a prop rather than building it from `catId` itself (§8.2 rule 4 —
 * components take props, they don't import from `lib/api` to construct their own data; the page
 * calls `getCatCageCardImageUrl(catId)` and passes the result in).
 *
 * **Recorded gap against HO-CAT-001, not worked around:** §11.7 asks this card to show the
 * owner, but `GET /api/cats/{catId}` (and Q7's `CatCageCardViewModel`) only ever carries
 * `ownerId`, a `Guid` — no owner name exists anywhere in the contract. Printing that GUID would
 * be exactly what NFR-USA-4 forbids ("a raw GUID … in the UI is a defect"), so this shows the
 * neutral "Owner on record" instead of either the id or a fabricated name, until Accounts (§1)
 * adds a real customer-name lookup this card can call.
 *
 * Its own `HighRiskBadge` is `hidden print:flex` — on screen the warning is already showing in
 * `CatProfileHeader` above it (`[catId]/layout.tsx`); printing this page hides that header
 * (print:hidden there) so the card must carry its own copy of the warning to still be true when
 * printed on its own. `<h2>`, not `<h1>` — the page's one `<h1>` is the cat's name in the header;
 * a card meant to be cut out and taped to a cage doesn't need to repeat that landmark.
 */
export interface CageCardProps {
  catName: string;
  qrImageUrl: string;
  isHighRisk: boolean;
  handlingNote: string | null;
}

export function CageCard({ catName, qrImageUrl, isHighRisk, handlingNote }: CageCardProps) {
  return (
    <div className="flex w-full max-w-sm flex-col items-center gap-4 rounded-[var(--radius-md)] border border-border bg-surface p-6 text-center print:w-full print:max-w-none print:border-0 print:p-0 print:shadow-none">
      {isHighRisk && (
        <HighRiskBadge handlingNote={handlingNote} className="hidden w-full text-left print:flex" />
      )}
      <h2 className="font-display text-2xl font-semibold text-ink">{catName}</h2>
      <p className="text-sm text-ink-muted">Owner on record</p>
      {/* eslint-disable-next-line @next/next/no-img-element -- a same-origin API image, not a Next static asset */}
      <img
        src={qrImageUrl}
        alt={`QR code linking to ${catName}'s profile`}
        width={180}
        height={180}
        className="h-[180px] w-[180px]"
      />
      <p className="text-xs text-ink-muted">Scan to open this cat&rsquo;s profile</p>
    </div>
  );
}
