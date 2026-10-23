import type { LucideIcon } from 'lucide-react';
import { HearthLogo } from './HearthLogo';
import { IconTile } from './IconTile';
import { LineArtCat } from './LineArtCat';
import { cn } from '@/lib/utils';

export interface HearthAuthReason {
  text: string;
  tone: 'warm' | 'teal';
  Icon: LucideIcon;
}

export interface HearthAuthShellProps {
  /** Small uppercase line above the headline ("Clinic · Boarding · Shop", "New here", …). */
  eyebrow: string;
  /** The plain words of the headline, then the italic warm-ink word, then the full stop. */
  headline: string;
  highlight: string;
  intro: string;
  /** Desktop-only list under the intro: reasons (icon tiles) … */
  reasons?: HearthAuthReason[];
  /** … or numbered steps (the first one is the current step). */
  steps?: string[];
  /** The line art in the white disc. */
  art?: 'cat' | 'key' | 'envelope';
  /** Accessible name of the welcome panel. */
  panelLabel: string;
  children: React.ReactNode;
}

function KeyArt() {
  return (
    <svg
      viewBox="0 0 64 64"
      fill="none"
      stroke="currentColor"
      strokeWidth={2.4}
      strokeLinecap="round"
      strokeLinejoin="round"
      className="h-[70px] w-[70px] md:h-[140px] md:w-[140px]"
    >
      <circle cx={22} cy={32} r={12} />
      <path d="M34 32h24M50 32v8M44 32v6" />
      <circle cx={22} cy={32} r={4} />
    </svg>
  );
}

function EnvelopeArt() {
  return (
    <svg
      viewBox="0 0 68 60"
      fill="none"
      stroke="currentColor"
      strokeWidth={2.2}
      strokeLinecap="round"
      strokeLinejoin="round"
      className="h-[66px] w-[76px] md:h-[150px] md:w-[170px]"
    >
      <rect x={6} y={14} width={56} height={38} rx={6} />
      <path d="M6 18l28 18 28-18" />
    </svg>
  );
}

/**
 * `components/hearth/HearthAuthShell.tsx` — the customer "Welcome home" frame (owner-approved 2026-10-16,
 * docs/design/accounts-identity/SignIn|Register|RegisterSuccess|ForgotPassword.dc.html): a warm-soft welcome panel
 * beside the page's form on desktop, a rounded band above it on a phone. It breaks out of the `(auth)` layout's
 * centring padding (`-mx-4 -my-10`) to fill the screen. The panel's headline is a `<p>`: the page's own `<h1>`
 * lives in the form column. Staff (auth) pages never use it.
 */
export function HearthAuthShell({
  eyebrow,
  headline,
  highlight,
  intro,
  reasons,
  steps,
  art = 'cat',
  panelLabel,
  children,
}: HearthAuthShellProps) {
  return (
    <div
      data-theme="customer"
      className="-mx-4 -my-10 grid min-h-screen flex-1 self-stretch bg-surface-page md:grid-cols-[minmax(0,1.15fr)_minmax(0,1fr)]"
    >
      <section
        aria-label={panelLabel}
        className="relative flex flex-col gap-4 overflow-hidden rounded-b-[32px] bg-warm-soft px-5 pb-7 pt-5 md:gap-8 md:rounded-none md:px-16 md:py-12"
      >
        <HearthLogo variant="portal" tagClassName="bg-surface" />
        <div className="relative z-10 flex max-w-[460px] flex-col gap-3 md:mt-6 md:gap-4">
          <p className="m-0 text-xs font-semibold uppercase tracking-[0.08em] text-warm-ink">
            {eyebrow}
          </p>
          <p className="m-0 max-w-[230px] font-display text-[38px] font-semibold leading-[1.02] text-ink md:max-w-none md:text-[56px]">
            {headline} <em className="font-normal text-warm-ink">{highlight}</em>.
          </p>
          <p className="m-0 max-w-[220px] text-sm leading-relaxed text-ink-muted md:max-w-none md:text-[17px]">
            {intro}
          </p>
        </div>
        {reasons && (
          <ul className="relative z-10 m-0 hidden max-w-[400px] list-none flex-col gap-3.5 p-0 md:flex">
            {reasons.map(({ text, tone, Icon }) => (
              <li
                key={text}
                className="flex items-center gap-3.5 rounded-[18px] bg-surface px-4 py-3.5"
              >
                <IconTile tone={tone} shape="tile" size="sm">
                  <Icon size={20} />
                </IconTile>
                <span className="text-[15px] font-semibold text-ink">{text}</span>
              </li>
            ))}
          </ul>
        )}
        {steps && (
          <ol className="relative z-10 m-0 hidden max-w-[400px] list-none flex-col gap-3 p-0 md:flex">
            {steps.map((text, index) => (
              <li
                key={text}
                className="flex items-center gap-3.5 rounded-[18px] bg-surface px-4 py-3.5"
              >
                <span
                  aria-hidden="true"
                  className={cn(
                    'grid size-8 flex-none place-items-center rounded-pill text-sm font-bold',
                    index === 0 ? 'bg-warm-ink text-white' : 'bg-warm-soft text-warm-ink',
                  )}
                >
                  {index + 1}
                </span>
                <span className="text-[15px] font-semibold text-ink">{text}</span>
              </li>
            ))}
          </ol>
        )}
        <div
          aria-hidden="true"
          className="absolute -bottom-6 -right-6 grid size-[170px] place-items-center rounded-pill bg-surface text-warm-ink md:-bottom-10 md:-right-10 md:size-[340px]"
        >
          {art === 'cat' && (
            <LineArtCat
              width={200}
              height={220}
              className="h-[110px] w-[100px] md:h-[220px] md:w-[200px]"
            />
          )}
          {art === 'key' && <KeyArt />}
          {art === 'envelope' && <EnvelopeArt />}
        </div>
      </section>

      <main className="flex items-start justify-center px-5 py-7 md:items-center md:p-12">
        {children}
      </main>
    </div>
  );
}
