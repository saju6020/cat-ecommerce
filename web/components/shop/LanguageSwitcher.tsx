'use client';

import { useId } from 'react';
import { usePathname, useRouter, useSearchParams } from 'next/navigation';

const LANGUAGE_NAMES: Record<string, string> = { en: 'English', bn: 'বাংলা' };

export interface LanguageSwitcherProps {
  defaultLocale: string;
  locales: string[];
}

/**
 * HO-SHP-002 §11.1 `LanguageSwitcher`, ADR-FE-037 — only when more than one content language is
 * enabled. A labelled `<select>` that rewrites `?lang=` (the page's other params stay). It changes
 * the language of product CONTENT only; the interface stays English (C-8). Each option carries its
 * own `lang` (WCAG 3.1.2).
 */
export function LanguageSwitcher({ defaultLocale, locales }: LanguageSwitcherProps) {
  const id = useId();
  const router = useRouter();
  const pathname = usePathname();
  const searchParams = useSearchParams();
  if (locales.length <= 1) return null;

  const current = searchParams.get('lang') ?? defaultLocale;
  function change(next: string) {
    const params = new URLSearchParams(searchParams.toString());
    if (next === defaultLocale) params.delete('lang');
    else params.set('lang', next);
    const query = params.toString();
    router.replace(query ? `${pathname}?${query}` : pathname, { scroll: false });
  }

  return (
    <div className="flex items-center gap-2">
      <label htmlFor={id} className="text-sm font-semibold text-ink-muted">
        Product language
      </label>
      <select
        id={id}
        value={current}
        onChange={(event) => change(event.target.value)}
        className="rounded-[var(--radius-sm)] border border-border-strong bg-surface px-2 py-1.5 text-sm text-ink"
      >
        {locales.map((locale) => (
          <option key={locale} value={locale} lang={locale}>
            {LANGUAGE_NAMES[locale] ?? locale}
          </option>
        ))}
      </select>
    </div>
  );
}
