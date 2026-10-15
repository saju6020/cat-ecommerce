'use client';

import { useMemo, useState } from 'react';
import Link from 'next/link';
import { EmptyState } from '@/components/common/EmptyState';
import type { ReportCatalogueEntry } from '@/lib/api/reports';
import { CATEGORY_ORDER, categoryLabel } from '@/lib/format/report';

/** The categories present in `reports`, known ones in the plain-language order, unknown ones last. */
export function groupByCategory(
  reports: ReportCatalogueEntry[],
): Array<{ category: string; reports: ReportCatalogueEntry[] }> {
  const known = CATEGORY_ORDER.map((category) => ({
    category: category as string,
    reports: reports.filter((report) => report.category === category),
  }));
  const knownSet = new Set<string>(CATEGORY_ORDER);
  const unknownCategories = [
    ...new Set(reports.map((report) => report.category).filter((c) => !knownSet.has(c))),
  ];
  const unknown = unknownCategories.map((category) => ({
    category,
    reports: reports.filter((report) => report.category === category),
  }));
  return [...known, ...unknown].filter((group) => group.reports.length > 0);
}

/**
 * `components/reports/ReportCatalogue.tsx` — HO-RPT-002 §11.3, US-RPT-01. The catalogue grouped by
 * category. A report the payload omits is simply absent (ADR-FE-137) — there is no "locked" card.
 * Each card shows the title, the description and "Covers up to N days"; the search box filters the
 * small list on the client by title and description.
 */
export function ReportCatalogue({ reports }: { reports: ReportCatalogueEntry[] }) {
  const [term, setTerm] = useState('');

  const visible = useMemo(() => {
    const needle = term.trim().toLowerCase();
    if (!needle) return reports;
    return reports.filter(
      (report) =>
        report.title.toLowerCase().includes(needle) ||
        report.description.toLowerCase().includes(needle),
    );
  }, [reports, term]);

  const groups = groupByCategory(visible);

  return (
    <div className="flex flex-col gap-6">
      <div className="flex max-w-md flex-col gap-1">
        <label htmlFor="report-search" className="text-sm font-semibold text-ink">
          Search reports
        </label>
        <input
          id="report-search"
          type="search"
          value={term}
          onChange={(event) => setTerm(event.target.value)}
          className="rounded-[var(--radius-sm)] border border-border-strong bg-surface px-3 py-2 text-sm text-ink"
        />
      </div>

      {groups.length === 0 ? (
        <EmptyState
          title={term ? `No report matches “${term.trim()}”` : 'No reports are available to you'}
          description={
            term
              ? 'Try a shorter word, or clear the search to see every report you can open.'
              : 'Ask an administrator if you expected to see reports here.'
          }
        />
      ) : (
        groups.map((group) => (
          <section key={group.category} aria-labelledby={`cat-${group.category}`}>
            <h2 id={`cat-${group.category}`} className="mb-2 text-base font-bold text-ink">
              {categoryLabel(group.category)}
            </h2>
            <ul className="m-0 grid list-none grid-cols-1 gap-3 p-0 md:grid-cols-2 xl:grid-cols-3">
              {group.reports.map((report) => (
                <li key={report.key} className="min-w-0">
                  <Link
                    href={`/staff/reports/${encodeURIComponent(report.key)}`}
                    className="flex h-full flex-col gap-1.5 rounded-[var(--radius-md)] border border-border bg-surface p-4 no-underline hover:bg-surface-sunken"
                  >
                    <span className="text-sm font-bold text-accent-ink underline">
                      {report.title}
                    </span>
                    <span className="text-sm text-ink-muted">{report.description}</span>
                    <span className="mt-auto pt-1 text-xs text-ink-muted">
                      Covers up to {report.maxRangeDays} days
                    </span>
                  </Link>
                </li>
              ))}
            </ul>
          </section>
        ))
      )}
    </div>
  );
}
