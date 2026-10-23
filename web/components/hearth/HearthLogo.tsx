import Link from 'next/link';
import { cn } from '@/lib/utils';

/** A sitting cat whose tail curls round, white on the teal badge (logo A, owner 2026-10-18). */
function SittingCat() {
  return (
    <svg width="68%" height="68%" viewBox="0 0 32 32" fill="currentColor" aria-hidden="true">
      <path d="M11 4l2.6 4.2h4.8L21 4l1.2 6.4c1.5 1.4 2.3 3.2 2.3 5.3 0 2.4-1.1 4.4-2.8 5.8L23 27c.1.9-.6 1.7-1.5 1.7H11.2c-1.4 0-2.4-1.3-2-2.6l1.4-4.6C8.8 20.1 7.7 18 7.7 15.7c0-2.1.8-3.9 2.2-5.3z" />
      <path
        d="M23.6 26.4c3.2-.2 4.9-2 4.9-4.4 0-2-1.3-3.2-2.8-3.2"
        fill="none"
        stroke="currentColor"
        strokeWidth={2.4}
        strokeLinecap="round"
      />
      <circle cx="13.6" cy="14.4" r="1.25" fill="var(--accent)" />
      <circle cx="18.6" cy="14.4" r="1.25" fill="var(--accent)" />
      <path
        d="M15 17.2l1.1.7 1.1-.7"
        fill="none"
        stroke="var(--accent)"
        strokeWidth={1.2}
        strokeLinecap="round"
      />
    </svg>
  );
}

/**
 * The shop's logo (owner picked A · Cat badge, 2026-10-18; design
 * https://claude.ai/artifact/WE2Ff71JLksoxvE7Wr5T21): a tilted teal badge with the sitting cat,
 * "Purr" in deep teal and "Care" in apricot, and a small "Shop" tag. Reads "PurrCare Shop".
 * Sized from the link's font size, so the header's `text-xl md:text-2xl` scales it.
 */
function CatBadgeLogo({
  href,
  className,
  tagClassName,
}: {
  href: string;
  className?: string;
  tagClassName?: string;
}) {
  return (
    <Link
      href={href}
      className={cn(
        'inline-flex items-center gap-2.5 whitespace-nowrap text-2xl text-accent-ink no-underline',
        className,
      )}
    >
      <span
        aria-hidden="true"
        className="grid size-[1.65em] flex-none rotate-[-4deg] place-items-center rounded-[0.52em] bg-accent text-white"
      >
        <SittingCat />
      </span>
      <span className="font-sans text-[1.12em] font-black leading-none tracking-[-0.045em]">
        Purr<span className="text-warm">Care</span>
      </span>{' '}
      <span
        className={cn(
          'self-center rounded-pill bg-warm-soft px-2 py-1 font-sans text-[11px] font-extrabold uppercase leading-none tracking-[0.14em] text-warm-ink',
          tagClassName,
        )}
      >
        Shop
      </span>
    </Link>
  );
}

export interface HearthLogoProps {
  /** Where the logo sits; it only sets the default link (the logo itself is the same everywhere). */
  variant?: 'shop' | 'portal';
  /** Defaults to `/shop` for the shop variant and `/` for the portal variant. */
  href?: string;
  className?: string;
  /** Override the "Shop" tag's fill, e.g. `bg-surface` where the logo sits on a warm-soft panel. */
  tagClassName?: string;
}

/**
 * The PurrCare logo. Owner, 2026-10-19: the shop's cat badge (with its "Shop" tag) is the one logo everywhere
 * (shop, sign-in pages, the customer account area and the staff area); the older cat-head disc is gone.
 */
export function HearthLogo({ variant = 'shop', href, className, tagClassName }: HearthLogoProps) {
  return (
    <CatBadgeLogo
      href={href ?? (variant === 'shop' ? '/shop' : '/')}
      className={className}
      tagClassName={tagClassName}
    />
  );
}
