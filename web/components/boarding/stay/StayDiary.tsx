'use client';

import Link from 'next/link';
import { Camera } from 'lucide-react';
import { CatHeaderCompact } from '@/components/boarding/shared/CatHeaderCompact';
import { QueryGate } from '@/components/boarding/shared/QueryGate';
import {
  LITTER_OUTPUT_LABEL,
  MOOD_LABEL,
  PORTION_SPOKEN,
  WATER_LABEL,
} from '@/components/boarding/shared/labels';
import type { DiaryDay, StayDiary as StayDiaryDto } from '@/lib/api/types';
import { formatFullDate } from '@/lib/format/boarding';
import { timeOnlyInZone } from '@/lib/format/time';
import { useMyStayDiary } from '@/lib/hooks/useMyBoarding';
import { portalStayHref } from '@/lib/routing/boarding-links';

function MediaPlaceholder({
  media,
  catName,
}: {
  media: DiaryDay['media'][number];
  catName: string | null;
}) {
  return (
    <li className="flex items-start gap-3 rounded-[var(--radius-sm)] border border-dashed border-border-strong bg-surface-sunken p-3 text-sm">
      <Camera size={18} className="mt-0.5 flex-none text-ink-muted" aria-hidden="true" />
      <div className="min-w-0">
        <p className="font-semibold text-ink">
          {media.caption ?? 'Photo'}
          {catName ? ` — ${catName}` : ''}
        </p>
        {media.fileUrl ? (
          <a
            href={media.fileUrl}
            target="_blank"
            rel="noreferrer"
            className="text-accent-ink underline"
          >
            View photo
          </a>
        ) : (
          <p className="text-ink-muted">This item is recorded without a photo you can open.</p>
        )}
        <p className="text-xs text-ink-muted">
          Recorded {formatFullDate(media.capturedOnLocalDate)}
        </p>
      </div>
    </li>
  );
}

function DayCard({ day, diary }: { day: DiaryDay; diary: StayDiaryDto }) {
  const empty =
    day.observations.length === 0 && day.media.length === 0 && day.completedTaskCount === 0;
  const catName = (id: string | null) =>
    diary.catHeaders.find((c) => c.catId === id)?.catName ?? null;
  return (
    <li className="rounded-[var(--radius-md)] border border-border bg-surface p-5">
      <h2 className="font-display text-lg font-semibold text-ink">{formatFullDate(day.date)}</h2>
      {empty ? (
        <p className="mt-2 text-sm text-ink-muted">No entries yet for this day.</p>
      ) : (
        <div className="mt-3 flex flex-col gap-4">
          {day.observations.length > 0 && (
            <ul aria-label="Observations" className="flex flex-col gap-3">
              {[...day.observations]
                .sort((a, b) => b.recordedOn.localeCompare(a.recordedOn))
                .map((o) => (
                  <li key={o.id} className="text-sm">
                    <p className="font-semibold text-ink">
                      {o.catName}{' '}
                      <span className="font-normal text-ink-muted">
                        · {timeOnlyInZone(o.recordedOn, diary.branchTimeZoneId)}
                      </span>
                    </p>
                    <p className="text-ink">
                      {PORTION_SPOKEN[o.portionEaten]} · Water: {WATER_LABEL[o.waterIntake]} ·
                      Litter: {LITTER_OUTPUT_LABEL[o.litterOutput]} · Mood: {MOOD_LABEL[o.mood]}
                    </p>
                    {o.notes && <p className="text-ink-muted">{o.notes}</p>}
                  </li>
                ))}
            </ul>
          )}
          {day.media.length > 0 && (
            <ul aria-label="Photos" className="flex flex-col gap-2">
              {day.media.map((m) => (
                <MediaPlaceholder key={m.id} media={m} catName={catName(m.catId)} />
              ))}
            </ul>
          )}
          <p className="text-sm text-ink-muted">
            {day.completedTaskCount} care task{day.completedTaskCount === 1 ? '' : 's'} completed
          </p>
        </div>
      )}
    </li>
  );
}

/**
 * `components/boarding/stay/StayDiary.tsx` — ⭐ HO-BRD-002 §11.3, US-BRD-12, FR-POR-4: the **only**
 * customer stay diary (README C-6; it inherits HO-POR-002's day-list guidance): day cards, newest
 * first, each with that day's observations (portion eaten, water, litter, mood, notes), media entries
 * and the count of completed care. Media are references until storage exists — an honest placeholder
 * with the caption and date, never a broken image (ADR-FE-104). An empty day says so.
 */
export function StayDiary({ stayId }: { stayId: string }) {
  const query = useMyStayDiary(stayId);
  return (
    <div className="flex flex-col gap-6">
      <div>
        <Link
          href={portalStayHref(stayId)}
          className="text-sm font-semibold text-accent-ink underline"
        >
          ← Back to the stay
        </Link>
        <h1 className="mt-2 font-display text-2xl font-semibold text-ink">Diary</h1>
      </div>
      <QueryGate
        query={query}
        loadingLabel="Loading the diary…"
        notFoundTitle="We can’t find that stay."
      >
        {(diary) => (
          <>
            <ul aria-label="Cats" className="flex flex-wrap gap-3">
              {diary.catHeaders.map((c) => (
                <li
                  key={c.catId}
                  className="rounded-[var(--radius-sm)] border border-border bg-surface px-3 py-2"
                >
                  <CatHeaderCompact
                    name={c.catName}
                    isHighRisk={c.catIsHighRisk}
                    handlingNote={c.handlingNote}
                  />
                </li>
              ))}
            </ul>
            <ol aria-label="Days, newest first" className="flex flex-col gap-4">
              {[...diary.days]
                .sort((a, b) => b.date.localeCompare(a.date))
                .map((day) => (
                  <DayCard key={day.date} day={day} diary={diary} />
                ))}
            </ol>
          </>
        )}
      </QueryGate>
    </div>
  );
}
